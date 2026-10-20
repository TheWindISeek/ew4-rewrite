"""Run native AI location/strength/attack scoring and stationary strike generation."""

from collections import Counter
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def f32(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


def main():
    p = NativeProbe()
    ai, source, target, attacker, defender, source_def, target_def, source_owner, target_owner, building, source_install, target_install = [SCRATCH + i * 0x1000 for i in range(1, 13)]
    candidates, targets = SCRATCH + 0xd000, SCRATCH + 0xe000
    p.hook(0x85824, lambda _, index, *args: source if index == 0 else target)

    def candidates_for_mode(_, cell, mode, *args):
        if mode == 0x7f8:
            p.write(targets, "i", 1)
            p.write(ai + 0x20, "2Q", targets, targets + 4)
        return 0

    p.hook(0x78608, candidates_for_mode)
    multipliers = list(p.read(0x77484, "3f"))
    counts, errors, fixtures = Counter(), [], []
    generator = random.Random(1926)

    def check(group, actual, expected):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, case=counts[group]-1, actual=actual, expected=expected))
            if len(errors) > 3:
                raise AssertionError(errors)

    def strength(health, grade, category, sea):
        value = int(f32(f32(health) * multipliers[grade-1])) if grade in (1, 2) else health
        return int(f32(f32(value) * (0.5 if category == 3 else multipliers[2]))) if category == 3 or sea else value

    def location(kind, level, installation, priority, commander, behavior):
        slopes, bases = [20, 40, 35, 30, 15], [41, 81, 71, 61, 1]
        result = level * slopes[kind] + bases[kind] if kind != 255 else 1
        result += 35 if installation == 0 else 40 if installation in (1, 2) else 0
        result += 260 if priority == 1 else 200 if priority == 2 else 0
        return result + (200 if commander and behavior == 1 else 0)

    for case in range(1600):
        for address in (ai, source, target, attacker, defender, source_def, target_def, source_owner, target_owner, building, source_install, target_install):
            p.cpu.mem_write(address, bytes(0x200))
        category, target_category = generator.randrange(5), generator.randrange(5)
        unit_id, target_id = generator.randrange(22), generator.randrange(22)
        grade, target_grade = generator.randrange(3), generator.randrange(3)
        maximum, target_maximum = generator.randrange(1, 241), generator.randrange(1, 241)
        health, target_health = generator.randrange(1, maximum+1), generator.randrange(1, target_maximum+1)
        if case % 8 == 0:
            health = maximum
        if case % 8 == 1:
            health, target_health, maximum, target_maximum = 50, 100, 100, 100
        source_sea, target_sea, commander, target_commander, human, transport = [bool(generator.randrange(2)) for _ in range(6)]
        source_camp, target_camp = generator.randrange(6), generator.randrange(6)
        target_has_owner = bool(generator.randrange(2))
        source_installation, target_installation = generator.choice([0, 1, 2, 255]), generator.choice([0, 1, 2, 255])
        kind, level, priority, behavior, construction = generator.choice([0, 1, 2, 3, 4, 255]), generator.randrange(1, 6), generator.randrange(3), generator.randrange(4), generator.randrange(4)
        veteran, target_veteran = generator.randrange(6), generator.randrange(6)
        p.write(source + 8, "i", 0)
        p.write(target + 8, "i", 1)
        p.write(source + 0x1c, "B", int(source_sea))
        p.write(target + 0x1c, "B", int(target_sea))
        p.write(target + 0x18, "i", priority)
        p.write(source + 0x70, "Q", attacker)
        p.write(target + 0x70, "Q", defender)
        p.write(source + 0x68, "Q", source_owner)
        p.write(target + 0x68, "Q", target_owner if target_has_owner else 0)
        p.write(source_owner, "i", source_camp)
        p.write(target_owner, "i", target_camp)
        p.write(target_owner + 0x54, "B", int(not human))
        p.write(target + 0x40, "Q", building if kind != 255 else 0)
        p.write(building + 4, "2i", kind, level)
        p.write(source + 0x48, "Q", source_install if source_installation != 255 else 0)
        p.write(target + 0x48, "Q", target_install if target_installation != 255 else 0)
        p.write(source_install, "i", source_installation)
        p.write(target_install, "i", target_installation)
        for address, definition, identity, cat, gr, hp, max_hp, xp, general in (
            (attacker, source_def, unit_id, category, grade, health, maximum, veteran, commander),
            (defender, target_def, target_id, target_category, target_grade, target_health, target_maximum, target_veteran, target_commander)):
            p.write(address + 8, "Q", definition)
            p.write(definition + 8, "3i", identity, cat, gr)
            p.write(address + 0x20, "2i", hp, max_hp)
            p.write(address + 0x34, "i", xp)
            p.write(address + 0x18, "Q", 0x123 if general else 0)
        p.write(defender + 0x54, "i", behavior)
        p.write(defender + 0x59, "B", int(transport))
        p.write(defender + 0x60, "i", construction)
        value = location(kind, level, target_installation, priority, target_commander, behavior)
        check("location", p.call(0x77490, ai, target), value)
        check("strength", p.call(0x773a0, ai, target), strength(target_health, target_grade, target_category, target_sea))
        score = (veteran - target_veteran) * 15
        camp = target_camp if target_has_owner else 5
        if source_camp == camp and source_camp != 5:
            score -= 10000
        else:
            score += value
            if health >= target_health:
                score += 100
            elif health > target_health * .5:
                score += 60
            elif health < target_health * .5:
                score += 40 if health == maximum else 20
        if category == 2:
            if 10 <= unit_id <= 12:
                if target_category in (0, 1): score += 250
                if target_category in (3, 4): score += 150
                if unit_id == 12:
                    if target_category == 4: score += 200
                    if kind != 255: score += level * 50
            elif unit_id == 13:
                score += 500
        elif category in (0, 1):
            if target_id in (5, 9): score -= 60
            if category == 0 and target_installation == 0: score -= 60
            if category == 1 and target_installation == 1: score -= 30
            if target_category == 3: score -= 30
        elif category == 3:
            if target_category != 3: score += -150 if target_id == 21 else 100
        elif category == 4 and target_id == 12:
            score -= 270
        if target_category == 2: score += 150
        if construction > 0: score += 400
        if category in (0, 1, 2) and source_sea and target_category == 3: score -= 180
        if commander: score += 40
        if transport: score += 5
        if target_installation == 2: score -= 25
        if source_installation in (0, 1): score -= 20
        if target_has_owner and human: score += 40
        actual = p.call(0x77618, ai, 0, 1)
        check("attack_score", actual, score)
        p.write(ai + 0x68, "3Q", candidates, candidates, candidates + 0x100)
        p.call(0x7a768, ai, 0x3000, 0)
        strike = list(p.read(candidates, "3i"))
        weighted = int(f32(f32(score + 50) * f32(f32(health) / f32(maximum))))
        check("strike_candidate", strike, [1, -1, weighted])
        fixtures.append(dict(category=category, target_category=target_category, unit_id=unit_id, target_id=target_id,
            grade=grade, target_grade=target_grade, health=health, maximum=maximum, target_health=target_health, target_maximum=target_maximum,
            source_sea=source_sea, target_sea=target_sea, commander=commander, target_commander=target_commander, human=human,
            transport=transport, source_camp=source_camp, target_camp=target_camp, target_has_owner=target_has_owner,
            source_installation=source_installation, target_installation=target_installation, building=kind, level=level, priority=priority,
            behavior=behavior, construction=construction, veteran=veteran, target_veteran=target_veteran,
            location=value, strength=strength(target_health, target_grade, target_category, target_sea), attack_score=actual, strike_score=strike[2]))

    report = dict(counts=dict(counts), errors=errors, grade_multipliers=multipliers,
        scope="Native 773a0/77490/77618 and 7a768 strike branch. Real category/relation/site accessors; only map lookup and attack candidate list injected. Not movement, strategic or production AI.")
    (ROOT / "reverse/reports/native_ai_attack_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_attack_vectors.json").write_text(json.dumps(dict(multipliers=multipliers, fixtures=fixtures), separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
