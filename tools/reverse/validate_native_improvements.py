"""Execute native training and building upgrade checks, costs and commits."""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    unit, definition, commander, country, site, building, system, building_row = [SCRATCH + i * 0x1000 for i in range(1, 9)]
    level_rows = [SCRATCH + 0x9000 + i * 0x20 for i in range(6)]
    p.hook(0x31844, lambda *args: system)
    p.hook(0x31c7c, lambda *args: system)
    p.hook(0x85824, lambda *args: site)
    p.hook(0x6a468, lambda *args: building_row)
    p.hook(0x6ce7c, lambda _, level, *args: level_rows[level])
    p.hook(0x570f8, lambda _, skill, *args: int(discount and skill == 30))
    for address in (0xbcf08, 0xbcfd8, 0xbcb4c, 0x55c80):
        p.hook(address, lambda *args: 0)
    generator = random.Random(1930)
    vectors, counts, errors = dict(training=[], building=[]), Counter(), []
    training_costs = list(p.read(0x194b70, "6i"))
    money_costs = list(p.read(0x194c40, "5i"))
    industry_costs = list(p.read(0x194c60, "5i"))

    def check(group, actual, expected):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, case=counts[group] - 1, actual=actual, expected=expected))
            if len(errors) > 3:
                raise AssertionError(errors)

    def reset():
        for address in (unit, definition, commander, country, site, building, system, building_row):
            p.cpu.mem_write(address, bytes(0x200))
        p.write(unit + 8, "3Q", definition, country, commander)
        p.write(site + 0x70, "Q", unit)
        p.write(country + 0x54, "B", 1)

    for case in range(1000):
        reset()
        present, acted = bool(generator.randrange(2)), bool(generator.randrange(2))
        movement, morale, construction = generator.randrange(7), generator.randrange(-3, 2), generator.randrange(4)
        level, cap = generator.randrange(6), generator.randrange(6)
        consumption, money, food = generator.randrange(30), generator.randrange(800), generator.randrange(100)
        health, maximum, healing, xp = generator.randrange(1, 101), 100, generator.randrange(40), generator.randrange(500)
        if case % 3 == 0:
            present, acted, movement, construction, level, cap = True, False, 0, 0, case % 5, 5
            money, food = training_costs[level], consumption * 3
        p.write(unit + 0x18, "Q", commander if present else 0)
        p.write(unit + 0x20, "3i", health, maximum, movement)
        p.write(unit + 0x34, "2i", level, xp)
        p.write(unit + 0x44, "i", morale)
        p.write(unit + 0x5a, "B", int(acted))
        p.write(unit + 0x60, "i", construction)
        p.write(commander + 0x3c, "i", cap)
        p.write(definition + 0x34, "i", consumption)
        p.write(country + 0x18, "3I", money ^ 0x7ead3, 0x7ead3, food ^ 0x7ead3)
        for row in level_rows:
            p.write(row + 8, "i", healing)
        eligible = present and ((movement > 0 and morale != -3) or (construction < 1 and not acted)) and level < cap
        success = eligible and money >= training_costs[level] and food >= consumption * 3
        check("training_eligibility", bool(p.call(0x52de4, unit, 0)), eligible)
        check("training_food", p.call(0x52eac, unit), consumption * 3)
        actual = dict(success=bool(p.call(0x5378c, unit)), level=p.read(unit + 0x34, "i")[0],
                      experience=p.read(unit + 0x38, "i")[0], health=p.read(unit + 0x20, "i")[0],
                      movement=p.read(unit + 0x28, "i")[0], acted=bool(p.read(unit + 0x5a, "B")[0]),
                      money=p.read(country + 0x18, "I")[0] ^ 0x7ead3, food=p.read(country + 0x20, "I")[0] ^ 0x7ead3)
        check("training_commit", actual, dict(success=success, level=level + int(success), experience=xp,
              health=min(maximum, health + healing) if success else health, movement=0 if success else movement,
              acted=acted or success, money=money-training_costs[level] if success else money, food=food-consumption*3 if success else food))
        vectors["training"].append(dict(commander=present, acted=acted, movement=movement, morale=morale, construction=construction,
            level=level, cap=cap, consumption=consumption, money=money, food=food, health=health, maximum=maximum,
            healing=healing, experience=xp, eligible=eligible, expected=actual))

    for case in range(800):
        reset()
        kind, level, cap = generator.randrange(5), generator.randrange(1, 6), generator.randrange(1, 7)
        used, owner, present, discount = [bool(generator.randrange(2)) for _ in range(4)]
        money, industry = generator.randrange(800), generator.randrange(300)
        if case % 3 == 0:
            used, owner, level, cap = False, True, 1, 6
            money, industry = 1000, 500
        p.write(building, "3i", 0, kind, level)
        p.write(building + 0x1c, "B", int(used))
        p.write(building_row + 4, "i", cap)
        p.write(site + 0x68, "Q", country if owner else 0)
        p.write(unit + 0x18, "Q", commander if present else 0)
        p.write(country + 0x18, "3I", money ^ 0x7ead3, industry ^ 0x7ead3, 0x7ead3)
        cost_money = money_costs[kind] * 6 // 10 if present and discount else money_costs[kind]
        cost_industry = industry_costs[kind] * 6 // 10 if present and discount else industry_costs[kind]
        check("building_cost", [p.call(0x558b8, building), p.call(0x55948, building)], [cost_money, cost_industry])
        eligible = not used and level < cap
        check("building_eligibility", bool(p.call(0x559dc, building, 0)), eligible)
        success = eligible and owner and money >= cost_money and industry >= cost_industry
        actual = dict(success=bool(p.call(0x55f54, building)), level=p.read(building + 8, "i")[0],
                      used=bool(p.read(building + 0x1c, "B")[0]), money=p.read(country + 0x18, "I")[0] ^ 0x7ead3,
                      industry=p.read(country + 0x1c, "I")[0] ^ 0x7ead3)
        check("building_commit", actual, dict(success=success, level=level+int(success), used=used or success,
              money=money-cost_money if success else money, industry=industry-cost_industry if success else industry))
        vectors["building"].append(dict(kind=kind, level=level, cap=cap, used=used, owner=owner, commander=present,
            discount=discount, money=money, industry=industry, eligible=eligible, cost_money=cost_money, cost_industry=cost_industry, expected=actual))

    report = dict(counts=dict(counts), errors=errors, training_costs=training_costs, building_money=money_costs, building_industry=industry_costs,
        scope="Real training eligibility/cost/commit/healing and building eligibility/cost/commit; config lookups, skill membership and visual effects injected. No Android or full world test.")
    (ROOT / "reverse/reports/native_improvement_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        vectors.update(training_costs=training_costs, building_money=money_costs, building_industry=industry_costs)
        (ROOT / "reverse/reports/improvement_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
