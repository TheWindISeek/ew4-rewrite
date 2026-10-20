"""Probe tactical move and move-then-strike candidate scoring in original 7a768."""

import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def f32(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


def main():
    p = NativeProbe()
    ai, source, destination, target, unit, enemy_unit, unit_def, enemy_def, country, other, enemy, building, action, candidates, map_object = [SCRATCH + i * 0x1000 for i in range(1, 16)]
    choices = SCRATCH
    tiles = [source, destination, target]
    p.hook(0x85824, lambda _, index, *args: tiles[index] if index < 3 else 0)
    p.hook(0xa9744, lambda _, index, *args: tiles[index] if index < 3 else 0)
    p.hook(0x77490, lambda _, site, *args: source_value if site == source else destination_value)
    p.hook(0x773a0, lambda *args: 0)
    p.hook(0x77618, lambda *args: attack_score)

    def reach(_, cell, mode, *args):
        if mode == 0x802:
            p.write(choices, "i", 1)
            p.write(ai + 0x20, "2Q", choices, choices + 4)
        return 0

    p.hook(0x78608, reach)
    threshold = p.read(0x7b9f0, "f")[0]
    generator = random.Random(1928)
    fixtures, errors = [], []
    for case in range(1200):
        for address in (ai, source, destination, target, unit, enemy_unit, unit_def, enemy_def, country, other, enemy, building, action, map_object):
            p.cpu.mem_write(address, bytes(0x400))
        source_value, destination_value = generator.randrange(600), generator.randrange(600)
        attack_score = generator.randrange(-500, 1001)
        environment, level = generator.randrange(5), generator.randrange(1, 6)
        own, has_owner, human, built, target_present, acted = [bool(generator.randrange(2)) for _ in range(6)]
        destination_camp = generator.randrange(6)
        health, maximum = generator.randrange(1, 151), 150
        if case % 5 == 0: health = maximum
        if own: has_owner, destination_camp = True, 1
        camp = destination_camp if has_owner else 5
        p.write(ai + 0x90, "Q", map_object)
        p.write(ai + 0x98, "Q", action)
        p.write(ai + 0x68, "3Q", candidates, candidates, candidates + 0x200)
        p.write(action + 0x20, "4i", 0, 0, 3, 1)
        p.write(map_object + 0x100, "2i", 3, 1)
        for i, tile in enumerate(tiles):
            p.write(tile, "5i", i, 0, i, i*64, 0)
        p.write(source + 0x68, "2Q", country, unit)
        p.write(destination + 0x68, "Q", country if own else other if has_owner else 0)
        p.write(target + 0x68, "2Q", enemy, enemy_unit if target_present else 0)
        p.write(destination + 0x40, "Q", building if built else 0)
        p.write(destination + 0x50, "i", environment)
        p.write(building + 8, "i", level)
        p.write(country, "i", 1)
        p.write(other, "i", destination_camp)
        p.write(enemy, "i", 2)
        # Owned sites use country too; keep human ownership consistent with that object.
        p.write((country if own else other) + 0x54, "B", int(not human))
        p.write(unit + 8, "2Q", unit_def, country)
        p.write(enemy_unit + 8, "2Q", enemy_def, enemy)
        p.write(unit + 0x20, "3i", health, maximum, 6)
        p.write(unit + 0x5a, "B", int(acted))
        p.write(unit_def + 0x2c, "2i", 1, 1)
        p.call(0x7a768, ai, 0x2000, 0)
        actual_count = (p.read(ai + 0x70, "Q")[0] - candidates) // 12
        actual = [list(p.read(candidates + i*12, "3i")) for i in range(actual_count)]
        delta = max(0, destination_value-source_value)
        relation = "hostile" if camp not in (1, 4, 5) else "nonallied" if camp != 1 else "owned" if own else "allied"
        score = (delta+25)*2 if relation == "hostile" else delta//2+25 if relation == "nonallied" else delta//4 if relation == "owned" else -10000
        score -= environment*25
        if built and health < maximum: score += level*10
        if has_owner and human: score += 40
        expected = [[1, -1, score]] if score > 0 else []
        combined = None
        if target_present and not acted:
            base = (delta+25)*2 if relation == "hostile" else delta+25 if relation == "nonallied" else delta//2 if relation == "owned" else -10000
            attack = int(attack_score/2) if destination_value < source_value else attack_score
            weight = f32(f32(health)/f32(maximum))
            if weight >= threshold: weight = 1.
            combined = int(f32(f32(base+attack)*weight))
            if combined > 0: expected.append([1, 2, combined])
        if actual != expected:
            errors.append(dict(case=case, actual=actual, expected=expected, relation=relation))
            if len(errors) > 3: raise AssertionError(errors)
        fixtures.append(dict(source_value=source_value, destination_value=destination_value, attack_score=attack_score, environment=environment,
            level=level, own=own, has_owner=has_owner, human=human, built=built, target_present=target_present, acted=acted,
            destination_camp=destination_camp, health=health, maximum=maximum, relation=relation, move_score=score, combined_score=combined, expected=actual))
    report = dict(count=len(fixtures), errors=errors, health_threshold=threshold,
        scope="Real 7a768 tactical move and move-attack generation, relations and 86010 post-move attack eligibility on 3x1 map. Reachable list and location/attack/strength score services injected. Not strategic or production AI.")
    (ROOT / "reverse/reports/native_ai_movement_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_movement_vectors.json").write_text(json.dumps(dict(health_threshold=threshold, fixtures=fixtures), separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors: raise SystemExit(1)


if __name__ == "__main__":
    main()
