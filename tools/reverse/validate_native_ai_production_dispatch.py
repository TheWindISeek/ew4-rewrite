"""Probe native affordability counts, preparation fields and production fallback ordering."""

import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, registers
from validate_native_ai_strategy import f32
from unicorn import UC_HOOK_CODE


def main():
    generator = random.Random(1946)
    p = NativeProbe()
    p.cpu.mem_map(SCRATCH + 0x10000, 0x20000)
    controller, ai, country, card = [SCRATCH + i * 0x1000 for i in range(1, 5)]
    fixture, calls, draws = {}, [], []
    p.hook(0x6ae84, lambda *args: card if fixture["exists"] else 0)
    p.hook(0x6aeac, lambda *args: card if fixture["exists"] else 0)
    p.hook(0x5a9f4, lambda *args: int(fixture["affordable"]))
    p.hook(0x59638, lambda *args: fixture["money"])
    p.hook(0x59744, lambda *args: fixture["industry"])
    p.write(controller + 0x40, "Q", country)
    p.write(controller + 8, "Q", controller)
    fixtures = dict(stock=[], preparation=[], dispatch=[])
    errors = []
    for case in range(800):
        fixture = dict(exists=bool(generator.randrange(2)), affordable=bool(generator.randrange(2)), money=generator.randrange(100_000),
            industry=generator.randrange(100_000), price=generator.randrange(1000), industry_price=generator.randrange(1000),
            kind=generator.randrange(47), grade=generator.randrange(3))
        if case % 11 == 0:
            fixture.update(exists=True, affordable=True, price=0)
        if case % 13 == 0:
            fixture.update(exists=True, affordable=True, price=1, industry_price=0)
        p.write(card + 0x18, "i", 5)
        p.write(card + 0x24, "2i", fixture["price"], fixture["industry_price"])
        actual = p.call(0xa7ea4, controller, fixture["grade"], fixture["kind"])
        expected = 0
        if fixture["exists"] and fixture["affordable"]:
            expected = fixture["money"] // fixture["price"] if fixture["price"] else 0
            if fixture["industry_price"] > 0:
                expected = min(expected, fixture["industry"] // fixture["industry_price"])
            expected = min(127, expected)
        if actual != expected:
            errors.append(dict(group="stock", case=case, actual=actual, expected=expected))
        fixtures["stock"].append(dict(inputs=fixture, expected=actual))

    tiles = [SCRATCH + 0x10000 + i * 0x100 for i in range(5)]
    units = [SCRATCH + 0x11000 + i * 0x100 for i in range(4)]
    definitions = [SCRATCH + 0x12000 + i * 0x100 for i in range(4)]
    nodes = [SCRATCH + 0x13000 + i * 0x40 for i in range(4)]
    buildings = [SCRATCH + 0x14000 + i * 0x100 for i in range(4)]
    vectors = [SCRATCH + 0x15000 + i * 0x100 for i in range(4)]
    records = [SCRATCH + 0x16000 + i * 0x100 for i in range(4)]
    p.hook(0x85824, lambda _, index, *args: tiles[index])
    p.hook(0x4ea88, lambda tile, *args: fixture["sites"][tiles.index(tile)]["building"])
    p.hook(0x4eb60, lambda tile, *args: fixture["sites"][tiles.index(tile)]["installation"])
    p.hook(0x5587c, lambda building, *args: vectors[buildings.index(building)])
    p.hook(0x859d8, lambda _, index, *args: int(fixture["sites"][index]["adjacent_sea"]))
    p.hook(0x85aa8, lambda *args: tiles[4])

    def count(_, grade, kind, *args):
        calls.append([grade, kind])
        return min(127, grade * 10 + kind)

    p.hook(0xa7ea4, count)
    global_fields = 0x222de8
    for case in range(600):
        fixture = dict(sites=[dict(building=generator.choice([0, 1, 2, 3, 4, 255]), installation=generator.choice([0, 255]), sea=bool(generator.randrange(2)),
            present=bool(generator.randrange(2)), category=generator.randrange(5), construction=generator.randrange(2), acted=bool(generator.randrange(2)),
            movement=generator.randrange(7), morale=generator.choice([0, -3]), transport=bool(generator.randrange(2)), adjacent_sea=bool(generator.randrange(2)),
            offers=[[unit, generator.randrange(3)] for unit in generator.sample(list(range(18)), generator.randrange(8))]) for _ in range(4)])
        if case % 13 == 0:
            fixture["sites"][0].update(building=3, present=False, offers=[[17, 0]])
        p.cpu.mem_write(controller, bytes(0x100))
        p.cpu.mem_write(ai, bytes(0x100))
        p.cpu.mem_write(country, bytes(0x100))
        p.write(controller + 0x40, "Q", country)
        p.write(controller + 0x70, "Q", ai)
        p.write(country + 8, "2Q", nodes[0], nodes[-1])
        p.write(tiles[4] + 0x1c, "B", 1)
        for index, site in enumerate(fixture["sites"]):
            p.cpu.mem_write(tiles[index], bytes(0x100))
            p.cpu.mem_write(units[index], bytes(0x100))
            p.write(nodes[index], "2Qi", nodes[index + 1] if index + 1 < 4 else country + 8, nodes[index - 1] if index else country + 8, index)
            p.write(tiles[index] + 8, "i", index)
            p.write(tiles[index] + 0x1c, "B", int(site["sea"]))
            p.write(tiles[index] + 0x40, "Q", buildings[index])
            p.write(tiles[index] + 0x70, "Q", units[index] if site["present"] else 0)
            p.write(units[index] + 8, "Q", definitions[index])
            p.write(definitions[index] + 0xc, "i", site["category"])
            p.write(units[index] + 0x28, "i", site["movement"])
            p.write(units[index] + 0x44, "i", site["morale"])
            p.write(units[index] + 0x59, "2B", int(site["transport"]), int(site["acted"]))
            p.write(units[index] + 0x60, "i", site["construction"])
            p.write(vectors[index], "3Q", records[index], records[index] + len(site["offers"]) * 8, records[index] + 0x80)
            for offset, pair in enumerate(site["offers"]):
                p.write(records[index] + offset * 8, "2i", *pair)
        calls = []
        p.call(0xa7fa4, controller)
        actual = list(p.read(global_fields, "32b"))
        expected = [1] * 6 + [-1] * 26
        for site in fixture["sites"]:
            skip_neighbor = site["building"] == 255 and site["sea"]
            if site["building"] == 255 and not site["sea"] and site["installation"] == 255:
                expected[24:28] = [0] * 4
                if site["present"]:
                    if site["construction"] > 0 or site["acted"]:
                        skip_neighbor = True
                    elif site["category"] == 0:
                        expected[28:31] = [0] * 3
            elif site["building"] in (0, 1, 2, 3) and (not site["present"] or site["movement"] > 0 and site["morale"] != -3):
                allowed = (range(6), range(10, 14), range(6, 10), range(14, 18))[site["building"]]
                for unit, grade in site["offers"]:
                    if unit in allowed:
                        expected[15 if unit == 17 else 6 + unit] = grade
            if not skip_neighbor and site["present"] and not site["transport"] and site["adjacent_sea"]:
                expected[31] = 0
        expected_calls = []
        for index in range(6, 32):
            if expected[index] >= 0:
                kind = index - 6 if index < 28 else (41, 43, 42, 46)[index - 28]
                expected_calls.append([expected[index], kind])
                expected[index] = min(127, expected[index] * 10 + kind)
        if actual != expected or calls != expected_calls:
            errors.append(dict(group="preparation", case=case, actual=actual, expected=expected, calls=calls, expected_calls=expected_calls))
            if len(errors) >= 3:
                raise AssertionError(errors)
        fixtures["preparation"].append(dict(inputs=fixture, expected=actual, stock_calls=calls))

    events = []
    p.hook(0x80904, lambda *args: country if fixture["country"] else 0)
    p.hook(0x5906c, lambda *args: int(fixture["defeated"]))
    p.hook(0xa7fa4, lambda *args: (p.write(global_fields, "32b", *fixture["flags"]) or 0))

    def attempted(_, kind, *args):
        events.append(kind)
        p.write(controller + 0x58, "B", int(fixture["production_success"]))
        return 0

    def fallback(name, success):
        def run(*args):
            events.append(name)
            p.write(controller + 0x58, "B", int(fixture[success]))
            return 0
        return run

    p.hook(0xa8868, attempted)
    p.hook(0xa7a0c, fallback("commander", "commander_success"))
    p.hook(0xa8b60, fallback("unit", "unit_success"))
    p.hook_import("rand", lambda *args: (draws.append(fixture["random"]) or fixture["random"]))
    constants = p.read(0xa90f0, "3f")
    for case in range(1000):
        fixture = dict(country=bool(generator.randrange(2)), defeated=bool(generator.randrange(2)), production_success=bool(generator.randrange(2)),
            commander_success=bool(generator.randrange(2)), unit_success=bool(generator.randrange(2)), flags=[generator.randrange(2) for _ in range(6)] + [-1] * 25 + [generator.choice([-1, 0, 2])],
            random=generator.randrange(32768))
        p.cpu.mem_write(controller, bytes(0x100))
        p.write(controller + 0x70, "Q", ai)
        p.write(controller + 0x5c, "i", -1)
        events, draws = [], []
        actual_pointer = p.call(0xa8e50, controller, 17)
        expected_events, expected_draws = [], []
        success = False
        if fixture["country"] and not fixture["defeated"]:
            land = 64 if any(fixture["flags"][:3]) else 32
            navy = 8 if fixture["flags"][3] else 4
            fortress = 6 if fixture["flags"][4] else 3
            total = land + navy + fortress + 15 + (7 if fixture["flags"][31] >= 0 else 3)
            scale = f32(100. / total)
            thresholds, threshold = [], 0
            for kind, weight in zip((0, 33, 38, 41, 44), (land, navy, fortress, 9, 6)):
                threshold += int(f32(scale * f32(weight)))
                thresholds.append((threshold, kind))
            draw = fixture["random"] % total
            request = next((kind for threshold, kind in thresholds if draw < threshold), 46)
            expected_events.append(request)
            expected_draws.append(fixture["random"])
            success = fixture["production_success"]
            if not success:
                expected_events.append("commander")
                success = fixture["commander_success"]
            if not success:
                expected_events.append("unit")
                success = fixture["unit_success"]
        expected_pointer = ai + 0x54 if success else 0
        if actual_pointer != expected_pointer or events != expected_events or draws != expected_draws:
            errors.append(dict(group="dispatch", case=case, actual=actual_pointer, expected=expected_pointer, events=events, expected_events=expected_events))
            if len(errors) >= 3:
                raise AssertionError(errors)
        fixtures["dispatch"].append(dict(inputs=fixture, expected_events=events, success=success))
    report = dict(counts={key: len(value) for key, value in fixtures.items()}, errors=errors, constants=constants,
        scope="Real a7ea4 affordability, a7fa4 preparation and a8e50 request/fallback dispatch; world/card/funds and lower production/commander/unit actions injected. Preparation retains original slot-15/17 alias and always-on flags as evidence, not desired modern behavior.")
    (ROOT / "reverse/reports/native_ai_production_dispatch_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_production_dispatch_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
