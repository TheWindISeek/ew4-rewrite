"""Independent structural and geographic checks of the saved map resource pack."""

import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
import numpy as np
from PIL import Image

from build_eastasia import OUT, ROOT, PROJ, decode_pvr, quantize
from build_city_font import read_font


def main():
    global OUT
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=OUT)
    parser.add_argument("--compare", type=Path, help="Baseline whose interior artwork must be preserved")
    args = parser.parse_args()
    OUT = args.output.resolve()
    checks = 0
    errors = []

    def check(condition, message):
        nonlocal checks
        checks += 1
        if not condition:
            errors.append(message)

    assets = OUT / "assets"
    metadata = json.loads((OUT / "validation.json").read_text(encoding="utf-8"))
    source = ROOT / "欧陆战争4_1.6.6.apk"
    check(hashlib.sha256(source.read_bytes()).hexdigest() == metadata["original_apk_sha256"],
          "Original APK differs from recorded source")
    for item in json.loads((OUT / "sources.json").read_text(encoding="utf-8")):
        check(hashlib.sha256((ROOT / item["path"]).read_bytes()).hexdigest() == item["sha256"],
              "Geographic source hash mismatch")
    manifest = json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))
    check(len(manifest) == 213, "Unexpected resource count")
    with ZipFile(OUT / "eastasia_map_resources.zip") as archive:
        check(archive.testzip() is None, "Resource ZIP CRC failure")
        check(set(archive.namelist()) == {r["path"] for r in manifest}, "ZIP inventory mismatch")
        for item in manifest:
            data = archive.read(item["path"])
            check(len(data) == item["bytes"] and hashlib.sha256(data).hexdigest() == item["sha256"],
                  "Resource ZIP differs from manifest: " + item["path"])
    for name, scale in [("map1.bin", 1), ("map1_hd.bin", 2)]:
        data = (assets / name).read_bytes()
        w, h = struct.unpack_from("<2i", data)
        check((w, h) == (4993 * scale, 3583 * scale), "Texture map pixel dimensions")
        check(len(data) == 8 + 81 * 58 * 4, "Texture map index byte length")
        ids = np.frombuffer(data[8:], dtype="<i4")
        check(int(ids.min()) >= -1 and int(ids.max()) < 102 * 16, "Texture index out of bounds")
        for number in range(1, 103):
            suffix = "@2x" if scale == 2 else ""
            pvr = (assets / f"map/map1_{number}{suffix}.pvr").read_bytes()
            header = struct.unpack_from("<13I", pvr)
            check(header[1:3] == (256 * scale, 256 * scale) and
                  header[5] == 256 * scale * 256 * scale * 2 and
                  len(pvr) == 52 + header[5] and header[11] == 559044176,
                  "PVR header/length error")
        padded_path = OUT / f"eastasia_overlay_padded{scale}x.png"
        if metadata.get("texture_edges"):
            check(padded_path.is_file(), f"Missing geographic edge reference: scale {scale}")
        if padded_path.exists():
            expected = np.array(quantize(Image.open(padded_path).convert("RGBA")))
            expected[expected[:, :, 3] == 0] = 0
            ids = ids.reshape(58, 81)
            block = 64 * scale
            step = 62 * scale
            decoded = {}
            mismatches = 0
            for row in range(58):
                for col in range(81):
                    tile_id = int(ids[row, col])
                    reference = expected[row * step:row * step + block, col * step:col * step + block]
                    if tile_id == -1:
                        matches = not reference.any()
                    elif tile_id == 0:
                        matches = np.all(reference == [68, 102, 136, 255])
                    else:
                        number, tile = tile_id // 16 + 1, tile_id % 16
                        if number not in decoded:
                            decoded[number] = np.asarray(decode_pvr(
                                (assets / f"map/map1_{number}{suffix}.pvr").read_bytes()))
                        x, y = tile % 4 * block, tile // 4 * block
                        actual = decoded[number][y:y + block, x:x + block].copy()
                        actual[actual[:, :, 3] == 0] = 0
                        matches = np.array_equal(reference, actual)
                    mismatches += not bool(matches)
            check(mismatches == 0, f"Saved texture tiles differ from padded geography: scale {scale}")
            check(ids.shape == ((h + step - 1) // step, (w + step - 1) // step),
                  f"Index dimensions differ from native 62-pixel stepping: scale {scale}")
            check(expected.shape[:2] == (58 * step + 2 * scale, 81 * step + 2 * scale),
                  f"Texture edge canvas does not cover native draw extent: scale {scale}")
            del expected, decoded
    binary = (assets / "europe.bin").read_bytes()
    check(struct.unpack_from("<4s3i", binary) == (b"YSAE", 1, 79, 68), "Terrain header")
    check(len(binary) == 64480, "Terrain file size")
    if args.compare:
        baseline = args.compare.resolve()
        for name in ("eastasia_land_mask.png", "eastasia_shores_rivers.png", "eastasia_base.png", "eastasia_base_hd.png"):
            with Image.open(OUT / name) as actual, Image.open(baseline / name) as previous:
                check(np.array_equal(np.asarray(actual), np.asarray(previous)), "Interior changed: " + name)
    cells = np.frombuffer(binary[16:], dtype=np.uint8).reshape(68, 79, 12)
    check(set(map(int, np.unique(cells[:, :, 0]))).issubset(set(range(10))), "Unexpected terrain category")
    check(np.all(cells[:, :, 4][cells[:, :, 0] == 1] == 11), "Land decoration in water")
    definitions = ET.parse(ROOT / "apk_work/original/assets/def_mapterrain.xml").getroot()
    for kind, definition in enumerate(definitions.findall("terrain")):
        allowed = {int(tile.get("idx")) for tile in definition.findall("tile")}
        background = set(map(int, cells[:, :, 1][cells[:, :, 0] == kind]))
        # Only plain land uses a missing background sprite in the original map.
        check(background.issubset(allowed | ({255} if kind == 0 else set())),
              f"Background sprite index outside terrain definition: {kind}")
        foreground = set(map(int, cells[:, :, 5][cells[:, :, 4] == kind]))
        check(foreground.issubset(allowed), f"Foreground sprite index outside terrain definition: {kind}")
    check(set(map(int, np.unique(cells[:, :, 4]))).issubset(set(range(10)) | {11}),
          "Unexpected foreground terrain category")
    check(np.all(cells[:, :, 5][cells[:, :, 4] == 11] == 255), "Invalid empty foreground sprite")
    check(np.all(cells[:, :, 8] < 64), "River edge flags exceed six bits")
    extended = np.asarray(Image.open(OUT / "eastasia_terrain_land_mask.png")) >= 128
    fractions = np.load(OUT / "cell_land_fraction.npy")
    independently_measured = np.zeros((68, 79))
    for row in range(68):
        for col in range(79):
            cx, cy = col * 64 + 32 * (row % 2) + 36, row * 54 + 36
            hits = total = 0
            for dx in range(-32, 32):
                vertical_radius = 36 - abs(dx + .5) * 18 / 32
                low = int(np.ceil(-vertical_radius - .5))
                high = int(np.floor(vertical_radius - .5)) + 1
                hits += np.count_nonzero(extended[cy + low:cy + high, cx + dx])
                total += high - low
            independently_measured[row, col] = hits / total
    check(np.array_equal(fractions, independently_measured), "Saved hexagon land fractions differ from measured areas")
    check(np.array_equal(cells[:, :, 0] == 1, independently_measured <= .5),
          "Water-majority hexagon classified as land, or land-majority hexagon classified as sea")
    for suffix in ("", "_hd"):
        name = f"font_city{suffix}"
        original = ROOT / "apk_work/original/assets"
        old_height, old_records = read_font((original / f"{name}.fnt").read_bytes())
        height, records = read_font((assets / f"{name}.fnt").read_bytes())
        check(height == old_height and [r for r in records if r[0] != 39] == old_records,
              "Original city font metrics changed: " + name)
        check({chr(r[0]) for r in records} == set("ABCDEFGHIJKLMNOPQRSTUVWXYZ'"), "City glyph inventory: " + name)
        with Image.open(assets / f"{name}.png") as atlas, Image.open(original / f"{name}.png") as old:
            check(np.array_equal(np.asarray(atlas.crop((0, 0, old.width, old.height))), np.asarray(old)),
                  "Original city glyph pixels changed: " + name)
            check(all(x + w <= atlas.width and y + h <= atlas.height for _, x, y, w, h, *_ in records),
                  "City glyph outside texture: " + name)
    # Independently enumerate all six neighbours and reciprocal edge masks.
    asymmetric = 0
    for row in range(68):
        odd = row % 2
        for col in range(79):
            offsets = [(1, 0, 1, 8), (odd, 1, 2, 16), (odd - 1, 1, 4, 32),
                       (-1, 0, 8, 1), (odd - 1, -1, 16, 2), (odd, -1, 32, 4)]
            for dx, dy, bit, reciprocal in offsets:
                if not int(cells[row, col, 8]) & bit:
                    continue
                x, y = col + dx, row + dy
                if not (0 <= x < 79 and 0 <= y < 68 and int(cells[y, x, 8]) & reciprocal):
                    asymmetric += 1
    check(asymmetric == 0, "Nonreciprocal river edges")
    mask = np.asarray(Image.open(OUT / "eastasia_land_mask.png"))
    for label, lon, lat, expected_land in [
        ("Beijing", 116.40, 39.90, True), ("Chengdu", 104.07, 30.57, True),
        ("Harbin", 126.64, 45.76, True), ("Lhasa", 91.14, 29.65, True),
        ("Urumqi", 87.62, 43.83, True), ("Kashgar", 75.99, 39.47, True),
        ("Taiwan interior", 121.0, 23.7, True), ("Hainan interior", 109.5, 19, True),
        ("Seoul", 126.98, 37.57, True), ("Japan interior", 138, 36, True),
        ("Yellow Sea", 124, 35, False), ("East China Sea", 126, 29, False),
        ("Japan Sea", 135, 40, False), ("Taiwan Strait", 119, 24, False),
    ]:
        x, y = map(lambda v: int(round(float(v))), PROJ.pixels(lon, lat))
        inside = 0 <= x < 4993 and 0 <= y < 3583
        check(inside, "Reference place outside map: " + label)
        if inside:
            check(bool(mask[y, x] > 127) == expected_land, "Reference place sea/land mismatch: " + label)
    with ZipFile(source) as archive:
        standard = archive.read("assets/map/map1_1.pvr")
        # The original's first RGBA4444 word is the blue open-ocean color.
        check(standard[52:54] == struct.pack("<H", 0x468F), "Original palette assumption")
    report = {"checks": checks, "errors": len(errors), "details": errors,
              "runtime_tested": False, "asymmetric_river_edges": asymmetric}
    (OUT / "verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=True))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
