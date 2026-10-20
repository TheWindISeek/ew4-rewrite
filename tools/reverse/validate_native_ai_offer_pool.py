"""Run original unit-card candidate pooling below its unsafe fixed stack capacity."""

import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, registers
from unicorn import UC_HOOK_CODE


def main():
    p = NativeProbe()
    ai, action, country, tile, building, choices, records = [SCRATCH + i * 0x1000 for i in range(1, 8)]
    cards = {(unit, grade): SCRATCH + 0x8000 + unit * 0x60 + grade * 0x10 for unit in range(18) for grade in range(3)}
    reverse_cards = {address: key for key, address in cards.items()}
    fixture, raw_draws, bounds = {}, [], []
    p.hook(0x85824, lambda *args: tile)
    p.hook(0x4ea88, lambda *args: fixture["building"])
    p.hook(0x5587c, lambda *args: choices)
    p.hook(0x5c60c, lambda _, unit, *args: 0 if fixture["unlocked"][str(unit)] else -1)
    p.hook(0x6aeac, lambda _, unit, grade, *args: cards[(unit, grade)])
    p.hook(0x5a9f4, lambda _, card, *args: int(fixture["affordable"][f"{reverse_cards[card][0]}.{reverse_cards[card][1]}"]))

    def draw(*args):
        value = fixture["raw_random"][len(raw_draws)]
        raw_draws.append(value)
        return value

    p.hook_import("rand", draw)
    p.cpu.hook_add(UC_HOOK_CODE, lambda cpu, *args: bounds.append(cpu.reg_read(registers.UC_ARM64_REG_X0) & 0xffffffff), begin=0xef658, end=0xef658)
    generator = random.Random(1924)
    base_ids = [0, 3, 6, 9, 12, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 34, 35, 36]
    fixtures, errors = [], []
    for case in range(1000):
        naval = bool(generator.randrange(2))
        units = generator.sample(list(range(14, 18)) if naval else list(range(14)), generator.randrange(1, 5))
        grades = [0 if naval else generator.randrange(3 if unit < 5 else 2) for unit in units]
        fixture = dict(building=3 if naval else generator.randrange(3), request=33 if naval else 0, units=units, grades=grades,
            unlocked={str(unit): bool(generator.randrange(2)) for unit in units},
            affordable={f"{unit}.{grade}": bool(generator.randrange(2)) for unit in units for grade in range(3)},
            raw_random=[generator.randrange(32768) for _ in range(40)])
        if case % 17 == 0:
            fixture["unlocked"] = {str(unit): True for unit in units}
            fixture["affordable"] = {key: True for key in fixture["affordable"]}
        p.cpu.mem_write(ai, bytes(0x200))
        p.write(ai + 0x98, "Q", action)
        p.write(action + 0x40, "Q", country)
        p.write(tile + 0x40, "Q", building)
        p.write(tile + 0x68, "Q", country)
        p.write(choices, "3Q", records, records + len(units) * 8, records + len(units) * 8)
        for index, (unit, grade) in enumerate(zip(units, grades)):
            p.write(records + index * 8, "2i", unit, grade)
        raw_draws, bounds = [], []
        actual = p.call(0x77a28, ai, fixture["request"], 0)
        expected_bounds, expected_draws, pool = [], [], []

        def sample(bound):
            expected_bounds.append(bound)
            value = fixture["raw_random"][len(expected_draws)]
            expected_draws.append(value)
            return value % bound

        for unit, maximum in zip(units, grades):
            if not fixture["unlocked"][str(unit)]:
                continue
            affordable_count = 0
            for grade in range(maximum, -1, -1):
                if not fixture["affordable"][f"{unit}.{grade}"]:
                    continue
                affordable_count += 1
                if affordable_count == 1 or sample(100) > (20 if affordable_count == 2 else 50):
                    pool.append(base_ids[unit] + grade)
        expected = 47
        if pool:
            expected = pool[sample(len(pool) * 20) // 20]
            if expected == 33 and sample(10) <= 7:
                expected = 47
        if actual != expected or bounds != expected_bounds or raw_draws != expected_draws:
            errors.append(dict(case=case, actual=actual, expected=expected, bounds=bounds, expected_bounds=expected_bounds, pool=pool))
            if len(errors) >= 3:
                raise AssertionError(errors)
        fixtures.append(dict(inputs=fixture, expected=actual, pool=pool, random_bounds=bounds))
    report = dict(count=len(fixtures), errors=errors,
        scope="Real 77a28 affordability-biased land/naval card selection and random order; building/catalog/unlock/affordability queries injected. At most four categories and three grades, below native 18-int stack capacity; not an overflow exploit or world production scheduler.")
    (ROOT / "reverse/reports/native_ai_offer_pool_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/ai_offer_pool_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
