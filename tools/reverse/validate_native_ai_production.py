"""Probe recruitment, building-upgrade and transport AI with injected world queries."""

import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, registers
from unicorn import UC_HOOK_CODE


def main():
    p = NativeProbe()
    p.cpu.mem_map(SCRATCH + 0x10000, 0x20000)
    ai, action, country, catalog, card = [SCRATCH + i * 0x1000 for i in range(1, 6)]
    tiles = [SCRATCH + 0x6000 + i * 0x100 for i in range(20)]
    units = [SCRATCH + 0x8000 + i * 0x100 for i in range(20)]
    definitions = [SCRATCH + 0xa000 + i * 0x100 for i in range(20)]
    buildings = [SCRATCH + 0xc000 + i * 0x80 for i in range(20)]
    nodes = [SCRATCH + 0xd000 + i * 0x30 for i in range(20)]
    fixture, raw_draws, bounds = {}, [], []
    heap = SCRATCH + 0x10000
    heap_cursor = heap

    def allocate(size, *args):
        nonlocal heap_cursor
        result = heap_cursor
        heap_cursor += (size + 15) & ~15
        if heap_cursor > heap + 0x20000:
            raise RuntimeError("Probe allocation arena exhausted")
        return result

    p.hook_import("_Znwm", allocate)
    p.hook_import("_ZdlPv", lambda *args: 0)
    p.hook_import("memmove", lambda destination, source, length, *args: (p.cpu.mem_write(destination, bytes(p.cpu.mem_read(source, length))) or destination))
    # The ELF loader normally binds this library's exported vector<int> growth template.
    p.write(0x220100, "Q", 0x5f980)
    p.hook(0x85824, lambda _, index, *args: tiles[index])
    p.hook(0x4ea88, lambda tile, *args: fixture["sites"][tiles.index(tile)]["building"])
    p.hook(0x4ebf8, lambda tile, *args: int(fixture["sites"][tiles.index(tile)]["coastal"]))
    p.hook(0x559dc, lambda building, *args: int(fixture["sites"][buildings.index(building)]["upgrade"]))
    p.hook(0x77490, lambda _, tile, *args: fixture["sites"][tiles.index(tile)]["value"])
    p.hook(0x773a0, lambda _, tile, *args: fixture["sites"][tiles.index(tile)]["strength"])
    p.hook(0x59e5c, lambda *args: fixture["food"])
    p.hook(0x59358, lambda *args: fixture["income"])
    p.hook(0x59efc, lambda *args: fixture["consumption"])
    p.hook(0x77a28, lambda *args: fixture["offer"])
    p.hook(0x6ae84, lambda *args: card if fixture["card"] else 0)
    p.hook(0x7720c, lambda *args: int(fixture["affordable"]))
    p.hook(0x5aa98, lambda *args: int(fixture["permitted"]))

    def reach(_, index, mode, side, *args):
        site = fixture["sites"][index]
        if mode == 0x7bc:
            return site["relocation"]
        if mode == 2000:
            return site["sea_neighbors"]
        if mode == 0x7da:
            return site["threat"] if side == 1 else site["friendly"]
        return 0

    p.hook(0x78608, reach)

    def draw(*args):
        value = fixture["raw_random"][len(raw_draws)]
        raw_draws.append(value)
        return value

    p.hook_import("rand", draw)
    p.cpu.hook_add(UC_HOOK_CODE, lambda cpu, *args: bounds.append(cpu.reg_read(registers.UC_ARM64_REG_X0) & 0xffffffff), begin=0xef658, end=0xef658)
    generator = random.Random(1915)
    fixtures, errors = [], []
    for group, entry in (("recruit", 0x79200), ("upgrade", 0x79db8), ("transport", 0x7975c)):
        for case in range(600):
            count = 5 if group == "recruit" else 20
            sites = [dict(building=generator.choice([0, 1, 2, 3, 4, 255]), present=bool(generator.randrange(2)),
                coastal=bool(generator.randrange(2)), sea=bool(generator.randrange(2)), transport=bool(generator.randrange(2)),
                category=generator.randrange(5), environment=generator.randrange(3), upgrade=bool(generator.randrange(2)),
                value=generator.randrange(1, 400), strength=generator.randrange(300), threat=generator.randrange(300), friendly=generator.randrange(1000),
                relocation=generator.choice([-1, 20]), movement=generator.randrange(7), morale=generator.choice([0, -3]),
                behavior=generator.randrange(4), sea_neighbors=generator.randrange(7)) for _ in range(count)]
            fixture = dict(group=group, sites=sites, food=generator.randrange(1000), consumption=generator.randrange(300), income=generator.randrange(300),
                offer=generator.choice([0, 8, 33, 47]), card=bool(generator.randrange(2)), affordable=bool(generator.randrange(2)), permitted=bool(generator.randrange(2)),
                auto_transport=bool(generator.randrange(2)), request=generator.choice([0, 33]), raw_random=[generator.randrange(32768) for _ in range(100)])
            if case % 19 == 0:
                fixture.update(food=1000, income=300, consumption=0, card=True, affordable=True, permitted=True, offer=0)
                for site in sites:
                    site.update(building=0, environment=0, present=False, upgrade=True, threat=0, friendly=10, value=200, strength=0)
            for address in (ai, action, country, catalog):
                p.cpu.mem_write(address, bytes(0x200))
            p.write(ai + 8, "Q", catalog)
            p.write(ai + 0x98, "Q", action)
            p.write(ai + 0x54, "5i", -1, -1, -1, -1, 0x10000)
            p.write(catalog + 0x110, "Q", catalog)
            p.write(action + 0x40, "Q", country)
            p.write(country + 8, "2Q", nodes[0], nodes[count - 1])
            for index, site in enumerate(sites):
                p.cpu.mem_write(tiles[index], bytes(0x100))
                p.cpu.mem_write(units[index], bytes(0x100))
                p.cpu.mem_write(definitions[index], bytes(0x100))
                p.write(nodes[index], "2Qi", nodes[index + 1] if index + 1 < count else country + 8, nodes[index - 1] if index else country + 8, index)
                p.write(tiles[index] + 0x40, "Q", buildings[index])
                p.write(tiles[index] + 0x70, "Q", units[index] if site["present"] else 0)
                p.write(tiles[index] + 0x1c, "B", int(site["sea"]))
                p.write(tiles[index] + 0x50, "i", site["environment"])
                p.write(units[index] + 8, "Q", definitions[index])
                p.write(definitions[index] + 0xc, "i", site["category"])
                p.write(units[index] + 0x28, "i", site["movement"])
                p.write(units[index] + 0x44, "i", site["morale"])
                p.write(units[index] + 0x59, "B", int(site["transport"]))
                p.write(units[index] + 0x64, "i", site["behavior"])
            p.write(0x222e07, "B", int(fixture["auto_transport"]))
            raw_draws, bounds, heap_cursor = [], [], heap
            try:
                p.call(entry, ai, fixture["request"])
            except Exception as error:
                pc = p.cpu.reg_read(registers.UC_ARM64_REG_PC)
                lr = p.cpu.reg_read(registers.UC_ARM64_REG_X30)
                raise RuntimeError(f"{group} case {case}, PC {pc:x}, LR {lr:x}: {error}") from None
            actual = list(p.read(ai + 0x54, "5i"))
            expected, expected_bounds, expected_draws = [-1, -1, -1, -1, 0x10000], [], []

            def sample(bound):
                expected_bounds.append(bound)
                if not bound:
                    return 0
                value = fixture["raw_random"][len(expected_draws)]
                expected_draws.append(value)
                return value % bound

            if group == "transport":
                choices = [i for i, site in enumerate(sites) if site["present"] and not site["transport"] and not site["sea"]
                    and site["coastal"] and site["category"] != 4 and site["environment"] == 0 and site["sea_neighbors"] != 0]
                if fixture["auto_transport"] and choices and fixture["card"]:
                    selected = max(choices, key=lambda i: sites[i]["sea_neighbors"])
                    expected = [46, selected, selected, selected, 0x10010]
            elif group == "upgrade":
                choices, weight = [], 0
                for index, site in enumerate(sites):
                    if site["building"] == 255 or site["environment"] > 0:
                        continue
                    enemy, friendly = site["threat"], site["friendly"]
                    if friendly > enemy * 4:
                        accept = True
                    elif friendly > enemy * 3:
                        accept = sample(10) > 1
                    elif friendly > enemy * 2:
                        accept = sample(10) > 2
                    elif friendly > enemy:
                        accept = sample(10) > 3
                    elif friendly == enemy:
                        accept = sample(10) > 4
                    else:
                        accept = False
                    if accept and site["upgrade"]:
                        choices.append(index)
                        weight += 3 if site["building"] in (0, 1, 2) else 2 if site["building"] == 3 else 1
                if choices:
                    chance = 20 if weight < 10 else 30 if weight < 20 else 40 if weight < 30 else 50 if weight < 40 else 60
                    if sample(100) <= chance:
                        selected = choices[sample(len(choices))]
                        if fixture["card"] and fixture["affordable"] and fixture["permitted"]:
                            expected = [44, selected, selected, -1, 0x10010]
            else:
                surplus = fixture["income"] - fixture["consumption"]
                bias = 2 if surplus >= 40 else 1 if surplus >= 0 else -1 if surplus >= -40 else -2
                ratio = fixture["food"] // fixture["consumption"] if fixture["consumption"] else fixture["food"]
                ratio //= 1 if surplus > 40 else 2 if surplus > 0 else 3 if surplus >= -39 else 4
                tier = ratio + bias
                draw = sample(100)
                accepted = tier >= 4 or tier >= 0 and draw <= (30, 50, 70, 90)[tier]
                selected, best = None, -1
                relocation = -1
                if accepted:
                    for index, site in enumerate(sites):
                        if site["building"] not in ((3,) if fixture["request"] == 33 else (0, 1, 2)):
                            continue
                        if site["present"]:
                            if site["movement"] <= 0 or site["morale"] == -3 or site["behavior"] == 3:
                                continue
                            relocation = site["relocation"]
                            if relocation == -1:
                                continue
                        strength = site["strength"] // 2 if site["threat"] == 0 and site["environment"] != 0 else site["strength"]
                        need = site["value"] + (0 if site["threat"] == 0 and site["environment"] != 0 else site["threat"])
                        band = 5 if strength >= need else 4 if strength >= need * .75 else 3 if strength >= need * .5 else 2 if strength >= need * .25 else 1
                        draw = sample(100)
                        skip = band != 1 and draw < {2: 21, 3: 41, 4: 56, 5: 71}[band]
                        low = site["value"] < 50 and site["threat"] == 0 and strength != 0
                        extra = sample(100) if low else 99
                        if not skip and (not low or extra >= 50) and need - strength > best:
                            selected, best, chosen_relocation = index, need - strength, relocation
                    if selected is not None and fixture["offer"] != 47 and fixture["card"] and fixture["permitted"]:
                        if sites[selected]["present"]:
                            expected = [fixture["offer"], selected, chosen_relocation, selected, 0x10050]
                        else:
                            expected = [fixture["offer"], selected, selected, selected, 0x10010]
            if actual != expected or bounds != expected_bounds or raw_draws != expected_draws:
                errors.append(dict(group=group, case=case, actual=actual, expected=expected, bounds=bounds, expected_bounds=expected_bounds,
                    draws=raw_draws, expected_draws=expected_draws))
                if len(errors) >= 3:
                    raise AssertionError(errors)
            fixtures.append(dict(inputs=fixture, expected=actual, random_bounds=bounds))
    report = dict(count=len(fixtures), errors=errors,
        scope="Real 79200/79db8/7975c recruitment, upgrade and transport selection; site/economy/pressure/placement/card services injected. Real native dynamic vector allocation/memmove, decision branches and random helper; no action execution or whole production scheduler.")
    (ROOT / "reverse/reports/native_ai_production_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_production_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
