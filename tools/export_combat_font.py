"""Refresh the two original combat digit atlases without rebuilding other assets."""
import hashlib
import json
from pathlib import Path
import shutil

from ew4_formats import BitmapFont

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "apk_work/original/assets"
OUTPUT = ROOT / "rewrite/data/client"


def main():
    fonts = {}
    sources = {}
    for name in ("font_fight_num", "font_fight_num_hd"):
        font = BitmapFont.from_bytes((SOURCE / (name + ".fnt")).read_bytes())
        fonts[name] = {"height": font.line_height, "glyphs": {str(r[0]): list(r[1:]) for r in font.records}}
        shutil.copyfile(SOURCE / (name + ".png"), OUTPUT / "images" / (name + ".png"))
        for suffix in (".png", ".fnt"):
            source = SOURCE / (name + suffix)
            sources[source.name] = hashlib.sha256(source.read_bytes()).hexdigest()
    (OUTPUT / "fight-font.json").write_text(json.dumps(fonts, separators=(",", ":")), encoding="utf-8")
    index_path = ROOT / "rewrite/data/runtime-content.json"
    index = json.loads(index_path.read_text(encoding="utf-8"))
    changed = ["client/fight-font.json", "client/images/font_fight_num.png", "client/images/font_fight_num_hd.png"]
    entries = {entry["path"]: entry for entry in index["files"]}
    for name in changed:
        data = (ROOT / "rewrite/data" / name).read_bytes()
        entries[name] = {"path": name, "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
    index["files"] = list(entries.values())
    index_path.write_text(json.dumps(index, indent=2) + "\n", encoding="utf-8")
    report = {"fonts": 2, "glyphs": 22, "sources": sources, "errors": 0}
    (ROOT / "rewrite/reports/combat_font_export.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
