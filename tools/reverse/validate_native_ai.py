"""Probe AI eligibility, hold scoring and stable candidate selection with injected facts."""

from collections import Counter
import itertools
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    ai, entity, country, tile, unit, definition, candidates = [SCRATCH + i * 0x1000 for i in range(1, 8)]
    p.write(ai + 0x40, "Q", country)
    p.write(ai + 0x70, "Q", entity)
    p.write(entity + 0x98, "Q", ai)
    p.write(tile + 8, "i", 0)
    p.write(tile + 0x70, "Q", unit)
    p.write(unit + 8, "Q", definition)
    p.hook(0x85824, lambda *args: tile)
    p.hook(0x4ef64, lambda *args: unit)
    strength, threat, pressure, bonus, bonus_eligible, maximum = 0, 0, 0, 255, False, 100
    p.hook(0x77490, lambda *args: strength)
    p.hook(0x773a0, lambda *args: threat)
    p.hook(0x78608, lambda *args: pressure)
    p.hook(0x59638, lambda *args: 0)
    p.hook(0x59744, lambda *args: 0)
    p.hook(0x52adc, lambda *args: int(bonus_eligible))
    p.hook(0x4eb60, lambda *args: bonus)
    p.hook(0x532e0, lambda *args: maximum)
    counts, errors = Counter(), []
    fixtures = dict(hold=[], eligibility=[], selection=[])
    generator = random.Random(1937)

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected, inputs=inputs))
            if len(errors) > 3:
                raise AssertionError(errors)

    def f32(value):
        return struct.unpack("<f", struct.pack("<f", value))[0]

    for case in range(600):
        strength, threat, pressure = [generator.randrange(500) for _ in range(3)]
        if case % 3 == 0:
            pressure = 0
        bonus = generator.choice([0, 1, 255])
        bonus_eligible, naval = bool(generator.randrange(2)), bool(generator.randrange(2))
        environment = generator.randrange(4)
        maximum = generator.randrange(50, 250)
        health = generator.randrange(1, maximum + 1)
        p.write(unit + 0x20, "i", health)
        p.write(definition + 0xc, "i", 3 if naval else 0)
        p.write(tile + 0x50, "i", environment)
        p.write(entity + 0x68, "3Q", candidates, candidates, candidates + 0x800)
        p.call(0x7a768, entity, 0x1000, 0)
        actual = list(p.read(candidates, "3i"))
        score = strength // 2 if pressure == 0 else strength * 2 if threat < pressure else strength
        if bonus_eligible and bonus != 255:
            score *= 2
        score = max(0, score - environment * 25 - (15 if naval else 0))
        score = int(f32(f32(score) * f32(2 - f32(f32(health) / f32(maximum)))))
        inputs = dict(strength=strength, threat=threat, pressure=pressure, bonus=bonus, bonus_eligible=bonus_eligible,
                      naval=naval, environment=environment, maximum=maximum, health=health)
        check("hold_score", actual, [0, -1, score], inputs)
        fixtures["hold"].append(dict(**inputs, expected=score))

    requested = []
    p.hook(0x7a768, lambda _, mode, *args: (requested.append(mode) or 0))
    for behavior, movement, morale, construction, acted in itertools.product(range(5), (0, 6), (-3, 0), (0, 1), (False, True)):
        p.write(unit + 0x28, "i", movement)
        p.write(unit + 0x44, "i", morale)
        p.write(unit + 0x60, "2i", construction, behavior)
        p.write(unit + 0x5a, "B", int(acted))
        requested = []
        actual = p.call(0xa89d4, ai, tile)
        eligible = not ((movement < 1 or morale == -3) and (construction > 0 or acted))
        expected = []
        if eligible:
            if behavior in (0, 2, 3):
                expected.append(0x1000)
            if behavior in (0, 1, 2) and movement > 0 and morale != -3:
                expected += [0x2000, 0x4000]
            if construction < 1 and not acted:
                expected.append(0x3000)
        inputs = dict(behavior=behavior, movement=movement, morale=morale, construction=construction, acted=acted)
        check("candidate_eligibility", [actual, requested], [int(eligible), expected], inputs)
        fixtures["eligibility"].append(dict(**inputs, expected=expected, eligible=eligible))

    # All candidate destinations use the source tile so only score selection is inspected.
    node = SCRATCH + 0x8000
    p.write(country + 8, "Q", node)
    p.write(node, "2Qi", country + 8, country + 8, 0)
    scores = []

    def supply_candidates(*args):
        for index, score in enumerate(scores):
            p.write(candidates + index * 12, "3i", 0, index + 1, score)
        p.write(entity + 0x70, "Q", candidates + len(scores) * 12)
        return 1

    p.hook(0xa89d4, supply_candidates)
    for case in range(300):
        scores = [generator.randrange(-3, 30) for _ in range(generator.randrange(1, 20))]
        if case == 0:
            scores = [-3, -2]
        p.write(entity + 0x68, "2Q", candidates, candidates)
        p.call(0xa8b60, ai)
        index = p.read(entity + 0x60, "i")[0] - 1
        best, expected = -1, 0
        for i, score in enumerate(scores):
            if score > best:
                best, expected = score, i
        check("stable_candidate_selection", index, expected, scores)
        fixtures["selection"].append(dict(scores=scores, expected=index))

    report = dict(counts=dict(counts), errors=errors,
                  scope="AI hold scoring with injected strength/threat facts; candidate class eligibility; stable selection. Other scoring/production not covered.")
    (ROOT / "reverse/reports/native_ai_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
