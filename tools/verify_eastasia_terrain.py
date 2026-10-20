"""Check geographic control points and the actual rewrite terrain presentation."""

from collections import Counter
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

import build_eastasia_runtime as runtime
from PIL import Image


def main():
    raw = (runtime.OUT / "original/eastasia.bin").read_bytes()
    magic, version, width, height = struct.unpack_from("<4s3i", raw)
    assert (magic, version, width, height) == (b"YSAE", 1, 158, 136)
    cells = runtime.np.frombuffer(raw[16:], dtype=runtime.np.uint8).reshape((height, width, 12))
    manifest = json.loads((runtime.OUT / "client/manifest.json").read_text(encoding="utf-8"))
    definitions = list(ET.parse(runtime.BASE / "original/def_mapterrain.xml").getroot())
    expected = {}
    for row in range(height):
        for col in range(width):
            kind, index = map(int, cells[row, col, :2])
            if kind in (0, 1):
                continue
            image = next(t.get("image") for t in definitions[kind] if int(t.get("idx")) == index)
            expected[row * width + col] = (image, runtime.center(col, row))
    all_overlays = manifest["maps"]["eastasia"]["overlays"]
    overlays = [overlay for overlay in all_overlays if not overlay['seaForeground']]
    relief = json.loads((runtime.REPORT/'relief.json').read_text(encoding='utf-8'))
    relief_by_cell = {item['cell']:item for item in relief['cells']}
    assert len(overlays) == len(expected)
    for overlay in overlays:
        image, position = expected.pop(overlay["cell"])
        item = relief_by_cell.get(overlay['cell'])
        if item and item['level'] != 'hill':
            image = f"eastasia_{item['level']}_{item['spriteIndex']+1}.png"
        assert image == overlay["image"] and position == (overlay["x"], overlay["y"])
        assert image in manifest["atlases"]["tiles"]
    assert not expected
    assert all(relief['counts'].get(level,0)>0 for level in ('hill','low','middle','high'))
    for name in ('tiles','tiles_hd'):
        with Image.open(runtime.BASE/f'client/images/{name}.png') as original, Image.open(runtime.OUT/f'client/images/{name}.png') as expanded:
            assert original.convert('RGBA').tobytes() == expanded.crop((0,0,*original.size)).tobytes(), 'Original terrain sprites changed'
        for index in range(1,7):
            sprites = [manifest['atlases'][name][f'eastasia_{level}_{index}.png'] for level in ('low','middle','high')]
            assert sprites[0]['h'] < sprites[1]['h'] < sprites[2]['h'], 'Mountain height bands must be visibly distinct'
    tile_metadata = {(tile['x'], tile['y']): tile for tile in manifest['maps']['eastasia']['tiles']}
    alpha_cache = {}
    fractions = runtime.np.load(runtime.REPORT / 'land_fraction.npy')
    land_samples = transparent_land = sea_samples = opaque_sea = 0
    for row in range(height):
        for col in range(width):
            x, y = runtime.center(col, row)
            left = (x-runtime.IMAGE_LEFT)//1024*1024+runtime.IMAGE_LEFT
            top = (y-runtime.IMAGE_TOP)//1024*1024+runtime.IMAGE_TOP
            tile = tile_metadata[left, top]
            if tile['file'] not in alpha_cache:
                with Image.open(runtime.OUT / 'client' / tile['file']) as image:
                    assert image.mode == 'RGBA', 'Coast tiles must preserve underlying terrain and faction colors'
                    alpha_cache[tile['file']] = image.getchannel('A')
            alpha = alpha_cache[tile['file']].getpixel((x-left, y-top))
            if fractions[row, col] >= .95:
                land_samples += 1
                transparent_land += alpha == 0
            elif fractions[row, col] <= .01:
                sea_samples += 1
                opaque_sea += alpha == 255
    assert transparent_land / land_samples > .95, 'Opaque land hides terrain and scenario ownership'
    assert opaque_sea / sea_samples > .99, 'Open sea must cover the neutral ground texture'
    facilities = json.loads((runtime.REPORT/'facilities.json').read_text(encoding='utf-8'))
    cities = json.loads((runtime.REPORT/'cities.json').read_text(encoding='utf-8'))
    protected = {s['cell'] for s in cities+facilities}
    translations = manifest['maps']['eastasia']['cityNames']
    assert len(translations) == len(cities)
    for city in cities:
        names = translations[str(city['cell'])]
        assert names['name'] == names['zh'] == city['name'] and names['en'].isascii() and names['en'].strip()
    landmarks = json.loads((runtime.REPORT/'mountain_landmark_cells.json').read_text(encoding='utf-8'))
    plan = json.loads((runtime.REPORT/'mountain_landmarks.json').read_text(encoding='utf-8'))['mountains']
    assert {m['name'] for m in landmarks} == {m['name'] for m in plan}
    for landmark in landmarks:
        assert landmark['cell'] not in protected, 'Mountain must not displace a settlement'
        assert cells[landmark['row'], landmark['col'], 0] == 9, landmark['name']+' must be physical mountain terrain'
        assert landmark['displacement_pixels'] <= 90
        assert relief_by_cell[landmark['cell']]['level'] == landmark['relief']
    with Image.open(runtime.REPORT/'land_mask.png') as coast:
        ports = [f for f in facilities if f['kind'] == 'port']
        for port in ports:
            x, y = port['world_pixel']
            assert cells[port['row'], port['col'], 0] == 1 and coast.getpixel((x+32, y+54)) < 128, 'Port anchor must be visibly at sea: '+port['anchor_city']
    controls = [
        ("Himalaya", 86.5, 28.6, 9), ("Kunlun", 84, 36, 9),
        ("Tianshan", 85, 43, 9), ("Hengduan", 99, 28, 9),
        ("Qinling", 108, 34, 9), ("Daba", 108, 32, 9),
        ("Nanling", 113, 25.2, 9), ("Sayan", 96, 52, 9),
        ("Sikhote Alin", 134.2, 45.8, 9), ("Taebaek", 128.5, 38.2, 9),
        ("Hokkaido", 142.7, 43.1, 9), ("Eastern Ghats", 83.5, 18.8, 8),
        ("Chota Nagpur", 85.5, 23.7, 8), ("Shan plateau", 98.5, 22, 8),
        ("Shandong hills", 120.5, 37, 8), ("Siberian taiga", 126, 58, 6),
        ("Hainan woodland", 110, 19.8, 5), ("Qaidam", 94, 37, 7),
        ("Junggar", 86, 45.5, 7), ("Gobi", 105, 43, 7),
        ("Sichuan basin", 104.5, 30.5, 0), ("Middle Yangtze", 114.3, 30.5, 0),
        ("Gangetic plain", 82.5, 26, 0), ("Bengal lowlands", 89.5, 23, 0),
        ("Yanshan", 117.6, 40.4, 9), ("Dayao range", 110.1, 24.2, 9),
        ("Yunkai range", 111.1, 22.5, 9), ("Lianhua range", 115.5, 23.5, 9),
        ("Eastern Guangdong hills", 115.2, 24.4, 8), ("Guangxi uplands", 106.8, 24, 8),
    ]
    results = []
    # A 75 km neighborhood allows for the 42 km game grid and intentionally sparse sprites.
    radius = 75000 / runtime.geo.METRES_PER_PIXEL
    for name, lon, lat, kind in controls:
        x, y = runtime.geo.PROJ.pixels(lon, lat)
        nearby = [(col, row) for row in range(height) for col in range(width)
                  if (runtime.center(col, row)[0] / 2 - x) ** 2
                  + (runtime.center(col, row)[1] / 2 - y) ** 2 <= radius ** 2]
        matching = sum(int(cells[row, col, 0]) == kind for col, row in nearby)
        assert matching, f"Missing expected physical terrain near {name}"
        results.append(dict(name=name, lon=lon, lat=lat, expectedKind=kind, matchingCells=matching))
    counts = Counter(int(k) for k in cells[:, :, 0].flat)
    names = [element.get("name") for element in definitions]
    cartography = runtime.eastasia_cartography.verify(runtime, cells, manifest)
    report = dict(cells=width*height, overlays=len(overlays), errors=0, visibleSeaPorts=len(ports),
                  terrainCounts={names[k]: count for k, count in sorted(counts.items())}, controls=results, reliefCounts=relief['counts'],
                  cartography=cartography, mountainLandmarks=landmarks, alpha=dict(landSamples=land_samples, transparentLand=transparent_land,
                             seaSamples=sea_samples, opaqueSea=opaque_sea))
    (runtime.REPORT / "terrain-verification.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(cells=report["cells"], overlays=len(overlays), controls=len(results),
                          errors=0, terrainCounts=report["terrainCounts"], reliefCounts=relief['counts'],cartography=cartography)))


if __name__ == "__main__":
    main()
