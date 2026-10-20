"""Verify and integrate East Asia resources, with an optional nonhistorical map probe."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image, ImageDraw
from build_city_font import read_font

ROOT = Path(__file__).resolve().parents[1]
GAME = ROOT / "apk_work/game"
ORIGINAL = ROOT / "apk_work/original"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def make_probe(folder):
    source = (ORIGINAL / "assets/conquest1.btl").read_bytes()
    header = list(struct.unpack_from("<18i", source))
    assert header[0] == 2
    legion_count = header[6]
    legion_end = 72 + 180 * legion_count
    build_start = legion_end + header[4] * header[5]
    army_start = build_start + header[7] * 16
    end = army_start + header[8] * 32 + header[16] * 8 + header[10] * 116 + header[14] * 108 + header[9] * 44
    assert end == len(source), "Source BTL layout not understood"
    legions = source[72:legion_end]
    buildings = [source[build_start + i * 16:build_start + (i + 1) * 16] for i in range(header[7])]
    army = next(source[army_start + i * 32:army_start + (i + 1) * 32]
                for i in range(header[8]) if source[army_start + i * 32 + 2] == 0)
    cells = np.frombuffer((folder / "assets/europe.bin").read_bytes()[16:], dtype=np.uint8).reshape(68, 79, 12)
    fractions = np.load(folder / "cell_land_fraction.npy")
    land_mask = np.asarray(Image.open(folder / "eastasia_land_mask.png")) >= 128
    owners = np.where(cells[:, :, 0] == 1, 255, 0).astype(np.uint8)
    centres = [(c, r, c * 64 + 32 * (r % 2), r * 54)
               for r in range(68) for c in range(79)
               if cells[r, c, 0] != 1 and fractions[r, c] >= .6
               and 12 <= c * 64 + 32 * (r % 2) < land_mask.shape[1] - 12
               and 12 <= r * 54 < land_mask.shape[0] - 12
               and land_mask[r * 54 - 12:r * 54 + 13,
                             c * 64 + 32 * (r % 2) - 12:c * 64 + 32 * (r % 2) + 13].mean() >= .95]
    places = json.loads((folder / "place_coordinates.json").read_text(encoding="utf-8"))
    assert len(places) == legion_count
    used = set()
    placements, city_data, army_data = [], [], []
    names = ET.Element("areanamelist")
    for owner, (name, place) in enumerate(places.items()):
        x, y = place["pixel"]
        col, row, cx, cy = min((p for p in centres if p[:2] not in used),
                               key=lambda p: (p[2] - x) ** 2 + (p[3] - y) ** 2)
        used.add((col, row))
        # Small ownership patches make every original country slot selectable for inspection.
        for r in range(max(0, row - 2), min(68, row + 3)):
            for c in range(max(0, col - 2), min(79, col + 3)):
                if cells[r, c, 0] != 1:
                    owners[r, c] = owner
        index = row * 79 + col
        template = next((b for b in buildings if b[4] == 0 and b[8] != 255 and source[legion_end + struct.unpack_from('<h', b)[0]] == owner),
                        next(b for b in buildings if b[4] == 0 and b[8] != 255))
        city = bytearray(template)
        struct.pack_into("<hh", city, 0, index, owner + 1)
        city[6] = 0
        city[11] = 255
        unit = bytearray(army)
        struct.pack_into("<h", unit, 0, index)
        struct.pack_into("<h", unit, 16, -1)
        unit[18], unit[23], unit[26] = 0, 0, 255
        city_data.append(city)
        army_data.append(unit)
        ET.SubElement(names, "areaname", id=str(owner + 1), name=name.upper())
        placements.append({"place": name, "original_country_slot": owner,
                           "col": col, "row": row, "pixel": [cx, cy],
                           "land_fraction": float(fractions[row, col]),
                           "placement_error_pixels": round(((cx - x) ** 2 + (cy - y) ** 2) ** .5, 2)})
    # Restore the exact owner at each marker in case ownership patches overlap.
    for placement in placements:
        owners[placement["row"], placement["col"]] = placement["original_country_slot"]
    for suffix in ("", "_hd"):
        _, glyphs = read_font((folder / f"assets/font_city{suffix}.fnt").read_bytes())
        supported = {chr(r[0]) for r in glyphs}
        assert all(set(item.get("name")) <= supported for item in names), "City label has unsupported characters"
    header[2:6] = [0, 0, 79, 68]
    header[7:11] = [len(city_data), len(army_data), 0, 0]
    header[14], header[16] = 0, 0
    data = struct.pack("<18i", *header) + legions + owners.tobytes() + b"".join(city_data) + b"".join(army_data)
    assert len(data) == 72 + 180 * legion_count + 79 * 68 + 16 * len(city_data) + 32 * len(army_data)
    output = folder / "probe_assets/assets"
    output.mkdir(parents=True, exist_ok=True)
    (output / "conquest1.btl").write_bytes(data)
    ET.ElementTree(names).write(output / "def_areaname.xml", encoding="utf-8", xml_declaration=True)
    (folder / "probe_placements.json").write_text(json.dumps(placements, indent=2) + "\n", encoding="utf-8")
    preview = Image.open(folder / "eastasia_base.png").convert("RGB")
    draw = ImageDraw.Draw(preview)
    for p in placements:
        x, y = p["pixel"]
        draw.ellipse((x - 12, y - 12, x + 12, y + 12), outline="red", width=3)
        draw.text((x + 16, y), p["place"].upper(), fill="black", stroke_width=2, stroke_fill="white")
    preview.crop((2950, 2100, 3550, 2500)).save(folder / "shanghai_placement_check.png")
    return [output / "conquest1.btl", output / "def_areaname.xml"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map", type=Path, default=ROOT / "maps/eastasia_v5")
    parser.add_argument("--probe", action="store_true")
    args = parser.parse_args()
    folder = args.map.resolve()
    if not folder.is_relative_to(ROOT / "maps"):
        raise ValueError("Map must be within project maps directory")
    verification = json.loads((folder / "verification.json").read_text(encoding="utf-8"))
    if verification["errors"]:
        raise ValueError("Map verification failed")
    resources = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    pending = []
    for resource in resources:
        path = (folder / resource["path"]).resolve()
        if not path.is_relative_to(folder / "assets") or sha(path.read_bytes()) != resource["sha256"]:
            raise ValueError("Map inventory path or hash mismatch")
        pending.append((path, resource["path"]))
    if args.probe:
        pending.extend((p, f"assets/{p.name}") for p in make_probe(folder))
    report_path = ROOT / "apk_work/map_integration.json"
    previous = json.loads(report_path.read_text(encoding="utf-8")) if report_path.exists() else {"files": []}
    previous_hashes = {r["path"]: r["sha256"] for r in previous["files"]}
    for path, name in pending:
        destination = GAME / name
        if not destination.is_file() or destination.is_symlink():
            raise ValueError("Unexpected destination: " + name)
        existing = sha(destination.read_bytes())
        if existing not in {sha((ORIGINAL / name).read_bytes()), sha(path.read_bytes()), previous_hashes.get(name)}:
            raise ValueError("Unrecognized working-tree edit; refusing to overwrite: " + name)
    files = []
    for path, name in pending:
        destination = GAME / name
        shutil.copyfile(path, destination)
        files.append({"path": name, "sha256": sha(destination.read_bytes())})
    report = {"map": str(folder.relative_to(ROOT)), "nonhistorical_probe": args.probe,
              "files": files, "native_libraries_modified": False, "runtime_tested": False}
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Integrated {len(files)} verified resources; probe={args.probe}; native code unchanged")


if __name__ == "__main__":
    main()
