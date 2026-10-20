"""Probe original cell end-turn ordering and per-cell/faction income aggregation."""

from collections import Counter
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH, RNG_STATE


def f32(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


def main():
    p = NativeProbe()
    tile, unit, country, building, general, system, row = [SCRATCH + i * 0x1000 for i in range(1, 8)]
    generator = random.Random(1927)
    counts, errors = Counter(), []
    vectors = dict(end_cell=[], income=[])
    context, trace = {}, []

    def service(name, value):
        trace.append(name)
        return value

    p.hook(0x31844, lambda *args: service("system", system))
    p.hook(0x55b98, lambda *args: service("building_heal", context["building_heal"]))
    p.hook(0x6ce7c, lambda *args: service("veterancy_heal", row if context["veterancy_row"] else 0))
    p.hook(0x56abc, lambda *args: service("field_heal", context["field_heal"]))
    p.hook(0x4ff30, lambda *args: service("adjacent_heal", context["adjacent_heal"]))
    p.hook(0x56a94, lambda *args: service("title_heal", context["title_heal"]))
    p.hook(0x4fcf8, lambda *args: service("local_morale", 0))

    def set_building(place, level, *args):
        trace.append("building_level_" + str(level))
        p.write(place + 8, "i", level)
        return 0

    p.hook(0x56028, set_building)

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, inputs=inputs, actual=actual, expected=expected))
            if len(errors) >= 3:
                raise AssertionError(errors)

    for case in range(1000):
        context = dict(unit=bool(generator.randrange(2)), general=bool(generator.randrange(2)),
                       building=generator.choice([None, 0, 4]), level=generator.randrange(1, 5),
                       shortage=generator.randrange(5), construction=generator.randrange(3),
                       acted=bool(generator.randrange(2)), force=bool(generator.randrange(2)),
                       environment=generator.randrange(4), burning=generator.randrange(4),
                       building_heal=generator.randrange(16), veterancy_heal=generator.randrange(16),
                       veterancy_row=bool(generator.randrange(2)), field_heal=generator.randrange(16),
                       adjacent_heal=generator.randrange(16), title_heal=generator.randrange(16))
        maximum = generator.randrange(40, 301)
        health, seed = generator.randrange(1, maximum + 1), generator.getrandbits(64)
        context.update(health=health, maximum=maximum, seed=seed)
        p.write(tile + 0x40, "Q", building if context["building"] is not None else 0)
        p.write(tile + 0x70, "Q", unit if context["unit"] else 0)
        p.write(tile + 0x50, "i", context["environment"])
        p.write(building + 4, "2i", context["building"] or 0, context["level"])
        p.write(unit + 0x10, "2Q", country, general if context["general"] else 0)
        p.write(unit + 0x20, "3i", health, maximum, 6)
        p.write(unit + 0x4c, "i", context["burning"])
        p.write(unit + 0x5a, "2B", int(context["acted"]), int(context["force"]))
        p.write(unit + 0x60, "i", context["construction"])
        p.write(country + 0x2c, "i", context["shortage"])
        p.write(row + 4, "i", context["veterancy_heal"])
        p.write(RNG_STATE, "Q", seed)
        trace = []
        p.call(0x50b44, tile)

        wanted_trace = ["building_heal"] if context["building"] is not None else []
        state, burning, environment, level = seed, context["burning"], context["environment"], context["level"]
        acted, force, movement = context["acted"], context["force"], 6

        def roll():
            nonlocal state
            state = (state * 214013 + 2531011) & ((1 << 64) - 1)
            return ((state >> 16) & 32767) % 100

        def damage(intensity):
            nonlocal health
            health = max(1, health - maximum * (8 if intensity >= 3 else 6 if intensity == 2 else 4 if intensity == 1 else 0) // 100)

        def decay(intensity):
            value = roll()
            return (2 if value < 50 else 1 if value < 80 else 0) if intensity >= 3 else (1 if value < 55 else 0) if intensity == 2 else 0

        if context["unit"]:
            if context["shortage"] == 0:
                if (context["construction"] < 1 and not acted) or force:
                    if context["building"] is not None:
                        health = min(maximum, health + context["building_heal"])
                    force = False
                    wanted_trace += ["system", "veterancy_heal"]
                    if context["veterancy_row"]:
                        health = min(maximum, health + context["veterancy_heal"])
                    if context["general"] and context["building"] is None:
                        wanted_trace.append("field_heal")
                        health = min(maximum, health + context["field_heal"])
                wanted_trace.append("adjacent_heal")
                health = min(maximum, health + context["adjacent_heal"])
                if context["general"]:
                    wanted_trace.append("title_heal")
                    health = min(maximum, health + context["title_heal"])
            damage(burning)
            if burning > 0:
                burning = decay(burning)
            movement, acted = 0, True
            wanted_trace.append("local_morale")
        if context["building"] == 4 and level > 1 and (environment > 0 or (context["unit"] and burning > 0)):
            if roll() < 30:
                level = 1
                wanted_trace.append("building_level_1")
        if environment > 0:
            if context["unit"]:
                damage(environment)
            environment = decay(environment)
        actual = dict(health=p.read(unit + 0x20, "i")[0], burning=p.read(unit + 0x4c, "i")[0],
                      environment=p.read(tile + 0x50, "i")[0], level=p.read(building + 8, "i")[0],
                      movement=p.read(unit + 0x28, "i")[0], acted=bool(p.read(unit + 0x5a, "B")[0]),
                      force=bool(p.read(unit + 0x5b, "B")[0]), seed=p.read(RNG_STATE, "Q")[0], trace=list(trace))
        expected = dict(health=health, burning=burning, environment=environment, level=level,
                        movement=movement, acted=acted, force=force, seed=state, trace=wanted_trace)
        check("end_cell", actual, expected, case)
        vectors["end_cell"].append(dict(**context, expected=actual))

    tiles = [SCRATCH + 0x8000 + i * 0x200 for i in range(6)]
    buildings = [place + 0x100 for place in tiles]
    units = [SCRATCH + 0xa000 + i * 0x100 for i in range(6)]
    generals = [SCRATCH + 0xb000 + i * 0x100 for i in range(6)]
    nodes = [SCRATCH + 0xc000 + i * 0x20 for i in range(6)]
    rows = [SCRATCH + 0xd000 + i * 0x20 for i in range(6)]
    city_rows = [place + 0x100 for place in rows]
    facts = []
    p.hook(0x85824, lambda _, cell, *args: tiles[cell])
    p.hook(0x6a4a8, lambda _, kind, index, *args: rows[index] if facts[index]["row"] else 0)
    p.hook(0x69cb8, lambda _, index, *args: city_rows[index] if facts[index]["city_row"] else 0)
    p.hook(0x578e4, lambda place, *args: facts[generals.index(place)]["multiplier"], floating=True)
    for case in range(600):
        facts = [dict(building=generator.choice([None, 0, 1, 4]), general=bool(generator.randrange(2)),
                      row=bool(generator.randrange(2)), city_row=bool(generator.randrange(2)),
                      base=[generator.randrange(71) for _ in range(3)], city_money=generator.randrange(81),
                      multiplier=generator.choice([1., 1.4, 1.8, 2.05, 2.3])) for _ in range(generator.randrange(7))]
        multiplier = generator.choice([0., .75, 1., 1.25, 1.6, 2.])
        technology = [generator.randrange(5) for _ in range(3)]
        sentinel = country + 8
        p.write(sentinel, "Q", nodes[0] if facts else sentinel)
        p.write(country + 0x24, "f", multiplier)
        p.write(country + 0x228, "3i", technology[2], technology[0], technology[1])
        totals = [0, 0, 0]
        for i, fact in enumerate(facts):
            p.write(nodes[i], "2Qi", nodes[i + 1] if i + 1 < len(facts) else sentinel, 0, i)
            p.write(tiles[i] + 0x40, "Q", buildings[i] if fact["building"] is not None else 0)
            p.write(tiles[i] + 0x70, "Q", units[i] if fact["general"] else 0)
            p.write(units[i] + 0x18, "Q", generals[i])
            p.write(buildings[i] + 4, "2i", fact["building"] or 0, i)
            p.write(buildings[i] + 0x18, "i", i)
            p.write(rows[i], "3i", *fact["base"])
            p.write(city_rows[i] + 4, "i", fact["city_money"])
            for resource in range(3):
                amount = fact["base"][resource] if fact["row"] and fact["building"] is not None else 0
                if resource == 0 and fact["building"] == 0 and fact["city_row"]:
                    amount = fact["city_money"]
                if fact["general"]:
                    amount = int(f32(f32(amount) * f32(fact["multiplier"])))
                totals[resource] += amount
        actual = [p.call(address, country) for address in (0x591e8, 0x592a0, 0x59358)]
        expected = [(int(f32(f32(totals[i]) * f32(multiplier))) if i < 2 else totals[i])
                    + (technology[i] * (20 if i == 0 else 10) if technology[i] in (1, 2, 3) else 0) for i in range(3)]
        check("income", actual, expected, case)
        vectors["income"].append(dict(tiles=facts, multiplier=multiplier, technology=technology, expected=actual))
    driver = NativeProbe()
    driver.hook(0x31844, lambda *args: system)
    current_round, driver_trace = 1, []
    driver.hook(0x2f0f0, lambda *args: current_round)
    for address, label in ((0x59404, "money"), (0x5949c, "industry"), (0x59534, "food"),
                           (0x59f68, "consume"), (0x58fb4, "actionability")):
        driver.hook(address, lambda *args, label=label: driver_trace.append(label) or 0)
    driver.hook(0x85824, lambda _, cell, *args: tiles[cell])
    driver.hook(0x51918, lambda place, *args: driver_trace.append("refresh_" + str(tiles.index(place))) or 0)
    driver.hook(0x50b44, lambda place, *args: driver_trace.append("end_" + str(tiles.index(place))) or 0)
    for case in range(500):
        current_round, count = generator.randrange(1, 6), generator.randrange(7)
        cooldowns = [generator.randrange(5) for _ in range((0x138 - 0x7c) // 4)]
        sentinel = country + 8
        driver.write(sentinel, "Q", nodes[0] if count else sentinel)
        for i in range(count):
            driver.write(nodes[i], "2Qi", nodes[i + 1] if i + 1 < count else sentinel, 0, i)
        driver.write(country + 0x77, "2B", 0, 1)
        driver.write(country + 0x7c, str(len(cooldowns)) + "i", *cooldowns)
        driver_trace = []
        driver.call(0x5b14c, country)
        expected_trace = (["money", "industry", "food", "consume"] if current_round > 1 else [])
        expected_trace += ["refresh_" + str(i) for i in range(count)] + ["actionability"]
        start_trace = list(driver_trace)
        check("faction_start_driver", [start_trace, list(driver.read(country + 0x77, "2B"))], [expected_trace, [1, 0]], case)
        driver_trace = []
        driver.call(0x5b1e4, country)
        check("faction_end_driver", [driver_trace, list(driver.read(country + 0x7c, str(len(cooldowns)) + "i")),
                                    driver.read(country + 0x77, "B")[0]],
              [["end_" + str(i) for i in range(count)], [max(0, value - 1) for value in cooldowns], 0], case)
        vectors.setdefault("driver", []).append(dict(round=current_round, count=count, cooldowns=cooldowns, start_trace=start_trace, end_trace=list(driver_trace)))

    adjacency = NativeProbe()
    neighbors = []
    adjacency.hook(0x859d8, lambda *args: len(neighbors))
    adjacency.hook(0x85aa8, lambda _, center, direction, *args: tiles[direction] if neighbors[direction]["present"] else 0)
    adjacency.hook(0x5786c, lambda place, *args: neighbors[generals.index(place)]["healing"])
    adjacency.hook(0x5777c, lambda place, *args: neighbors[generals.index(place)]["attack"])
    adjacency.hook(0x577f4, lambda place, *args: neighbors[generals.index(place)]["defense"])
    for case in range(600):
        camp = generator.randrange(1, 5)
        neighbors = [dict(present=bool(generator.randrange(2)), active=bool(generator.randrange(2)),
                          owner=generator.choice([None, 1, 2, 3, 4]), unit=bool(generator.randrange(2)),
                          commander=bool(generator.randrange(2)), healing=generator.randrange(21),
                          attack=generator.randrange(21), defense=generator.randrange(21)) for _ in range(6)]
        adjacency.write(country, "i", camp)
        adjacency.write(tile + 0x68, "Q", country)
        adjacency.write(tile + 0x70, "Q", unit)
        for i, neighbor in enumerate(neighbors):
            adjacency.write(tiles[i] + 0x14, "B", int(neighbor["active"]))
            adjacency.write(tiles[i] + 0x68, "Q", buildings[i] if neighbor["owner"] is not None else 0)
            adjacency.write(buildings[i], "i", neighbor["owner"] or 0)
            adjacency.write(tiles[i] + 0x70, "Q", units[i] if neighbor["unit"] else 0)
            adjacency.write(units[i] + 0x18, "Q", generals[i] if neighbor["commander"] else 0)
        active = [n for n in neighbors if n["present"] and n["active"]]
        surrounded = all(n["owner"] is not None and n["owner"] != camp and n["owner"] != 4 and n["unit"] for n in active)
        flanked = any(neighbors[i]["present"] and neighbors[i + 3]["present"] and neighbors[i]["unit"]
                      and neighbors[i + 3]["unit"] and neighbors[i]["owner"] is not None and neighbors[i + 3]["owner"] is not None
                      and neighbors[i]["owner"] != camp and neighbors[i + 3]["owner"] != camp for i in range(3))
        # The original flanking function assumes any occupied neighbor has an owner.
        for neighbor in neighbors:
            if neighbor["unit"] and neighbor["owner"] is None:
                neighbor["unit"] = False
        for i, neighbor in enumerate(neighbors):
            adjacency.write(tiles[i] + 0x70, "Q", units[i] if neighbor["unit"] else 0)
        actual = 2 if adjacency.call(0x4fc40, tile) else 1 if adjacency.call(0x4fb88, tile) else 0
        check("local_penalty", actual, 2 if surrounded else 1 if flanked else 0, case)
        healing = sum(n["healing"] for n in neighbors if n["present"] and n["active"] and n["owner"] == camp and n["unit"] and n["commander"])
        attack = sum(n["attack"] for n in neighbors if n["present"] and n["active"] and n["owner"] == camp and n["unit"] and n["commander"])
        defense = sum(n["defense"] for n in neighbors if n["present"] and n["active"] and n["owner"] == camp and n["unit"] and n["commander"])
        check("adjacent_healing", adjacency.call(0x4ff30, tile), healing, case)
        check("adjacent_attack", adjacency.call(0x4fd98, tile), attack, case)
        check("adjacent_defense", adjacency.call(0x4fe64, tile), defense, case)
        vectors.setdefault("adjacency", []).append(dict(camp=camp, neighbors=neighbors, penalty=actual, healing=healing, attack=attack, defense=defense))
    city = NativeProbe()
    neighbors, claimed, refreshed = [], [], []
    city.hook(0x85aa8, lambda _, center, direction, *args: tiles[direction] if neighbors[direction]["present"] else 0)

    def claim(place, owner, *args):
        claimed.append(tiles.index(place))
        city.write(place + 0x68, "Q", owner)
        return 0

    city.hook(0x4f260, claim)
    city.hook(0x53ef4, lambda *args: refreshed.append(True) or 0)
    for case in range(400):
        kind, level, has_unit = generator.choice([None, 0, 1, 2, 4]), generator.randrange(1, 6), bool(generator.randrange(2))
        neighbors = [dict(present=bool(generator.randrange(2)), active=bool(generator.randrange(2)),
                          sea=bool(generator.randrange(2)), objective=bool(generator.randrange(2)),
                          unit=bool(generator.randrange(2)), building=generator.choice([None, 0, 4])) for _ in range(6)]
        city.write(tile + 0x40, "Q", building if kind is not None else 0)
        city.write(building + 4, "2i", kind or 0, level)
        city.write(building + 0x1c, "B", 1)
        city.write(tile + 0x68, "Q", country)
        city.write(tile + 0x70, "Q", unit if has_unit else 0)
        for i, neighbor in enumerate(neighbors):
            city.write(tiles[i] + 0x14, "B", int(neighbor["active"]))
            city.write(tiles[i] + 0x18, "i", int(neighbor["objective"]))
            city.write(tiles[i] + 0x1c, "B", int(neighbor["sea"]))
            city.write(tiles[i] + 0x70, "Q", units[i] if neighbor["unit"] else 0)
            city.write(tiles[i] + 0x40, "Q", buildings[i] if neighbor["building"] is not None else 0)
            city.write(buildings[i] + 4, "i", neighbor["building"] or 0)
        claimed, refreshed = [], []
        city.call(0x51918, tile)
        expands = kind is not None and kind in (0, 1, 2) and level > (3 if kind == 0 else 2 if kind == 1 else 1)
        expected = [i for i, n in enumerate(neighbors) if expands and n["present"] and n["active"] and not n["sea"]
                    and not n["unit"] and not n["objective"] and n["building"] in (None, 4)]
        check("start_city_expansion", [claimed, len(refreshed), city.read(building + 0x1c, "B")[0]],
              [expected, int(has_unit), 0 if kind is not None else 1], case)
        vectors.setdefault("expansion", []).append(dict(kind=kind, level=level, unit=has_unit, neighbors=neighbors, claimed=claimed))
    report = dict(counts=dict(counts), errors=errors, scope="Original 50b44 with real healing/burning/environment primitives and injected healing lookup/adjacency/local-morale/building setter; income chain with injected building/commander rows; faction start/end drivers with injected services; actual adjacency/flanking and 51918 city-expansion functions with injected map/equipment/ownership/refresh services. Not Android or full battle validation.")
    (ROOT / "reverse/reports/native_turn_settlement_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/turn_settlement_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
