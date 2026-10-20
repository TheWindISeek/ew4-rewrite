"""Capture original objective-circle and flag draw calls, without running a game."""
import hashlib
import json
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH
from unicorn import arm64_const as registers


def main():
    probe = NativeProbe()
    tile, unit, renderer, building, country, circle = [SCRATCH + i * 0x1000 for i in range(6)]
    calls = []

    def position():
        return [struct.unpack('<f', struct.pack('<I', probe.cpu.reg_read(register)))[0]
                for register in (registers.UC_ARM64_REG_S0, registers.UC_ARM64_REG_S1)]

    def capture_circle(source, color, *args):
        calls.append(dict(position=position(), color=color, renderer=source))
        return 0

    probe.hook(0x56030, lambda *args: 0)
    probe.hook(0x6760c, capture_circle)
    probe.write(tile + 0xc, '2i', 320, 540)
    probe.write(tile + 0xe0, 'Q', renderer)
    probe.write(tile + 0x70, 'Q', unit)
    objectives = []
    for site_marker in (-1, 0, 1, 2, 3):
        for unit_marker in (-1, 0, 1, 2, 3):
            calls.clear()
            probe.write(tile + 0x18, 'i', site_marker)
            probe.write(unit + 0x54, 'i', unit_marker)
            probe.call(0x506c4, tile)
            selected = site_marker or unit_marker
            expected = [] if selected not in (1, 2) else [dict(position=[320, 540],
                color=0xff0000ff if selected == 1 else 0xff00ffff, renderer=renderer)]
            assert calls == expected, (site_marker, unit_marker, calls)
            objectives.append(dict(site=site_marker, unit=unit_marker, draws=list(calls)))

    flags = []
    probe.hook(0x62cb8, lambda *args: flags.append(dict(position=position(), animated=bool(args[2]))) or 0)
    probe.hook(0x5335c, lambda *args: 0)
    probe.hook(0x80c18, lambda *args: country)
    probe.hook(0x4fb38, lambda *args: 0)
    probe.write(tile + 0x40, 'Q', building)
    probe.write(tile + 0x68, 'Q', country)
    for occupied in (False, True):
        for kind in range(5):
            flags.clear()
            probe.write(tile + 0x70, 'Q', unit if occupied else 0)
            probe.write(building + 4, 'i', kind)
            probe.call(0x50900, tile)
            assert len(flags) == (1 if occupied or kind != 4 else 0)
            assert all(f['position'] == [293, 495] for f in flags)

    # The helper supplies the original circle library, tint and HD/SD matrices.
    second = NativeProbe()
    second.hook(0x2f09c, lambda *args: int(hd))
    second.hook(0x67184, lambda *args: circle)
    matrices = []
    second.hook(0xabad8, lambda animation, matrix, *args:
        matrices.append(dict(matrix=list(second.read(matrix, '6f')), color=second.read(circle + 0x38, 'I')[0])) or 0)
    for hd in (False, True):
        second.cpu.reg_write(registers.UC_ARM64_REG_S0, struct.unpack('<I', struct.pack('<f', 320))[0])
        second.cpu.reg_write(registers.UC_ARM64_REG_S1, struct.unpack('<I', struct.pack('<f', 540))[0])
        second.call(0x6760c, renderer, 0xff0000ff)
        assert matrices[-1] == dict(matrix=[1 if hd else .5, 0, 0, 1 if hd else .5,
            640 if hd else 320, 1080 if hd else 540], color=0xff0000ff)
    flag_step = probe.read(0x66bdc, 'f')[0]
    assert abs(flag_step - .1) < 1e-7
    result = dict(schema=1, checks=len(objectives) + 10 + 2 + 1, errors=0,
        sourceSha256=hashlib.sha256((ROOT / 'apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so').read_bytes()).hexdigest(),
        objectives=objectives, circleMatrices=matrices, flagOffset=[-27, -45], flagFrameSeconds=flag_step,
        scope='Original ARM64 draw submissions with rendering/resource services intercepted; not Android pixel acceptance.')
    (ROOT / 'reverse/reports/objective_marker_draws.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: result[k] for k in ('checks', 'errors', 'flagOffset', 'flagFrameSeconds')}))


if __name__ == '__main__':
    main()
