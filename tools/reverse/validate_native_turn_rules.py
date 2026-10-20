"""Probe movement-cost and single-human victory branches with supplied world facts."""

from collections import Counter
import itertools
import json

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    counts, errors, vectors = Counter(), [], []
    movement_vectors, supply_vectors, morale_vectors = [], [], []

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append({"group": group, "actual": actual, "expected": expected, "inputs": inputs})
            if len(errors) > 10:
                raise AssertionError(errors)

    active, other, unit = [SCRATCH + i * 0x1000 for i in range(1, 4)]
    active_exists = True
    p.hook(0x80c18, lambda *args: active if active_exists else 0)
    p.hook(0x31844, lambda *args: SCRATCH + 0x4000)
    p.hook(0x6aa70, lambda *args: 3)
    for sea, category, transport, enemy, block_occupied, geography, blocked in itertools.product(
            (False, True), range(5), (False, True), (False, True), (False, True), (False, True), (False, True)):
        p.write(active, "i", 1)
        p.write(other, "i", 2 if enemy else 1)
        p.write(unit + 0x10, "Q", other)
        p.write(SCRATCH + 0x70, "Q", unit)
        p.write(SCRATCH + 0x1c, "B", int(sea))
        p.write(SCRATCH + 0x25, "B", 1 << 2 if blocked else 0)
        p.write(SCRATCH + 0x38, "i", 6)
        expected = -1
        if not (enemy and block_occupied):
            if sea and not blocked and (category == 3 or transport):
                expected = 1
            elif not sea and category != 3:
                expected = 3 if category == 0 or geography else 6
        check("tile_movement_cost", p.call(0x50110, SCRATCH, 2, category, transport, block_occupied, geography),
              expected, [sea, category, transport, enemy, block_occupied, geography, blocked])
        movement_vectors.append({"sea": sea, "category": category, "transport": transport, "enemy": enemy,
                                 "block_occupied": block_occupied, "geography": geography, "blocked": blocked,
                                 "expected": expected})
    active_exists = False
    check("movement_no_active_country", p.call(0x50110, SCRATCH, 0, 0, 0, 0, 0), -1, [])

    countries = []
    current_round = 1
    p.hook(0x2f0f0, lambda *args: current_round)
    p.hook(0x80b38, lambda *args: len(countries))
    p.hook(0x80904, lambda _, index, *args: SCRATCH + 0x5000 + index * 0x400)
    p.hook(0x5906c, lambda country, *args: int(countries[(country - SCRATCH - 0x5000) // 0x400]["defeated"]))
    p.hook(0x58e24, lambda country, *args: countries[(country - SCRATCH - 0x5000) // 0x400]["objectives"])
    for camp, human_alive, enemy_alive, allied_ai, objective_mode, own_objectives, enemy_objectives, overtime in itertools.product(
            range(1, 4), (False, True), (False, True), (False, True), (False, True), (0, 1), (0, 1), (False, True)):
        countries = [dict(camp=camp, ai=False, defeated=not human_alive, objectives=own_objectives),
                     dict(camp=camp % 3 + 1, ai=True, defeated=not enemy_alive, objectives=enemy_objectives)]
        if allied_ai:
            countries.append(dict(camp=camp, ai=True, defeated=False, objectives=1))
        current_round = 11 if overtime else 10
        p.cpu.mem_write(SCRATCH, bytes(0x1000))
        p.write(SCRATCH + 0x68, "i", 1)
        p.write(SCRATCH + 0x50, "i", 10)
        p.write(SCRATCH + 0x4c, "i", 0 if objective_mode else 1)
        for index, country in enumerate(countries):
            address = SCRATCH + 0x5000 + index * 0x400
            p.cpu.mem_write(address, bytes(0x400))
            p.write(address, "i", country["camp"])
            p.write(address + 0x54, "B", int(country["ai"]))
        expected = "continue"
        if overtime or not human_alive:
            expected = "defeat"
        elif not enemy_alive:
            expected = "victory"
        elif objective_mode:
            if not enemy_objectives:
                expected = "victory"
            elif not own_objectives and not allied_ai:
                expected = "defeat"
        finished = p.call(0x7e238, SCRATCH)
        actual = "continue" if not finished else "victory" if p.read(SCRATCH + 0x92, "B")[0] else "defeat"
        check("single_human_victory", actual, expected, [camp, human_alive, enemy_alive, allied_ai, objective_mode,
                                                        own_objectives, enemy_objectives, overtime])
        vectors.append({"camp": camp, "countries": countries, "round": current_round,
                        "round_limit": 10, "objectives_enabled": objective_mode, "expected": actual})

    consumption = 1
    p.hook(0x59efc, lambda *args: consumption)
    p.write(SCRATCH + 0x54, "B", 1)
    p.write(SCRATCH + 0x238, "i", 0)
    for food, consumption in itertools.product(range(101), (0, 1, 4, 10, 33, 100, 120, 9999)):
        p.write(SCRATCH + 0x20, "I", food ^ 0x7ead3)
        p.call(0x59f68, SCRATCH)
        actual = [p.read(SCRATCH + 0x20, "I")[0] ^ 0x7ead3, p.read(SCRATCH + 0x2c, "i")[0]]
        ratio = food / consumption if consumption else 1
        expected = [max(0, food - consumption), 0 if food >= consumption else
                    1 if ratio >= .75 else 2 if ratio >= .5 else 3 if ratio >= .25 else 4]
        check("food_consumption", actual, expected, [food, consumption])
        supply_vectors.append({"food": food, "consumption": consumption, "expected": actual})

    unit, country, general, definition = [SCRATCH + i * 0x1000 for i in range(7, 11)]
    p.write(unit + 0x10, "Q", country)
    p.write(unit + 0x18, "Q", general)
    p.write(general + 8, "Q", definition)
    for baseline, duration, local, shortage, logistics, leadership in itertools.product(
            (-1, 0, 1), range(4), range(3), range(5), (False, True), (False, True)):
        p.write(unit + 0x3c, "2i", baseline, duration)
        p.write(unit + 0x48, "i", local)
        p.write(country + 0x2c, "i", shortage)
        skills = ([22] if logistics else []) + ([3] if leadership else [])
        p.write(general + 0x40, "i", len(skills))
        p.write(definition + 0x34, "4i", *(skills + [-1] * (4 - len(skills))))
        p.call(0x539b0, unit)
        actual = p.read(unit + 0x44, "i")[0]
        expected = baseline
        if duration < 3:
            penalty = -local
            if shortage >= 2 and not logistics:
                penalty = max(-3, penalty - shortage + 1)
            expected = max(-3, baseline + penalty)
        if leadership and expected < 0:
            expected = 0
        inputs = [baseline, duration, local, shortage, logistics, leadership]
        check("morale", actual, expected, inputs)
        morale_vectors.append({"inputs": inputs, "expected": actual})

    report = {"scope": "Movement cost and single-human camps1..3 victory with injected country facts; not full turn or multiplayer.",
              "counts": dict(counts), "errors": errors}
    (ROOT / "reverse/reports/native_turn_rules_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/victory_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
        (ROOT / "reverse/reports/movement_economy_vectors.json").write_text(json.dumps({"movement": movement_vectors,
            "supply": supply_vectors, "morale": morale_vectors}, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps({"counts": dict(counts), "errors": errors}))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
