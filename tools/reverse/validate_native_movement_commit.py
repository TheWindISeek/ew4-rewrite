"""Execute fast native movement/capture with an injected already-computed route.

The native 0x51374 -> 0x50e00 -> 0x4f5a4 chain runs unchanged. Route search,
defeat predicate, event lookup and visual/cache services are outside this probe.
"""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, expected_neighbor


def main():
    p = NativeProbe()
    world, unit, definition, country, other, system = [SCRATCH + i * 0x1000 for i in range(1, 7)]
    tiles = [SCRATCH + 0x7000 + i * 0x100 for i in range(15)]
    buildings = [SCRATCH + 0x9000 + i * 0x20 for i in range(15)]
    route = []
    p.hook(0x85960, lambda *args: 0)
    p.hook(0x859b8, lambda *args: len(route))
    p.hook(0x859cc, lambda _, index, *args: route[index])
    p.hook(0x85824, lambda _, cell, *args: tiles[cell])
    p.hook(0x85aa8, lambda _, cell, direction, *args: (lambda index: 0 if index < 0 else tiles[index])(expected_neighbor(5, 3, cell, direction)))
    p.hook(0x52774, lambda *args: 0)
    p.hook(0x5bc4c, lambda *args: 0)
    p.hook(0x86b50, lambda *args: 0)
    p.hook(0x58c00, lambda *args: 0)
    p.hook(0x58b94, lambda *args: 0)
    p.hook(0x86d7c, lambda *args: 0)
    p.hook(0x86ddc, lambda *args: 0)
    p.hook(0x31844, lambda *args: system)
    p.hook(0x31c7c, lambda *args: 0)
    p.hook(0x5906c, lambda *args: 0)
    p.hook(0x2f0f0, lambda *args: 3)
    p.hook(0x86958, lambda *args: 0)
    p.hook(0x5c618, lambda *args: 0)
    generator = random.Random(1930)
    counts, errors, vectors = Counter(), [], []
    for case in range(400):
        route = list(range(5, 5 + generator.randrange(1, 6)))
        start, end = route[0], route[-1]
        facts = [dict(sea=bool(generator.randrange(2)), active=bool(generator.randrange(2)),
                      owner=generator.randrange(3), occupied=bool(generator.randrange(4) == 0),
                      objective=bool(generator.randrange(5) == 0), building=generator.choice([None, 0, 4])) for _ in tiles]
        facts[start]["owner"] = 1
        facts[start]["occupied"] = True
        if end != start:
            facts[end]["occupied"] = case % 7 == 0
        movement, stunned, source_count = (0 if case % 11 == 0 else 6), case % 13 == 0, 0 if case % 17 == 0 else 1
        for i, tile in enumerate(tiles):
            p.cpu.mem_write(tile, bytes(0x100))
            p.write(tile, "3i", i % 5, i // 5, i)
            p.write(tile + 0x14, "B", int(facts[i]["active"]))
            p.write(tile + 0x18, "i", int(facts[i]["objective"]))
            p.write(tile + 0x1c, "B", int(facts[i]["sea"]))
            p.write(tile + 0x40, "Q", buildings[i] if facts[i]["building"] is not None else 0)
            p.write(buildings[i] + 4, "i", facts[i]["building"] or 0)
            p.write(tile + 0x68, "Q", [0, country, other][facts[i]["owner"]])
            p.write(tile + 0x70, "Q", unit if facts[i]["occupied"] else 0)
            p.write(tile + 0x78, "i", int(facts[i]["occupied"]))
            p.write(tile + 0xc8, "i", -1)
        p.write(tiles[start] + 0x78, "i", source_count)
        p.cpu.mem_write(unit, bytes(0x200))
        p.write(unit + 8, "Q", definition)
        p.write(unit + 0x10, "Q", country)
        p.write(unit + 0x28, "i", movement)
        p.write(unit + 0x44, "i", -3 if stunned else 0)
        p.write(country, "i", 1)
        p.write(other, "i", 2)
        p.call(0x51374, tiles[start], tiles[end], 1)
        owners = [f["owner"] for f in facts]
        occupied = [f["occupied"] for f in facts]
        counts_before = [int(v) for v in occupied]
        counts_before[start] = source_count
        moved = source_count > 0 and movement > 0 and not stunned and counts_before[end] == 0 and len(route) >= 2
        expected_counts = list(counts_before)
        if moved:
            occupied[start], occupied[end] = False, True
            expected_counts[start], expected_counts[end] = 0, 1
            if facts[start]["sea"] and facts[start]["building"] is None:
                owners[start] = 0
            def capture(cell):
                fact = facts[cell]
                if not fact["sea"] and not occupied[cell] and not fact["objective"] and fact["building"] in (None, 4):
                    owners[cell] = 1
            for intermediate in route[1:-1]:
                if facts[intermediate]["sea"]:
                    continue
                capture(intermediate)
                for direction in range(6):
                    neighbor = expected_neighbor(5, 3, intermediate, direction)
                    if neighbor >= 0 and facts[neighbor]["active"]:
                        capture(neighbor)
            owners[end] = 1
        actual = dict(owners=[[0, country, other].index(p.read(t + 0x68, "Q")[0]) for t in tiles],
                      occupied=[bool(p.read(t + 0x70, "Q")[0]) for t in tiles],
                      counts=[p.read(t + 0x78, "i")[0] for t in tiles], movement=p.read(unit + 0x28, "i")[0],
                      acted=bool(p.read(unit + 0x5a, "B")[0]))
        expected = dict(owners=owners, occupied=occupied, counts=expected_counts, movement=0 if moved else movement, acted=False)
        counts["fast_movement_capture"] += 1
        counts["moved"] += int(moved)
        if actual != expected:
            errors.append(dict(case=case, route=route, actual=actual, expected=expected))
            if len(errors) > 3:
                raise AssertionError(errors)
        vectors.append(dict(route=route, tiles=facts, movement=movement, stunned=stunned, source_count=source_count, expected=actual))
    if counts["moved"] < 100:
        raise AssertionError("Insufficient completed movement coverage")
    report = dict(counts=dict(counts), errors=errors,
                  scope="Fast movement, path/adjacent land claiming, empty-sea release, endpoint capture and remaining movement. Injected route/defeat; no campaign event/diplomacy/UI validation.")
    (ROOT / "reverse/reports/native_movement_commit_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/movement_commit_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
