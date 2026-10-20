"""Compare native rocket splash wrapper plus both real combat calculations."""

from dataclasses import asdict
import json
import random

from validate_native_combat import CombatProbe, Combatant, reference
from validate_native_rules import ROOT, SCRATCH, RNG_STATE, registers
from unicorn import UC_HOOK_CODE


def main():
    probe = CombatProbe()
    p = probe.native
    extra = SCRATCH + 0x10000
    p.cpu.mem_map(extra, 0x6000)
    third_tile, third_unit, third_def, third_general, third_general_def = [extra + i * 0x1000 for i in range(5)]
    neighbors = []
    present = occupied = False

    def neighbor(_, center, direction, *args):
        neighbors.append([center, direction])
        return third_tile if present else 0

    p.hook(0x85aa8, neighbor)
    calls = []

    def observe(cpu, pc, size, user_data):
        calls.append(cpu.reg_read(registers.UC_ARM64_REG_X2))

    p.cpu.hook_add(UC_HOOK_CODE, observe, begin=0x81634, end=0x81634)
    inventory = json.loads((ROOT / "reverse/native/rules_inventory.json").read_text(encoding="utf-8"))
    multipliers = inventory["tables"]["weapon_category_multipliers"]
    generator = random.Random(1949)
    fixtures, errors = [], []
    count = 0
    for case in range(600):
        pair = []
        for i in range(3):
            category = 2 if i == 0 else generator.randrange(5)
            identity = (13 if case % 3 != 2 else 0) if i == 0 else generator.choice([0, 12, 21])
            if identity == 12: category = 2
            if identity == 21: category = 4
            pair.append(Combatant(unit_id=identity, category=category, grade=generator.randrange(3),
                health=generator.randrange(1, 151), maximum=150, minimum_attack=generator.randrange(1, 5),
                maximum_attack=generator.randrange(4, 12), weapon=generator.randrange(len(multipliers)//5),
                minimum_range=1, maximum_range=3, morale=generator.randrange(-3, 2), construction_turns=generator.randrange(3),
                skills=tuple(sorted(generator.sample([4, 5, 8, 14, 17, 29, 31, 32], 4))),
                attack_bonus=generator.randrange(8), defense_bonus=generator.randrange(8), armor=generator.randrange(6),
                sea=bool(generator.randrange(2)), building=generator.choice([-1, 0, 4]), evasion=generator.choice([0., .1, .3])))
        a, d, s = pair
        seed, distance, facing = generator.getrandbits(64), generator.randrange(1, 4), generator.choice([-1., 0., 1.])
        present, occupied = generator.choice([(False, False), (True, False), (True, True)])
        probe.tiles = probe.tiles[:2]
        probe.units = probe.units[:2]
        probe.run(a, d, distance, seed)
        probe.tiles.append(third_tile)
        probe.units.append(third_unit)
        probe.combatants.append(s)
        p.cpu.mem_write(extra, bytes(0x6000))
        p.write(third_tile + 8, "i", 3)
        p.write(third_tile + 0x70, "Q", third_unit if occupied else 0)
        p.write(third_tile + 0x1c, "B", int(s.sea))
        p.write(third_tile + 0x40, "Q", 0 if s.building < 0 else SCRATCH + 0xc000)
        p.write(third_unit + 8, "Q", third_def)
        p.write(third_unit + 0x18, "Q", third_general)
        p.write(third_unit + 0x20, "2i", s.health, s.maximum)
        p.write(third_unit + 0x34, "i", 2)
        p.write(third_unit + 0x44, "i", s.morale)
        p.write(third_unit + 0x60, "i", s.construction_turns)
        p.write(third_def + 8, "3i", s.unit_id, s.category, s.grade)
        p.write(third_def + 0x20, "5i", s.minimum_attack, s.maximum_attack, s.weapon, s.minimum_range, s.maximum_range)
        p.write(third_general + 8, "Q", third_general_def)
        p.write(third_general + 0x40, "i", len(s.skills))
        p.write(third_general + 0x44, "2i", -1, -1)
        p.write(third_general_def + 0x34, "4i", *s.skills)
        p.write(SCRATCH + 0xb008, "i", s.armor)
        p.write(probe.units[0] + 0x50, "f", facing)
        p.write(RNG_STATE, "Q", seed)
        neighbors, calls = [], []
        p.call(0x81f38, probe.context, 1, 2)
        splash = a.unit_id == 13 and present and occupied
        initial = reference(a, s, distance, seed, multipliers) if splash else None
        primary = reference(a, d, distance, initial["rng_state"] if initial else seed, multipliers)
        actual = dict(attacker_damage=p.read(probe.context + 0x30, "i")[0], defender_damage=p.read(probe.context + 0x34, "i")[0],
            counterattack=bool(p.read(probe.context + 0x44, "B")[0]), splash_target=p.read(probe.context + 0x3c, "i")[0],
            splash_damage=p.read(probe.context + 0x40, "i")[0], rng_state=p.read(RNG_STATE, "Q")[0], calls=list(calls), neighbors=list(neighbors))
        wanted = dict(attacker_damage=primary["attacker_damage"], defender_damage=primary["defender_damage"], counterattack=primary["counterattack"],
            splash_target=3 if splash else -1, splash_damage=max(1, initial["defender_damage"]*60//100) if splash else 0,
            rng_state=primary["rng_state"], calls=[3, 2] if splash else [2], neighbors=[[2, 0 if facing > 0 else 3]] if a.unit_id == 13 else [])
        count += 1
        if actual != wanted:
            errors.append(dict(case=case, actual=actual, expected=wanted))
            if len(errors) > 3: raise AssertionError(errors)
        fixtures.append(dict(attacker=asdict(a), defender=asdict(d), splash=asdict(s), distance=distance, seed=seed, facing=facing,
            present=present, occupied=occupied, expected=actual))
    report = dict(count=count, errors=errors, scope="Real 81f38 wrapper with real 81634 calculators and RNG. Neighbor/map/context definitions injected; observed secondary-before-primary calls. Facing was supplied; action dispatcher orientation not executed.")
    (ROOT / "reverse/reports/native_splash_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/splash_vectors.json").write_text(json.dumps(dict(multipliers=multipliers, fixtures=fixtures), separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors: raise SystemExit(1)


if __name__ == "__main__":
    main()
