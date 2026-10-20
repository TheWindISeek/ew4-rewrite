"""Run original terrain/installation/building/AI evasion context functions."""

import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH, registers


def f32(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


def main():
    p = NativeProbe()
    tile, country, building, installation, building_row, definition, system, installation_row = [SCRATCH + i * 0x1000 for i in range(1, 9)]
    rows = [SCRATCH + 0x9000 + i * 0x20 for i in range(7)]
    row_exists = False
    p.hook(0x31844, lambda *args: system)
    p.hook(0x6c7bc, lambda *args: definition)
    p.hook(0x6aa58, lambda _, kind, *args: rows[kind])
    p.hook(0x6a4a8, lambda *args: building_row if row_exists else 0)
    generator = random.Random(1929)
    fixtures, errors = [], []
    for case in range(800):
        built, installed, ai, ignore_building, skip_ai, row_exists = [bool(generator.randrange(2)) for _ in range(6)]
        terrain, overlay, category = generator.randrange(7), generator.randrange(3), generator.randrange(5)
        penalties = [[generator.randrange(61) for _ in range(3)] for _ in rows]
        installation_penalties = [generator.randrange(61) for _ in range(3)]
        building_penalty, bonus = generator.randrange(61), generator.choice([0., .05, .15, .25])
        p.cpu.mem_write(tile, bytes(0x100))
        p.write(tile + 0x3c, "i", overlay)
        p.write(tile + 0x40, "2Q", building if built else 0, installation if installed else 0)
        p.write(tile + 0x68, "Q", country)
        p.write(country + 0x54, "B", int(ai))
        p.write(country + 0x28, "f", bonus)
        p.write(installation + 8, "Q", installation_row)
        p.write(installation_row + 4, "3i", *installation_penalties)
        p.write(building_row + 0x10, "i", building_penalty)
        p.write(definition + 4, "i", terrain)
        for i, row in enumerate(rows):
            p.write(row + 8, "3i", *penalties[i])
        p.call(0x4fffc, tile, category, int(ignore_building), int(skip_ai))
        actual = struct.unpack("<f", struct.pack("<I", p.cpu.reg_read(registers.UC_ARM64_REG_S0)))[0]
        index = category if category < 2 else 2
        if built:
            expected = f32(building_penalty / 100) if row_exists and not ignore_building else 0.
        else:
            terrain_index = terrain if overlay == 0 else 2 if overlay == 1 else 3
            expected = f32(penalties[terrain_index][index] / 100)
            if installed:
                expected = max(expected, f32(installation_penalties[index] / 100))
        if ai and not skip_ai:
            expected = f32(expected + f32(bonus))
        if actual != expected:
            errors.append(dict(case=case, actual=actual, expected=expected))
            if len(errors) > 3:
                raise AssertionError(errors)
        fixtures.append(dict(built=built, installed=installed, ai=ai, ignore_building=ignore_building, skip_ai=skip_ai,
                             building_row=row_exists, terrain=terrain, overlay=overlay, category=category, penalties=penalties,
                             installation_penalties=installation_penalties, building_penalty=building_penalty, bonus=bonus, expected=actual))
    report = dict(counts=dict(evasion_context=len(fixtures)), errors=errors,
                  scope="Real 4fffc/55be0/5c974 with injected terrain/map/building config lookup; building bypass and AI tail addition retained separately. Not full battle formula or rendering.")
    (ROOT / "reverse/reports/native_defense_context_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/defense_context_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
