"""Capture original exchange quotes and full-promotion prices without Android."""
import json
import math
import struct
from validate_native_rules import NativeProbe, ROOT, SCRATCH
from unicorn.arm64_const import UC_ARM64_REG_S0


def single(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


def main():
    probe = NativeProbe()
    rank = probe.read(0x194da0, "14i")
    title = probe.read(0x194e20, "9i")
    full = []
    for r in range(15):
        for t in range(10):
            for xp in (0, 17):
                probe.write(SCRATCH + 0x10, "4i", r, t, xp, xp)
                price = probe.call(0x56754, SCRATCH)
                expected = min(3600, math.ceil(single(single(max(0, sum(rank[r:]) - xp)) * single(.008)))
                               + math.ceil(single(single(max(0, sum(title[t:]) - xp)) * single(.2))))
                assert price == expected, (r, t, xp, price, expected)
                full.append(dict(rank=r, title=t, experience=xp, price=price))
    has_commander = [False]
    commerce = [0]
    probe.hook(0x85824, lambda *args: SCRATCH + 0x1000 if has_commander[0] else 0)
    probe.hook(0x4f42c, lambda *args: SCRATCH + 0x2000)
    probe.hook(0x5720c, lambda *args: commerce[0])
    quotes = []
    for commander in (False, True):
        has_commander[0] = commander
        for level in range(6):
            commerce[0] = level
            for offer in range(4):
                sell = probe.call(0x5a0f8, SCRATCH, 0, offer)
                buy = probe.call(0x5a39c, SCRATCH, 0, offer)
                rate = single(single(.4) * (5 - level) + 1) if commander else 3
                assert sell == int(single([2, 10, 50, 250][offer] * rate))
                assert buy == int(single([10, 50, 10, 50][offer] * rate))
                quotes.append(dict(commander=commander, commerce=level, offer=offer, buyMoney=buy, sellAmount=sell))
    impacts = []
    for weapon in range(6):
        for category in range(5):
            for damage in (0, 10, 11, 25, 26, 40, 41):
                for sea in (False, True):
                    for facing in (-1, 1):
                        probe.cpu.reg_write(UC_ARM64_REG_S0, struct.unpack('<I', struct.pack('<f', facing))[0])
                        pointer = probe.call(0x82084, 0, weapon, 0, category, damage, sea)
                        name = bytes(probe.cpu.mem_read(pointer, 64)).split(b'\0')[0].decode('ascii')
                        impacts.append(dict(weapon=['gun', 'guns', 'mgun', 'cannon', 'rocket', 'cold'][weapon],
                                            category=['infantry', 'cavalry', 'artillery', 'naval', 'fort'][category],
                                            damage=damage, sea=sea, facing=facing, name=name))
    report = dict(schema=2, fullPromotion=full, exchange=quotes, impacts=impacts, errors=0)
    (ROOT / "reverse/reports/frontend_service_contracts.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(checks=len(full) + len(quotes) * 2, nativeImpacts=len(impacts), errors=0)))


if __name__ == "__main__":
    main()
