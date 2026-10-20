"""Execute native strategic candidate weighting with injected goals and paths."""

import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH, registers
from unicorn import UC_HOOK_CODE


def f32(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


def main():
    p = NativeProbe()
    ai, source, destination, target, unit, target_unit, unit_def, target_def, country, enemy, action, candidates, map_object = [SCRATCH + i * 0x1000 for i in range(1, 14)]
    choices, sea_choices, node, container = SCRATCH, SCRATCH+0x100, SCRATCH+0x400, SCRATCH+0x800
    tiles = [source, destination, target]
    p.hook(0x85824, lambda _, index, *args: tiles[index] if index < 3 else 0)
    p.hook(0xa9744, lambda _, index, *args: tiles[index] if index < 3 else 0)
    p.hook(0x77490, lambda _, tile, *args: goal_value if tile == target else 1)
    p.hook(0x773a0, lambda _, tile, *args: goal_strength if tile == target else own_strength)
    p.hook(0x31c7c, lambda *args: container)
    p.hook(0x80b38, lambda *args: 1)
    p.hook(0x80904, lambda *args: enemy)
    p.hook(0x6ae84, lambda *args: 0)
    p.hook(0x5a9f4, lambda *args: 0)
    p.hook(0x8dd7c, lambda *args: 0)
    p.hook_import("_ZdlPv", lambda *args: 0)
    rand_calls, bounds = [], []

    def draw(*args):
        rand_calls.append(random_value)
        return random_value

    p.hook_import("rand", draw)

    def observe(cpu, pc, size, user_data):
        bound = cpu.reg_read(registers.UC_ARM64_REG_X0) & 0xffffffff
        bounds.append(bound if bound < 0x80000000 else bound-0x100000000)

    p.cpu.hook_add(UC_HOOK_CODE, observe, begin=0xef658, end=0xef658)

    def reach(_, cell, mode, *args):
        if mode in (2000, 2001):
            p.write(choices, "i", 2)
            p.write(ai + 0x20, "2Q", choices, choices+4)
            return 1
        return pressure if mode == 0x7da else 0

    def paths(_, start, output, *args):
        p.write(choices, "i", 1)
        p.write(output, "3Q", choices, choices+4, choices+4)
        return 0

    def sea_paths(_, output, *args):
        p.write(sea_choices, "i", 2)
        p.write(output, "3Q", sea_choices, sea_choices+4 if sea_reachable else sea_choices, sea_choices+4)
        return 0

    p.hook(0x78608, reach)
    p.hook(0x862c4, paths)
    p.hook(0x8e0bc, sea_paths)
    generator = random.Random(1932)
    fixtures, errors = [], []
    for case in range(1200):
        for address in (ai, source, destination, target, unit, target_unit, unit_def, target_def, country, enemy, action, map_object):
            p.cpu.mem_write(address, bytes(0x400))
        naval, transport, commander, source_sea, target_sea, target_present, target_naval, sea_reachable = [bool(generator.randrange(2)) for _ in range(8)]
        maximum = generator.randrange(120, 301)
        health, movement, behavior = generator.randrange(1, maximum+1), generator.randrange(1, 9), generator.randrange(4)
        if case % 6 == 0: health = maximum
        goal_value, goal_strength, own_strength, pressure = generator.randrange(300, 800), generator.randrange(300), generator.randrange(300), generator.randrange(300)
        if case % 7 == 0: pressure = 0
        if case % 11 == 0: own_strength = pressure//2
        if case % 13 == 0: goal_value, target_present, naval = 20, False, False
        random_value = generator.randrange(32768)
        p.write(ai + 8, "Q", container)
        p.write(ai + 0x90, "Q", map_object)
        p.write(ai + 0x98, "Q", action)
        p.write(ai + 0x68, "3Q", candidates, candidates, candidates+0x100)
        p.write(action + 0x40, "Q", country)
        p.write(map_object + 0x100, "2i", 3, 1)
        for i, tile in enumerate(tiles):
            p.write(tile, "5i", i, 0, i, i*64, 0)
        p.write(source + 0x68, "2Q", country, unit)
        p.write(destination + 0x68, "Q", country)
        p.write(target + 0x68, "2Q", enemy, target_unit if target_present else 0)
        p.write(source + 0x1c, "B", int(source_sea))
        p.write(target + 0x1c, "B", int(target_sea))
        p.write(country, "i", 1)
        p.write(enemy, "i", 2)
        p.write(enemy + 8, "2Q", node, node)
        p.write(node, "2Qi", enemy+8, enemy+8, 2)
        p.write(unit + 8, "2Q", unit_def, country)
        p.write(unit + 0x18, "Q", 1 if commander else 0)
        p.write(unit + 0x20, "3i", health, maximum, movement)
        p.write(unit + 0x59, "B", int(transport))
        p.write(unit + 0x64, "i", behavior)
        p.write(unit_def + 8, "2i", 14 if naval else 0, 3 if naval else 0)
        p.write(target_unit + 8, "2Q", target_def, enemy)
        p.write(target_def + 0xc, "i", 3 if target_naval else 0)
        p.write(0x222e07, "B", 0)
        bounds, rand_calls = [], []
        p.call(0x7a768, ai, 0x4000, 0)
        actual_count = (p.read(ai + 0x70, "Q")[0]-candidates)//12
        actual = [list(p.read(candidates+i*12, "3i")) for i in range(actual_count)]
        goal_eligible = naval or goal_value > 44 or target_present
        expected, expected_bounds, expected_calls = [], [], []
        base = (30 if health > 119 else 10) + (10 if health == maximum else 0) + (20 if naval or transport else 0) + (20 if commander else 0)
        objective = goal_value//2 + goal_strength
        if naval:
            objective += 30 if target_present and sea_reachable else -120
        elif source_sea:
            objective += -30 if target_present and target_naval else 30 if target_sea else 60
        else:
            objective += (7 if target_naval else 15) if target_present and target_sea else 30
        score = base+objective
        if pressure != 0:
            if own_strength >= pressure: score = int(score/2)
            elif own_strength > pressure//2: score = int(score/3)
            elif own_strength < pressure//2: score = int(score/4)
        score = int(f32(f32(score)*f32(f32(health)/f32(maximum))))
        turns = 2//(movement//2) if movement//2 else 0
        score = int(score/(turns+1))
        before_behavior = score
        if goal_eligible:
            if behavior == 3: score = 0
            elif behavior in (0, 2):
                floor = int(score/(2 if behavior == 0 else 4))
                bound = floor if behavior == 0 else score-floor
                expected_bounds.append(bound)
                score = floor + (random_value % bound if bound else 0)
                if bound: expected_calls.append(random_value)
            expected = [[1, -1, score]]
        if actual != expected or bounds != expected_bounds or rand_calls != expected_calls:
            errors.append(dict(case=case, actual=actual, expected=expected, bounds=bounds, expected_bounds=expected_bounds, draws=rand_calls))
            if len(errors) > 3: raise AssertionError(errors)
        fixtures.append(dict(naval=naval, transport=transport, commander=commander, source_sea=source_sea, target_sea=target_sea,
            target_present=target_present, target_naval=target_naval, sea_reachable=sea_reachable, health=health, maximum=maximum,
            movement=movement, behavior=behavior, goal_value=goal_value, goal_strength=goal_strength, own_strength=own_strength,
            pressure=pressure, random_value=random_value, eligible=goal_eligible, before_behavior=before_behavior, score=score, expected=actual, random_bounds=bounds))
    report = dict(count=len(fixtures), errors=errors,
        scope="Real 7a768 strategic single-goal candidate weighting plus real ef658 modulo; injected goal/site/strength/path lists and raw rand return. Transport acquisition, multi-goal choice and full path search not covered.")
    (ROOT / "reverse/reports/native_ai_strategy_validation.json").write_text(json.dumps(report, indent=2)+"\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_strategy_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":"))+"\n", encoding="utf-8")
    print(json.dumps(report))
    if errors: raise SystemExit(1)


if __name__ == "__main__":
    main()
