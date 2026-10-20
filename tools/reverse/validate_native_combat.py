"""Differential combat evidence using the original ARM64 calculator.

Map/definition lookups, tile bonuses, armor and evasion are injected. The combat
calculator, skill checks, attack count, min/max helpers and RNG run native bytes.
This verifies calculation given resolved inputs, not legality or damage commits.
"""

from collections import Counter
from dataclasses import asdict, dataclass
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH, RNG_STATE, expected_attack_count


def f32(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


@dataclass
class Combatant:
    unit_id: int = 0
    category: int = 0
    grade: int = 0
    health: int = 80
    maximum: int = 100
    minimum_attack: int = 1
    maximum_attack: int = 8
    weapon: int = 0
    minimum_range: int = 1
    maximum_range: int = 1
    morale: int = 0
    construction_turns: int = 0
    skills: tuple = ()
    stars: int = 0
    attack_bonus: int = 0
    defense_bonus: int = 0
    armor: int = 0
    sea: bool = False
    building: int = -1
    evasion: float = 0.0
    equipment: tuple = ()


class RandomState:
    def __init__(self, seed):
        self.state = seed

    def next(self, bound):
        if bound == 0:
            return 0
        self.state = (self.state * 214013 + 2531011) & ((1 << 64) - 1)
        return ((self.state >> 16) & 32767) % bound


def attack_count(c):
    return expected_attack_count(c.health, c.maximum, c.grade,
                                 (c.category == 0 and 15 in c.skills) or any(i[0] == 4 for i in c.equipment))


def reference(attacker, defender, distance, seed, multipliers):
    rng = RandomState(seed)
    a, d = attacker, defender
    def item_sum(c, function):
        return sum(value for effect, value, target in c.equipment if effect == function and target in (-1, c.category))
    ac, dc = attack_count(a), attack_count(d)
    counter = (a.unit_id != 13 and d.unit_id != 13
               and d.minimum_range <= distance <= d.maximum_range and d.morale != -3
               and not (d.category == 4 and d.construction_turns > 0))
    a_morale = ac * (1 if a.morale == 1 else -1 if a.morale == -1 else -2 if a.morale < -1 else 0)
    d_morale = dc * (1 if d.morale == 1 else -1 if d.morale == -1 else -2 if d.morale < -1 else 0)
    # Positive morale adds attack only; negative morale affects attack and defense.
    a_defense = min(0, a_morale) + item_sum(a, 10)
    d_defense = min(0, d_morale) + item_sum(d, 10)
    a_attack = a_morale + a.attack_bonus + min(ac, 5) * a.stars + item_sum(a, 11)
    d_attack = d_morale + d.attack_bonus + min(dc, 5) * d.stars + item_sum(d, 11)
    offensive_proc = 31 in a.skills and rng.next(100) < 10
    defensive_proc = 32 in d.skills and rng.next(100) < 10

    def attack_range(c):
        lower_skill = {0: 13, 1: 11, 2: 7}.get(c.category)
        upper_skill = {0: 12, 1: 10, 2: 8}.get(c.category)
        return (c.minimum_attack + (lower_skill in c.skills),
                c.maximum_attack + (upper_skill in c.skills))

    amin, amax = attack_range(a)
    dmin, dmax = attack_range(d)
    attack = sum(amin if amax <= amin else amax if offensive_proc
                 else max(rng.next(amax + 1), amin) for _ in range(ac))
    retaliation = sum(max(rng.next(dmax + 1), dmin) if dmax > dmin else dmin
                      for _ in range(dc)) if counter else 0

    def damage(source, target, raw, *, retaliation=False):
        factor = multipliers[source.weapon * 5 + target.category]
        if source.sea and source.category != 3 and 17 not in source.skills and not any(i[0] == 12 for i in source.equipment):
            factor = f32(factor * f32(.8))
        if target.category == 3 and 16 in target.skills:
            factor = f32(factor * f32(.9))
        if target.category == 4 and 4 in source.skills:
            factor = f32(factor * 1.5)
        if source.unit_id == 12:
            if target.category == 4:
                factor = f32(factor * f32(1.8))
            elif target.building >= 0 and (source.building if retaliation else target.building) != 4:
                factor = f32(factor * f32(1.4))
        if source.unit_id == 21:
            if target.category == 3:
                factor = f32(factor + factor)
            elif not target.sea:
                factor = f32(factor * .5)
        ignore = {0: 14, 1: 9, 2: 5}.get(source.category) in source.skills
        evasion = 0.0 if ignore or (target.building >= 0 and 29 in source.skills) else f32(target.evasion)
        return max(1, int(f32(f32(raw) * f32(factor * f32(1.0 - evasion)))))

    taken_by_d = damage(a, d, a_attack + attack - d_defense - d.defense_bonus - d.armor)
    taken_by_a = damage(d, a, d_attack + retaliation - a_defense - a.defense_bonus - a.armor, retaliation=True) if counter else 0
    if defensive_proc:
        taken_by_d = 1
    return {"defender_damage": taken_by_d, "attacker_damage": taken_by_a,
            "counterattack": counter, "offensive_proc": offensive_proc,
            "defensive_proc": defensive_proc, "rng_state": rng.state}


class CombatProbe:
    def __init__(self):
        self.native = NativeProbe()
        p = self.native
        self.context = SCRATCH
        self.tiles = [SCRATCH + 0x1000, SCRATCH + 0x2000]
        self.units = [SCRATCH + 0x3000, SCRATCH + 0x4000]
        self.distance = 1
        self.combatants = []
        p.hook(0x85824, lambda _, cell, *rest: self.tiles[cell - 1])
        p.hook(0x86a68, lambda *args: self.distance)
        p.hook(0x4fd98, lambda tile, *rest: self.by_tile(tile).attack_bonus)
        p.hook(0x4fe64, lambda tile, *rest: self.by_tile(tile).defense_bonus)
        p.hook(0x4ea88, lambda tile, *rest: self.by_tile(tile).building)
        p.hook(0x6ce7c, lambda _, armor_id, *rest: SCRATCH + 0xb000 + armor_id * 4)
        p.hook(0x4fffc, lambda tile, category, siege, *rest:
               0.0 if siege and self.by_tile(tile).building >= 0 else self.by_tile(tile).evasion,
               floating=True)
        p.hook(0x31844, lambda *args: SCRATCH + 0xe000)
        p.hook(0x6cb1c, lambda _, item, *rest: SCRATCH + 0xd000 + item * 0x40 if item < 4 else 0)

    def by_tile(self, tile):
        return self.combatants[self.tiles.index(tile)]

    def run(self, a, d, distance, seed):
        self.combatants = [a, d]
        self.distance = distance
        p = self.native
        p.cpu.mem_write(SCRATCH, bytes(0x10000))
        p.write(self.context + 8, "Q", SCRATCH + 0xa000)
        for i, c in enumerate(self.combatants):
            tile, unit = self.tiles[i], self.units[i]
            definition, general, general_definition = [SCRATCH + (5 + i * 3 + j) * 0x1000 for j in range(3)]
            # Scratch layouts overlap unit/tile ranges only through distinct 4KiB pages.
            p.write(tile + 0x70, "Q", unit)
            p.write(tile + 0x1c, "B", int(c.sea))
            p.write(tile + 0x40, "Q", 0 if c.building < 0 else SCRATCH + 0xc000)
            p.write(unit + 8, "Q", definition)
            p.write(unit + 0x18, "Q", general)
            p.write(unit + 0x20, "2i", c.health, c.maximum)
            p.write(unit + 0x34, "i", i)
            p.write(unit + 0x44, "i", c.morale)
            p.write(unit + 0x60, "i", c.construction_turns)
            p.write(definition + 8, "3i", c.unit_id, c.category, c.grade)
            p.write(definition + 0x20, "5i", c.minimum_attack, c.maximum_attack, c.weapon,
                    c.minimum_range, c.maximum_range)
            p.write(general + 8, "Q", general_definition)
            p.write(general + 0x20 + c.category * 4, "i", c.stars)
            p.write(general + 0x40, "i", len(c.skills))
            p.write(general + 0x44, "2i", *([i * 2 + index for index in range(len(c.equipment))] + [-1] * (2 - len(c.equipment))))
            for index, (effect, value, target) in enumerate(c.equipment):
                address = SCRATCH + 0xd000 + (i * 2 + index) * 0x40
                p.write(address, "i", i * 2 + index)
                p.write(address + 0x10, "3i", effect, value, target)
            p.write(general_definition + 0x34, "4i", *(list(c.skills) + [-1] * (4 - len(c.skills))))
            p.write(SCRATCH + 0xb000 + i * 4, "i", c.armor)
        p.write(RNG_STATE, "Q", seed)
        p.call(0x81634, self.context, 1, 2)
        return {"defender_damage": p.read(self.context + 0x2c, "i")[0],
                "attacker_damage": p.read(self.context + 0x28, "i")[0],
                "counterattack": bool(p.read(self.context + 0x44, "B")[0]),
                "offensive_proc": bool(p.read(self.context + 0x49, "B")[0]),
                "defensive_proc": bool(p.read(self.context + 0x4a, "B")[0]),
                "rng_state": p.read(RNG_STATE, "Q")[0]}


def main():
    probe = CombatProbe()
    inventory = json.loads((ROOT / "reverse/native/rules_inventory.json").read_text(encoding="utf-8"))
    multipliers = inventory["tables"]["weapon_category_multipliers"]
    rng = random.Random(1937)
    fixtures, errors = [], []
    coverage = Counter()
    for case in range(1200):
        pair = []
        for i in range(2):
            category = rng.randrange(5)
            special = rng.choice([0, 0, 0, 12, 13, 21])
            if special in (12, 13):
                category = 2
            if special == 21:
                category = 4
            skills = tuple(sorted(rng.sample([4, 5, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 29, 31, 32], 4)))
            pair.append(Combatant(unit_id=special, category=category, grade=rng.randrange(3),
                                  health=rng.randrange(1, 151), maximum=150,
                                  minimum_attack=rng.randrange(1, 5), maximum_attack=rng.randrange(4, 12),
                                  weapon=rng.randrange(6), maximum_range=rng.randrange(1, 4),
                                  morale=rng.choice([-3, -2, -1, 0, 1]),
                                  construction_turns=rng.randrange(2) if category == 4 else 0,
                                  skills=skills, stars=rng.randrange(7), attack_bonus=rng.randrange(5),
                                  defense_bonus=rng.randrange(5), armor=rng.randrange(5), sea=bool(rng.randrange(2)),
                                  building=rng.choice([-1, 0, 1, 4]), evasion=rng.choice([0, .1, .25, .5]),
                                  equipment=tuple((rng.choice([4, 10, 11, 12]), rng.randrange(1, 7), rng.choice([-1, 0, 1, 2, 3, 4]))
                                                  for _ in range(rng.randrange(3)))))
        distance, seed = rng.randrange(1, 4), rng.getrandbits(64)
        expected = reference(*pair, distance, seed, multipliers)
        actual = probe.run(*pair, distance, seed)
        if actual != expected:
            errors.append({"case": case, "actual": actual, "expected": expected,
                           "pair": [asdict(c) for c in pair], "distance": distance, "seed": seed})
            if len(errors) >= 5:
                break
        coverage.update(k for k in ("counterattack", "offensive_proc", "defensive_proc") if actual[k])
        fixtures.append({"attacker": asdict(pair[0]), "defender": asdict(pair[1]),
                         "distance": distance, "seed": seed, "expected": actual})
    report = {"method": "Original ARM64 combat calculator with injected resolved map/definition inputs.",
              "limitations": ["No action legality, real XML definitions, full adjacent aura lookup, or damage commit tested here.",
                             "Not an APK/emulator/phone compatibility test."],
              "cases": len(fixtures), "coverage": dict(coverage), "errors": errors}
    (ROOT / "reverse/reports/native_combat_validation.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/combat_vectors.json").write_text(
            json.dumps({"multipliers": multipliers, "fixtures": fixtures}, ensure_ascii=False, separators=(",", ":")) + "\n",
            encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
