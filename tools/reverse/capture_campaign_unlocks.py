"""Exercise the original campaign result gate with intercepted profile services."""
import json
from validate_native_rules import NativeProbe, ROOT, SCRATCH
from unicorn import arm64_const as registers


def main():
    p = NativeProbe()
    game, context, profile, manager, metadata, text = [SCRATCH + i * 0x1000 for i in range(6)]
    countries = [SCRATCH + 0x6000 + i * 0x400 for i in range(3)]
    case, calls = {}, []
    p.write(game + 8, 'Q', context)
    p.write(game + 0xc0, 'Q', manager)
    p.write(game + 0xe8, 'Q', profile)
    p.write(context + 0x110, 'Q', metadata)
    p.write(game + 0x50, '2i', 100, 50)
    p.write(game + 0x70, '2i', 0, 3)
    p.write(metadata + 0x30, 'Q', text + 0x18)
    p.hook(0x2f0f0, lambda *args: 10)
    p.hook(0x2c5dc, lambda *args: calls.append('complete') or 0)
    p.hook(0x80b38, lambda *args: len(countries))
    p.hook(0x80904, lambda manager, index, *args: countries[index])
    p.hook(0x58eb4, lambda country, *args: case['yellow'][countries.index(country)])
    p.hook(0x6baec, lambda chapter, stage, buffer, *args: p.write(buffer, 'i', stage) or 0)
    p.hook(0x6b938, lambda catalog, key, *args: metadata)
    p.hook(0x2c870, lambda *args: profile)
    p.hook(0x5ec18, lambda progress, stage, value, *args: calls.append(['unlock', stage, value]) or 0)
    p.hook(0x80c50, lambda *args: countries[0])
    p.hook(0x172d58, lambda *args: 0 if case['branch_match'] else 1)
    for address in (0x2c6d4, 0x2c320, 0x331a0, 0x2c708):
        p.hook(address, lambda *args: 0)
    vectors = []
    for won in (False, True):
        for ai in ([False, True, True], [False, False, True]):
            for yellow in ([0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 2], [1, 1, 1]):
                for branched, match in ((False, False), (True, False), (True, True)):
                    case.update(yellow=yellow, branch_match=match)
                    calls.clear()
                    p.write(game + 0x68, 'i', 1)
                    p.write(game + 0x92, 'B', int(won))
                    p.write(text, 'Q', int(branched))
                    for country, flag in zip(countries, ai):
                        p.write(country + 0x54, 'B', int(flag))
                    try:
                        p.call(0x7f300, game)
                    except Exception as error:
                        raise RuntimeError(f"Native settlement stopped at {p.cpu.reg_read(registers.UC_ARM64_REG_PC):x}, calls={calls}") from error
                    remaining = sum(n for n, flag in zip(yellow, ai) if flag)
                    expected = won and remaining == 0 and (not branched or match)
                    unlocked = [c for c in calls if isinstance(c, list)]
                    assert bool(unlocked) == expected, (won, ai, yellow, branched, match, calls)
                    if unlocked:
                        assert unlocked == [['unlock', 4, 0]]
                    vectors.append(dict(won=won, ai=ai, yellow=yellow, branched=branched, match=match, unlock=expected))
    output = dict(checks=len(vectors), errors=0, vectors=vectors,
        scope='Original ARM64 7f300 settlement with country, metadata and profile services intercepted.')
    (ROOT / 'reverse/reports/campaign_unlock_gate.json').write_text(json.dumps(output, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(checks=len(vectors), errors=0)))


if __name__ == '__main__':
    main()
