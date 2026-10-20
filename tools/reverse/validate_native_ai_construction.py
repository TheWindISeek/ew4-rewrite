"""Probe original installation and fortress AI with controlled neighborhood queries."""

import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, registers
from unicorn import UC_HOOK_CODE


def main():
    p = NativeProbe()
    p.cpu.mem_map(SCRATCH + 0x10000, 0x20000)
    ai, action, country, catalog, card, buffer = [SCRATCH + i * 0x1000 for i in range(1, 7)]
    tiles = [SCRATCH + 0x10000 + i * 0x100 for i in range(64)]
    units = [SCRATCH + 0x15000 + i * 0x100 for i in range(5)]
    definitions = [SCRATCH + 0x16000 + i * 0x100 for i in range(5)]
    nodes = [SCRATCH + 0x17000 + i * 0x40 for i in range(4)]
    fixture, all_sites, bounds, raw_draws = {}, [], [], []
    p.hook(0x85824, lambda _, index, *args: tiles[index])
    p.hook(0x4ea88, lambda tile, *args: all_sites[tiles.index(tile)]["building"])
    p.hook(0x4eaa0, lambda tile, *args: all_sites[tiles.index(tile)]["level"])
    p.hook(0x4eb60, lambda tile, *args: all_sites[tiles.index(tile)]["installation"])
    p.hook(0x4ebf8, lambda tile, *args: int(all_sites[tiles.index(tile)]["coastal"]))
    p.hook(0x859d8, lambda _, index, *args: int(all_sites[index]["adjacent_fort"]))
    p.hook(0x85aa8, lambda *args: tiles[63])
    p.hook(0x7725c, lambda _, origin, index, mode, *args: int(all_sites[index]["nonallied"]))
    p.hook(0x6ae84, lambda *args: card if fixture["card"] else 0)
    p.hook(0x7720c, lambda *args: int(fixture["affordable"]))
    p.hook(0x5aa98, lambda *args: int(fixture["permitted"]))

    def reach(_, index, mode, side, *args):
        site = all_sites[index]
        count = site["counts"].get(str(mode), 0)
        p.write(ai + 0x20, "2Q", buffer, buffer + count * 4)
        return count

    def neighbors(_, index, output, mode, *args):
        indices = list(range(20 + index * 10, 20 + index * 10 + len(fixture["sites"][index]["neighbors"])))
        if indices:
            p.write(buffer, f"{len(indices)}i", *indices)
        p.write(output, "3Q", buffer, buffer + len(indices) * 4, buffer + 0x200)
        return 0

    p.hook(0x78608, reach)
    p.hook(0x77df4, neighbors)
    p.hook_import("_ZdlPv", lambda *args: 0)

    def draw(*args):
        value = fixture["raw_random"][len(raw_draws)]
        raw_draws.append(value)
        return value

    p.hook_import("rand", draw)
    p.cpu.hook_add(UC_HOOK_CODE, lambda cpu, *args: bounds.append(cpu.reg_read(registers.UC_ARM64_REG_X0) & 0xffffffff), begin=0xef658, end=0xef658)
    generator = random.Random(1937)
    fixtures, errors = [], []
    query_modes = (0x7da, 0x848, 0x852, 0x85c, 0x870, 0x866, 0x7f3, 0x7d3)
    for group, entry in (("installation", 0x798e0), ("fortress", 0x7a148)):
        for case in range(700):
            def make_site():
                return dict(building=generator.choice([0, 255, 255]), level=generator.randrange(4), installation=generator.choice([0, 255, 255]),
                    sea=bool(generator.randrange(2)), coastal=bool(generator.randrange(2)), present=bool(generator.randrange(2)),
                    category=generator.randrange(5), construction=generator.randrange(2), acted=bool(generator.randrange(2)),
                    environment=generator.randrange(2), adjacent_fort=bool(generator.randrange(2)), nonallied=bool(generator.randrange(2)),
                    counts={str(mode): generator.randrange(7) for mode in query_modes})
            sites = [dict(**make_site(), neighbors=[make_site() for _ in range(generator.randrange(9))]) for _ in range(4)]
            fixture = dict(group=group, sites=sites, stock=[generator.randrange(13) for _ in range(7)], fort_disabled=bool(generator.randrange(2)),
                card=bool(generator.randrange(2)), affordable=bool(generator.randrange(2)), permitted=bool(generator.randrange(2)),
                raw_random=[generator.randrange(32768) for _ in range(200)])
            if case % 5 == 0:
                fixture.update(card=True, affordable=True, permitted=True, fort_disabled=False, stock=[12] * 7)
                for site in sites:
                    site.update(building=255, installation=255, sea=False, present=group == "installation", category=0, construction=0,
                        acted=False, environment=0, adjacent_fort=False)
            all_sites = [make_site() for _ in tiles]
            for index, site in enumerate(sites):
                all_sites[index] = site
                for offset, nearby in enumerate(site["neighbors"]):
                    all_sites[20 + index * 10 + offset] = nearby
            for address in (ai, action, country, catalog):
                p.cpu.mem_write(address, bytes(0x200))
            p.write(ai + 8, "Q", catalog)
            p.write(ai + 0x98, "Q", action)
            p.write(ai + 0x54, "5i", -1, -1, -1, -1, 0x10000)
            p.write(action + 0x40, "Q", country)
            p.write(country + 8, "2Q", nodes[0], nodes[-1])
            p.write(country + 0x78, "B", int(fixture["fort_disabled"]))
            p.write(catalog + 0x110, "Q", catalog)
            for index, site in enumerate(all_sites):
                p.cpu.mem_write(tiles[index], bytes(0x100))
                p.write(tiles[index] + 0x1c, "B", int(site["sea"]))
                p.write(tiles[index] + 0x50, "i", site["environment"])
            for index, site in enumerate(sites):
                p.cpu.mem_write(units[index], bytes(0x100))
                p.write(nodes[index], "2Qi", nodes[index + 1] if index + 1 < 4 else country + 8, nodes[index - 1] if index else country + 8, index)
                p.write(tiles[index] + 0x70, "Q", units[index] if site["present"] else 0)
                p.write(units[index] + 8, "Q", definitions[index])
                p.write(definitions[index] + 0xc, "i", site["category"])
                p.write(units[index] + 0x60, "i", site["construction"])
                p.write(units[index] + 0x5a, "B", int(site["acted"]))
            p.write(tiles[63] + 0x70, "Q", units[4])
            p.write(units[4] + 8, "Q", definitions[4])
            p.write(definitions[4] + 0xc, "i", 4)
            p.write(0x222e00, "7B", *fixture["stock"])
            raw_draws, bounds = [], []
            p.call(entry, ai)
            actual = list(p.read(ai + 0x54, "5i"))
            expected, expected_bounds, expected_draws = [-1, -1, -1, -1, 0x10000], [], []

            def sample(bound):
                expected_bounds.append(bound)
                value = fixture["raw_random"][len(expected_draws)]
                expected_draws.append(value)
                return value % bound

            best, selected, selected_card, availability = -1, None, None, 0
            if group == "installation":
                preference = (0, 2, 1)[sample(3)]
                for index, site in enumerate(sites):
                    if site["building"] != 255 or site["installation"] != 255 or site["sea"] or not site["present"] or site["category"] != 0 or site["construction"] > 0 or site["acted"] or site["environment"] > 0 or site["counts"][str(0x7da)] == 0:
                        continue
                    scores = [-1 if i == preference else -20 for i in range(3)]
                    for kind, mode, bound, stock_index in ((0, 0x848, 3, 4), (1, 0x852, 5, 6), (2, 0x85c, 6, 5), (2, 0x870, 6, 5), (2, 0x866, 3, 5)):
                        if fixture["stock"][stock_index] > 0:
                            scores[kind] += sum(sample(bound) for _ in range(site["counts"][str(mode)]))
                    for kind, score in enumerate(scores):
                        if score > best:
                            selected, selected_card, best = index, 41 + kind, score
                            availability = fixture["stock"][(4, 6, 5)[kind]]
                if selected is not None and sample(100) >= 100 - min(10, availability) * 10 and fixture["card"] and fixture["affordable"] and fixture["permitted"]:
                    expected = [selected_card, selected, selected, selected, 0x10010]
            elif not fixture["fort_disabled"]:
                value = sample(9)
                mode = 0 if value < 3 else 1 if value < 5 else 2 if value < 7 else 3
                for index, site in enumerate(sites):
                    if site["building"] != 255 or site["installation"] != 255 or site["sea"] or site["present"] or site["environment"] > 0 or site["adjacent_fort"]:
                        continue
                    scores = [0, 0, 0, 0]
                    if mode == 0:
                        if site["coastal"]:
                            scores[0] = 11 + (15 * site["counts"][str(0x870)] if fixture["stock"][3] > 0 else 0) + 3 * site["counts"][str(0x7d3)]
                    else:
                        scores[1:] = [-3 * site["counts"][str(0x7f3)]] * 3
                        scores[mode] += sum((3 if near["building"] == 255 else (near["level"] + 1) * 3) + (0 if near["sea"] else 6)
                            for near in site["neighbors"] if not near["nonallied"])
                    for kind, score in enumerate(scores):
                        if score > best:
                            selected, selected_card, best = index, (40, 37, 38, 39)[kind], score
                            availability = fixture["stock"][(3, 0, 1, 2)[kind]]
                if selected is not None and best >= 12 and sample(60) >= 60 - min(6, availability) * 10 and fixture["card"] and fixture["affordable"] and fixture["permitted"]:
                    expected = [selected_card, selected, selected, -1, 0x10010]
            if actual != expected or bounds != expected_bounds or raw_draws != expected_draws:
                errors.append(dict(group=group, case=case, actual=actual, expected=expected, bounds=bounds, expected_bounds=expected_bounds))
                if len(errors) >= 3:
                    raise AssertionError(errors)
            fixtures.append(dict(inputs=fixture, expected=actual, random_bounds=bounds))
    report = dict(count=len(fixtures), errors=errors,
        scope="Real 798e0 installation and 7a148 fortress selection/scoring/random order; neighbors/category lists/country/card/placement and affordability injected. Actual unit/building/environment eligibility branches execute; full world query/production scheduling not covered.")
    (ROOT / "reverse/reports/native_ai_construction_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_construction_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
