"""Run original unit candidate selection and command encoding, without executing actions."""

import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    controller, ai, country, node, candidates, map_object = [SCRATCH + i * 0x1000 for i in range(1, 7)]
    tiles = [SCRATCH + 0x7000 + i * 0x100 for i in range(9)]
    units = [SCRATCH + 0x8000 + i * 0x100 for i in range(9)]
    rows, present = [], []
    p.hook(0x85824, lambda _, index, *args: tiles[index] if index < len(tiles) else 0)

    def generate(*args):
        for index, row in enumerate(rows):
            p.write(candidates + index * 12, "3i", *row)
        p.write(ai + 0x70, "Q", candidates + len(rows) * 12)
        return 1

    p.hook(0xa89d4, generate)
    generator = random.Random(1945)
    fixtures, errors = [], []
    for case in range(600):
        p.cpu.mem_write(controller, bytes(0x100))
        p.cpu.mem_write(ai, bytes(0x100))
        p.write(controller + 0x40, "Q", country)
        p.write(controller + 0x60, "Q", map_object)
        p.write(controller + 0x70, "Q", ai)
        p.write(ai + 0x54, "5i", -1, -1, -1, -1, 0x10000)
        p.write(ai + 0x68, "3Q", candidates, candidates, candidates + 0x100)
        p.write(country + 8, "2Q", node, node)
        p.write(node, "2Qi", country + 8, country + 8, 0)
        present = [True] + [bool(generator.randrange(2)) for _ in range(8)]
        for index, tile in enumerate(tiles):
            p.cpu.mem_write(tile, bytes(0x100))
            p.write(tile + 8, "i", index)
            p.write(tile + 0x70, "Q", units[index] if present[index] else 0)
        rows = [[generator.randrange(-1, 9), generator.randrange(-1, 9), generator.randrange(501)] for _ in range(generator.randrange(8))]
        if case % 7 == 0 and rows:
            for row in rows:
                row[2] = 10
        if case % 13 == 0 and rows:
            rows[0][:2] = [-1, -1]
            rows[0][2] = 999
        p.call(0xa8b60, controller)
        actual = list(p.read(ai + 0x54, "5i"))
        if not rows:
            selected, expected = None, [-1, 0, 0, -1, 0x10020]
        else:
            selected = max(range(len(rows)), key=lambda index: rows[index][2])
            destination, target, _ = rows[selected]
            occupied = destination >= 0 and present[destination]
            target_present = target >= 0 and present[target]
            if destination == -1:
                kind = 0x10050 if target == -1 else 0x10040 if target_present else 0x10020
            elif occupied:
                kind = 0x10030 if destination != 0 else 0x10020
            elif target_present:
                kind = 0x10040
            else:
                kind = 0x10050 if destination == target else 0x10020
            expected = [46 if kind == 0x10050 else -1, 0, destination, target, kind]
        if actual != expected or p.read(controller + 0x58, "B")[0] != 1:
            errors.append(dict(case=case, actual=actual, expected=expected, rows=rows, present=present))
            if len(errors) >= 3:
                raise AssertionError(errors)
        fixtures.append(dict(candidates=rows, present=present, selected=selected, expected=actual))
    report = dict(count=len(fixtures), errors=errors,
        scope="Real a8b60 stable maximum selection and action encoding; a89d4 candidate generation injected, execution not invoked. Includes zero candidates, equal scores and occupancy combinations.")
    (ROOT / "reverse/reports/native_ai_unit_dispatch_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_unit_dispatch_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
