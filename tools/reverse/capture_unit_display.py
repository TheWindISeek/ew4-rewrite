"""Record original ARM64 flag eligibility and health tint submissions."""
import hashlib
import itertools
import json

from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    probe = NativeProbe()
    tile, unit, renderer, country, enemy = [SCRATCH + i * 0x1000 for i in range(5)]
    probe.write(tile + 0x70, "Q", unit)
    probe.write(tile + 0xe0, "Q", renderer)
    probe.hook(0x80c18, lambda *args: country)
    probe.hook(0x5335c, lambda *args: 0)
    flags = []
    submitted = []
    probe.hook(0x62cb8, lambda *args: submitted.append(bool(args[2])) or 0)
    for owned, movement, morale, construction, acted, target in itertools.product(
            (False, True), (0, 6), (0, -3), (0, 2), (False, True), (False, True)):
        probe.write(tile + 0x68, "Q", country if owned else enemy)
        probe.write(unit + 0x28, "i", movement)
        probe.write(unit + 0x44, "i", morale)
        probe.write(unit + 0x60, "i", construction)
        probe.write(unit + 0x5a, "B", acted)
        probe.write(unit + 0x5c, "B", target)
        submitted.clear()
        probe.call(0x50900, tile)
        expected = owned and (movement > 0 and morale != -3 or construction < 1 and not acted and target)
        assert submitted == [expected], (owned, movement, morale, construction, acted, target, submitted)
        flags.append(dict(owned=owned, movement=movement, morale=morale,
                          construction=construction, acted=acted, target=target, animated=submitted[0]))

    colors = []
    tints = []
    for address in (0xc4a34, 0xc31c8, 0xc3af0):
        probe.hook(address, lambda *args: 0)
    probe.hook(0xc4968, lambda _, tint, *args: tints.append(tint & 0xffffffff) or 0)
    for maximum in (100, 137, 999):
        for health in range(maximum + 1):
            tints.clear()
            probe.call(0x60958, renderer, 0, 0, health, maximum, 0, 0, 0)
            packed = tints[0]
            colors.append(dict(health=health, maximum=maximum, packed=packed,
                               rgb=[packed & 255, packed >> 8 & 255, packed >> 16 & 255]))
    report = dict(checks=len(flags) + len(colors), errors=0, flags=flags, colors=colors,
                  sourceSha256=hashlib.sha256((ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so").read_bytes()).hexdigest(),
                  evidence=["50900", "4fb38", "62cb8", "60958", "60b68"],
                  scope="Original flag predicate and packed ABGR health tint; graphics calls intercepted, not device pixels.")
    (ROOT / "reverse/reports/unit_display_vectors.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(checks=report["checks"], errors=0)))


if __name__ == "__main__":
    main()
