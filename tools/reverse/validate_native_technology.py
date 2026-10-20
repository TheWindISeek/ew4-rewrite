"""Check warzone technology addressing, faction copy and roster routing."""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    rng, counts = random.Random(1930), Counter()
    p = NativeProbe()
    head, faction, metadata = SCRATCH + 0x1000, SCRATCH + 0x5000, SCRATCH + 0x7000
    vectors = dict(technology=[], roster=[])

    def check(group, actual, expected):
        counts[group] += 1
        if actual != expected:
            raise AssertionError(dict(group=group, actual=actual, expected=expected))

    for case in range(200):
        warzone = rng.randrange(6)
        levels = [rng.randrange(-1, 6) for _ in range(26)]
        p.write(head + warzone * 0xe4 + 0x1ac, "26i", *levels)
        p.cpu.mem_write(faction, bytes([0xa5]) * 0x300)
        actual = []
        for index, expected in enumerate(levels):
            value = p.call(0x2c97c, head, warzone, index) & 0xffffffff
            value = value - 0x100000000 if value >= 0x80000000 else value
            check("warzone_getter_chain", value, expected)
            p.call(0x5c600, faction, index, value)
            actual.append(value)
        check("faction_technology_copy", list(p.read(faction + 0x1d0, "26i")), levels)
        check("copy_left_guard", bytes(p.cpu.mem_read(faction + 0x1c8, 8)).hex(), "a5" * 8)
        check("copy_right_guard", bytes(p.cpu.mem_read(faction + 0x238, 8)).hex(), "a5" * 8)
        vectors["technology"].append(dict(warzone=warzone, levels=levels, expected=actual))

    for case in range(400):
        mode, ai, present = rng.randrange(1, 4), bool(rng.randrange(2)), bool(rng.randrange(2))
        p.cpu.mem_write(faction, bytes(0x300))
        p.write(faction + 0x238, "i", mode)
        p.write(faction + 0x54, "B", int(ai))
        p.write(faction + 0x240, "Q", metadata if present else 0)
        p.write(faction + 0x250, "Q", head)
        actual = p.call(0x5c658, faction)
        expected = (metadata + 0x60 if present and not ai else 0) if mode == 3 else head + 0x40
        check("roster_selection", actual, expected)
        vectors["roster"].append(dict(mode=mode, ai=ai, metadata=present, available=bool(actual)))
    report = dict(counts=dict(counts), errors=[], scope="Real 2c97c->5ec30 getter and 5c600 setter with boundaries; 5c658 roster routing. Mode-1 player overwrite is decompiled 7dd18 evidence, not full game Start emulation.")
    (ROOT / "reverse/reports/native_technology_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/technology_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
