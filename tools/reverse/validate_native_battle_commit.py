"""Probe original combat result ordering with damage/progression services recorded.

This isolates 0x825d4. It does not claim that the injected death, fire, rank,
nobility, reward, or multiplayer implementations have been verified here.
"""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, RNG_STATE


def main():
    p = NativeProbe()
    battle = SCRATCH
    tiles = [SCRATCH + 0x1000 + i * 0x100 for i in range(3)]
    units = [SCRATCH + 0x2000 + i * 0x200 for i in range(3)]
    definitions = [SCRATCH + 0x3000 + i * 0x100 for i in range(3)]
    factions = [SCRATCH + 0x4000 + i * 0x400 for i in range(2)]
    trace = []

    def tile_id(tile):
        return (tile - tiles[0]) // 0x100

    def unit_id(unit):
        return (unit - units[0]) // 0x200

    def signed(value):
        return value if value < 0x80000000 else value - 0x100000000

    def record(kind, identity, amount=0):
        trace.append([kind, identity, signed(amount)])
        return 0

    p.hook(0x85824, lambda _, cell, *args: tiles[cell] if cell < 3 else 0)
    p.hook(0x4ef64, lambda tile, *args: p.read(tile + 0x70, "Q")[0])
    p.hook(0x31c7c, lambda *args: 0)
    p.hook(0x4ed18, lambda tile, weapon, *args: record("environment_check", tile_id(tile), weapon))

    def damage(tile, amount, killer, commander, *args):
        identity = tile_id(tile)
        unit = p.read(tile + 0x70, "Q")[0]
        health = max(0, p.read(unit + 0x20, "i")[0] - amount)
        p.write(unit + 0x20, "i", health)
        if not health:
            p.write(tile + 0x70, "Q", 0)
        record("damage", identity, amount)
        return int(not health)

    p.hook(0x4f6b8, damage)
    p.hook(0x53808, lambda unit, value, *args: record("experience", unit_id(unit), value))
    p.hook(0x4f444, lambda tile, value, *args: record("rank_experience", tile_id(tile), value))
    p.hook(0x86b50, lambda _, cell, *args: record("refresh_empty_tile", cell))
    p.hook(0x5bc88, lambda faction, value, *args: record("kill", (faction - factions[0]) // 0x400, value))
    p.hook(0x58fb4, lambda faction, *args: record("refresh_faction", (faction - factions[0]) // 0x400))
    p.hook(0x4f4f4, lambda tile, value, *args: record("nobility_experience", tile_id(tile), value))
    p.hook(0x5c618, lambda *args: 0)
    p.hook(0x4eab8, lambda tile, value, *args: record("erase_bonus", tile_id(tile), value))
    p.hook(0x2f0f0, lambda *args: 3)
    p.hook(0x80c18, lambda *args: 0)
    generator = random.Random(1924)
    fixtures, counts, errors = [], Counter(), []
    for case in range(600):
        health = [generator.randrange(1, 151) for _ in units]
        categories = [generator.randrange(5) for _ in units]
        dealt, taken = generator.randrange(180), generator.randrange(180)
        splash = generator.randrange(90) if generator.randrange(2) else 0
        seed = generator.getrandbits(64)
        counter = bool(generator.randrange(2))
        morales = [generator.randrange(-3, 2) for _ in units]
        weapons = [generator.randrange(5) for _ in units]
        for i, (tile, unit, definition) in enumerate(zip(tiles, units, definitions)):
            p.cpu.mem_write(tile, bytes(0x100))
            p.cpu.mem_write(unit, bytes(0x200))
            p.cpu.mem_write(definition, bytes(0x100))
            p.write(tile + 8, "i", i)
            p.write(tile + 0x68, "Q", factions[0 if i == 0 else 1])
            p.write(tile + 0x70, "Q", unit)
            p.write(unit + 8, "Q", definition)
            p.write(unit + 0x20, "3i", health[i], 150, 6)
            p.write(unit + 0x3c, "3i", morales[i], 0, morales[i])
            p.write(definition + 8, "3i", 10 + i, categories[i], i)
            p.write(definition + 0x28, "i", weapons[i])
        p.cpu.mem_write(battle, bytes(0x100))
        p.write(battle + 0x20, "2i", 0, 1)
        p.write(battle + 0x30, "2i", taken, dealt)
        p.write(battle + 0x3c, "2i", 2 if splash else -1, splash)
        p.write(battle + 0x44, "B", int(counter))
        p.write(RNG_STATE, "Q", seed)
        trace = []
        p.call(0x825d4, battle)
        hp = [p.read(unit + 0x20, "i")[0] for unit in units]
        expected_health = [max(0, health[0] - taken), max(0, health[1] - dealt), max(0, health[2] - splash)]
        damage_order = [entry[1:] for entry in trace if entry[0] == "damage"]
        expected_order = [[0, taken], [1, dealt]] + ([[2, splash]] if splash else [])
        counts["damage_order_health"] += 1
        if hp != expected_health or damage_order != expected_order:
            errors.append(dict(case=case, health=hp, expected=expected_health, damage_order=damage_order))
        if len(errors) > 3:
            raise AssertionError(errors)
        counts["attacker_dead" if hp[0] == 0 else "attacker_alive"] += 1
        counts["defender_dead" if hp[1] == 0 else "defender_alive"] += 1
        fixtures.append(dict(health=health, categories=categories, weapons=weapons, morale=morales, taken=taken, dealt=dealt,
                             splash=splash, counter=counter, seed=seed, expected=dict(health=hp, trace=trace,
                             morale=[list(p.read(unit + 0x3c, "3i")) for unit in units],
                             movement=[p.read(unit + 0x28, "i")[0] for unit in units],
                             acted=[bool(p.read(unit + 0x5a, "B")[0]) for unit in units],
                             rng_state=p.read(RNG_STATE, "Q")[0])))
    report = dict(counts=dict(counts), errors=errors, method="Original ARM64 scheduling with injected damage/progression services.",
                  exclusions=["Death cascade", "Fire probability", "Rank/nobility effects", "Campaign rewards", "Multiplayer/diplomacy"])
    (ROOT / "reverse/reports/native_battle_commit_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/battle_commit_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
