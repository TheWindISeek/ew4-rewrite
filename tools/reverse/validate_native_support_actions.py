"""Execute installation/transport card pricing, eligibility and action commits."""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    country, other, card, tile, unit, definition, general, general_def, installation = [SCRATCH + i * 0x1000 for i in range(1, 10)]
    p.hook(0x85824, lambda *args: tile)

    def construct(site, kind, *args):
        p.write(site + 0x48, "Q", installation)
        p.write(installation, "i", kind)
        return 0

    p.hook(0x4eab8, construct)
    generator = random.Random(1915)
    fixtures, counts, errors = [], Counter(), []

    def check(group, actual, expected):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, case=counts[group]-1, actual=actual, expected=expected))
            if len(errors) > 3: raise AssertionError(errors)

    for case in range(1600):
        for address in (country, other, card, tile, unit, definition, general, general_def, installation):
            p.cpu.mem_write(address, bytes(0x400))
        identity = generator.choice([41, 42, 43, 46])
        active, own, occupied, built, installed, transport, acted, commander, skilled = [bool(generator.randrange(2)) for _ in range(9)]
        category, movement, morale, construction = generator.randrange(5), generator.randrange(7), generator.randrange(-3, 2), generator.randrange(4)
        money, industry, money_cost, industry_cost = [generator.randrange(101) for _ in range(4)]
        if case % 3 == 0:
            active, own, occupied, built, installed, transport, category, movement, morale, construction = True, True, True, False, False, False, 0, 0, 0, 0
            money, industry = 200, 200
        skill = {41: 27, 42: 28, 43: 25, 46: 26}[identity]
        p.write(card + 0x18, "2i", identity, 3 if identity == 46 else 2)
        p.write(card + 0x24, "2i", money_cost, industry_cost)
        p.write(tile + 0x14, "B", int(active))
        p.write(tile + 0x40, "2Q", 1 if built else 0, installation if installed else 0)
        p.write(tile + 0x68, "2Q", country if own else other, unit if occupied else 0)
        p.write(installation, "i", 9)
        p.write(unit + 8, "Q", definition)
        p.write(definition + 0xc, "i", category)
        p.write(unit + 0x18, "Q", general if commander else 0)
        p.write(general + 8, "Q", general_def)
        p.write(general + 0x40, "i", 1 if skilled else 0)
        p.write(general_def + 0x34, "4i", skill, -1, -1, -1)
        p.write(unit + 0x28, "i", movement)
        p.write(unit + 0x44, "i", morale)
        p.write(unit + 0x59, "2B", int(transport), int(acted))
        p.write(unit + 0x60, "i", construction)
        p.write(country + 0x18, "3I", money ^ 0x7ead3, industry ^ 0x7ead3, 0x7ead3)
        p.write(country + 0x54, "B", 1)
        p.write(country + 0x57, "B", 1)
        price = money_cost // 2 if occupied and commander and skilled else money_cost
        check("price", p.call(0x5a728, country, card, 0), price)
        eligible = active and own and occupied
        if identity == 46:
            eligible = eligible and category != 3 and not transport
        else:
            eligible = eligible and not built and not installed and category == 0 and ((movement > 0 and morale != -3) or construction < 1)
        check("eligibility", bool(p.call(0x5aa98, country, card, 0)), eligible)
        success = eligible and money >= price and industry >= industry_cost
        actual = dict(success=bool(p.call(0x5ac5c, country, card, 0, -1)), money=p.read(country + 0x18, "I")[0] ^ 0x7ead3,
            industry=p.read(country + 0x1c, "I")[0] ^ 0x7ead3, movement=p.read(unit + 0x28, "i")[0],
            acted=bool(p.read(unit + 0x5a, "B")[0]), transport=bool(p.read(unit + 0x59, "B")[0]),
            installation=p.read(installation, "i")[0] if p.read(tile + 0x48, "Q")[0] else None)
        expected = dict(success=success, money=money-price if success else money, industry=industry-industry_cost if success else industry,
            movement=0 if success and identity != 46 else movement, acted=acted or (success and identity != 46),
            transport=transport or (success and identity == 46), installation=identity-41 if success and identity != 46 else 9 if installed else None)
        check("commit", actual, expected)
        fixtures.append(dict(identity=identity, active=active, own=own, occupied=occupied, built=built, installed=installed,
            transport=transport, acted=acted, commander=commander, skilled=skilled, category=category, movement=movement,
            morale=morale, construction=construction, money=money, industry=industry, money_cost=money_cost, industry_cost=industry_cost,
            eligible=eligible, price=price, expected=actual))
    report = dict(counts=dict(counts), errors=errors,
        scope="Real 5a728/5aa98/5ac5c, skill membership, category and resource functions. Map lookup and installation allocation/visual service injected. Not whole UI/Android.")
    (ROOT / "reverse/reports/native_support_action_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/support_action_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors: raise SystemExit(1)


if __name__ == "__main__":
    main()
