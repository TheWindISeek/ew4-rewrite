"""Build offline WOFF2 fonts and an exact Unicode coverage index, without subsetting."""

import hashlib
import json
from pathlib import Path
import sys
from zipfile import ZipFile

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / "rewrite/local/font-sources-20261020"
OUTPUT = ROOT / "rewrite/data/client/fonts"


def ranges(points):
    result = []
    for point in sorted(points):
        if result and point == result[-1][1] + 1:
            result[-1][1] = point
        else:
            result.append([point, point])
    return result


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with ZipFile(SOURCES / "Jigmo-20250912.zip") as archive:
        for name in ("Jigmo.ttf", "Jigmo2.ttf", "Jigmo3.ttf", "LICENSE.txt"):
            matches = [n for n in archive.namelist() if Path(n).name == name]
            if len(matches) != 1:
                raise ValueError(f"Jigmo source is missing {name}")
            destination = OUTPUT / "Jigmo-LICENSE.txt" if name == "LICENSE.txt" else SOURCES / name
            destination.write_bytes(archive.read(matches[0]))

    recipes = [
        ("NotoSerifSC-Variable.ttf", "NotoSerifSC-Regular.woff2", "EW4 Serif", "A", "https://github.com/google/fonts/tree/main/ofl/notoserifsc"),
        ("Jigmo.ttf", "Jigmo.woff2", "EW4 Han", "\u4e00", "https://kamichikoichi.github.io/jigmo/Jigmo-20250912.zip"),
        ("Jigmo2.ttf", "Jigmo2.woff2", "EW4 Han Plane 2", "\U00020000", "https://kamichikoichi.github.io/jigmo/Jigmo-20250912.zip"),
        ("Jigmo3.ttf", "Jigmo3.woff2", "EW4 Han Plane 3", "\U00030000", "https://kamichikoichi.github.io/jigmo/Jigmo-20250912.zip"),
        ("unifont-17.0.05.otf", "Unifont.woff2", "EW4 Unicode", "A", "https://unifoundry.com/pub/unifont/unifont-17.0.05/"),
        ("unifont_upper-17.0.05.otf", "UnifontUpper.woff2", "EW4 Unicode Supplement", "\U00010000", "https://unifoundry.com/pub/unifont/unifont-17.0.05/"),
    ]
    all_points = set()
    faces = []
    for source_name, filename, family, probe, upstream in recipes:
        source = SOURCES / source_name
        destination = OUTPUT / filename
        source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        cached = SOURCES / (filename + ".source.json")
        if cached.exists() and destination.exists() and json.loads(cached.read_text())["sha256"] == source_hash:
            font = TTFont(destination)
        else:
            font = TTFont(source)
            if "fvar" in font:
                font = instantiateVariableFont(font, {"wght": 400}, inplace=True)
            points_before = set(font.getBestCmap())
            font.flavor = "woff2"
            font.save(destination)
            font.close()
            font = TTFont(destination)
            assert set(font.getBestCmap()) == points_before, "Compression must not drop characters"
            cached.write_text(json.dumps({"sha256": source_hash}) + "\n", encoding="utf-8")
        points = {point for point, glyph in font.getBestCmap().items() if glyph != ".notdef"}
        assert ord(probe) in points, family
        all_points.update(points)
        faces.append(dict(file=filename, family=family, probe=probe, glyphs=len(points),
                          bytes=destination.stat().st_size, sha256=hashlib.sha256(destination.read_bytes()).hexdigest(),
                          sourceSha256=source_hash, upstream=upstream))
        font.close()
        print(f"{filename}: {len(points)} characters, {destination.stat().st_size} bytes", flush=True)
    assert 0xFFFD in all_points
    manifest = dict(schema=1, unicodeVersion="17.0", faces=faces, characters=len(all_points), ranges=ranges(all_points))
    (OUTPUT / "coverage.json").write_text(json.dumps(manifest, ensure_ascii=True, indent=2) + "\n", encoding="utf-8", newline="\n")
    index_file = ROOT / "rewrite/data/runtime-content.json"
    index = json.loads(index_file.read_text(encoding="utf-8"))
    font_position = next((i for i, entry in enumerate(index["files"]) if entry["path"].startswith("client/fonts/")), len(index["files"]))
    index["files"] = [entry for entry in index["files"] if not entry["path"].startswith("client/fonts/")]
    font_entries = []
    for file in sorted(OUTPUT.iterdir()):
        if file.name == "README.md" or not file.is_file():
            continue
        font_entries.append(dict(path="client/fonts/" + file.name, bytes=file.stat().st_size,
                                   sha256=hashlib.sha256(file.read_bytes()).hexdigest()))
    index["files"][font_position:font_position] = font_entries
    index_file.write_text(json.dumps(index, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps(dict(characters=len(all_points), bytes=sum(f["bytes"] for f in faces), faces=len(faces))), flush=True)


if __name__ == "__main__":
    main()
