"""Probe roster record layout, title commerce and medal balance arithmetic."""

from collections import Counter
import hashlib
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    counts, errors, vectors = Counter(), [], dict(records=[], commerce=[], medals=[], inheritance=[])
    rng = random.Random(1924)

    def check(group, actual, expected):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected))
            raise AssertionError(errors[-1])

    p = NativeProbe()
    runtime, source, target = [SCRATCH + i * 0x1000 for i in range(1, 4)]
    for case in range(400):
        values = [rng.randrange(1, 589), rng.randrange(15), rng.randrange(10), rng.randrange(1000), rng.randrange(1000)]
        values += [rng.randrange(6) for _ in range(8)] + [rng.randrange(-1, 16000), rng.randrange(-1, 16000)]
        raw = struct.pack("<15i", *values)
        p.cpu.mem_write(runtime, bytes([0xa5]) * 0x80)
        p.write(runtime, "i", values[0])
        p.cpu.mem_write(source, raw)
        p.call(0x5873c, runtime, source)
        actual = [p.read(runtime + offset, "i")[0] for offset in (0, *range(0x10, 0x40, 4), 0x44, 0x48)]
        check("roster_load", actual, values)
        check("skill_count_untouched", bytes(p.cpu.mem_read(runtime + 0x40, 4)).hex(), "a5" * 4)
        p.cpu.mem_write(target, bytes([0xcc]) * 68)
        p.call(0x586ec, runtime, target)
        check("roster_save", bytes(p.cpu.mem_read(target, 60)).hex(), raw.hex())
        check("roster_guard", bytes(p.cpu.mem_read(target + 60, 8)).hex(), "cc" * 8)
        vectors["records"].append(dict(raw=list(raw)))

    title_commerce = list(p.read(0x194e80, "10i"))
    for case in range(300):
        title, business = rng.randrange(10), rng.randrange(10)
        p.write(runtime + 0x14, "i", title)
        p.write(runtime + 0x34, "i", business)
        actual = p.call(0x571e8, runtime)
        check("effective_commerce", actual, max(business, title_commerce[title]))
        vectors["commerce"].append(dict(title=title, business=business, expected=actual))
    writes = []
    p.hook(0x331a0, lambda *args: (writes.append(1) or 0))
    for case in range(300):
        balance = rng.randrange(20_000_000)
        delta = rng.randrange(-30_000_000, 30_000_000)
        p.write(runtime + 0x28, "I", balance ^ 0x7ead3)
        writes.clear()
        p.call(0x2c320, runtime, delta)
        actual = p.read(runtime + 0x28, "I")[0] ^ 0x7ead3
        check("medal_balance", actual, max(0, balance + delta))
        check("medal_notification", len(writes), 1)
        vectors["medals"].append(dict(balance=balance, delta=delta, expected=actual))

    p = NativeProbe()
    roster, receiver, donor = [SCRATCH + i * 0x1000 for i in range(1, 4)]
    events = []
    p.hook(0x58238, lambda recv, give, *args: (events.append(["inherit", recv == receiver, give == donor]) or 0))
    p.hook(0x54994, lambda _, identity, *args: (events.append(["remove", identity]) or 0))
    for case in range(300):
        present = [bool(rng.randrange(2)), bool(rng.randrange(2))]
        donor_id, receiver_id = rng.choice([10, 999]), rng.choice([20, 999])
        p.cpu.mem_write(roster, bytes(0x100))
        p.write(donor, "i", 10)
        p.write(receiver, "i", 20)
        if present[0]: p.write(roster + 0x10 + (case % 6) * 8, "Q", donor)
        if present[1]: p.write(roster + 0x40 + (case % 6) * 8, "Q", receiver)
        events.clear()
        success = p.call(0x5520c, roster, donor_id, receiver_id)
        expected = bool(all(present) and donor_id == 10 and receiver_id == 20)
        check("roster_inheritance_dispatch", [success, events], [int(expected), [["inherit", True, True], ["remove", 10]] if expected else []])
        vectors["inheritance"].append(dict(present=present, donor=donor_id, receiver=receiver_id, success=bool(success)))
    report = dict(counts=dict(counts), errors=errors,
        source_sha256=hashlib.sha256((ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so").read_bytes()).hexdigest(),
        scope="Original 5873c/586ec 60-byte record, 571e8 title-commerce, 2c320 medals, 5520c donor-removal dispatch. Notification/inheritance/removal services injected; no complete account/client test.")
    (ROOT / "reverse/reports/native_roster_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/roster_vectors.json").write_text(json.dumps(dict(**vectors, title_commerce=title_commerce), separators=(",", ":")) + "\n", encoding="utf-8")
    tables = dict(schema_version=1, source_sha256=report["source_sha256"], title_commerce=title_commerce,
                  rank_percent=list(p.read(0x194de0, "15i")), title_percent=list(p.read(0x194e50, "10i")))
    (ROOT / "reverse/reports/metagame_rule_tables.json").write_text(json.dumps(tables, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
