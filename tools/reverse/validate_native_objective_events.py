"""Probe original objective counting and capture/death event targeting.

Scene, ownership, survival and dialogue services are injected, not an APK test.
"""

from collections import Counter
import itertools
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    counts, errors = Counter(), []
    vectors = {"objectives": [], "capture": [], "death": []}

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected, inputs=inputs))
            raise AssertionError(errors)

    country, tile, unit, old, new, event, system = [SCRATCH + i * 0x1000 for i in range(1, 8)]
    nodes = [SCRATCH + 0x8000 + i * 0x20 for i in range(9)]
    tiles = [SCRATCH + 0x9000 + i * 0x100 for i in range(9)]
    armies = [SCRATCH + 0xa000 + i * 0x100 for i in range(9)]
    p.hook(0x85824, lambda _, cell, *args: tiles[cell])
    p.hook(0x4ef64, lambda cell, *args: p.read(cell + 0x70, "Q")[0])
    generator = random.Random(1937)
    for case in range(400):
        size = generator.randrange(10)
        sentinel = country + 8
        p.write(sentinel, "2Q", nodes[0] if size else sentinel, nodes[size - 1] if size else sentinel)
        marks = [generator.randrange(3) for _ in range(size)]
        goals = [generator.choice([None, -1, 0, 1, 2]) for _ in range(size)]
        for i in range(size):
            p.write(nodes[i], "2Qi", nodes[i + 1] if i + 1 < size else sentinel, sentinel, i)
            p.write(tiles[i] + 0x18, "i", marks[i])
            p.write(tiles[i] + 0x70, "Q", 0 if goals[i] is None else armies[i])
            p.write(armies[i] + 0x54, "i", goals[i] or 0)
        actual = p.call(0x58e24, country)
        expected = marks.count(1) + goals.count(1)
        check("objective_count", actual, expected, case)
        vectors["objectives"].append(dict(markers=marks, unit_objectives=goals, expected=actual))

    trace = []
    exists, country_exists, defeated, won = True, True, False, False
    p.hook(0x52774, lambda *args: 0)
    p.hook(0x5bc4c, lambda *args: 0)
    p.hook(0x86ddc, lambda *args: 0)
    p.hook(0x31844, lambda *args: system)
    p.hook(0x31c7c, lambda *args: system)
    p.hook(0x2f0f0, lambda *args: 3)
    p.hook(0x5906c, lambda *args: int(defeated))
    p.hook(0x59118, lambda *args: 0)
    p.hook(0x7e238, lambda *args: int(won))
    p.hook(0x31b9c, lambda *args: 0)
    p.hook(0x38e10, lambda *args: 0)
    p.hook(0x8d678, lambda *args: event if exists else 0)
    p.hook(0x6b8e0, lambda *args: 0)
    p.hook(0x86b50, lambda *args: 0)
    p.hook(0x86958, lambda *args: 1)
    p.hook(0x5c618, lambda *args: 0)

    def morale(target, value, *args):
        value = value if value < 0x80000000 else value - 0x100000000
        trace.append(["morale", "old" if target == old else "new" if target == new else "explicit", value])
        return 0

    p.hook(0x5bca4, morale)
    p.hook(0x86c5c, lambda _, cell, *args: (trace.append(["environment", cell]) or 0))

    def claim(cell, *args):
        p.write(cell + 0x68, "Q", p.read(unit + 0x10, "Q")[0])
        return 0

    p.hook(0x4f5a4, claim)
    # The raw condition field is deliberately varied: these reference entry points ignore it.
    for condition, effect, had_owner, opposing_camps, already, exists, defeated, won in itertools.product(
            range(3), range(6), (False, True), (False, True), (False, True), (False, True), (False, True), (False, True)):
        for address in (tile, unit, old, new, event):
            p.cpu.mem_write(address, bytes(0x300))
        p.write(old, "i", 1)
        p.write(new, "i", 2 if opposing_camps else 1)
        p.write(tile + 0x68, "Q", old if had_owner else 0)
        p.write(tile + 0x78, "i", 0)
        p.write(tile + 0xc8, "i", 7)
        p.write(unit + 0x10, "Q", new)
        p.write(event, "6i", 7, condition, effect, 99, -1, 4)
        p.write(event + 0x28, "B", int(already))
        trace = []
        p.call(0x50e00, tile, unit, 1)
        fire = had_owner and opposing_camps and exists and not already and not won
        expected = []
        if fire and effect < 4:
            expected = [["morale", "new" if effect == 0 else "old", [1, -1, -2, -3][effect]]]
        elif fire and effect == 5:
            expected = [["environment", 4]]
        actual = dict(trace=trace, fired=bool(p.read(event + 0x28, "B")[0]), reference=p.read(tile + 0xc8, "i")[0])
        wanted = dict(trace=expected, fired=already or fire, reference=-1 if had_owner and opposing_camps and not won else 7)
        inputs = dict(condition=condition, effect=effect, had_owner=had_owner, opposing_camps=opposing_camps, already=already,
                      exists=exists, defeated=defeated, won=won)
        check("capture_reference", actual, wanted, inputs)
        vectors["capture"].append(dict(**inputs, expected=actual))

    p.hook(0x51c84, lambda *args: 0)
    p.hook(0x80c18, lambda *args: 0)
    p.hook(0x80b9c, lambda *args: country if country_exists else 0)
    p.hook(0x5be90, lambda *args: 0)
    p.hook(0x58c00, lambda *args: 0)
    p.hook(0x5906c, lambda *args: 0)
    for condition, effect, country_exists, general in itertools.product(range(3), range(6), (False, True), (False, True)):
        for address in (tile, unit, event):
            p.cpu.mem_write(address, bytes(0x300))
        exists = True
        p.write(tile + 0x68, "Q", old)
        p.write(tile + 0x70, "Q", unit)
        p.write(tile + 0x78, "i", 1)
        p.write(unit + 8, "Q", system)
        p.write(unit + 0x18, "Q", system if general else 0)
        p.write(unit + 0x20, "i", 1)
        p.write(unit + 0x70, "i", 7)
        p.write(event, "6i", 7, condition, effect, 99, -1, 4)
        trace = []
        p.call(0x4f6b8, tile, 1, 0, 0)
        expected = []
        if general and effect < 4:
            expected = [["morale", "explicit" if country_exists else "old", [1, -1, -2, -3][effect]]]
        elif general and effect == 5:
            expected = [["environment", 4]]
        actual = dict(trace=trace, fired=bool(p.read(event + 0x28, "B")[0]))
        inputs = dict(condition=condition, effect=effect, country_exists=country_exists, general=general)
        check("death_reference_target", actual, dict(trace=expected, fired=general), inputs)
        vectors["death"].append(dict(**inputs, expected=actual))

    report = dict(counts=dict(counts), errors=errors, scope=__doc__.strip())
    (ROOT / "reverse/reports/native_objective_event_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/objective_event_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
