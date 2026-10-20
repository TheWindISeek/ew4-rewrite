"""Execute event dispatch, morale mutation and territory surrender in the original SO.

Engine services are injected and recorded; this is not a full game or APK test.
"""

from collections import Counter
import itertools
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH, RNG_STATE, expected_neighbor


def main():
    p = NativeProbe()
    counts, errors = Counter(), []
    vectors = {"morale": [], "spread": [], "round_events": [], "surrender": []}

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append(dict(group=group, actual=actual, expected=expected, inputs=inputs))
            if len(errors) >= 5:
                raise AssertionError(errors)

    unit, general = SCRATCH + 0x1000, SCRATCH + 0x2000
    leadership = False
    p.hook(0x570f8, lambda *args: int(leadership))
    for baseline, value, leadership, operation in itertools.product(range(-3, 2), range(-3, 2), (False, True), ("set", "add")):
        p.write(unit + 0x18, "Q", general)
        p.write(unit + 0x3c, "3i", baseline, 0, baseline)
        p.call(0x538bc if operation == "set" else 0x53910, unit, value)
        result = value if operation == "set" else max(-3, min(1, baseline + value))
        expected = [result, 3, 0 if leadership and result < 0 else result]
        actual = list(p.read(unit + 0x3c, "3i"))
        inputs = dict(baseline=baseline, value=value, leadership=leadership, operation=operation)
        check("morale_mutation", actual, expected, inputs)
        vectors["morale"].append(dict(**inputs, expected=actual))

    # Environmental effects only increase on land. Sea rejects even an existing effect.
    for sea, before, requested in itertools.product((False, True), range(5), range(5)):
        p.write(SCRATCH + 0x1c, "B", int(sea))
        p.write(SCRATCH + 0x50, "i", before)
        result = p.call(0x4ef30, SCRATCH, requested)
        check("environment_raise", [result, p.read(SCRATCH + 0x50, "i")[0]],
              [int(not sea and max(before, requested) > 0), before if sea else max(before, requested)],
              [sea, before, requested])

    world = SCRATCH + 0x3000
    tiles = [SCRATCH + 0x4000 + i * 0x100 for i in range(9)]
    p.hook(0xa9744, lambda _, cell, *args: tiles[cell] if 0 <= cell < 9 else 0)
    p.hook(0x859e0, lambda _, cell, direction, *args: expected_neighbor(3, 3, cell, direction))
    generator = random.Random(1930)
    for case in range(120):
        center = generator.randrange(9)
        seed = generator.getrandbits(64)
        seas = [bool(generator.randrange(3) == 0) for _ in tiles]
        before = [generator.randrange(4) for _ in tiles]
        for tile, sea, effect in zip(tiles, seas, before):
            p.write(tile + 0x1c, "B", int(sea))
            p.write(tile + 0x50, "i", effect)
        p.write(RNG_STATE, "Q", seed)
        p.call(0x86c5c, world, center)
        expected, state = list(before), seed
        if not seas[center]:
            expected[center] = max(expected[center], 3)
        for direction in range(6):
            neighbor = expected_neighbor(3, 3, center, direction)
            if neighbor < 0:
                continue
            state = (state * 214013 + 2531011) & ((1 << 64) - 1)
            roll = ((state >> 16) & 32767) % 100
            effect = 2 if roll < 30 else 1 if roll < 60 else 0
            if not seas[neighbor]:
                expected[neighbor] = max(expected[neighbor], effect)
        actual = [p.read(tile + 0x50, "i")[0] for tile in tiles]
        check("environment_spread", [actual, p.read(RNG_STATE, "Q")[0]], [expected, state], case)
        vectors["spread"].append(dict(center=center, seed=seed, sea=seas, before=before,
                                      expected=actual, rng_state=state))

    # The round dispatcher stops after an existing dialogue, even without a scene.
    manager, event_array = SCRATCH + 0x6000, SCRATCH + 0x7000
    events = [SCRATCH + 0x8000 + i * 0x80 for i in range(5)]
    current_round, dialogue_ids, trace = 1, set(), []
    p.hook(0x2f0f0, lambda *args: current_round)
    p.hook(0x6b8e0, lambda _, identity, *args: general if identity in dialogue_ids else 0)
    p.hook_import("sprintf", lambda *args: 0)
    p.hook(0x31b9c, lambda *args: 0)
    p.hook(0x38e10, lambda *args: 0)
    p.hook(0x31c7c, lambda *args: world)
    p.hook(0x80b9c, lambda _, identity, *args: unit if p.read(identity, "i")[0] == 1 else 0)

    def morale(_, value, *args):
        trace.append(["morale", value if value < 0x80000000 else value - 0x100000000])
        return 0

    p.hook(0x5bca4, morale)
    p.hook(0x86c5c, lambda _, cell, *args: (trace.append(["environment", cell]) or 0))
    p.write(manager + 0x20, "2Q", event_array, event_array + len(events) * 8)
    for i, address in enumerate(events):
        p.write(event_array + i * 8, "Q", address)
    for case in range(160):
        current_round = generator.randrange(1, 5)
        dialogue_ids = {i for i in range(5) if generator.randrange(3) == 0}
        definitions = []
        for i, address in enumerate(events):
            definition = dict(condition=generator.choice([0, 1, 2]), effect=generator.randrange(6),
                              round=generator.randrange(1, 5), dialogue=i, cell=i,
                              country=generator.randrange(2), triggered=bool(generator.randrange(4) == 0))
            definitions.append(definition)
            p.cpu.mem_write(address, bytes(0x80))
            p.write(address, "7i", i, definition["condition"], definition["effect"], definition["round"], i, i, definition["country"])
            p.write(address + 0x28, "B", int(definition["triggered"]))
        trace = []
        p.call(0x8d95c, manager)
        expected_trace, expected_triggered = [], [d["triggered"] for d in definitions]
        for i, d in enumerate(definitions):
            if d["triggered"] or d["condition"] != 2 or d["round"] != current_round:
                continue
            if d["country"] == 1 and d["effect"] in (0, 1, 2):
                expected_trace.append(["morale", [1, -1, -2][d["effect"]]])
            if d["effect"] == 5:
                expected_trace.append(["environment", d["cell"]])
            expected_triggered[i] = True
            if d["dialogue"] in dialogue_ids:
                break
        triggered = [bool(p.read(address + 0x28, "B")[0]) for address in events]
        check("round_dispatch", [trace, triggered], [expected_trace, expected_triggered], case)
        vectors["round_events"].append(dict(round=current_round, dialogues=sorted(dialogue_ids), events=definitions,
                                            expected_trace=trace, expected_triggered=triggered))

    # Surrender iterates the losing country's tile list; units are removed first.
    loser, winner = SCRATCH + 0xa000, SCRATCH + 0xa400
    sentinel, nodes = loser + 8, [SCRATCH + 0xb000 + i * 0x20 for i in range(9)]
    p.hook_import("_ZdlPv", lambda *args: 0)
    p.hook(0x85824, lambda _, cell, *args: tiles[cell])
    p.hook(0x4f2b0, lambda tile, *args: (trace.append(["remove", (tile - tiles[0]) // 0x100]) or 0))
    p.hook(0x58b94, lambda _, cell, *args: (trace.append(["transfer", cell]) or 0))
    for case in range(90):
        count = generator.randrange(10)
        successor = generator.choice([0, loser, winner])
        facts = [dict(sea=bool(generator.randrange(2)), building=bool(generator.randrange(2))) for _ in range(count)]
        p.write(sentinel, "2Q", nodes[0] if count else sentinel, nodes[count - 1] if count else sentinel)
        for i, fact in enumerate(facts):
            p.write(nodes[i], "2Qi", nodes[i + 1] if i + 1 < count else sentinel, sentinel if i == 0 else nodes[i - 1], i)
            p.write(tiles[i] + 0x1c, "B", int(fact["sea"]))
            p.write(tiles[i] + 0x40, "Q", int(fact["building"]))
            p.write(tiles[i] + 0x68, "Q", loser)
        trace = []
        p.call(0x59118, loser, successor)
        expected_trace, expected_owners = [], []
        for i, fact in enumerate(facts):
            expected_trace.append(["remove", i])
            owner = 0 if fact["sea"] and not fact["building"] else successor
            if owner and successor != loser:
                expected_trace.append(["transfer", i])
            expected_owners.append(owner)
        actual_owners = [p.read(tiles[i] + 0x68, "Q")[0] for i in range(count)]
        check("surrender", [trace, actual_owners, list(p.read(sentinel, "2Q"))],
              [expected_trace, expected_owners, [sentinel, sentinel]], case)
        vectors["surrender"].append(dict(tiles=facts, successor="none" if successor == 0 else "self" if successor == loser else "winner",
                                          expected_owners=["none" if o == 0 else "loser" if o == loser else "winner" for o in actual_owners],
                                          expected_trace=trace))

    report = dict(method="Original ARM64 functions; external services injected and recorded.", counts=dict(counts), errors=errors,
                  scope="Morale mutation, environmental spread, round event order, surrender territory transfer; not full combat commit.")
    (ROOT / "reverse/reports/native_event_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/event_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(dict(counts=dict(counts), errors=errors)))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
