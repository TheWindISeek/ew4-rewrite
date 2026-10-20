"""Check commander XP and fire application in isolated original ARM64 routines."""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, RNG_STATE


def main():
    p = NativeProbe()
    generator = random.Random(1915)
    commander, definition, item, system = [SCRATCH + i * 0x1000 for i in range(1, 5)]
    p.write(commander + 8, "Q", definition)
    item_percent = None
    equipment_lookup = p.hook(0x56cf0, lambda *args: -1 if item_percent is None else 1)
    p.hook(0x31844, lambda *args: system)
    p.hook(0x6cb1c, lambda *args: item)
    counts, errors = Counter(), []
    fixtures, fire_fixtures = [], []
    rank_costs = list(p.read(0x194da0, "14i"))
    nobility_costs = list(p.read(0x194e20, "9i"))
    rank_multipliers = list(p.read(0x56f54, "2f"))

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected, inputs=inputs))
            if len(errors) > 3:
                raise AssertionError(errors)

    def f32(value):
        import struct
        return struct.unpack("<f", struct.pack("<f", value))[0]

    for case in range(1000):
        kind = generator.choice(["rank", "nobility"])
        costs = rank_costs if kind == "rank" else nobility_costs
        level = generator.randrange(len(costs) + 1)
        previous = generator.randrange(300)
        gain = generator.randrange(300000)
        training = bool(generator.randrange(3) == 0)
        skills = generator.sample([19, 20, 21, 0], generator.randrange(5))
        item_percent = generator.choice([None, 100, 130, 200])
        p.write(commander + 0x10, "4i", level if kind == "rank" else 0, level if kind == "nobility" else 0,
                previous if kind == "rank" else 0, previous if kind == "nobility" else 0)
        p.write(commander + 0x40, "i", len(skills))
        p.write(definition + 0x34, "4i", *(skills + [-1] * (4 - len(skills))))
        p.write(item + 0x14, "i", item_percent or 0)
        advanced = p.call(0x56d98 if kind == "rank" else 0x56f5c, commander, gain, training)
        actual = [p.read(commander + (0x10 if kind == "rank" else 0x14), "i")[0],
                  p.read(commander + (0x18 if kind == "rank" else 0x1c), "i")[0], advanced]
        wanted_level, xp = level, previous
        if level < len(costs):
            adjusted = gain
            if not training:
                if item_percent is not None:
                    adjusted = gain * item_percent // 100
                elif kind == "rank":
                    factor = rank_multipliers[0] if 21 in skills else rank_multipliers[1] if 20 in skills else 1
                    adjusted = int(f32(f32(gain) * factor))
                elif 19 in skills:
                    adjusted = int(f32(f32(gain) * 1.5))
            xp += adjusted
            while wanted_level < len(costs) and xp >= costs[wanted_level]:
                xp -= costs[wanted_level]
                wanted_level += 1
            if wanted_level == len(costs):
                xp = 0
        check("commander_progression", actual, [wanted_level, xp, int(wanted_level > level)], case)
        fixtures.append(dict(kind=kind, level=level, experience=previous, gain=gain, training=training,
                             skills=skills, equipment_percent=item_percent, expected=actual))

    tile, unit, unit_definition, building, equipment = [SCRATCH + i * 0x1000 for i in range(5, 10)]
    immune, naval = False, False
    p.hook(0x570f8, lambda *args: int(immune))
    p.cpu.hook_del(equipment_lookup)
    p.hook(0x56cf0, lambda *args: 1 if equipment_present else -1)
    p.hook(0x52b0c, lambda *args: int(naval))
    p.write(unit + 8, "Q", unit_definition)
    for case in range(600):
        sea, occupied, built, naval, transport, immune, equipment_present = [bool(generator.randrange(2)) for _ in range(7)]
        weapon, terrain, effect, burning = generator.randrange(6), generator.randrange(3), generator.randrange(4), generator.randrange(4)
        seed = generator.getrandbits(64)
        p.cpu.mem_write(tile, bytes(0x100))
        p.cpu.mem_write(unit, bytes(0x200))
        p.write(tile + 0x1c, "B", int(sea))
        p.write(tile + 0x3c, "i", terrain)
        p.write(tile + 0x40, "Q", building if built else 0)
        p.write(tile + 0x50, "i", effect)
        p.write(tile + 0x70, "Q", unit if occupied else 0)
        p.write(unit + 0x18, "Q", commander)
        p.write(unit + 0x4c, "i", burning)
        p.write(unit + 0x59, "B", int(transport))
        p.write(RNG_STATE, "Q", seed)
        result = p.call(0x4ed18, tile, weapon)
        expected_effect, expected_burning, state, success = effect, burning, seed, 0
        eligible = weapon in (3, 4) and (not occupied or burning < 1 if sea else effect < 1)
        if eligible and not (occupied and (immune or equipment_present)):
            if built:
                thresholds = [6, 4, 2]
            else:
                thresholds = [[6, 3, 1], [6, 5, 4], [6, 6, 6]][terrain]
            if weapon == 4:
                thresholds = [t * 5 // 2 for t in thresholds]
            state = (state * 214013 + 2531011) & ((1 << 64) - 1)
            roll = ((state >> 16) & 32767) % 100
            level = 1 if roll < thresholds[0] else 2 if roll < sum(thresholds[:2]) else 3 if roll < sum(thresholds) else 0
            if level:
                success = 1
                if sea:
                    if occupied and (naval or transport):
                        expected_burning = level
                else:
                    expected_effect = level
        actual = [result, p.read(tile + 0x50, "i")[0], p.read(unit + 0x4c, "i")[0], p.read(RNG_STATE, "Q")[0]]
        expected = [success, expected_effect, expected_burning, state]
        inputs = dict(sea=sea, occupied=occupied, building=built, naval=naval, transport=transport,
                                  immune=immune, equipment=equipment_present, weapon=weapon, terrain=terrain,
                                  effect=effect, burning=burning, seed=seed)
        check("fire_application", actual, expected, inputs)
        fire_fixtures.append(dict(**inputs, expected=actual))
    report = dict(counts=dict(counts), errors=errors,
                  scope="Commander XP with injected equipment lookup, and fire application with injected resistance/ship flags.")
    (ROOT / "reverse/reports/native_progression_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/progression_vectors.json").write_text(json.dumps(dict(rank_costs=rank_costs,
            nobility_costs=nobility_costs, rank_multipliers=rank_multipliers, fixtures=fixtures, fire=fire_fixtures), separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
