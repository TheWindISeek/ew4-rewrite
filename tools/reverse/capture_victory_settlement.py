"""Probe original ARM64 rating and conquest awards with profile/resource services intercepted."""
import json
from validate_native_rules import NativeProbe, ROOT, SCRATCH


def main():
    p = NativeProbe()
    game, context, manager, profile, general, country = [SCRATCH + i * 0x1000 for i in range(6)]
    p.write(game + 8, 'Q', context)
    p.write(game + 0xc0, 'Q', manager)
    p.write(game + 0xe8, 'Q', profile)
    case = {}
    p.hook(0x2f0f0, lambda *args: case['round'])
    p.hook(0x80c50, lambda *args: country)
    p.hook(0x54c60, lambda _, i, *args: general if i < case.get('generals', 0) else 0)
    p.hook(0x59638, lambda *args: case['money'])
    p.hook(0x59744, lambda *args: case['industry'])
    p.hook(0x59e5c, lambda *args: case['food'])
    p.hook(0x2c7d4, lambda *args: case['progress'])
    ratings = []
    for minimum, highest in ((20, 10), (30, 17), (10, 10), (1, 0), (100, 50)):
        p.write(game + 0x50, '2i', minimum, highest)
        for won in (False, True):
            p.write(game + 0x92, 'B', int(won))
            for round_number in range(1, minimum + 3):
                case['round'] = round_number
                ratings.append(dict(minimum=minimum, highest=highest, won=won, round=round_number,
                                    stars=p.call(0x7eeb8, game)))
    conquest = []
    for round_number in (1, 31, 32, 45, 46, 55, 56, 65, 66, 75, 76, 99, 100, 150):
        for count, rank, title, resources, progress in ((0, 0, 0, (0, 0, 0), 0),
                (1, 2, 1, (100, 80, 60), 50), (12, 14, 9, (9999, 9999, 9999), 350)):
            case.update(round=round_number, generals=count, money=resources[0], industry=resources[1], food=resources[2], progress=progress)
            p.write(general + 0x10, '2i', rank, title)
            conquest.append(dict(round=round_number, generals=count, rank=rank, title=title,
                                 resources=resources, progress=progress, award=p.call(0x7ef38, game, 0)))
    result = dict(ratings=ratings, conquest=conquest, campaignAwards=p.read(0x196fc0, '6i'),
                  scope='Original ARM64 7eeb8 and 7ef38(false); external profile and resource reads intercepted.')
    output = ROOT / 'reverse/reports/victory_settlement_vectors.json'
    output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(ratings=len(ratings), conquest=len(conquest), errors=0)))


if __name__ == '__main__':
    main()
