"""Probe native shop/tavern records and item prices without touching an APK."""

from collections import Counter
import hashlib
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    rng = random.Random(1930)
    counts, errors = Counter(), []
    vectors = dict(records=[], availability=[], prices=[], insertion=[])

    def check(group, actual, expected):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected))
            raise AssertionError(errors[-1])

    p = NativeProbe()
    runtime, source, target = [SCRATCH + i * 0x1000 for i in range(1, 4)]
    refreshed = []
    p.hook(0x5d674, lambda address, *args: (refreshed.append(address) or 0))
    for kind, stride, load, save, key in (("market", 116, 0x5d7fc, 0x5d2d0, 0x80),
                                          ("tavern", 108, 0x5e354, 0x5e1ec, 0x68)):
        for case in range(250):
            if kind == "market":
                values = [v for _ in range(14) for v in (rng.randrange(-1, 16000), rng.randrange(4))]
            else:
                values = [v for _ in range(5) for v in (rng.randrange(588), rng.randrange(1000),
                          rng.randrange(500), rng.randrange(200), rng.randrange(1, 40))] + [rng.randrange(6)]
            cell = rng.randrange(5000)
            raw = struct.pack("<" + str(stride // 4) + "i", *values, cell)
            p.cpu.mem_write(runtime, bytes([0xa5]) * 0x100)
            p.cpu.mem_write(source, raw)
            p.write(runtime + key, "i", cell)
            refreshed.clear()
            p.call(load, runtime, source)
            check(kind + "_load", bytes(p.cpu.mem_read(runtime, stride - 4)), raw[:-4])
            check(kind + "_key", p.read(runtime + key, "i")[0], cell)
            check(kind + "_refresh", refreshed, [runtime] if kind == "market" else [])
            p.cpu.mem_write(target, bytes([0xcc]) * (stride + 8))
            p.call(save, runtime, target)
            check(kind + "_save", bytes(p.cpu.mem_read(target, stride)).hex(), raw.hex())
            check(kind + "_guard", bytes(p.cpu.mem_read(target + stride, 8)).hex(), "cc" * 8)
            vectors["records"].append(dict(kind=kind, raw=list(raw)))

    p = NativeProbe()
    runtime, root, definition, tile, commander = [SCRATCH + i * 0x1000 for i in range(1, 6)]
    fixture = {}
    p.hook(0x31844, lambda *args: root)
    p.hook(0x6cb1c, lambda *args: definition)
    p.hook(0x2d3e0, lambda _, item, *args: 0 if fixture["inventory"][item] else -1)
    p.hook(0x55454, lambda _, item, *args: int(fixture["equipped"][item]))
    for case in range(400):
        fixture = dict(items=[rng.randrange(-1, 14) for _ in range(14)], quantities=[rng.randrange(3) for _ in range(14)],
                       inventory=[bool(rng.randrange(2)) for _ in range(14)], equipped=[bool(rng.randrange(2)) for _ in range(14)])
        consumable = bool(case % 2)
        p.cpu.mem_write(runtime, bytes(0x100))
        p.write(definition + 0x20, "B", int(consumable))
        for i, (item, quantity) in enumerate(zip(fixture["items"], fixture["quantities"])):
            p.write(runtime + i * 8, "2i", item, quantity)
        p.call(0x5d674, runtime)
        expected = [int(item >= 0 and (quantity > 0 if consumable else not fixture["inventory"][item] and not fixture["equipped"][item]))
                    for item, quantity in zip(fixture["items"], fixture["quantities"])]
        actual = list(p.cpu.mem_read(runtime + 0x70, 14))
        check("item_availability", actual, expected)
        vectors["availability"].append(dict(**fixture, consumable=consumable, expected=actual))

    p.hook(0x85824, lambda *args: tile)
    p.hook(0x4f42c, lambda *args: commander if fixture["commander"] else 0)
    p.hook(0x5c618, lambda *args: int(fixture["human"]))
    p.hook(0x571e8, lambda *args: fixture["commerce"])
    output = SCRATCH + 0x6000
    for case in range(500):
        fixture = dict(price=rng.randrange(1, 3000), map=bool(rng.randrange(2)), commander=bool(rng.randrange(2)),
                       human=bool(rng.randrange(2)), commerce=rng.randrange(6))
        p.write(runtime + 0x88, "Q", root if fixture["map"] else 0)
        p.write(definition + 0x1c, "i", fixture["price"])
        eligible = fixture["map"] and fixture["commander"] and fixture["human"]
        discount = fixture["commerce"] * 4 if eligible else 0
        purchase = fixture["price"] - fixture["price"] * discount // 100
        purchase_discount = discount if fixture["price"] * discount // 100 else 0
        sale = fixture["price"] * (60 + discount) // 100
        for kind, address, wanted, percentage in (("purchase", 0x5cdb0, purchase, purchase_discount),
                                                  ("sale", 0x5cfa0, sale, discount)):
            value = p.call(address, runtime, 13, output)
            actual = [value, p.read(output, "i")[0]]
            check(kind + "_price", actual, [wanted, percentage])
            vectors["prices"].append(dict(**fixture, kind=kind, expected=actual))

    p = NativeProbe()
    tavern, candidate = SCRATCH + 0x1000, SCRATCH + 0x2000
    for case in range(400):
        rows = sorted([[100 + i, 1, 2, 3, rng.randrange(1, 40)] for i in range(rng.randrange(6))], key=lambda row: row[4])
        row = [700, 4, 5, 6, rng.randrange(1, 40)]
        p.cpu.mem_write(tavern, bytes(0x100))
        p.write(tavern + 100, "i", len(rows))
        for i, existing in enumerate(rows):
            p.write(tavern + i * 20, "5i", *existing)
        p.write(candidate, "5i", *row)
        success = p.call(0x5e4b4, tavern, candidate)
        expected = sorted(rows + [row], key=lambda entry: entry[4]) if len(rows) < 5 else rows
        actual = [list(p.read(tavern + i * 20, "5i")) for i in range(p.read(tavern + 100, "i")[0])]
        check("tavern_stable_insertion", [success, actual], [int(len(rows) < 5), expected])
        vectors["insertion"].append(dict(rows=rows, candidate=row, success=bool(success), expected=actual))

    report = dict(counts=dict(counts), errors=errors,
        source_sha256=hashlib.sha256((ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so").read_bytes()).hexdigest(),
        scope="Native 116/108 load/save, item availability, purchase/sale price, stable candidate insertion; item/ownership/inventory services injected; no client or device test.")
    (ROOT / "reverse/reports/native_commerce_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/commerce_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
