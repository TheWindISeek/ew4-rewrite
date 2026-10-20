"""Recover resolved commander/equipment effects using original ARM64 routines."""

from collections import Counter
import itertools
import json
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH
from unicorn import arm64_const as registers


def main():
    p = NativeProbe()
    general, definition, unit, unit_definition, root = [SCRATCH + i * 0x1000 for i in range(1, 6)]
    p.write(general + 8, "Q", definition)
    p.write(general + 0x38, "i", 3)
    p.write(unit + 8, "Q", unit_definition)
    p.write(unit + 0x18, "Q", general)
    p.write(unit_definition + 0x34, "i", 12)
    p.hook(0x31844, lambda *args: root)
    p.hook(0x6cb1c, lambda _, item, *args: SCRATCH + 0x6000 + item * 0x100 if item in (0, 1) else 0)
    counts, errors, vectors = Counter(), [], []

    def check(name, actual, expected, inputs):
        counts[name] += 1
        if actual != expected and len(errors) < 5:
            keys = [key for key in actual if actual[key] != expected[key]]
            errors.append({"name": name, "actual": {k: actual[k] for k in keys},
                           "expected": {k: expected[k] for k in keys}, "inputs": inputs})

    # One active skill and optionally two items. Effects sum some item functions,
    # while presence/economic functions select the greatest-value matching item.
    for skill, category, effect, target in itertools.product(range(-1, 40), range(5), (-1, 0, 3, 4, 5, 9, 10, 11, 12, 13, 14, 15), (-1, 0, 3)):
        skills = [] if skill < 0 else [skill]
        p.write(general + 0x40, "i", len(skills))
        p.write(definition + 0x34, "4i", *(skills + [-1] * (4 - len(skills))))
        p.write(general + 0x44, "2i", *([-1, -1] if effect < 0 else [0, 1]))
        for item, value in ((0, 2), (1, 4)):
            p.write(SCRATCH + 0x6000 + item * 0x100, "i", item)
            p.write(SCRATCH + 0x6010 + item * 0x100, "3i", effect, value, target)
        p.write(unit_definition + 0xc, "i", category)
        applies = target in (-1, category)
        actual = {"minimum_attack_bonus": p.call(0x573a4, general, category),
                  "maximum_attack_bonus": p.call(0x574f8, general, category),
                  "movement_bonus": p.call(0x57230, general, category),
                  "attack_equipment": p.call(0x5764c, general, category),
                  "defense_equipment": p.call(0x576e4, general, category),
                  "field_healing": p.call(0x56abc, general),
                  "adjacent_attack": p.call(0x5777c, general),
                  "adjacent_defense": p.call(0x577f4, general),
                  "adjacent_healing": p.call(0x5786c, general),
                  "geography": bool(p.call(0x57164, general)),
                  "sea_immunity": bool(p.call(0x579f8, general)),
                  "full_attack_rolls": p.call(0x56cf0, general, 4) >= 0,
                  "food_consumption": p.call(0x52b48, unit)}
        p.call(0x578e4, general)
        actual["economic_multiplier"] = struct.unpack("<f", struct.pack("<I", p.cpu.reg_read(registers.UC_ARM64_REG_S0)))[0]
        expected = {"minimum_attack_bonus": int(skill == {0: 13, 1: 11, 2: 7}.get(category, -2)),
                    "maximum_attack_bonus": int(skill == {0: 12, 1: 10, 2: 8}.get(category, -2)),
                    "movement_bonus": (0 if category == 3 else 3) + (2 if category == 2 and skill == 6 else 0)
                                      + (1 if category == 3 and skill == 18 else 0) + (6 if effect == 9 and applies else 0),
                    "attack_equipment": 6 if effect == 11 and applies else 0,
                    "defense_equipment": 6 if effect == 10 and applies else 0,
                    "field_healing": 6 if effect == 5 else 0,
                    "adjacent_attack": 6 if effect == 14 else 0,
                    "adjacent_defense": 6 if effect == 15 else 0,
                    "adjacent_healing": 6 if effect == 13 else 0,
                    "geography": skill == 1 or effect == 0,
                    "sea_immunity": skill == 17 or effect == 12,
                    "full_attack_rolls": effect == 4,
                    "food_consumption": 0 if skill == 22 else 12,
                    "economic_multiplier": struct.unpack("<f", struct.pack("<f", 1.8 if skill == 24 else 1.4 if skill == 23 else 1.0))[0]}
        if effect == 3:
            expected["economic_multiplier"] += 4 / 100
            expected["economic_multiplier"] = struct.unpack("<f", struct.pack("<f", expected["economic_multiplier"]))[0]
        check("resolved_effects", actual, expected, [skill, category, effect, target])
        vectors.append({"skill": skill, "category": category, "effect": effect, "target": target, "expected": actual})
    report = {"method": "Native commander, item selection, min/max, movement, aura, food and economic routines; injected item lookup only.",
              "counts": dict(counts), "errors": errors}
    (ROOT / "reverse/reports/native_skill_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/skill_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps({"counts": dict(counts), "errors": errors}))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
