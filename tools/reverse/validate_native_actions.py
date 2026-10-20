"""Check attack range/action flags and recruit eligibility/resource commit."""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, expected_distance


def main():
    p = NativeProbe()
    world, country, other, card, new_unit, definition, building = [SCRATCH + i * 0x1000 for i in range(1, 8)]
    places = [SCRATCH + 0x8000 + i * 0x200 for i in range(3)]
    units = [SCRATCH + 0x9000 + i * 0x200 for i in range(2)]
    p.hook(0xa9744, lambda _, cell, *args: places[cell] if cell < 3 else 0)
    p.hook(0x4ef64, lambda tile, *args: p.read(tile + 0x70, "Q")[0])
    counts, errors, vectors = Counter(), [], dict(attack=[], recruit=[])
    generator = random.Random(1924)

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected, inputs=inputs))
            if len(errors) > 3:
                raise AssertionError(errors)

    p.write(units[0] + 8, "Q", definition)
    for case in range(500):
        points = [generator.randrange(8) for _ in range(4)]
        minimum, maximum = generator.randrange(4), generator.randrange(1, 6)
        construction, acted, stunned, attacker, defender = [bool(generator.randrange(2)) for _ in range(5)]
        if case % 3 != 2:
            construction, acted, stunned, attacker, defender = False, False, False, True, True
            distance = expected_distance(*points)
            minimum, maximum = max(0, distance - 1), distance + 1
            if case % 3 == 1:
                failure = generator.randrange(7)
                if failure == 0: attacker = False
                elif failure == 1: defender = False
                elif failure == 2: construction = True
                elif failure == 3: acted = True
                elif failure == 4: stunned = True
                elif failure == 5: minimum = distance + 1
                else: maximum = distance - 1
        p.write(places[0] + 0x70, "Q", units[0] if attacker else 0)
        p.write(places[1] + 0x70, "Q", units[1] if defender else 0)
        p.write(places[1], "2i", points[2], points[3])
        p.write(places[2], "2i", points[0], points[1])
        p.write(units[0] + 0x60, "i", int(construction))
        p.write(units[0] + 0x5a, "B", int(acted))
        p.write(units[0] + 0x44, "i", -3 if stunned else 0)
        p.write(definition + 0x2c, "2i", minimum, maximum)
        actual = p.call(0x86010, world, 2, 1, 0)
        distance = expected_distance(*points)
        expected = int(attacker and defender and not construction and not acted and not stunned and minimum <= distance <= maximum)
        inputs = dict(points=points, minimum=minimum, maximum=maximum, construction=construction,
                      acted=acted, stunned=stunned, attacker=attacker, defender=defender)
        check("attack_range_and_flags", actual, expected, inputs)
        counts["attack_allowed"] += int(bool(actual))
        vectors["attack"].append(dict(**inputs, expected=bool(actual)))

    target = places[0]
    available, recruitable, created, money_cost = True, True, True, 0
    p.hook(0x85824, lambda *args: target)
    p.hook(0x5575c, lambda *args: int(recruitable))
    p.hook(0x5a728, lambda *args: money_cost)
    p.hook(0x4f01c, lambda *args: new_unit if created else 0)
    for address in (0x51f84, 0x4eab8):
        p.hook(address, lambda *args: 0)
    def spend(offset, delta):
        delta = delta if delta < 0x80000000 else delta - 0x100000000
        value = (p.read(country + offset, "I")[0] ^ 0x7ead3) + delta
        p.write(country + offset, "I", value ^ 0x7ead3)
        return 0
    p.hook(0x5964c, lambda _, delta, *args: spend(0x18, delta))
    p.hook(0x59758, lambda _, delta, *args: spend(0x1c, delta))
    for case in range(600):
        fort, active, own, occupied, built, sea, available, recruitable, created, fort_used = [bool(generator.randrange(2)) for _ in range(10)]
        money, industry, money_cost, industry_cost = [generator.randrange(100) for _ in range(4)]
        category, transport, veteran = generator.randrange(5), generator.randrange(4), generator.randrange(4)
        if case % 3 != 2:
            active, own, occupied, built, sea, available, recruitable, created, fort_used = True, True, False, not fort, False, True, True, True, False
            money, industry = money_cost + 20, industry_cost + 20
            if case % 3 == 1:
                failure = generator.randrange(10)
                if failure == 0: active = False
                elif failure == 1: own = False
                elif failure == 2: occupied = True
                elif failure == 3: built = not built
                elif failure == 4: available = False
                elif failure == 5: created = False
                elif failure == 6: money_cost = money + 1
                elif failure == 7: industry_cost = industry + 1
                elif failure == 8:
                    if fort: fort_used = True
                    else: recruitable = False
                elif fort: sea = True
                else: recruitable = False
        for address in (country, card, target, new_unit, definition):
            p.cpu.mem_write(address, bytes(0x400))
        p.write(target + 0x14, "B", int(active))
        p.write(target + 0x1c, "B", int(sea))
        p.write(target + 0x40, "Q", building if built else 0)
        p.write(target + 0x68, "Q", country if own else other)
        p.write(target + 0x70, "Q", units[0] if occupied else 0)
        p.write(country + 0x18, "3I", money ^ 0x7ead3, industry ^ 0x7ead3, 0x7ead3)
        p.write(country + 0x57, "B", 1)
        p.write(country + 0x78, "B", int(fort_used))
        p.write(country + 0x1d0, "i", veteran if available else -1)
        p.write(country + 0x234, "i", transport)
        p.write(card + 0x1c, "2i", int(fort), 0)
        p.write(card + 0x28, "i", industry_cost)
        p.write(card + 0x30, "2i", 2, 0)
        p.write(new_unit + 8, "Q", definition)
        p.write(new_unit + 0x28, "i", 6)
        p.write(definition + 8, "2i", 0, category)
        eligibility = bool(p.call(0x5aa98, country, card, 0))
        expected_eligible = active and own and not occupied and available and ((not built and not sea and not fort_used) if fort else (built and recruitable))
        actual = bool(p.call(0x5ac5c, country, card, 0, -1))
        success = expected_eligible and money >= money_cost and industry >= industry_cost and created
        counts["fort_success" if fort else "ordinary_success"] += int(success)
        result = dict(eligible=eligibility, success=actual, money=p.read(country + 0x18, "I")[0] ^ 0x7ead3,
                      industry=p.read(country + 0x1c, "I")[0] ^ 0x7ead3, movement=p.read(new_unit + 0x28, "i")[0],
                      acted=bool(p.read(new_unit + 0x5a, "B")[0]), veteran=p.read(new_unit + 0x34, "i")[0],
                      transport=bool(p.read(new_unit + 0x59, "B")[0]), fort_used=bool(p.read(country + 0x78, "B")[0]))
        expected = dict(eligible=expected_eligible, success=success, money=money-money_cost if success else money,
                        industry=industry-industry_cost if success else industry, movement=0 if success else 6, acted=success,
                        veteran=veteran if success else 0,
                        transport=success and not fort and category in (0, 1, 2) and transport > category,
                        fort_used=fort_used or (fort and success))
        inputs = dict(fort=fort, active=active, own=own, occupied=occupied, building=built, sea=sea, available=available,
                      recruitable=recruitable, created=created, fort_used=fort_used, money=money, industry=industry,
                      money_cost=money_cost, industry_cost=industry_cost, category=category, transport_level=transport, veteran=veteran)
        check("recruit_eligibility_and_commit", result, expected, inputs)
        vectors["recruit"].append(dict(**inputs, expected=result))
    if counts["attack_allowed"] < 100 or counts["fort_success"] < 40 or counts["ordinary_success"] < 40:
        raise AssertionError("Insufficient successful recruitment branch coverage")
    report = dict(counts=dict(counts), errors=errors,
                  scope="Attack range/flags predicate; ordinary/fort recruitment eligibility and spending with injected building capability, costs and unit creation.")
    (ROOT / "reverse/reports/native_action_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/action_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
