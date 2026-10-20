"""Verify original camera rectangles and local commander promotion prices."""
import json
from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    probe = NativeProbe()
    vectors = {"camera": [], "promotion": []}
    for left in (0, 1, 7, 24):
        for top in (0, 1, 2, 13):
            for width, height in ((1, 1), (2, 2), (12, 19), (79, 68)):
                probe.write(SCRATCH + 0x108, "4i", left, top, width, height)
                outputs = [SCRATCH + 0x800 + i * 4 for i in range(4)]
                probe.call(0x840f0, SCRATCH, *outputs)
                original = list(probe.read(outputs[0], "4f"))
                expected = [left * 64 - 32, (top - 1) * 54, width * 64 + 32, (height - 1) * 54 + 72]
                assert original == expected, (left, top, width, height, original, expected)
                vectors["camera"].append(dict(source=[left, top, width, height], rectangle=original))
    assert probe.read(0x84d14, "f")[0] == 40
    for address, cost_address, offset, levels, factor in ((0x56444, 0x194da0, 0x10, 14, .008), (0x56650, 0x194e20, 0x14, 9, .2)):
        costs = probe.read(cost_address, str(levels) + "i")
        for level in range(levels + 1):
            for experience in (0, 17, costs[min(level, levels - 1)] - 1):
                probe.write(SCRATCH + offset, "i", level)
                probe.write(SCRATCH + offset + 8, "i", experience)
                price = probe.call(address, SCRATCH)
                vectors["promotion"].append(dict(title=offset == 0x14, level=level, experience=experience, price=price))
    output = ROOT / "reverse/reports/frontend_contracts.json"
    output.write_text(json.dumps(dict(schema=1, cameraMargin=20, recruitment={"counts": [6, 4, 2], "ratingRanges": [[1, 3], [4, 6], [7, 9]], "badgePrices": list(probe.read(0x195150, "8i"))}, vectors=vectors), indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(camera=len(vectors["camera"]), promotion=len(vectors["promotion"]), errors=0)))


if __name__ == "__main__":
    main()
