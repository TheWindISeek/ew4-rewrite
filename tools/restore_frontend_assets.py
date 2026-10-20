"""Refresh only missing UI caches; preserve the original assets and map caches."""
import hashlib
import json
import struct
import shutil
import sys
from pathlib import Path
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from PIL import Image
import texture2ddecoder
from ew4_formats import BitmapFont

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "apk_work/original/assets"
OUTPUT = ROOT / "rewrite/data/client"


def main():
    manifest_path = OUTPUT / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    sources, count = {}, 0
    for name in ("princesses_hd", "princesses_sketch_hd"):
        texture = SOURCE / (name + (".png" if "sketch" in name else ".pkm"))
        data = texture.read_bytes()
        if texture.suffix == ".pkm":
            ew, eh, width, height = struct.unpack_from(">4H", data, 8)
            if data[:6] != b"PKM 10" or len(data) != 16 + ew * eh // 2:
                raise ValueError("Invalid original princess texture")
            decoded = texture2ddecoder.decode_etc1(data[16:], ew, eh)
            bitmap = Image.frombytes("RGBA", (ew, eh), decoded, "raw", "BGRA").crop((0, 0, width, height))
        else:
            bitmap = Image.open(texture).convert("RGBA")
        index = SOURCE / (name + ".xml")
        atlas = ET.fromstring("<atlas>" + index.read_text(encoding="utf-8-sig") + "</atlas>")
        sprites = {e.attrib["name"]: {k: float(v) for k, v in e.attrib.items() if k != "name"} for e in atlas.findall("Images/Image")}
        folder = OUTPUT / "ui" / name
        folder.mkdir(parents=True, exist_ok=True)
        for key, s in sprites.items():
            bitmap.crop((int(s["x"]), int(s["y"]), int(s["x"] + s["w"]), int(s["y"] + s["h"]))).save(folder / key)
            count += 1
        manifest["atlases"][name] = sprites
        sources.update({p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (texture, index)})
    font_path = SOURCE / "font_city_hd.fnt"
    font = BitmapFont.from_bytes(font_path.read_bytes())
    manifest["fonts"]["font_city_hd"] = {"height": font.line_height, "glyphs": {str(r[0]): list(r[1:]) for r in font.records}}
    shutil.copyfile(SOURCE / "font_city_hd.png", OUTPUT / "images/font_city_hd.png")
    sources.update({p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (font_path, SOURCE / "font_city_hd.png")})
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    report = {"sprites": count, "sources": sources, "errors": 0}
    (ROOT / "rewrite/reports/frontend_asset_refresh.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"sprites": count, "sources": len(sources), "errors": 0}))


if __name__ == "__main__":
    main()
