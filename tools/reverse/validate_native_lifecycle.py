"""Probe faction survival, turn refresh, unit XP and nonlethal fire damage."""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, RNG_STATE


def main():
    p = NativeProbe()
    country, tile, unit, definition, general, row, system = [SCRATCH + i * 0x1000 for i in range(1, 8)]
    generator = random.Random(1926)
    counts, errors, vectors = Counter(), [], dict(survival=[], refresh=[], unit_xp=[], burn=[])

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected, inputs=inputs))
            if len(errors) > 3:
                raise AssertionError(errors)

    tiles = [SCRATCH + 0x8000 + i * 0x100 for i in range(6)]
    nodes = [SCRATCH + 0x9000 + i * 0x20 for i in range(6)]
    buildings = [SCRATCH + 0xa000 + i * 0x20 for i in range(6)]
    p.hook(0x85824, lambda _, cell, *args: tiles[cell])
    p.hook(0x4ef64, lambda place, *args: p.read(place + 0x70, "Q")[0])
    for case in range(300):
        mode, count = generator.randrange(5), generator.randrange(7)
        facts = [dict(unit=bool(generator.randrange(2)), building=generator.choice([None, 0, 4])) for _ in range(count)]
        sentinel = country + 8
        p.write(country + 4, "i", mode)
        p.write(sentinel, "Q", nodes[0] if count else sentinel)
        for i, fact in enumerate(facts):
            p.write(nodes[i], "2Qi", nodes[i + 1] if i + 1 < count else sentinel, 0, i)
            p.write(tiles[i] + 0x70, "Q", unit if fact["unit"] else 0)
            p.write(tiles[i] + 0x40, "Q", 0 if fact["building"] is None else buildings[i])
            p.write(buildings[i] + 4, "i", fact["building"] or 0)
        actual = p.call(0x5906c, country)
        survives = (mode in (1, 2) and any(f["unit"] for f in facts)) or (mode in (0, 2) and any(f["building"] not in (None, 4) for f in facts))
        check("faction_survival", actual, int(not survives), case)
        vectors["survival"].append(dict(mode=mode, tiles=facts, defeated=bool(actual)))

    p.write(unit + 8, "Q", definition)
    p.write(unit + 0x10, "Q", country)
    movement_bonus, health_bonus, current_round, leadership, logistics = 0, 0, 1, False, False
    p.hook(0x57230, lambda *args: movement_bonus)
    p.hook(0x56a6c, lambda *args: health_bonus)
    p.hook(0x31844, lambda *args: system)
    p.hook(0x2f0f0, lambda *args: current_round)
    p.hook(0x570f8, lambda _, skill, *args: int(leadership if skill == 3 else logistics if skill == 22 else False))
    p.hook(0x51f84, lambda *args: 0)
    for case in range(500):
        old_max, new_base = generator.randrange(50, 250), generator.randrange(50, 250)
        health = generator.randrange(1, old_max + 1)
        has_general, leadership, logistics = [bool(generator.randrange(2)) for _ in range(3)]
        movement, movement_bonus, health_bonus = generator.randrange(8), generator.randrange(5), generator.randrange(60)
        baseline, duration, local, shortage = generator.randrange(-3, 2), generator.randrange(4), generator.randrange(3), generator.randrange(5)
        construction, current_round = generator.randrange(4), generator.randrange(1, 8)
        behavior, previous_behavior, expires = generator.randrange(4), generator.randrange(4), generator.randrange(8)
        p.write(unit + 0x18, "Q", general if has_general else 0)
        p.write(unit + 0x20, "3i", health, old_max, 0)
        p.write(definition + 0x14, "2i", new_base, movement)
        p.write(unit + 0x3c, "4i", baseline, duration, baseline, local)
        p.write(country + 0x2c, "i", shortage)
        p.write(unit + 0x5a, "B", 1)
        p.write(unit + 0x60, "4i", construction, behavior, previous_behavior, expires)
        p.call(0x53ef4, unit)
        new_max = new_base + (health_bonus if has_general else 0)
        new_health = min(new_max, health * new_max // old_max)
        new_duration, new_baseline = max(0, duration - 1), 0 if duration == 1 else baseline
        effective = new_baseline
        if new_duration < 3:
            penalty = -local
            if shortage >= 2 and not (has_general and logistics):
                penalty = max(-3, penalty - shortage + 1)
            effective = max(-3, effective + penalty)
        if has_general and leadership and effective < 0:
            effective = 0
        actual = dict(health=p.read(unit + 0x20, "i")[0], maximum=p.read(unit + 0x24, "i")[0],
                      movement=p.read(unit + 0x28, "i")[0], morale=list(p.read(unit + 0x3c, "3i")),
                      acted=bool(p.read(unit + 0x5a, "B")[0]), construction=p.read(unit + 0x60, "i")[0],
                      behavior=p.read(unit + 0x64, "i")[0], expires=p.read(unit + 0x6c, "i")[0])
        expected = dict(health=new_health, maximum=new_max, movement=movement + (movement_bonus if movement > 0 and has_general else 0),
                        morale=[new_baseline, new_duration, effective], acted=False, construction=max(0, construction-1),
                        behavior=previous_behavior if expires > 0 and expires == current_round else behavior,
                        expires=0 if expires > 0 and expires == current_round else expires)
        inputs = dict(health=health, maximum=old_max, base_health=new_base, general=has_general, leadership=leadership, logistics=logistics,
                      base_movement=movement, movement_bonus=movement_bonus, health_bonus=health_bonus, baseline=baseline,
                      duration=duration, local=local, shortage=shortage, construction=construction, round=current_round,
                      behavior=behavior, previous_behavior=previous_behavior, expires=expires)
        check("unit_refresh", actual, expected, case)
        vectors["refresh"].append(dict(**inputs, expected=actual))

    p.hook(0x6ce7c, lambda *args: row)
    for address in (0xbcf08, 0xbcfd8, 0xbcb4c, 0xc60d0, 0xc6328):
        p.hook(address, lambda *args: 0)
    for case in range(400):
        level, xp, gain, cost, heal = generator.randrange(6), generator.randrange(300), generator.randrange(1500), generator.randrange(1, 500), generator.randrange(30)
        has_general, naval = bool(generator.randrange(2)), bool(generator.randrange(2))
        health, maximum = generator.randrange(1, 100), 100
        p.write(unit + 0x18, "Q", general if has_general else 0)
        p.write(unit + 0x20, "2i", health, maximum)
        p.write(unit + 0x34, "2i", level, xp)
        p.write(definition + 0xc, "i", 3 if naval else 0)
        p.write(row + 8, "2i", heal, cost)
        actual_advanced = p.call(0x53808, unit, gain)
        actual = [p.read(unit + 0x34, "i")[0], p.read(unit + 0x38, "i")[0], p.read(unit + 0x20, "i")[0], actual_advanced]
        threshold = (cost * 3 // 2 if has_general else cost) * (2 if naval else 1)
        advanced = level < 5 and xp + gain >= threshold
        expected = [level + int(advanced), xp + gain - (threshold if advanced else 0), min(maximum, health + heal) if advanced else health, int(advanced)]
        check("unit_experience", actual, expected, case)
        vectors["unit_xp"].append(dict(level=level, experience=xp, gain=gain, cost=cost, heal=heal, general=has_general,
                                       naval=naval, health=health, maximum=maximum, expected=actual))

    for case in range(400):
        maximum, health = generator.randrange(1, 300), 0
        health = generator.randrange(maximum + 1)
        intensity = generator.randrange(5)
        seed = generator.getrandbits(64)
        p.write(unit + 0x20, "3i", health, maximum, 6)
        p.write(unit + 0x4c, "i", intensity)
        p.write(unit + 0x5a, "B", 0)
        p.write(RNG_STATE, "Q", seed)
        p.call(0x54028, unit)
        state, remaining = seed, intensity
        if intensity > 0:
            state = (state * 214013 + 2531011) & ((1 << 64) - 1)
            roll = ((state >> 16) & 32767) % 100
            remaining = (2 if roll < 50 else 1 if roll < 80 else 0) if intensity >= 3 else (1 if roll <= 54 else 0) if intensity == 2 else 0
        damage = maximum * (8 if intensity >= 3 else 6 if intensity == 2 else 4 if intensity == 1 else 0) // 100
        expected_health = max(1, health-damage) if health > 0 else 0
        actual = [p.read(unit + 0x20, "i")[0], p.read(unit + 0x4c, "i")[0], p.read(unit + 0x28, "i")[0], p.read(unit + 0x5a, "B")[0], p.read(RNG_STATE, "Q")[0]]
        expected = [expected_health, remaining, 0, 1, state]
        check("unit_burn_end_turn", actual, expected, case)
        vectors["burn"].append(dict(health=health, maximum=maximum, intensity=intensity, seed=seed, expected=actual))
    report = dict(counts=dict(counts), errors=errors,
                  scope="Faction asset survival; unit refresh with injected commander bonuses; unit XP; nonlethal burning and decay. Not full end-turn healing/economy.")
    (ROOT / "reverse/reports/native_lifecycle_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/lifecycle_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
