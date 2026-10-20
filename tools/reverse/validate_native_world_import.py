"""Original map/faction/site deserialization; host services are explicit stubs."""

import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    generator = random.Random(1915)
    vectors = dict(terrain=[], faction=[], site=[], army=[], variant=[])
    p = NativeProbe()
    tile, record, system = [SCRATCH + i * 0x1000 for i in range(1, 4)]
    definitions = [SCRATCH + 0x4000 + i * 0x100 for i in range(10)]
    tile_lists = [SCRATCH + 0x5000 + i * 0x80 for i in range(10)]
    sprites = [SCRATCH + 0x6000 + i * 0x100 for i in range(10)]
    costs = [3, 1, 4, 5, 4, 5, 6]
    types = [0, 1, 3, 3, 3, 3, 3, 4, 5, 6]
    p.hook(0x31844, lambda *args: system)
    p.hook(0x6c7bc, lambda _, kind, *args: definitions[kind])
    p.hook(0x6aa70, lambda _, kind, *args: costs[kind])
    for kind in range(10):
        p.write(definitions[kind] + 4, "i", types[kind])
        p.write(definitions[kind] + 8, "Q", tile_lists[kind])
        for index in range(6):
            p.write(tile_lists[kind] + index * 8, "Q", sprites[kind] + index * 16)
            p.write(sprites[kind] + index * 16 + 8, "i", index % 4 + 1 if types[kind] == 3 else 0)
    for case in range(1200):
        background, foreground = generator.randrange(10), generator.choice([*range(10), 11])
        bi, fi = generator.randrange(6), generator.randrange(6)
        raw = bytes([background, bi, 0, 0, foreground, 255 if foreground == 11 else fi, 0, 0,
                     generator.randrange(64), generator.randrange(64), 0, 0])
        p.cpu.mem_write(record, raw)
        p.cpu.mem_write(tile, bytes(0x100))
        p.call(0x50340, tile, record)
        cost, overlay = p.read(tile + 0x38, "2i")
        bt, ft = types[background], None if foreground == 11 else types[foreground]
        bn, fn = bi % 4 + 1 if bt == 3 else 0, fi % 4 + 1 if ft == 3 else 0
        if foreground == 11:
            expected_cost, expected_overlay = costs[bt], 0
        elif bt == ft == 3:
            expected_cost, expected_overlay = (costs[2], 1) if bn + fn < 3 else (costs[3], 2)
        else:
            density = bn if bt == 3 else fn
            expected_cost = max(costs[bt], costs[ft])
            expected_overlay = 2 if density > 2 else 1 if density > 0 else 0
        assert (cost, overlay) == (expected_cost, expected_overlay)
        assert bytes(p.cpu.mem_read(tile + 0x1c, 12)) == raw
        defense = 2 if overlay == 1 else 3 if overlay == 2 else bt
        vectors["terrain"].append(dict(raw=list(raw), cost=cost, overlay=overlay,
                                       defense_type=defense, sea=background == 1))

    p = NativeProbe()
    country, record = SCRATCH + 0x1000, SCRATCH + 0x2000

    def strcpy(destination, source, *args):
        value = bytearray()
        while True:
            byte = bytes(p.cpu.mem_read(source + len(value), 1))
            value.extend(byte)
            if byte == b"\0":
                break
        p.cpu.mem_write(destination, bytes(value))
        return destination

    p.hook_import("strcpy", strcpy)
    for address, offset in ((0x595cc, 0x18), (0x596d8, 0x1c), (0x597e4, 0x20)):
        p.hook(address, lambda owner, amount, *args, offset=offset: (p.write(owner + offset, "I", amount) or 0))
    for case in range(400):
        money, industry, food = [generator.randrange(100000) for _ in range(3)]
        ai, camp, survival = generator.randrange(2), generator.randrange(1, 10), generator.randrange(4)
        income, evasion = generator.choice([1., 1.25, 1.5]), generator.choice([0., .125, .25])
        color, tail_value, count, expiry = [generator.randrange(30) for _ in range(4)]
        raw = bytearray(180)
        struct.pack_into("<6i", raw, 0, money, industry, food, ai, camp, survival)
        raw[24:28], raw[40:44] = b"fra\0", b"rus\0"
        struct.pack_into("<2f", raw, 56, income, evasion)
        struct.pack_into("<i", raw, 64, color)
        struct.pack_into("<3i", raw, 168, tail_value, count, expiry)
        p.cpu.mem_write(record, bytes(raw))
        p.cpu.mem_write(country, bytes(0x300))
        p.call(0x59850, country, record)
        actual = dict(resources=list(p.read(country + 0x18, "3I")), ai=bool(p.read(country + 0x54, "B")[0]),
                      camp=p.read(country, "i")[0], survival=p.read(country + 4, "i")[0],
                      income=p.read(country + 0x24, "f")[0], evasion=p.read(country + 0x28, "f")[0],
                      color=p.read(country + 0x50, "i")[0], tail_value=p.read(country + 0x234, "i")[0],
                      count=p.read(country + 0x174, "i")[0], expiry=p.read(country + 0x2c, "i")[0])
        expected = dict(resources=[money, industry, food], ai=bool(ai), camp=camp, survival=survival,
                        income=income, evasion=evasion, color=color, tail_value=tail_value, count=min(12, count), expiry=expiry)
        assert actual == expected, (actual, expected)
        vectors["faction"].append(dict(raw=list(raw), expected=actual))

    p = NativeProbe()
    tile, record, building, installation = [SCRATCH + i * 0x1000 for i in range(1, 5)]
    p.hook(0x4e9dc, lambda target, kind, level, *args: (p.write(target + 0x40, "Q", 0 if kind == 255 else building) or 0))
    p.hook(0x55c80, lambda *args: 0)
    p.hook(0x5c91c, lambda *args: 0)
    p.hook(0x5c918, lambda *args: 0)
    p.hook(0x5c920, lambda target, kind, *args: (p.write(target, "i", kind) or 0))
    p.hook(0x31844, lambda *args: tile)
    p.hook(0x6c830, lambda *args: 0)
    p.hook_import("_Znwm", lambda *args: installation)
    p.hook_import("_ZdlPv", lambda *args: 0)
    for case in range(600):
        raw = bytearray(16)
        struct.pack_into("<2h", raw, 0, generator.randrange(5000), generator.choice([-1, 20]))
        raw[4:] = bytes([generator.choice([0, 1, 2, 3, 4, 255]), generator.randrange(8),
                         generator.randrange(5), generator.randrange(8), generator.randrange(255),
                         generator.choice([0, 1, 2, 255]), generator.randrange(3), 255,
                         generator.randrange(64), generator.randrange(64), generator.randrange(64), 0])
        p.cpu.mem_write(record, bytes(raw))
        p.cpu.mem_write(tile, bytes(0x100))
        p.cpu.mem_write(building, bytes(0x100))
        p.call(0x51b2c, tile, record)
        actual = dict(marker=p.read(tile + 0x18, "i")[0], state11=p.read(tile + 200, "i")[0],
                      bridge_edges=p.read(tile + 0x54, "i")[0], bridge_styles=list(p.read(tile + 0x58, "3i")),
                      name_id=p.read(tile + 0xcc, "i")[0], building=bool(p.read(tile + 0x40, "Q")[0]),
                      installation=bool(p.read(tile + 0x48, "Q")[0]))
        expected = dict(marker=raw[10], state11=-1, bridge_edges=raw[12] | raw[13] | raw[14],
                        bridge_styles=list(raw[12:15]), name_id=struct.unpack_from("<h", raw, 2)[0],
                        building=raw[4] != 255, installation=raw[9] != 255)
        assert actual == expected
        vectors["site"].append(dict(raw=list(raw), expected=actual))

    p = NativeProbe()
    unit, record, definition, owner, commander, collection = [SCRATCH + i * 0x1000 for i in range(1, 7)]
    fixture = {}
    p.hook_import("_Znwm", lambda *args: commander)
    p.hook_import("_ZdlPv", lambda *args: 0)
    p.hook(0x561fc, lambda *args: 0)
    p.hook(0x56204, lambda *args: 0)
    p.hook(0x56200, lambda *args: 0)
    p.hook(0x5c658, lambda *args: collection)
    p.hook(0x54c88, lambda *args: commander if fixture["roster_found"] else 0)
    p.hook(0x56a6c, lambda *args: fixture["health_bonus"])
    p.hook(0x57230, lambda *args: fixture["movement_bonus"])
    p.hook(0x570f8, lambda _, skill, *args: int(fixture["leadership"] if skill == 3 else fixture["logistics"] if skill == 22 else False))
    p.hook(0x51f84, lambda *args: 0)
    p.hook(0x51c84, lambda *args: 0)
    for case in range(800):
        fixture = dict(base_health=generator.randrange(40, 400), base_movement=generator.randrange(13), category=generator.randrange(5),
                       health_bonus=generator.randrange(151), movement_bonus=generator.randrange(6),
                       leadership=bool(generator.randrange(2)), logistics=bool(generator.randrange(2)),
                       roster_found=bool(generator.randrange(2)), shortage=generator.randrange(5))
        maximum = generator.randrange(20, 600)
        raw = bytearray(32)
        struct.pack_into("<hbbhhI4bh", raw, 0, generator.randrange(5000), generator.randrange(22), generator.randrange(3),
                         generator.randrange(1, maximum + 1), maximum, generator.randrange(1000), generator.randrange(6),
                         generator.randrange(-2, 15), generator.randrange(-3, 2), generator.randrange(5), generator.choice([-1, 1, 200]))
        raw[18:28] = bytes([generator.randrange(2), 0, generator.randrange(4), generator.randrange(2), generator.randrange(2),
                           generator.randrange(2), generator.randrange(2), generator.randrange(2), 255, generator.randrange(4)])
        struct.pack_into("<hBb", raw, 28, generator.randrange(50), generator.randrange(4), generator.randrange(4))
        for pointer in (unit, definition, owner, commander):
            p.cpu.mem_write(pointer, bytes(0x100))
        p.cpu.mem_write(record, bytes(raw))
        p.write(unit + 8, "2Q", definition, owner)
        p.write(definition + 0xc, "i", fixture["category"])
        p.write(definition + 0x14, "2i", fixture["base_health"], fixture["base_movement"])
        p.write(owner + 0x2c, "i", fixture["shortage"])
        p.call(0x53cf8, unit, record)
        attached = struct.unpack_from("<h", raw, 16)[0] >= 0 and (not raw[18] or fixture["roster_found"])
        old_health = struct.unpack_from("<h", raw, 4)[0]
        health = min(fixture["base_health"], old_health * fixture["base_health"] // maximum)
        target_max = fixture["base_health"] + (fixture["health_bonus"] if attached else 0)
        health = min(target_max, health * target_max // fixture["base_health"])
        movement = struct.unpack_from("<b", raw, 13)[0]
        if movement > 0:
            movement = fixture["base_movement"] + (fixture["movement_bonus"] if attached and fixture["base_movement"] > 0 else 0)
        movement = max(0, movement)
        morale = struct.unpack_from("<b", raw, 14)[0]
        if raw[15] < 3 and fixture["shortage"] >= 2 and not (attached and fixture["logistics"]):
            morale = max(-3, morale - fixture["shortage"] + 1)
        if morale < 0 and attached and fixture["leadership"]:
            morale = 0
        actual = dict(health=p.read(unit + 0x20, "i")[0], maximum=p.read(unit + 0x24, "i")[0],
                      movement=p.read(unit + 0x28, "i")[0], morale=p.read(unit + 0x44, "i")[0],
                      attached=bool(p.read(unit + 0x18, "Q")[0]), behavior=p.read(unit + 0x64, "I")[0],
                      expiry=p.read(unit + 0x6c, "i")[0], restore_behavior=p.read(unit + 0x68, "I")[0],
                      state70=p.read(unit + 0x70, "i")[0], burning=p.read(unit + 0x4c, "i")[0],
                      acted=bool(p.read(unit + 0x5a, "B")[0]), transport=bool(p.read(unit + 0x59, "B")[0]))
        expected = dict(health=health, maximum=target_max, movement=movement, morale=morale, attached=bool(attached),
                        behavior=raw[20], expiry=struct.unpack_from("<h", raw, 28)[0], restore_behavior=raw[30], state70=-1,
                        burning=raw[31], acted=bool(raw[23]), transport=bool(raw[22]))
        assert actual == expected, (fixture, actual, expected)
        vectors["army"].append(dict(raw=list(raw), inputs=fixture, expected=actual))

    p = NativeProbe()
    catalog, node, table, variant_text = [SCRATCH + i * 0x1000 for i in range(1, 5)]
    empty_data = p.read(0x220a18, "Q")[0] + 0x18
    lookup = {}

    def construct(destination, source, *args):
        value = bytearray()
        while (byte := bytes(p.cpu.mem_read(source + len(value), 1))) != b"\0":
            value.extend(byte)
        lookup["strings"].append(value.decode("ascii"))
        p.write(destination, "Q", empty_data)
        return 0

    p.hook(0x170f30, construct)

    def find(*args):
        lookup["calls"] += 1
        return node if lookup["found"] or lookup["calls"] == 2 else catalog + 0x238

    p.hook(0x70184, find)
    p.write(node + 0x28, "Q", table)
    p.cpu.mem_write(variant_text, b"pie\0")
    for case in range(300):
        unit_id, grade = generator.randrange(22), generator.randrange(3)
        lookup = dict(found=bool(generator.randrange(2)), calls=0, strings=[])
        expected = SCRATCH + 0x5000 + unit_id * 3 + grade
        p.write(table + (unit_id * 3 + grade) * 8, "Q", expected)
        actual = p.call(0x702a4, catalog, unit_id, grade, variant_text)
        assert actual == expected and lookup["calls"] == (1 if lookup["found"] else 2)
        assert lookup["strings"] == (["pie"] if lookup["found"] else ["pie", "others"])
        vectors["variant"].append(dict(unit_id=unit_id, grade=grade, country_found=lookup["found"], lookups=lookup["calls"]))

    report = dict(counts={key: len(value) for key, value in vectors.items()}, errors=0,
                  scope="Original 50340 terrain, 59850 faction, 51b2c site, 53cf8 -> 52bcc -> 539b0 army load, 702a4 variant fallback. Config/effect/tree lookup, string construction, resource setters, allocation and graphics/name services injected. Not a full initialization or device test.")
    output = ROOT / "reverse/reports"
    (output / "world_import_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    (output / "native_world_import_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
