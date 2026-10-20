"""Normalize original BILE graphs and atlas manifests for a new renderer."""

import json
from pathlib import Path
import xml.etree.ElementTree as ET

from ew4_state_formats import BileLibrary

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "apk_work/original/assets"


def main():
    counts = {"libraries": 0, "items": 0, "atlas_manifests": 0, "sprites": 0, "layers": 0}
    warnings = []
    output = ROOT / "rewrite/data/legacy/animations"
    output.mkdir(parents=True, exist_ok=True)
    for path in sorted(ASSETS.glob("*.bin")):
        data = path.read_bytes()
        if data[:4] != b"BILE":
            continue
        normalized = BileLibrary.from_bytes(data).normalized()
        atlas = ASSETS / (path.stem + ".xml")
        if atlas.exists():
            # Easytech atlas files are XML fragments with multiple root elements.
            fragment = atlas.read_text(encoding="utf-8-sig")
            root = ET.fromstring("<atlas>" + fragment + "</atlas>")
            textures = [t.attrib for t in root.findall("Texture")]
            sprites = [image.attrib for image in root.findall("Images/Image")]
            for texture in textures:
                declared = ASSETS / texture["name"]
                resolved = declared if declared.is_file() else declared.with_suffix(".png")
                if not resolved.is_file():
                    raise ValueError("Missing atlas texture " + texture["name"])
                texture["resolved_asset"] = str(resolved.relative_to(ROOT)).replace("\\", "/")
                texture["resolution"] = "declared" if resolved == declared else "matching_png_asset"
            normalized["atlas"] = {"source": str(atlas.relative_to(ROOT)).replace("\\", "/"),
                                   "textures": textures, "sprites": sprites}
            sprite_names = {s["name"] for s in sprites}
            referenced = {pair[1] for i in normalized["items"] if i["kind"] == 0
                          for layer in i["layers"] for frame in layer for pair in frame["indices"] if pair[1] >= 0}
            for index, item in enumerate(normalized["items"]):
                if item["kind"] and item["name"] + ".png" not in sprite_names:
                    warnings.append({"library": path.stem, "item": item["name"], "id": index,
                                     "referenced_by_composite": index in referenced, "issue": "unresolved_sprite_name"})
                if item["kind"] == 0:
                    for layer in item["layers"]:
                        keys = [f["key"] for f in layer]
                        if not keys or keys[0] != 0 or keys != sorted(set(keys)) or keys[-1] >= item["duration"]:
                            raise ValueError(f"Invalid layer keys: {path.name}/{item['name']}")
                        counts["layers"] += 1
            counts["atlas_manifests"] += 1
            counts["sprites"] += len(sprites)
        encoded = json.dumps(normalized, ensure_ascii=False, separators=(",", ":")) + "\n"
        target = output / (path.stem + ".json")
        if not target.exists() or target.read_text(encoding="utf-8") != encoded:
            target.write_text(encoded, encoding="utf-8")
        counts["libraries"] += 1
        counts["items"] += len(normalized["items"])
    report_dir = ROOT / "rewrite/reports"
    report_dir.mkdir(parents=True, exist_ok=True)
    (report_dir / "legacy_animation_import.json").write_text(json.dumps({"counts": counts, "warnings": warnings, "errors": []},
        ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"counts": counts, "warnings": len(warnings), "referenced_warnings": sum(w["referenced_by_composite"] for w in warnings), "errors": 0}))


if __name__ == "__main__":
    main()
