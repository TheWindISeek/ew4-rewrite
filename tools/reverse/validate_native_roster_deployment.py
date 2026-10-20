"""Probe short deployment history, native command 10, and the distinct int helper."""

from collections import Counter
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    rng, counts, vectors = random.Random(1915), Counter(), dict(capacity=[], dispatch=[], history=[], int_helper=[])

    def check(group, actual, expected):
        counts[group] += 1
        if actual != expected:
            raise AssertionError(dict(group=group, actual=actual, expected=expected))

    p = NativeProbe()
    country, unit, record = [SCRATCH + i * 0x1000 for i in range(1, 4)]
    for case in range(400):
        used, capacity = rng.randrange(13), rng.randrange(13)
        p.write(country + 0x170, "2i", used, capacity)
        actual = bool(p.call(0x5c694, country))
        check("capacity", actual, used < capacity)
        vectors["capacity"].append(dict(used=used, capacity=capacity, expected=actual))
    for case in range(400):
        ids = [rng.randrange(1, 589) for _ in range(rng.randrange(13))]
        ids += [-1] * (12 - len(ids))
        needle = rng.choice(ids + [1000])
        p.write(country + 0x158, "12h", *ids)
        actual = bool(p.call(0x5c6d8, country, needle))
        check("history_membership", actual, needle in ids)
        index = rng.randrange(12)
        value = p.call(0x5c6cc, country, index) & 0xffff
        value = value - 0x10000 if value >= 0x8000 else value
        check("history_entry", value, ids[index])

    fixture, events = {}, []
    p.hook(0x85824, lambda *args: record)
    p.hook(0x4ef64, lambda *args: unit if fixture["unit"] else 0)
    p.hook(0x52bcc, lambda target, identity, *args: (events.append(["attach", identity]) or 0))
    for case in range(500):
        fixture = dict(used=rng.randrange(13), capacity=rng.randrange(13), unit=bool(rng.randrange(2)), commander=rng.randrange(1, 589))
        p.cpu.mem_write(country, bytes(0x300))
        p.cpu.mem_write(unit, bytes(0x100))
        p.write(country + 0x58, "i", 10)
        p.write(country + 0x68, "i", fixture["commander"])
        p.write(country + 0x158, "12h", *([-1] * 12))
        p.write(country + 0x170, "2i", fixture["used"], fixture["capacity"])
        events.clear()
        p.call(0x5b260, country)
        ids = [-1] * 12
        records = fixture["unit"] and fixture["used"] < fixture["capacity"]
        if records: ids[fixture["used"]] = fixture["commander"]
        actual = dict(ids=list(p.read(country + 0x158, "12h")), used=p.read(country + 0x170, "i")[0],
                      events=list(events), roster_flag=p.read(unit + 0x58, "B")[0], command=p.read(country + 0x58, "i")[0])
        expected = dict(ids=ids, used=fixture["used"] + int(records), events=[["attach", fixture["commander"]]] if fixture["unit"] else [],
                        roster_flag=int(fixture["unit"]), command=0)
        check("command10_short_history", actual, expected)
        vectors["dispatch"].append(dict(**fixture, expected=actual))

    p = NativeProbe()
    p.hook(0x59850, lambda *args: 0)
    for case in range(400):
        count = rng.randrange(13)
        ids = [rng.randrange(1, 589) for _ in range(count)] + [-1] * (12 - count)
        raw = bytearray(428)
        struct.pack_into("<12h", raw, 0xd4, *ids)
        p.cpu.mem_write(record, bytes(raw))
        p.cpu.mem_write(country, bytes(0x300))
        p.call(0x59994, country, record)
        actual = dict(ids=list(p.read(country + 0x158, "12h")), used=p.read(country + 0x170, "i")[0])
        check("save_history_restore", actual, dict(ids=ids, used=count))
        vectors["history"].append(dict(ids=ids, expected=actual))
    changed_capacity = 0
    for used in range(12):
        p.cpu.mem_write(country, bytes(0x300))
        p.write(country + 0x158, "12h", *([-1] * 12))
        p.write(country + 0x170, "2i", used, 12)
        expected = bytearray(p.cpu.mem_read(country, 0x300))
        struct.pack_into("<i", expected, 0x158 + used * 4, 500)
        struct.pack_into("<i", expected, 0x170, used + 1)
        p.call(0x5c6a8, country, 500)
        check("int_helper_distinct_layout", bytes(p.cpu.mem_read(country, 0x300)).hex(), expected.hex())
        after_capacity = p.read(country + 0x174, "i")[0]
        changed_capacity += after_capacity != 12
        vectors["int_helper"].append(dict(used=used, capacity=after_capacity))
    report = dict(counts=dict(counts), errors=[], int_helper_changed_capacity=changed_capacity,
        scope="Native capacity/short history, command10 including 52d5c flag, save history restore, incompatible int helper; target/attach services injected, base 59850 separately verified. No UI or full saved-world import.")
    (ROOT / "reverse/reports/native_roster_deployment_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/roster_deployment_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
