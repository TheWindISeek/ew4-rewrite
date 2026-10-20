"""Validate layer timing and affine/color submission without a graphics driver."""

from collections import Counter
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    counts, errors, timing, transforms = Counter(), [], [], []

    def check(name, actual, expected):
        counts[name] += 1
        if actual != expected and len(errors) < 5:
            errors.append({"name": name, "actual": actual, "expected": expected})

    layer, frames = SCRATCH, SCRATCH + 0x1000
    p.write(layer + 8, "i", 3)
    p.write(layer + 0x10, "Q", frames)
    p.write(layer + 0x20, "i", 12)
    for i, key in enumerate((0, 4, 8)):
        p.write(frames + i * 0x18 + 0xc, "i", key)
    p.hook(0xabef0, lambda *args: 0)
    p.hook(0xac020, lambda *args: 0)
    for mode in range(3):
        for start in range(12):
            p.write(layer + 0x1c, "i", mode)
            p.call(0xac394, layer, start)
            index, tick = start // 4, start
            check("layer_seek", p.read(layer + 0x24, "2i"), (index, tick))
            series = [[index, tick]]
            for _ in range(20):
                if mode != 2 and not (mode == 1 and index == 2):
                    tick += 1
                    if index == 2 and tick >= 12:
                        index, tick = 0, 0
                    elif index < 2 and tick >= (index + 1) * 4:
                        index += 1
                p.call(0xac448, layer)
                actual = p.read(layer + 0x24, "2i")
                check("layer_tick", actual, (index, tick))
                series.append(list(actual))
            timing.append({"mode": mode, "start": start, "expected": series})

    instance, item, element, sprite, parent = [SCRATCH + i * 0x1000 for i in range(2, 7)]
    drawn = []

    def draw(_, matrix, *args):
        drawn.append(list(p.read(matrix, "6f")))
        return 0

    p.hook(0xad3b8, draw)
    p.write(instance, "Q", item)
    p.write(instance + 8, "Q", element)
    p.write(instance + 0x30, "Q", sprite)
    p.write(item + 0x18, "i", 1)
    rng = random.Random(1924)
    for _ in range(300):
        local = [rng.uniform(-2, 2) for _ in range(4)] + [rng.uniform(-100, 100) for _ in range(2)]
        outer = [rng.uniform(-2, 2) for _ in range(4)] + [rng.uniform(-100, 100) for _ in range(2)]
        alpha = rng.choice([.25, .5, 1.0])
        p.write(element, "7f", *local, alpha)
        p.write(parent, "6f", *outer)
        local, outer = list(p.read(element, "6f")), list(p.read(parent, "6f"))
        p.write(0x221438, "I", 0xc0334455)
        p.call(0xabad8, instance, parent)
        check("leaf_submission", len(drawn), 1)
        expected_alpha = int(192 * alpha)
        color = p.read(sprite + 0x78, "I")[0]
        check("leaf_alpha", color, expected_alpha * 0x1000000 + 0x334455)
        transforms.append({"local": local, "parent": outer, "alpha": alpha, "expected": drawn.pop(), "color": color})
    report = {"scope": "Native layer seek/tick and affine/alpha submission; nested drawing and rendered pixels not verified.",
              "counts": dict(counts), "errors": errors}
    (ROOT / "reverse/reports/native_animation_validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not errors:
        (ROOT / "reverse/reports/animation_vectors.json").write_text(json.dumps({"timing": timing, "transforms": transforms}, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps({"counts": dict(counts), "errors": errors}))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
