"""Capture original death alpha and floating-number motion from ARM64 functions."""
import json
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH
from unicorn.arm64_const import UC_ARM64_REG_S0


def main():
    p = NativeProbe()
    unit = SCRATCH
    p.write(unit + 0xbc, "B", 0)
    p.call(0x52ac8, unit)
    deaths = [{"ms": 0, "alpha": p.read(unit + 0xd8, "f")[0]}]
    for tick in range(1, 6):
        p.cpu.reg_write(UC_ARM64_REG_S0, struct.unpack("<I", struct.pack("<f", .1))[0])
        p.call(0x524f8, unit)
        deaths.append({"ms": tick * 100, "alpha": p.read(unit + 0xd8, "f")[0]})
    assert all(abs(d["alpha"] - max(0, 1 - d["ms"] / 500)) < 1e-6 for d in deaths)
    report = {"deaths": deaths, "risePerSecond": p.read(0x82ec8, "f")[0],
              "numberLifeDecayPerSecond": .5, "numberExpiry": p.read(0x82ecc, "f")[0],
              "numberStartLife": 1, "numberOffsetY": -30,
              "evidence": ["52ac8", "524f8", "50900", "5335c", "82d90", "82e14", "82ed0", "8346c"], "errors": 0}
    (ROOT / "reverse/reports/combat_presentation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
