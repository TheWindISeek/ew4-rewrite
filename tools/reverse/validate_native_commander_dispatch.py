"""Probe original commander availability, deployment, attachment and AI site choice."""

import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    generator = random.Random(1927)
    errors, vectors = [], dict(availability=[], deployment=[], attachment=[], ai=[], inheritance=[])

    def check(group, inputs, actual, expected):
        if actual != expected:
            errors.append(dict(group=group, inputs=inputs, actual=actual, expected=expected))
            raise AssertionError(errors[-1])
        vectors[group].append(dict(inputs=inputs, expected=actual))

    p = NativeProbe()
    tavern, root, tile, country, bank, unit = [SCRATCH + i * 0x1000 for i in range(1, 7)]
    fixture, events = {}, []
    p.hook(0x31844, lambda *args: root)
    p.hook(0x31c7c, lambda *args: root)
    p.hook(0x2f0f0, lambda *args: fixture["round"])
    p.hook(0x85824, lambda *args: tile)
    p.hook(0x4ef64, lambda *args: unit if fixture.get("unit", False) else 0)
    p.hook(0x59638, lambda *args: fixture["money"])
    p.hook(0x59744, lambda *args: fixture["industry"])
    p.hook(0x2c2e4, lambda *args: fixture["medals"])
    p.hook(0x5964c, lambda _, value, *args: (events.append(["money", value & 0xffffffff]) or 0))
    p.hook(0x59758, lambda _, value, *args: (events.append(["industry", value & 0xffffffff]) or 0))
    p.hook(0x2c320, lambda _, value, *args: (events.append(["medals", value & 0xffffffff]) or 0))
    p.hook(0x52bcc, lambda _, value, *args: (events.append(["attach", value]) or 0))
    for case in range(600):
        fixture = dict(round=generator.randrange(1, 30), offers=[generator.randrange(1, 35) for _ in range(generator.randrange(6))])
        p.cpu.mem_write(tavern, bytes(0x100))
        p.write(tavern + 100, "i", len(fixture["offers"]))
        for i, available_round in enumerate(fixture["offers"]):
            p.write(tavern + i * 20, "5i", i + 100, 0, 0, 0, available_round)
        actual = p.call(0x5dd28, tavern)
        check("availability", fixture, actual, sum(r <= fixture["round"] for r in fixture["offers"]))

    for case in range(800):
        rows = [[100 + i, generator.randrange(500), generator.randrange(200), generator.randrange(50), generator.randrange(1, 20)]
                for i in range(generator.randrange(1, 6))]
        fixture = dict(round=generator.randrange(1, 20), rows=rows, index=generator.randrange(len(rows) + 1),
                       money=generator.randrange(1000), industry=generator.randrange(500), medals=generator.randrange(100),
                       country=bool(generator.randrange(2)), human=bool(generator.randrange(2)), unit=bool(generator.randrange(2)),
                       assigned=bool(generator.randrange(2)))
        if case % 7 == 0:
            fixture.update(country=True, human=False, unit=True, assigned=False, money=1000, industry=500, medals=100, round=20, index=0)
        for pointer in (tavern, tile, country, unit):
            p.cpu.mem_write(pointer, bytes(0x100))
        p.write(tile + 0x68, "Q", country if fixture["country"] else 0)
        p.write(country + 0x54, "B", int(fixture["human"]))
        p.write(unit + 0x18, "Q", root if fixture["assigned"] else 0)
        p.write(tavern + 100, "i", len(rows))
        p.write(tavern + 0x68, "i", 6)
        p.write(tavern + 0x70, "Q", bank)
        for i, row in enumerate(rows):
            p.write(tavern + i * 20, "5i", *row)
        events = []
        actual = bool(p.call(0x5df98, tavern, fixture["index"]))
        selected = rows[fixture["index"]] if fixture["index"] < len(rows) else None
        expected = bool(selected and selected[4] <= fixture["round"] and fixture["country"]
                        and (fixture["human"] or selected[1] <= fixture["money"] and selected[2] <= fixture["industry"] and selected[3] <= fixture["medals"])
                        and fixture["unit"] and not fixture["assigned"])
        expected_rows = [row[0] for i, row in enumerate(rows) if not (expected and i == fixture["index"])]
        remaining = p.read(tavern + 100, "i")[0]
        actual_rows = [p.read(tavern + i * 20, "i")[0] for i in range(remaining)]
        expected_events = []
        if expected:
            if not fixture["human"]:
                expected_events = [[kind, (-value) & 0xffffffff] for kind, value in zip(("money", "industry", "medals"), selected[1:4])]
            expected_events.append(["attach", selected[0]])
        check("deployment", fixture, dict(success=actual, rows=actual_rows, events=events),
              dict(success=expected, rows=expected_rows, events=expected_events))

    p = NativeProbe()
    unit, definition, old, new, owner, collection = [SCRATCH + i * 0x1000 for i in range(1, 7)]
    fixture = {}
    p.hook(0x56200, lambda *args: 0)
    p.hook_import("_ZdlPv", lambda *args: 0)
    p.hook_import("_Znwm", lambda *args: new)
    p.hook(0x561fc, lambda *args: 0)
    p.hook(0x56204, lambda *args: 0)
    p.hook(0x5c658, lambda *args: collection if fixture["collection"] else 0)
    p.hook(0x54c88, lambda *args: new if fixture["found"] else 0)
    p.hook(0x56a6c, lambda *args: fixture["bonus"])
    p.hook(0x57230, lambda *args: fixture["movement_bonus"])
    for case in range(800):
        fixture = dict(base_health=generator.randrange(50, 500), old_maximum=generator.randrange(50, 800),
                       old_health=0, base_movement=generator.randrange(12), remaining=generator.randrange(12),
                       bonus=generator.randrange(300), movement_bonus=generator.randrange(8), old=bool(generator.randrange(2)),
                       deploy=bool(generator.randrange(2)), human=bool(generator.randrange(2)), collection=bool(generator.randrange(2)), found=bool(generator.randrange(2)))
        fixture["old_health"] = generator.randrange(1, fixture["old_maximum"] + 1)
        for pointer in (unit, definition, old, new):
            p.cpu.mem_write(pointer, bytes(0x100))
        p.write(unit + 8, "Q", definition)
        p.write(unit + 0x10, "Q", owner)
        p.write(unit + 0x18, "Q", old if fixture["old"] else 0)
        p.write(unit + 0x20, "3i", fixture["old_health"], fixture["old_maximum"], fixture["remaining"])
        p.write(unit + 0x58, "B", int(fixture["human"]))
        p.write(definition + 0x14, "2i", fixture["base_health"], fixture["base_movement"])
        p.write(old + 0x4c, "B", 1)
        p.write(old + 0x50, "Q", unit)
        p.call(0x52bcc, unit, 100 if fixture["deploy"] else -1)
        attached = fixture["deploy"] and (not fixture["human"] or fixture["collection"] and fixture["found"])
        health = fixture["old_health"]
        if fixture["old_maximum"] != fixture["base_health"]:
            health = health * fixture["base_health"] // fixture["old_maximum"]
        health = min(health, fixture["base_health"])
        maximum = fixture["base_health"] + (fixture["bonus"] if attached else 0)
        if fixture["deploy"] and maximum != fixture["base_health"]:
            health = health * maximum // fixture["base_health"]
        health = min(health, maximum)
        movement = fixture["remaining"]
        if movement > 0:
            movement = fixture["base_movement"]
            if movement > 0 and attached:
                movement += fixture["movement_bonus"]
        actual = dict(health=p.read(unit + 0x20, "i")[0], maximum=p.read(unit + 0x24, "i")[0], movement=p.read(unit + 0x28, "i")[0],
                      attached=p.read(unit + 0x18, "Q")[0] == new, old_detached=p.read(old + 0x50, "Q")[0] == 0 if fixture["old"] else True)
        check("attachment", fixture, actual, dict(health=health, maximum=maximum, movement=movement, attached=bool(attached), old_detached=True))

    p = NativeProbe()
    p.cpu.mem_map(SCRATCH + 0x10000, 0x10000)
    controller, country, ai, tavern = [SCRATCH + i * 0x1000 for i in range(1, 5)]
    nodes = [SCRATCH + 0x6000 + i * 0x40 for i in range(8)]
    tiles = [SCRATCH + 0x7000 + i * 0x100 for i in range(8)]
    units = [SCRATCH + 0x8000 + i * 0x100 for i in range(8)]
    definitions = [SCRATCH + 0x9000 + i * 0x100 for i in range(8)]
    records = [SCRATCH + 0xa000 + i * 0x100 for i in range(8)]
    fixture = {}
    p.hook(0x31c7c, lambda *args: tavern)
    p.hook(0x85824, lambda _, index, *args: tiles[index])
    p.hook(0x4ea88, lambda tile, *args: fixture["sites"][tiles.index(tile)]["building"])
    p.hook(0x8d0d8, lambda _, index, *args: records[index] if fixture["sites"][index]["record"] else 0)
    p.hook(0x5dd28, lambda record, *args: fixture["sites"][records.index(record)]["available"])
    p.hook(0x4ef64, lambda tile, *args: units[tiles.index(tile)] if fixture["sites"][tiles.index(tile)]["unit"] else 0)
    for case in range(800):
        fixture = dict(sites=[dict(building=generator.choice([0, 1, 2, 3, 4, 255]), record=bool(generator.randrange(2)), available=generator.randrange(6),
                                  unit=bool(generator.randrange(2)), assigned=bool(generator.randrange(2)), sea=bool(generator.randrange(2)),
                                  category=generator.randrange(5), health=generator.choice([59, 60, 61, 89, 90, 91, 99, 100, 101, 125, 126, 200]))
                              for _ in range(generator.randrange(1, 9))])
        p.cpu.mem_write(ai, bytes(0x100))
        p.write(controller + 0x40, "Q", country)
        p.write(controller + 0x70, "Q", ai)
        p.write(country + 8, "2Q", nodes[0], nodes[len(fixture["sites"]) - 1])
        for i, site in enumerate(fixture["sites"]):
            p.write(nodes[i], "2Qi", nodes[i + 1] if i + 1 < len(fixture["sites"]) else country + 8, nodes[i - 1] if i else country + 8, i)
            p.write(tiles[i] + 0x1c, "B", int(site["sea"]))
            p.write(units[i] + 8, "Q", definitions[i])
            p.write(units[i] + 0x18, "Q", tavern if site["assigned"] else 0)
            p.write(units[i] + 0x20, "i", site["health"])
            p.write(definitions[i] + 0xc, "i", site["category"])
        p.call(0xa7a0c, controller)
        selected = None
        for i, site in enumerate(fixture["sites"]):
            threshold = (126 if site["category"] == 3 else None) if site["sea"] else {0: 90, 1: 100, 2: 60}.get(site["category"])
            # Infantry and artillery use strict >89/>59; cavalry and naval >99/>125.
            if site["building"] != 255 and site["record"] and site["available"] and site["unit"] and not site["assigned"]:
                if threshold is not None and site["health"] >= threshold:
                    selected = i
                    break
        actual = p.read(ai + 0x54, "5i") if selected is not None else None
        expected = (-1, selected, selected, selected, 0x10060) if selected is not None else None
        check("ai", fixture, dict(command=list(actual) if actual else None, success=bool(p.read(controller + 0x58, "B")[0])),
              dict(command=list(expected) if expected else None, success=selected is not None))
        p.write(controller + 0x58, "B", 0)

    p = NativeProbe()
    receiver, donor, definition = [SCRATCH + i * 0x1000 for i in range(1, 4)]
    tables = dict(rank_costs=list(p.read(0x194da0, "14i")), title_costs=list(p.read(0x194e20, "9i")),
                  rank_percent=list(p.read(0x194de0, "15i")), title_percent=list(p.read(0x194e50, "10i")))

    def advance(level, experience, gain, costs):
        if level >= len(costs):
            return [level, experience]
        experience += gain
        while level < len(costs) and experience >= costs[level]:
            experience -= costs[level]
            level += 1
        return [level, 0 if level == len(costs) else experience]

    for case in range(1000):
        fixture = dict(receiver_rank=generator.randrange(15), receiver_title=generator.randrange(10),
                       receiver_rank_xp=generator.randrange(300), receiver_title_xp=generator.randrange(100),
                       donor_rank=generator.randrange(15), donor_title=generator.randrange(10),
                       donor_rank_xp=generator.randrange(300), donor_title_xp=generator.randrange(100),
                       abilities=[generator.randrange(6) for _ in range(7)], skills=generator.sample(list(range(33, 40)), generator.randrange(5)))
        p.cpu.mem_write(receiver, bytes(0x100))
        p.cpu.mem_write(donor, bytes(0x100))
        p.write(donor + 8, "Q", definition)
        p.write(receiver + 0x10, "4i", fixture["receiver_rank"], fixture["receiver_title"], fixture["receiver_rank_xp"], fixture["receiver_title_xp"])
        p.write(receiver + 0x20, "7i", *fixture["abilities"])
        p.write(donor + 0x10, "4i", fixture["donor_rank"], fixture["donor_title"], fixture["donor_rank_xp"], fixture["donor_title_xp"])
        p.write(donor + 0x40, "i", len(fixture["skills"]))
        p.write(definition + 0x34, "4i", *(fixture["skills"] + [-1] * (4 - len(fixture["skills"]))))
        p.call(0x58238, receiver, donor)
        rank_gain = (300 + sum(tables["rank_costs"][:fixture["donor_rank"]]) + fixture["donor_rank_xp"]) * tables["rank_percent"][fixture["donor_rank"]] // 100
        title_gain = (60 + sum(tables["title_costs"][:fixture["donor_title"]]) + fixture["donor_title_xp"]) * tables["title_percent"][fixture["donor_title"]] // 100
        rank = advance(fixture["receiver_rank"], fixture["receiver_rank_xp"], rank_gain, tables["rank_costs"])
        title = advance(fixture["receiver_title"], fixture["receiver_title_xp"], title_gain, tables["title_costs"])
        abilities = [min(5, value + 1) if 33 + i in fixture["skills"] else value for i, value in enumerate(fixture["abilities"])]
        actual = list(p.read(receiver + 0x10, "11i"))
        check("inheritance", fixture, actual, [rank[0], title[0], rank[1], title[1]] + abilities)

    report = dict(counts={key: len(value) for key, value in vectors.items()}, errors=errors,
                  scope="Original 5dd28/5df98/52bcc/a7a0c/58238; environment, funds and lookup/allocation/commander-effect services injected. Inheritance uses real ELF tables. No GUI, phone or complete deployment state chain.")
    (ROOT / "reverse/reports/native_commander_dispatch_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/commander_dispatch_vectors.json").write_text(json.dumps(dict(**vectors, inheritance_rules=tables), separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
