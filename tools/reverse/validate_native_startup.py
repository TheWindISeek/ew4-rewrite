"""Check scenario dispatch ordering and first human turn-order promotion."""

from collections import Counter
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH, registers


def main():
    rng, counts, vectors = random.Random(1946), Counter(), dict(dispatch=[], ordering=[], initialization=[], ai_bounds=[])

    def check(group, actual, expected):
        counts[group] += 1
        if actual != expected:
            raise AssertionError(dict(group=group, actual=actual, expected=expected))

    p = NativeProbe()
    battle, countries, raw_pointer, player = SCRATCH + 0x1000, SCRATCH + 0x3000, SCRATCH + 0x9000, SCRATCH + 0x8000
    tile_base = 0x7000000
    p.cpu.mem_map(tile_base, 0x10000)
    events, allocated = [], []
    p.hook(0x7c9b4, lambda *args: (events.append(["clear"]) or 0))
    p.hook(0x84ae4, lambda _, map_id, left, top, *args: (events.append(["map", map_id, left, top]) or 0))
    p.hook(0x2f108, lambda *args: player)

    def allocate(*args):
        identity = countries + len(allocated) * 0x400
        allocated.append(identity)
        return identity

    def record(kind, offset):
        return lambda _, source, *args: (events.append([kind, source - raw_pointer - offset]) or 0)

    p.hook(0x807d0, allocate)
    p.hook(0x59c20, lambda *args: 0)
    p.hook(0x59850, record("faction", 0))
    p.hook(0x80904, lambda _, index, *args: allocated[index])
    p.hook(0x85374, lambda _, x, y, *args: tile_base + (y * 8 + x) * 0x100)
    p.hook(0xa9744, lambda _, cell, *args: tile_base + cell * 0x100)
    p.hook(0x58b94, lambda *args: 0)
    p.hook(0x51b2c, record("site", 0))
    p.hook(0x702a4, lambda *args: SCRATCH + 0x7000)
    p.hook_import("_Znwm", lambda *args: SCRATCH + 0x6000)
    p.hook(0x51d60, lambda *args: 0)
    p.hook(0x52318, lambda *args: 0)
    p.hook(0x53cf8, record("army", 0))
    p.hook(0x4ef6c, lambda *args: 0)
    p.hook(0x837e4, lambda *args: SCRATCH + 0x7000)
    p.hook(0x5d7fc, record("market", 0))
    p.hook(0x8d120, lambda *args: SCRATCH + 0x7000)
    p.hook(0x5e354, record("tavern", 0))
    p.hook(0x8d6c0, record("event", 0))
    p.hook(0x83f6c, lambda *args: (events.append(["finalize_map"]) or 0))
    p.hook(0x80dc4, lambda _, index, *args: (events.append(["active", index]) or 0))

    def compare(left, right, *args):
        a = bytes(p.cpu.mem_read(left, 16)).split(b"\0")[0]
        b = bytes(p.cpu.mem_read(right, 16)).split(b"\0")[0]
        return (a > b) - (a < b)

    p.hook_import("strcmp", compare)
    for case in range(200):
        mode = rng.choice([1, 2, 4])
        selected = rng.randrange(3)
        p.cpu.mem_write(player, f"f{selected}".encode() + bytes(14))
        p.cpu.mem_write(battle, bytes(0x100))
        p.cpu.mem_write(tile_base, bytes(0x10000))
        p.write(battle + 0x68, "i", mode)
        header = [2, 1, 1, 2, 2, 3, 3, 2, 2, 2, 1, 1, 50, 20, 1, 1, 1, 0]
        data = bytearray(struct.pack("<18i", *header))
        expected = [["clear"], ["map", 1, 1, 2]]
        for index in range(3):
            source = bytearray(180)
            struct.pack_into("<i", source, 12, 1)
            source[24:27] = f"f{index}\0".encode()
            source[40:44] = b"fra\0"
            expected.append(["faction", len(data)])
            data += source
        data += bytes([0, 1, 0, 1, 255, 255])
        for kind, count, stride in [("site", 2, 16), ("army", 2, 32), ("environment", 1, 8), ("market", 1, 116), ("tavern", 1, 108), ("event", 2, 44)]:
            for index in range(count):
                source = bytearray(stride)
                struct.pack_into("<h", source, 0, 17 + index)
                if kind == "market": struct.pack_into("<i", source, 112, 17)
                if kind == "tavern": struct.pack_into("<i", source, 104, 17)
                if kind != "environment": expected.append([kind, len(data)])
                data += source
        expected += [["finalize_map"], ["active", 0]]
        p.cpu.mem_write(raw_pointer, bytes(data))
        events.clear(); allocated.clear()
        check("scenario_dispatch_return", p.call(0x7cb80, battle, raw_pointer, len(data), 1), 1)
        check("scenario_dispatch_order", events, expected)
        actual_ai = [p.read(raw_pointer + 72 + i * 180 + 12, "i")[0] for i in range(3)]
        check("selected_conquest_human", actual_ai, [int(mode != 2 or i != selected) for i in range(3)])
        events.clear(); allocated.clear()
        check("scenario_length_rejection", p.call(0x7cb80, battle, raw_pointer, len(data) - 1, 1), 0)
        check("bad_length_no_subsystem_import", events, [["clear"]])
        vectors["dispatch"].append(dict(mode=mode, selected=selected, expected_ai=actual_ai))

    p = NativeProbe()
    country_manager, array, countries = SCRATCH + 0x1000, SCRATCH + 0x2000, SCRATCH + 0x3000
    p.hook_import("memmove", lambda dest, src, length, *args: (p.cpu.mem_write(dest, bytes(p.cpu.mem_read(src, length))) or dest))

    def insert(vector, where, item, *args):
        begin, end = p.read(vector, "2Q")
        values = list(p.read(begin, f"{(end - begin) // 8}Q"))
        values.insert((where - begin) // 8, p.read(item, "Q")[0])
        p.write(begin, f"{len(values)}Q", *values)
        p.write(vector + 8, "Q", end + 8)
        return where

    p.hook(0x81030, insert)
    for case in range(400):
        count = rng.randrange(1, 15)
        ai = [bool(rng.randrange(2)) for _ in range(count)]
        pointers = [countries + i * 0x400 for i in range(count)]
        for pointer, flag in zip(pointers, ai):
            p.cpu.mem_write(pointer, bytes(0x260))
            p.write(pointer + 0x238, "i", 2)
            p.write(pointer + 0x54, "B", int(flag))
        p.write(array, f"{count}Q", *pointers)
        p.write(country_manager + 0x38, "3Q", array, array + count * 8, array + (count + 1) * 8)
        p.call(0x811dc, country_manager)
        selected = ai.index(False) if False in ai else None
        expected = pointers if selected is None else [pointers[selected]] + pointers[:selected] + pointers[selected + 1:]
        actual = list(p.read(array, f"{count}Q"))
        check("first_human_stable_promotion", actual, expected)
        vectors["ordering"].append(dict(ai=ai, expected=[pointers.index(value) for value in actual]))

    p = NativeProbe()
    battle, countries, manager = SCRATCH + 0x1000, SCRATCH + 0x3000, SCRATCH + 0x2000
    fixture, order, stages = {}, [], []
    p.hook(0x2f108, lambda *args: 0)
    p.hook(0x938a4, lambda *args: 0)
    p.hook(0x7d258, lambda *args: (stages.append("load") or 0))
    p.hook(0x331a0, lambda *args: 0)
    p.hook(0x2f100, lambda *args: 0)
    p.hook(0x80b38, lambda *args: len(order))

    def promote(*args):
        player_index = order.index(fixture["player"])
        order.insert(0, order.pop(player_index))
        stages.append("promote")
        return 0

    p.hook(0x811dc, promote)
    p.hook(0x80c50, lambda *args: countries + fixture["player"] * 0x400)
    p.hook(0x80904, lambda _, index, *args: countries + order[index] * 0x400)
    p.hook(0x5c2d4, lambda country, *args: (stages.append("commander_cache." + str((country - countries) // 0x400)) or 0))
    p.hook(0x2c97c, lambda _, warzone, index, *args: fixture["technology"][index])
    p.hook(0x7dcd4, lambda *args: (stages.append("center") or 0))
    p.hook(0x86af8, lambda *args: (stages.append("map_refresh") or 0))
    p.hook(0xa7d38, lambda *args: (stages.append("ai_refresh") or 0))
    for case in range(300):
        count = rng.randrange(2, 10)
        fixture = dict(mode=rng.choice([1, 2, 4]), player=rng.randrange(count), camps=[rng.randrange(1, 5) for _ in range(count)],
            technology=[rng.randrange(-1, 6) for _ in range(26)])
        p.cpu.mem_write(battle, bytes(0x100))
        p.write(battle + 0x68, "i", fixture["mode"])
        p.write(battle + 0x90, "B", 1)
        initial, expected = [], []
        for index in range(count):
            pointer = countries + index * 0x400
            p.cpu.mem_write(pointer, bytes(0x300))
            p.write(pointer, "i", fixture["camps"][index])
            p.write(pointer + 0x54, "B", int(index != fixture["player"]))
            income, evasion = rng.choice([0.5, 1.0, 1.5, 2.0]), rng.choice([0.0, 0.25, 0.5])
            p.write(pointer + 0x24, "2f", income, evasion)
            initial.append([income, evasion])
            normal = fixture["mode"] == 2 and fixture["camps"][index] == fixture["camps"][fixture["player"]]
            expected.append([1.0, 0.0] if normal else [income, evasion])
            p.write(pointer + 0x1d0, "26i", *([-99] * 26))
        order[:] = range(count); stages.clear()
        try:
            p.call(0x7dd18, battle)
        except Exception as error:
            raise RuntimeError(dict(function="7dd18", pc=hex(p.cpu.reg_read(registers.UC_ARM64_REG_PC)), stages=stages, fixture=fixture)) from error
        actual = [list(p.read(countries + index * 0x400 + 0x24, "2f")) for index in range(count)]
        check("new_game_camp_normalization", actual, expected)
        check("new_game_initialization_sequence", stages, ["load", "promote"] + ["commander_cache." + str(index) for index in order] + ["center", "map_refresh", "ai_refresh"])
        for index in range(count):
            tech = fixture["technology"] if fixture["mode"] == 1 and index == fixture["player"] else [-99] * 26
            check("new_game_technology_target", list(p.read(countries + index * 0x400 + 0x1d0, "26i")), tech)
        vectors["initialization"].append(dict(**fixture, initial=initial, expected=actual, order=list(order)))

    p = NativeProbe()
    planner, terrain, assist, tile = [SCRATCH + i * 0x1000 for i in range(1, 5)]
    p.hook(0x31844, lambda *args: 0)
    p.hook(0x31c7c, lambda *args: 0)
    p.write(planner + 0x60, "Q", terrain)
    p.write(planner + 0x70, "Q", assist)
    visits = []

    def location(_, x, y, *args):
        visits.append([x, y])
        p.write(tile + 0x14, "B", int((x + y) % 3 != 0))
        p.write(tile + 0x1c, "B", (x + y) % 2)
        return tile

    p.hook(0x85374, location)
    different = 0
    for case in range(400):
        left, top, width, height = rng.randrange(5), rng.randrange(5), rng.randrange(1, 6), rng.randrange(1, 6)
        p.write(terrain + 0x108, "4i", left, top, width, height)
        visits.clear()
        p.call(0xa7d38, planner)
        expected = [[x, y] for y in range(left, top + height) for x in range(left, left + width)]
        correct = [[x, y] for y in range(top, top + height) for x in range(left, left + width)]
        check("native_ai_region_iteration", visits, [pair for pair in expected for _ in range(1 + int(sum(pair) % 3 != 0))])
        active = [pair for pair in expected if sum(pair) % 3 != 0]
        sea = sum(sum(pair) % 2 == 1 for pair in active)
        check("native_ai_region_counts", list(p.read(assist + 0x38, "3i")), [len(active) - sea, sea, len(active)])
        different += expected != correct
        vectors["ai_bounds"].append(dict(rect=[left, top, width, height], native_visited=len(expected), correct_visited=len(correct)))
    report = dict(counts=dict(counts), errors=[], native_ai_wrong_region_cases=different,
        scope="7cb80 real driver with injected subsystem loaders/allocations; 811dc real promotion with vector insertion+memmove services; 7dd18 real camp normalization/technology dispatch with loaders/getters/caches injected; a7d38 real rectangle iteration with tile lookup injected. Not complete game initialization or device testing.")
    (ROOT / "reverse/reports/native_startup_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/startup_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
