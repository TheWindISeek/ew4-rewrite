"""Run native unit death dispatch with recorded country/commander/event services."""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, RNG_STATE


def main():
    p = NativeProbe()
    tile, unit, definition, commander, event, country, active, system = [SCRATCH + i * 0x1000 for i in range(1, 9)]
    killer = SCRATCH + 0x9000
    trace, retreat_mode, defeated, has_event = [], False, False, False

    def signed(value):
        return value if value < 0x80000000 else value - 0x100000000

    def record(kind, value=0):
        trace.append([kind, signed(value)])
        return 0

    p.hook(0x51c84, lambda *args: 0)  # Fort image damage cache; not gameplay HP.
    p.hook(0x80c18, lambda *args: active)
    p.hook(0x31844, lambda *args: system)
    p.hook(0x31c7c, lambda *args: system)
    p.hook(0x8d678, lambda *args: event if has_event else 0)
    p.hook(0x6b8e0, lambda *args: 0)  # No dialogue fixtures in this death probe.
    p.hook(0x80b9c, lambda *args: 0)  # Missing event faction falls back to dying unit owner.
    p.hook(0x5bca4, lambda _, value, *args: record("morale", value))
    p.hook(0x86c5c, lambda _, value, *args: record("environment", value))
    p.hook(0x5be90, lambda _, identity, negative, *args: record("retreat_dialogue", negative))
    p.hook(0x51e74, lambda *args: record("dispose"))
    p.hook_import("_ZdlPv", lambda *args: record("free"))
    p.hook(0x5906c, lambda *args: int(defeated))
    p.hook(0x59118, lambda *args: record("surrender"))
    p.hook(0x58c00, lambda *args: record("release_sea"))
    p.hook(0x5c060, lambda _, identity, amount, *args: record("kill_dialogue", identity))
    generator = random.Random(1946)
    counts, errors, fixtures = Counter(), [], []
    for case in range(600):
        health, damage = generator.randrange(1, 120), generator.randrange(150)
        general, retreat_mode, defeated, sea, building, owner, has_event, already = [bool(generator.randrange(2)) for _ in range(8)]
        trigger = generator.choice([-1, 0, 1])
        effect, killer_portrait_id = generator.randrange(6), generator.choice([-1, 0, 7])
        seed = generator.getrandbits(64)
        for address in (tile, unit, definition, commander, event, country, active):
            p.cpu.mem_write(address, bytes(0x300))
        p.write(tile + 0x1c, "B", int(sea))
        p.write(tile + 0x40, "Q", 1 if building else 0)
        p.write(tile + 0x68, "Q", country if owner else 0)
        p.write(tile + 0x70, "Q", unit)
        p.write(tile + 0x78, "i", 1)
        p.write(unit + 8, "Q", definition)
        p.write(unit + 0x18, "Q", commander if general else 0)
        p.write(unit + 0x20, "i", health)
        p.write(unit + 0x70, "i", trigger)
        p.write(commander, "i", 123)
        p.write(event + 8, "i", effect)
        p.write(event + 0x14, "i", 42)
        p.write(event + 0x28, "B", int(already))
        p.write(active + 0x57, "B", int(retreat_mode))
        p.write(RNG_STATE, "Q", seed)
        trace = []
        result = p.call(0x4f6b8, tile, damage, killer, killer_portrait_id)
        dead = damage >= health
        expected_trace, state = [], seed
        fired = already
        if dead:
            if general:
                if trigger >= 0 and has_event and not already:
                    if effect < 4:
                        expected_trace.append(["morale", [1, -1, -2, -3][effect]])
                    elif effect == 5:
                        expected_trace.append(["environment", 42])
                    fired = True
                expected_trace.append(["retreat_dialogue", int(trigger < 0)])
            if retreat_mode:
                expected_trace += [["dispose", 0], ["free", 0]]
            if owner and defeated:
                expected_trace.append(["surrender", 0])
            else:
                if owner and sea and not building:
                    expected_trace.append(["release_sea", 0])
                if not general and not retreat_mode and killer_portrait_id > 0:
                    state = (state * 214013 + 2531011) & ((1 << 64) - 1)
                    if ((state >> 16) & 32767) % 100 < 8:
                        expected_trace.append(["kill_dialogue", killer_portrait_id])
        actual = dict(dead=bool(result), health=p.read(unit + 0x20, "i")[0], trace=trace,
                      occupied=bool(p.read(tile + 0x70, "Q")[0]), occupant_count=p.read(tile + 0x78, "i")[0],
                      corpse=bool(p.read(tile + 0x90, "Q")[0]), fired=bool(p.read(event + 0x28, "B")[0]),
                      owner=bool(p.read(tile + 0x68, "Q")[0]), rng_state=p.read(RNG_STATE, "Q")[0])
        expected = dict(dead=dead, health=max(0, health-damage), trace=expected_trace, occupied=not dead,
                        occupant_count=0 if dead else 1, corpse=dead and not retreat_mode, fired=fired,
                        owner=owner and not (dead and not defeated and sea and not building), rng_state=state)
        counts["death_dispatch"] += 1
        counts["dead" if dead else "alive"] += 1
        inputs = dict(health=health, damage=damage, general=general, retire_immediately=retreat_mode, defeated=defeated,
                      sea=sea, building=building, owner=owner, has_event=has_event, already=already,
                      trigger=trigger, effect=effect, killer_portrait_id=killer_portrait_id, seed=seed)
        if actual != expected:
            errors.append(dict(inputs=inputs, actual=actual, expected=expected))
            if len(errors) > 3:
                raise AssertionError(errors)
        fixtures.append(dict(**inputs, expected=actual))
    report = dict(counts=dict(counts), errors=errors,
                  scope="Original damage/death dispatch; injected event services, defeat predicate, surrender, retreat/kill dialogue and destruction. 5be90/5c060 are dialogue services, not commander-roster return or loot grants.")
    (ROOT / "reverse/reports/native_death_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/death_vectors.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
