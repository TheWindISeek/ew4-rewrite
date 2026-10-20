"""Run original building XML loader and rule accessors with parsed XML services.

XML traversal, string storage and allocation are injected. The original type mapping,
row construction, lookup, recruitment, capital-priority and upgrade clamp run unchanged.
"""

from collections import Counter
import json
import xml.etree.ElementTree as ET

from validate_native_rules import NativeProbe, ROOT, SCRATCH, STACK, RETURN


def main():
    p = NativeProbe()
    definitions, building, capital = SCRATCH, SCRATCH + 0x400, SCRATCH + 0x500
    heap, next_node, next_string = SCRATCH + 0x6000, SCRATCH + 0x1000, SCRATCH + 0xc000
    values, strings = {}, {}
    counters, errors = Counter(), []
    type_names = ["city", "industry", "stable", "port", "farmland"]
    army_names = ["Militia", "Line Infantry", "Light Infantry", "Grenadier", "Guards", "Machine Gun",
                  "Light Cavalry", "Heavy Cavalry", "Guards Cavalry", "Armored Car", "Light Artillery", "Heavy Artillery",
                  "Siege Artillery", "Rocket", "Privateer", "Frigate", "Battleship", "Ironclad", "Small Fortress", "Fortress", "Large Fortress", "Coastal Fort"]

    def text(address):
        data = bytearray()
        while True:
            value = p.read(address + len(data), "B")[0]
            if not value:
                return data.decode("utf-8")
            data.append(value)

    def string(value):
        nonlocal next_string
        if value not in strings:
            raw = value.encode("utf-8") + b"\0"
            strings[value] = next_string
            p.cpu.mem_write(next_string, raw)
            next_string += len(raw)
            if next_string >= SCRATCH + 0xf000:
                raise ValueError("String arena exhausted")
        return strings[value]

    def allocate(size, *args):
        nonlocal heap
        result = heap
        heap += (size + 15) & ~15
        if heap >= SCRATCH + 0xc000:
            raise ValueError("Probe heap exhausted")
        p.cpu.mem_write(result, bytes(size))
        return result

    def node(element):
        nonlocal next_node
        address = next_node
        next_node += 0x80
        if next_node >= SCRATCH + 0x6000:
            raise ValueError("XML node arena exhausted")
        values[address] = element.attrib
        p.write(address, "Q", SCRATCH + 0xf000)
        children = [node(child) for child in element]
        if children:
            p.write(address + 0x28, "Q", children[0])
            for left, right in zip(children, children[1:]):
                p.write(left + 0x48, "Q", right)
        return address

    xml = ET.parse(ROOT / "apk_work/original/assets/def_construction.xml").getroot()
    root_node = node(xml)
    p.write(SCRATCH + 0xf060, "Q", RETURN + 0x200)
    p.hook(RETURN + 0x200, lambda address, *args: address)
    p.hook(0x938a4, lambda *args: string("unused-resource-name"))
    p.hook(0xe92c8, lambda *args: 0)
    p.hook(0xe9718, lambda *args: 1)
    p.hook(0xe84b8, lambda *args: root_node)
    p.hook(0xbb48c, lambda address, key, fallback, *args: int(values[address].get(text(key), fallback)))
    p.hook(0xbb54c, lambda address, key, fallback, *args: string(values[address].get(text(key), text(fallback))))
    p.hook(0xe8cf4, lambda address, key, *args: string(values[address][text(key)]))
    p.hook(0x676e8, lambda name, *args: army_names.index(text(name)))
    p.hook(0x173504, lambda *args: 0)
    p.hook(0xe7df8, lambda *args: 0)
    p.hook_import("_Znwm", allocate)
    p.hook_import("_ZdlPv", lambda *args: 0)
    p.hook_import("strlen", lambda address, *args: len(text(address).encode("utf-8")))
    p.hook_import("strcmp", lambda a, b, *args: (text(a) > text(b)) - (text(a) < text(b)))

    def move(target, source, count, *args):
        p.cpu.mem_write(target, bytes(p.cpu.mem_read(source, count)))
        return target

    p.hook_import("memmove", move)
    p.cpu.mem_write(STACK, bytes(0x10000))
    p.call(0x69d88, definitions)

    def check(group, actual, expected, inputs):
        counters[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected, inputs=inputs))
            raise AssertionError(errors)

    system = SCRATCH + 0x900
    p.write(system + 0x110, "Q", definitions)
    p.hook(0x31844, lambda *args: system)
    p.hook(0x31c7c, lambda *args: 0)
    capital_found = False
    p.hook(0x69cb8, lambda *args: capital if capital_found else 0)
    vectors = []
    for construction in xml:
        kind = type_names.index(construction.attrib["type"])
        metadata = definitions + 0xb8 + kind * 32
        actual_kind, maximum = p.read(metadata, "2i")
        check("type_and_upgrade_maximum", [actual_kind, maximum], [kind, int(construction.attrib["maxlevel"])], kind)
        rows = list(construction)
        for level in range(len(rows) + 2):
            p.write(building + 4, "2i", kind, level)
            p.write(building + 0x18, "i", -1)
            row = rows[level - 1] if 1 <= level <= len(rows) else None
            actual = [p.call(address, building) for address in (0x55a94, 0x55b08, 0x55b50, 0x55b98, 0x55c38)]
            expected = [int(row.get(key, 0)) if row is not None else 0 for key in ("tax", "industry", "food", "supply", "avoid")]
            check("level_accessors", actual, expected, [kind, level])
            allowed = {army_names.index(r.attrib["name"]): int(r.attrib["grade"]) for r in row} if row is not None else {}
            for unit in range(22):
                check("recruit_grade", p.call(0x557f4, building, unit), allowed.get(unit, -1), [kind, level, unit])
                for grade in range(3):
                    check("recruit_eligibility", p.call(0x5575c, building, unit, grade), int(unit in allowed and grade <= allowed[unit]), [kind, level, unit, grade])
            vectors.append(dict(kind=kind, name=type_names[kind], level=level, expected=actual, recruitment=allowed))
        for requested in (0, 1, len(rows), len(rows) + 1, 255):
            p.call(0x556b4, building, kind, requested, 42)
            check("constructor_level_clamp", list(p.read(building, "4i")), [42, kind, min(requested, len(rows)), maximum], [kind, requested])
        for capital_found in (False, True):
            p.write(building + 4, "2i", kind, 1)
            p.write(building + 0x18, "i", 5)
            p.write(capital + 4, "i", 123)
            actual = p.call(0x55a94, building)
            expected = 123 if kind == 0 and capital_found else int(rows[0].get("tax", 0))
            check("capital_priority", actual, expected, [kind, capital_found])
            vectors.append(dict(kind=kind, name=type_names[kind], level=1, capital=123 if capital_found else None, expected_tax=actual))

    report = dict(counts=dict(counters), errors=errors, scope=__doc__.strip())
    (ROOT / "reverse/reports/native_building_catalog_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/building_catalog_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
