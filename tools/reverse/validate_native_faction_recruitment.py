"""Execute original card loader, faction table access, income and recruit commit.

XML/string services, map lookup and unit allocation are explicit host stubs.
Original card field mapping, faction loading, building eligibility and spending run.
"""

from collections import Counter
import json
import random
import struct
import xml.etree.ElementTree as ET

from validate_native_rules import NativeProbe, ROOT, SCRATCH, RETURN


def main():
    p = NativeProbe()
    system, catalog = SCRATCH, SCRATCH + 0x1000
    country, record, tile, unit, definition, building, row = [SCRATCH + n for n in
        (0xa000, 0xa400, 0xa800, 0xac00, 0xad00, 0xae00, 0xb000)]
    next_node, next_string = SCRATCH + 0x3000, SCRATCH + 0x6000
    attributes, strings = {}, {}
    counts, errors, vectors = Counter(), [], dict(cards=[], tables=[], income=[], recruitment=[])
    names = ["Militia", "Line Infantry", "Light Infantry", "Grenadier", "Guards", "Machine Gun",
             "Light Cavalry", "Heavy Cavalry", "Guards Cavalry", "Armored Car", "Light Artillery", "Heavy Artillery",
             "Siege Artillery", "Rocket", "Privateer", "Frigate", "Battleship", "Ironclad",
             "Small Fortress", "Fortress", "Large Fortress", "Coastal Fort"]

    def text(address):
        if not address:
            return ""
        data = bytearray()
        while p.read(address + len(data), "B")[0]:
            data.append(p.read(address + len(data), "B")[0])
        return data.decode("utf-8")

    def string(value):
        nonlocal next_string
        if value not in strings:
            raw = value.encode("utf-8") + b"\0"
            strings[value] = next_string
            p.cpu.mem_write(next_string, raw)
            next_string += len(raw)
            if next_string >= country:
                raise ValueError("XML string arena exhausted")
        return strings[value]

    def node(element):
        nonlocal next_node
        address = next_node
        next_node += 0x80
        if next_node >= SCRATCH + 0x6000:
            raise ValueError("XML node arena exhausted")
        attributes[address] = element.attrib
        p.write(address, "Q", SCRATCH + 0xf000)
        children = [node(child) for child in element]
        if children:
            p.write(address + 0x28, "Q", children[0])
            for left, right in zip(children, children[1:]):
                p.write(left + 0x48, "Q", right)
        return address

    def integer(address, key, destination, *args):
        value = attributes[address].get(text(key))
        if value is None:
            return 1
        p.write(destination, "i", int(value))
        return 0

    cards = ET.parse(ROOT / "apk_work/original/assets/def_card.xml").getroot()
    root = node(cards)
    p.write(SCRATCH + 0xf060, "Q", RETURN + 0x200)
    p.hook(RETURN + 0x200, lambda address, *args: address)
    p.hook(0x938a4, lambda *args: string("unused"))
    p.hook(0xe92c8, lambda *args: 0)
    p.hook(0xe9718, lambda *args: 1)
    p.hook(0xe84b8, lambda *args: root)
    p.hook(0xe8ed8, integer)
    p.hook(0xe8cf4, lambda address, key, *args: string(attributes[address][text(key)]) if text(key) in attributes[address] else 0)
    p.hook(0x676e8, lambda address, *args: names.index(text(address)) if text(address) in names else -1)
    p.hook(0x173504, lambda *args: 0)
    p.hook(0xe7df8, lambda *args: 0)
    p.hook_import("_ZdlPv", lambda *args: 0)
    p.hook_import("strlen", lambda address, *args: len(text(address).encode("utf-8")))
    p.hook_import("strcmp", lambda a, b, *args: (text(a) > text(b)) - (text(a) < text(b)))
    p.call(0x6aa88, catalog)

    def check(group, actual, expected, context):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected, context=context))
            raise AssertionError(errors)

    for card in cards:
        a = card.attrib
        index = int(a["id"])
        actual = list(p.read(catalog + 0x2a8 + index * 0x38, "8i"))
        expected = [index, ["recruit", "fortress", "installation", "special"].index(a["type"]),
                    int(a.get("grade", 0)), int(a.get("price", 0)), int(a.get("industry", 0)),
                    int(a.get("round", 0)), int(a.get("buildround", 0)), names.index(a["army"]) if "army" in a else -1]
        check("native_card_loader", actual, expected, index)
        vectors["cards"].append(dict(id=index, expected=actual))

    def strcpy(destination, source, *args):
        p.cpu.mem_write(destination, text(source).encode("utf-8") + b"\0")
        return destination

    p.hook_import("strcpy", strcpy)
    for address, offset in ((0x595cc, 0x18), (0x596d8, 0x1c), (0x597e4, 0x20)):
        p.hook(address, lambda owner, amount, *args, offset=offset: p.write(owner + offset, "I", amount ^ 0x7ead3) or 0)

    def load(values, shortage=0):
        raw = bytearray(180)
        struct.pack_into("<6i", raw, 0, 10000, 10000, 10000, 1, 2, 2)
        raw[24:28], raw[40:44] = b"fra\0", b"fra\0"
        struct.pack_into("<2f", raw, 56, 1.25, 0.125)
        struct.pack_into("<26i", raw, 68, *values)
        struct.pack_into("<2i", raw, 172, 6, shortage)
        p.cpu.mem_write(country, bytes(0x300))
        p.cpu.mem_write(record, bytes(raw))
        p.call(0x59850, country, record)
        p.write(country + 8, "Q", country + 8)

    for index in range(26):
        for value in (-1, 0, 1, 2, 3, 7):
            values = [0] * 26
            values[index] = value
            load(values, 3)
            actual = [p.call(0x5c60c, country, i) for i in range(26)]
            check("table_deserialization", actual, values, [index, value])
            check("shortage_deserialization", p.read(country + 0x2c, "i")[0], 3, [index, value])
            p.call(0x5c600, country, index, 11)
            check("table_setter", p.call(0x5c60c, country, index), 11, index)
            vectors["tables"].append(dict(index=index, value=value))

    p.hook(0x85824, lambda *args: tile)
    p.hook(0x4e898, lambda *args: 31)
    p.hook(0x4e8f0, lambda *args: 17)
    p.hook(0x4e948, lambda *args: 13)
    for index in range(26):
        for value in (-1, 0, 1, 2, 3, 4, 9):
            values = [0] * 26
            values[index] = value
            load(values)
            sentinel, link = country + 8, SCRATCH + 0xb400
            p.write(sentinel, "Q", link)
            p.write(link, "2Qi", sentinel, 0, 0)
            actual = [p.call(address, country) for address in (0x591e8, 0x592a0, 0x59358)]
            bonus = value if value in (1, 2, 3) else 0
            expected = [38 + (20 * bonus if index == 23 else 0),
                        21 + (10 * bonus if index == 24 else 0), 13 + (10 * bonus if index == 22 else 0)]
            check("income_table_slots", actual, expected, [index, value])
            vectors["income"].append(dict(index=index, value=value, expected=actual))

    p.hook(0x31844, lambda *args: system)
    p.hook(0x6a4a8, lambda *args: row if row_valid else 0)
    p.hook(0x4ef64, lambda address, *args: p.read(address + 0x70, "Q")[0])
    p.hook(0x4f01c, lambda *args: unit)
    p.hook(0x4f42c, lambda *args: 0)
    for address in (0x51f84, 0x4eab8):
        p.hook(address, lambda *args: 0)

    def spend(offset, delta):
        delta = delta if delta < 0x80000000 else delta - 0x100000000
        value = (p.read(country + offset, "I")[0] ^ 0x7ead3) + delta
        p.write(country + offset, "I", value ^ 0x7ead3)
        return 0

    p.hook(0x5964c, lambda _, delta, *args: spend(0x18, delta))
    p.hook(0x59758, lambda _, delta, *args: spend(0x1c, delta))
    buildings = list(ET.parse(ROOT / "apk_work/original/assets/def_construction.xml").getroot())
    generator = random.Random(1930)
    unit_categories = [0] * 6 + [1] * 4 + [2] * 4 + [3] * 4 + [4] * 4
    for case in range(1200):
        card_id = case % 41
        card = cards[card_id].attrib
        army = names.index(card["army"])
        category = unit_categories[army]
        grade = int(card["grade"])
        kind = generator.randrange(5)
        if case % 3 == 0 and card_id < 37:
            choices = [(k, i + 1) for k, b in enumerate(buildings) for i, r in enumerate(b)
                       if any(c.attrib["name"] == card["army"] and int(c.attrib["grade"]) >= grade for c in r)]
            kind, forced_level = generator.choice(choices)
        rows = list(buildings[kind])
        level = forced_level if case % 3 == 0 and card_id < 37 else generator.randrange(len(rows) + 2)
        row_valid = 1 <= level <= len(rows)
        allowed = {names.index(r.attrib["name"]): int(r.attrib["grade"]) for r in rows[level - 1]} if row_valid else {}
        veteran = generator.choice([-1, 0, 1, 2, 3])
        transport = generator.randrange(5)
        values = [0] * 26
        values[army], values[25] = veteran, transport
        load(values)
        active, own, occupied, sea, built, fort_used = [bool(generator.randrange(2)) for _ in range(6)]
        if case % 3 == 0:
            active, own, occupied, sea, built, fort_used = True, True, False, False, card_id < 37, False
            veteran = values[army] = max(0, veteran)
            load(values)
        money = int(card["price"]) - (1 if case % 7 == 1 else -100)
        industry = int(card["industry"]) - (1 if case % 11 == 1 and int(card["industry"]) > 0 else -100)
        p.write(country + 0x18, "2I", money ^ 0x7ead3, industry ^ 0x7ead3)
        p.write(country + 0x57, "B", 1)
        p.write(country + 0x78, "B", int(fort_used))
        p.cpu.mem_write(tile, bytes(0x100))
        p.write(tile + 0x14, "B", int(active))
        p.write(tile + 0x1c, "B", int(sea))
        p.write(tile + 0x40, "Q", building if built else 0)
        p.write(tile + 0x68, "Q", country if own else record)
        p.write(tile + 0x70, "Q", definition if occupied else 0)
        p.write(building + 4, "2i", kind, level)
        entries = SCRATCH + 0xb100
        for i, pair in enumerate(allowed.items()):
            p.write(entries + i * 8, "2i", *pair)
        p.write(row + 0x20, "2Q", entries, entries + len(allowed) * 8)
        p.cpu.mem_write(unit, bytes(0x100))
        p.write(unit + 8, "Q", definition)
        p.write(unit + 0x28, "i", 6)
        p.write(definition + 8, "2i", army, category)
        address = catalog + 0x290 + card_id * 0x38
        eligible = active and own and not occupied and veteran >= 0 and (
            built and army in allowed and grade <= allowed[army] if card_id < 37 else not built and not sea and not fort_used)
        success = eligible and money >= int(card["price"]) and industry >= int(card["industry"])
        actual_eligible = bool(p.call(0x5aa98, country, address, 0))
        actual_success = bool(p.call(0x5ac5c, country, address, 0, -1))
        actual = [actual_eligible, actual_success, p.read(country + 0x18, "I")[0] ^ 0x7ead3,
                  p.read(country + 0x1c, "I")[0] ^ 0x7ead3, p.read(unit + 0x34, "i")[0],
                  bool(p.read(unit + 0x59, "B")[0]), bool(p.read(country + 0x78, "B")[0]),
                  p.read(unit + 0x60, "i")[0], p.read(unit + 0x28, "i")[0], bool(p.read(unit + 0x5a, "B")[0])]
        expected = [eligible, success, money - (int(card["price"]) if success else 0),
                    industry - (int(card["industry"]) if success else 0), veteran if success else 0,
                    success and card_id < 37 and category < 3 and transport > category,
                    fort_used or success and card_id >= 37,
                    int(card.get("buildround", 0)) if success else 0, 0 if success else 6, success]
        check("loaded_faction_card_recruitment", actual, expected, case)
        counts["successful_recruitments"] += int(success)
        vectors["recruitment"].append(dict(card=card_id, kind=kind, level=level, veteran=veteran,
            transport=transport, active=active, own=own, occupied=occupied, sea=sea, building=built,
            fort_used=fort_used, money=money, industry=industry, expected=actual))
    if counts["successful_recruitments"] < 50:
        raise AssertionError("Insufficient successful recruit coverage")
    report = dict(counts=dict(counts), errors=errors,
        scope="Original card loader and faction table/income/recruit chain; injected XML primitives, map lookup, row source and allocation; no full scenario or device test.")
    (ROOT / "reverse/reports/native_faction_recruitment_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/faction_recruitment_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
