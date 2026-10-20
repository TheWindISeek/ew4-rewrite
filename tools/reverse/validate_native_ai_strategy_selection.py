"""Observe original multi-country strategy selection with injected path/site services."""

import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, expected_distance, registers
from validate_native_ai_strategy import f32
from unicorn import UC_HOOK_CODE


def main():
    p = NativeProbe()
    p.cpu.mem_map(SCRATCH + 0x10000, 0x30000)
    ai, unit, definition, action, candidates, map_object, container = [SCRATCH + i * 0x1000 for i in range(1, 8)]
    tiles = [SCRATCH + 0x10000 + i * 0x100 for i in range(27)]
    countries = [SCRATCH + 0x13000 + i * 0x100 for i in range(6)]
    nodes = [SCRATCH + 0x14000 + i * 0x40 for i in range(12)]
    target_units = [SCRATCH + 0x15000 + i * 0x100 for i in range(27)]
    target_defs = [SCRATCH + 0x17000 + i * 0x100 for i in range(27)]
    path_buffer, reachable_buffer, approach_buffer = SCRATCH, SCRATCH + 0x100, SCRATCH + 0x200
    fixture, draws, bounds = {}, [], []
    p.hook(0x85824, lambda _, index, *args: tiles[index])
    p.hook(0xa9744, lambda _, index, *args: tiles[index])
    p.hook(0x77490, lambda _, tile, *args: fixture["sites"][tiles.index(tile)]["value"])
    p.hook(0x773a0, lambda _, tile, *args: fixture["sites"][tiles.index(tile)]["strength"])
    p.hook(0x31c7c, lambda *args: container)
    p.hook(0x80b38, lambda *args: len(countries))
    p.hook(0x80904, lambda _, index, *args: countries[index])
    p.hook(0x6ae84, lambda *args: 1)
    p.hook(0x5a9f4, lambda *args: int(fixture["affordable"]))
    p.hook(0x8dd7c, lambda *args: 0)
    p.hook_import("_ZdlPv", lambda *args: 0)

    def relation(_, source, destination, mode, *args):
        value = fixture["sites"][destination]["relation"]
        return int(mode == 0x100 and value == "allied" or mode == 0x400 and value == "owned")

    p.hook(0x7725c, relation)
    p.hook(0x4ea88, lambda tile, *args: fixture["sites"][tiles.index(tile)]["building"])

    def draw(*args):
        value = fixture["raw_random"][len(draws)]
        draws.append(value)
        return value

    p.hook_import("rand", draw)

    def observe(cpu, pc, size, user_data):
        bounds.append(cpu.reg_read(registers.UC_ARM64_REG_X0) & 0xffffffff)

    p.cpu.hook_add(UC_HOOK_CODE, observe, begin=0xef658, end=0xef658)

    def reach(_, cell, mode, *args):
        if mode in (2000, 2001):
            p.write(approach_buffer, "i", cell)
            p.write(ai + 0x20, "2Q", approach_buffer, approach_buffer + 4)
            return int(fixture["sites"][cell]["sea_approach"])
        return fixture["pressure"] if mode == 0x7da else 0

    def paths(_, source, output, *args):
        cells = fixture["paths"]
        if cells:
            p.write(path_buffer, f"{len(cells)}i", *cells)
        p.write(output, "3Q", path_buffer, path_buffer + len(cells) * 4, path_buffer + 0x80)
        return 0

    def sea_paths(_, output, *args):
        cells = [i for i, site in enumerate(fixture["sites"]) if site["sea_approach"]]
        if cells:
            p.write(reachable_buffer, f"{len(cells)}i", *cells)
        p.write(output, "3Q", reachable_buffer, reachable_buffer + len(cells) * 4, reachable_buffer + 0x80)
        return 0

    p.hook(0x78608, reach)
    p.hook(0x862c4, paths)
    p.hook(0x8e0bc, sea_paths)
    generator = random.Random(1930)
    fixtures, errors = [], []
    distance = lambda a, b: expected_distance(a % 9, a // 9, b % 9, b // 9)
    for case in range(800):
        for address in (ai, unit, definition, action, map_object, container):
            p.cpu.mem_write(address, bytes(0x400))
        sites = [dict(value=generator.randrange(10, 800), strength=generator.randrange(200), present=bool(generator.randrange(2)),
            naval=bool(generator.randrange(2)), sea=bool(generator.randrange(2)), environment=generator.randrange(3),
            relation=generator.choice(["owned", "hostile", "allied"]), building=generator.choice([0, 1, 2, 3, 4, 255]),
            sea_approach=bool(generator.randrange(2))) for _ in tiles]
        sites[0].update(present=True, strength=generator.randrange(300))
        naval, transport, commander = [bool(generator.randrange(2)) for _ in range(3)]
        maximum = generator.randrange(120, 301)
        fixture = dict(sites=sites, paths=generator.sample(list(range(12)), generator.randrange(13)),
            naval=naval, transport=transport, commander=commander, maximum=maximum, health=generator.randrange(1, maximum+1),
            movement=generator.randrange(1, 9), behavior=generator.randrange(4), pressure=generator.randrange(300),
            affordable=bool(generator.randrange(2)), auto_transport=bool(generator.randrange(2)),
            raw_random=[generator.randrange(32768) for _ in range(100)])
        if case % 13 == 0:
            fixture["health"] = maximum
        if case % 17 == 0:
            sites[12]["value"], sites[12]["present"] = 44, False
        p.write(ai + 8, "Q", container)
        p.write(ai + 0x90, "Q", map_object)
        p.write(ai + 0x98, "Q", action)
        p.write(ai + 0x68, "3Q", candidates, candidates, candidates + 0x100)
        p.write(action + 0x40, "Q", countries[0])
        p.write(map_object + 0x100, "2i", 9, 3)
        for index, tile in enumerate(tiles):
            p.cpu.mem_write(tile, bytes(0x100))
            p.cpu.mem_write(target_units[index], bytes(0x100))
            p.cpu.mem_write(target_defs[index], bytes(0x100))
            site = sites[index]
            p.write(tile, "5i", index % 9, index // 9, index, index % 9 * 64 + 32 * (index // 9 % 2), index // 9 * 54)
            p.write(tile + 0x1c, "B", int(site["sea"]))
            p.write(tile + 0x50, "i", site["environment"])
            p.write(tile + 0x70, "Q", target_units[index] if site["present"] else 0)
            p.write(target_units[index] + 8, "Q", target_defs[index])
            p.write(target_defs[index] + 0xc, "i", 3 if site["naval"] else 0)
        p.write(tiles[0] + 0x68, "2Q", countries[0], unit)
        for index, country in enumerate(countries):
            p.cpu.mem_write(country, bytes(0x100))
            p.write(country, "i", index + 1)
            first = nodes[index * 2]
            second = nodes[index * 2 + 1]
            p.write(country + 8, "2Q", first, second)
            p.write(first, "2Qi", second, country + 8, 12 + index * 2)
            p.write(second, "2Qi", country + 8, first, 13 + index * 2)
        p.write(unit + 8, "2Q", definition, countries[0])
        p.write(unit + 0x18, "Q", 1 if commander else 0)
        p.write(unit + 0x20, "3i", fixture["health"], maximum, fixture["movement"])
        p.write(unit + 0x59, "B", int(transport))
        p.write(unit + 0x64, "i", fixture["behavior"])
        p.write(definition + 8, "2i", 14 if naval else 0, 3 if naval else 0)
        p.write(0x222e07, "B", int(fixture["auto_transport"]))
        draws, bounds = [], []
        p.call(0x7a768, ai, 0x4000, 0)
        count = (p.read(ai + 0x70, "Q")[0] - candidates) // 12
        actual = [list(p.read(candidates + i * 12, "3i")) for i in range(count)]
        expected, expected_bounds, expected_draws, selected_goals = [], [], [], []

        def sample(bound):
            expected_bounds.append(bound & 0xffffffff)
            if not bound:
                return 0
            value = fixture["raw_random"][len(expected_draws)]
            expected_draws.append(value)
            return value - int(value / bound) * bound

        rolling = (30 if fixture["health"] > 119 else 10) + (10 if fixture["health"] == maximum else 0) + (20 if naval or transport else 0) + (20 if commander else 0)
        marker = -1
        for country_index in (1, 2, 5):
            goal, best = None, -10000000
            for index in (12 + country_index * 2, 13 + country_index * 2):
                site = sites[index]
                if not naval and site["value"] <= 44 and not site["present"]:
                    continue
                score = site["value"] // 2 + site["strength"]
                if naval:
                    score += 30 if site["present"] and site["sea_approach"] else -120
                elif sites[0]["sea"]:
                    score += -30 if site["present"] and site["naval"] else 30 if site["sea"] else 60
                else:
                    score += (7 if site["naval"] else 15) if site["present"] and site["sea"] else 30
                if score > best:
                    goal, best = index, score
            if goal is None:
                continue
            selected_goals.append(goal)
            rolling += best
            if fixture["pressure"]:
                if sites[0]["strength"] >= fixture["pressure"]:
                    rolling = int(rolling / 2)
                elif sites[0]["strength"] > fixture["pressure"] // 2:
                    rolling = int(rolling / 3)
                elif sites[0]["strength"] < fixture["pressure"] // 2:
                    rolling = int(rolling / 4)
            rolling = int(f32(f32(rolling) * f32(f32(fixture["health"]) / f32(maximum))))
            rolling = int(rolling / ((distance(0, goal) // (fixture["movement"] // 2) if fixture["movement"] // 2 else 0) + 1))
            destination, nearest = None, 100000
            for index in fixture["paths"]:
                site = sites[index]
                if site["present"] or site["environment"] > 0:
                    continue
                if site["relation"] == "allied":
                    if distance(0, index) != 1 or site["building"] in (0, 1, 2, 3):
                        continue
                    if site["building"] == 4 and sample(100) >= 6:
                        continue
                d = distance(index, goal)
                if d < nearest and (fixture["behavior"] != 2 or distance(0, index) == 1):
                    destination, nearest = index, d
            if destination is None:
                continue
            if not sites[0]["sea"] and not transport and not naval:
                if fixture["auto_transport"]:
                    if sites[destination]["sea_approach"]:
                        marker = destination
                elif fixture["affordable"]:
                    value = sample(100)
                    if sites[destination]["sea_approach"] and value > 50:
                        marker = destination
            behavior = fixture["behavior"]
            if behavior == 3:
                rolling = 0
            elif behavior in (0, 2):
                floor = int(rolling / (2 if behavior == 0 else 4))
                rolling = floor + sample(floor if behavior == 0 else rolling - floor)
            expected.append([destination, marker, rolling])
        if actual != expected or bounds != expected_bounds or draws != expected_draws:
            errors.append(dict(case=case, actual=actual, expected=expected, bounds=bounds, expected_bounds=expected_bounds,
                draws=draws, expected_draws=expected_draws))
            if len(errors) >= 3:
                raise AssertionError(errors)
        fixtures.append(dict(inputs=fixture, expected=actual, selected_goals=selected_goals, random_bounds=bounds))
    report = dict(count=len(fixtures), errors=errors,
        scope="Real 7a768 multi-country/two-goal selection, destination filters and transport marker; injected site/strength/reachability/affordability and raw rand. Map distance, behavior weighting and ef658 execute unchanged. Confirms rolling score across factions.")
    (ROOT / "reverse/reports/native_ai_strategy_selection_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_strategy_selection_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
