"""Verify nonstandard Mod input against isolated original ARM64 functions."""

import json
from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    faction, unit = SCRATCH + 0x1000, SCRATCH + 0x2000
    p.write(faction + 0x54, "B", 1)
    balances = []
    for initial, income in [(-3000, 100), (-1500, 1600), (200, 100)]:
        p.call(0x595cc, faction, initial)
        before = p.call(0x59638, faction)
        p.call(0x5964c, faction, income)
        after = p.call(0x59638, faction)
        assert before == initial and after == max(0, min(9999, initial + income))
        balances.append(dict(initial=initial, income=income, before=before, after=after))
    p.write(unit + 0x10, "Q", faction)
    p.write(unit + 0x18, "Q", 0)
    morale = []
    for baseline, duration, shortage in [(3, 0, 0), (3, 0, 6), (0, 0, 6), (3, 3, 6), (-3, 0, 2147483647)]:
        p.write(unit + 0x3c, "2i", baseline, duration)
        p.write(unit + 0x48, "i", 0)
        p.write(faction + 0x2c, "i", shortage)
        p.call(0x539b0, unit)
        actual = p.read(unit + 0x44, "i")[0]
        expected = max(-3, baseline + max(-3, 1 - shortage)) if duration < 3 and shortage >= 2 else baseline
        assert actual == expected
        morale.append(dict(baseline=baseline, duration=duration, shortage=shortage, expected=actual))
    result = dict(balances=balances, morale=morale, errors=0,
                  scope="Original ARM64 595cc/59638/5964c/539b0 only; no Mod native code, Android runtime or full game execution")
    output = ROOT / "rewrite/reports/mod_input_native_vectors.json"
    output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(checks=len(balances) * 2 + len(morale), errors=0, report=str(output))))


if __name__ == "__main__":
    main()
