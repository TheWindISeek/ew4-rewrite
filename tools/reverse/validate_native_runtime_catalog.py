"""Export actual native progression rows and run their original access/XP chain."""

from collections import Counter
import hashlib
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    rows = [list(p.read(0x2211d0 + i * 16, "4i")) for i in range(6)]
    unit, definition, system, commander = [SCRATCH + i * 0x1000 for i in range(1, 5)]
    counts, errors, xp_vectors, worth_vectors = Counter(), [], [], []

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected, inputs=inputs))
            raise AssertionError(errors)

    for level in range(32):
        pointer = p.call(0x6ce7c, 0, level)
        check("actual_veterancy_rows", list(p.read(pointer, "4i")), rows[min(level, 5)], level)
    p.hook(0x31844, lambda *args: system)
    for address in (0xbcf08, 0xbcfd8, 0xbcb4c, 0xc60d0, 0xc6328):
        p.hook(address, lambda *args: 0)
    generator = random.Random(1946)
    for case in range(600):
        level = case % 6
        xp, gain = generator.randrange(500), generator.randrange(2000)
        attached, naval = bool(generator.randrange(2)), bool(generator.randrange(2))
        health, maximum = generator.randrange(1, 101), 100
        p.cpu.mem_write(unit, bytes(0x200))
        p.write(unit + 8, "Q", definition)
        p.write(unit + 0x18, "Q", commander if attached else 0)
        p.write(unit + 0x20, "2i", health, maximum)
        p.write(unit + 0x34, "2i", level, xp)
        p.write(definition + 0xc, "i", 3 if naval else 0)
        advanced = p.call(0x53808, unit, gain)
        cost = rows[min(level + 1, 5)][3]
        cost = (cost * 3 // 2 if attached else cost) * (2 if naval else 1)
        promotes = level < 5 and xp + gain >= cost
        expected = [level + int(promotes), xp + gain - (cost if promotes else 0),
                    min(maximum, health + rows[min(level + 1, 5)][2]) if promotes else health, int(promotes)]
        actual = [*p.read(unit + 0x34, "2i"), p.read(unit + 0x20, "i")[0], advanced]
        check("actual_table_unit_experience", actual, expected, case)
        xp_vectors.append(dict(level=level, experience=xp, gain=gain, attached=attached, naval=naval,
                               health=health, maximum=maximum, expected=actual))
    for grade in range(3):
        for attached in (False, True):
            p.write(definition + 0x10, "i", grade)
            p.write(unit + 0x18, "Q", commander if attached else 0)
            actual = p.call(0x52b94, unit)
            check("kill_experience_worth", actual, (grade + 1) * (2 if attached else 1), [grade, attached])
            worth_vectors.append(dict(grade=grade, attached=attached, expected=actual))
    training = list(p.read(0x194b70, "6i"))
    for level, cost in enumerate(training):
        p.write(unit + 0x34, "i", level)
        check("training_money_table", p.call(0x52e98, unit), cost, level)
    tables = dict(schema_version=1,
        source_sha256=hashlib.sha256((ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so").read_bytes()).hexdigest(),
        unit_veterancy_rows=rows, training_money=training, commander_rank_xp=list(p.read(0x194da0, "14i")),
        commander_nobility_xp=list(p.read(0x194e20, "9i")),
        commander_rank_health_bonus=list(p.read(0x195780, "15i")),
        commander_nobility_healing=list(p.read(0x1957c0, "10i")))
    report = dict(counts=dict(counts), errors=errors,
        scope="Actual native six veterancy rows and 6ce7c/53808/536f8/52b94; system and rendering/audio services injected; no full client.")
    (ROOT / "reverse/reports/runtime_rule_tables.json").write_text(json.dumps(tables, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/runtime_catalog_vectors.json").write_text(json.dumps(dict(xp=xp_vectors, worth=worth_vectors), separators=(",", ":")) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/native_runtime_catalog_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
