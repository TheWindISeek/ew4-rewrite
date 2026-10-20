"""Check the real single-player turn entry with injected scene and subsystem services."""

from collections import Counter
import json
import random

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    rng, counts, vectors = random.Random(1926), Counter(), []

    def check(group, actual, expected):
        counts[group] += 1
        if actual != expected:
            raise AssertionError(dict(group=group, actual=actual, expected=expected))

    p = NativeProbe()
    battle, faction, manager = [SCRATCH + i * 0x1000 for i in range(1, 4)]
    trace, fixture = [], {}
    p.hook(0x80c18, lambda *args: faction if fixture["exists"] else 0)
    p.hook(0x5b14c, lambda *args: (trace.append("start_faction") or 0))
    p.hook(0x8d95c, lambda *args: (trace.append("round_events") or 0))
    p.hook(0x31b9c, lambda *args: manager)
    p.hook(0x38e10, lambda *args: manager if fixture["scene"] else 0)
    p.hook(0x9dde4, lambda *args: (trace.append("human_ui") or 0))
    p.hook(0x58d50, lambda *args: fixture["selection"])

    def command(_, pointer, *args):
        trace.append(["select", p.read(pointer, "i")[0], p.read(pointer + 12, "i")[0]])
        return 0

    p.hook(0x5ba0c, command)
    for case in range(360):
        fixture = dict(mode=rng.choice([1, 2, 4]), exists=bool(rng.randrange(2)), ai=bool(rng.randrange(2)),
                       scene=bool(rng.randrange(2)), selection=rng.choice([-1, 0, 17]))
        p.cpu.mem_write(battle, bytes(0x100))
        p.cpu.mem_write(faction, bytes(0x300))
        p.write(battle + 0x68, "i", fixture["mode"])
        p.write(faction + 0x54, "B", int(fixture["ai"]))
        p.write(battle + 0x93, "B", 7)
        trace.clear()
        p.call(0x7e0b0, battle)
        expected = []
        if fixture["exists"]:
            expected.append("start_faction")
            if not fixture["ai"]:
                if fixture["mode"] == 4:
                    expected.append("round_events")
                else:
                    if fixture["scene"]:
                        expected.append("human_ui")
                    if fixture["selection"] >= 0:
                        expected.append(["select", 5, fixture["selection"]])
        check("single_player_entry_order", trace, expected)
        expected_flag = 0 if fixture["exists"] and not fixture["ai"] and fixture["mode"] != 4 else 7
        check("single_player_entry_flag", p.read(battle + 0x93, "B")[0], expected_flag)
        vectors.append(dict(**fixture, trace=list(trace)))

    p = NativeProbe()
    trace, fixture = [], {}
    p.hook(0x7e214, lambda *args: (trace.append("end_faction") or 0))
    p.hook(0x80b38, lambda *args: 1)
    p.hook(0x80de8, lambda *args: (trace.append("advance") or int(fixture["wrapped"])))
    p.hook(0x2f0f0, lambda *args: fixture["round"])
    p.hook(0x331a0, lambda _, key, value, *args: (trace.append(["round", value]) or 0))
    p.hook(0x7e238, lambda *args: (trace.append("victory") or int(fixture["finished"])))
    p.hook(0x31b9c, lambda *args: manager)
    p.hook(0x38e10, lambda *args: manager)
    p.hook(0x9e00c, lambda *args: (trace.append("finish_scene") or 0))
    p.hook(0x80c18, lambda *args: faction)
    p.hook(0x5c618, lambda *args: 1)
    p.hook(0x7e0b0, lambda *args: (trace.append("start_faction") or 0))
    for case in range(240):
        fixture = dict(wrapped=bool(rng.randrange(2)), finished=bool(rng.randrange(2)), round=rng.randrange(1, 20))
        p.cpu.mem_write(battle, bytes(0x100))
        p.cpu.mem_write(faction, bytes(0x300))
        p.write(battle + 0x68, "i", rng.choice([1, 2, 4]))
        trace.clear()
        p.call(0x7e638, battle)
        expected = ["end_faction", "advance"]
        if fixture["wrapped"]:
            expected += [["round", fixture["round"] + 1], "victory"]
        expected.append("finish_scene" if fixture["wrapped"] and fixture["finished"] else "start_faction")
        check("single_player_transition_order", trace, expected)

    p = NativeProbe()
    trace, fixture = [], {}
    p.hook(0x2f0f0, lambda *args: fixture["mode"])
    p.hook(0x31844, lambda *args: manager)
    p.hook(0x331a0, lambda *args: (trace.append("unpause") or 0))
    p.hook(0x31b9c, lambda *args: manager)
    p.hook(0x38e80, lambda *args: (trace.append("close_scene") or 0))
    p.hook(0x38e10, lambda *args: manager if fixture["scene"] else 0)
    p.hook(0x9de50, lambda *args: (trace.append("finish_flow") or 0))

    def service(_, key, *args):
        name = bytes(p.cpu.mem_read(key, 32)).split(b"\0")[0]
        return battle if name == b"Battle" and fixture["battle"] else manager if name == b"TriggerEvent" and fixture["events"] else 0

    p.hook(0x31c7c, service)
    p.hook(0x8d95c, lambda *args: (trace.append("round_events") or 0))
    for case in range(240):
        fixture = dict(mode=rng.choice([1, 2, 4]), battle=bool(rng.randrange(2)), events=bool(rng.randrange(2)),
                       finished=bool(rng.randrange(2)), scene=bool(rng.randrange(2)))
        p.cpu.mem_write(battle, bytes(0x100))
        p.write(battle + 0x91, "B", int(fixture["finished"]))
        trace.clear()
        p.call(0x48be0, 0, manager)
        expected = []
        if fixture["mode"] != 4:
            expected = ["unpause", "close_scene"]
            if fixture["battle"]:
                if fixture["finished"]:
                    if fixture["scene"]:
                        expected.append("finish_flow")
                elif fixture["events"]:
                    expected.append("round_events")
        check("dialogue_close_dispatch", trace, expected)
        trace.clear()
        p.call(0x42750, 0, manager)
        check("intro_close_dispatch", trace, ["close_scene", "round_events"])

    report = dict(counts=dict(counts), errors=[], scope="Real 7e0b0/7e638 single-player control flow and 42750/48be0 intro/dialogue close callbacks; faction refresh, event processing, rotation, victory and UI services injected. Not a complete turn or device test.")
    (ROOT / "reverse/reports/native_turn_entry_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (ROOT / "reverse/reports/turn_entry_vectors.json").write_text(json.dumps(vectors, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
