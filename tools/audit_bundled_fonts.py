"""Audit the exact shipped cmap against built-in text and local legacy Mod text."""

import hashlib
import json
from pathlib import Path
import sys
import unicodedata
from zipfile import ZipFile

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[1]
FONTS = ROOT / "rewrite/data/client/fonts"
REPORT = ROOT / "rewrite/reports/bundled-fonts-20261020/coverage-audit.json"


def decode(data):
    if data.startswith((b"\xff\xfe", b"\xfe\xff")):
        return data.decode("utf-16")
    if b"\0" in data[:100]:
        return data.decode("utf-16-le")
    for encoding in ("utf-8-sig", "gb18030"):
        try:
            return data.decode(encoding)
        except UnicodeDecodeError:
            pass
    raise ValueError("Unrecognized text encoding")


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, list):
        for item in value:
            yield from strings(item)
    elif isinstance(value, dict):
        for key, item in value.items():
            yield key
            yield from strings(item)


def main():
    coverage = json.loads((FONTS / "coverage.json").read_text())
    points = set()
    for face in coverage["faces"]:
        file = FONTS / face["file"]
        assert hashlib.sha256(file.read_bytes()).hexdigest() == face["sha256"], file
        font = TTFont(file)
        cmap = {p for p, glyph in font.getBestCmap().items() if glyph != ".notdef"}
        assert len(cmap) == face["glyphs"], file
        points.update(cmap)
        font.close()
    indexed = {p for start, end in coverage["ranges"] for p in range(start, end + 1)}
    assert points == indexed and len(points) == coverage["characters"]
    sources = {}

    def check(name, entries):
        chars, count = set(), 0
        for text in entries:
            chars.update(ord(c) for c in unicodedata.normalize("NFC", text) if not unicodedata.category(c).startswith("C"))
            count += 1
        missing = sorted(chars - points)
        sources[name] = dict(textEntries=count, characters=len(chars), missing=[f"U+{p:04X}" for p in missing])

    builtin = []
    for directory in (ROOT / "rewrite/data/original", ROOT / "rewrite/Ew4.Web/wwwroot"):
        for file in directory.rglob("*"):
            if file.suffix.lower() in (".xml", ".ini", ".json", ".html", ".js"):
                text = decode(file.read_bytes())
                builtin.extend(strings(json.loads(text)) if file.suffix == ".json" else [text])
    check("builtin-and-frontend", builtin)
    for apk in sorted(ROOT.iterdir()):
        if apk.suffix.lower() != ".apk":
            continue
        with ZipFile(apk) as archive:
            texts = [decode(archive.read(name)) for name in archive.namelist()
                     if name.startswith("assets/") and Path(name).suffix.lower() in (".xml", ".ini")]
        check(apk.name, texts)
    probes = "简体繁體龘靐齉𠀀𠮷𰀀𱍐𲎰英語 Français Ελληνικά Русский العربية 日本語 한국어 ★→0123456789"
    check("multilingual-and-rare-Han-probes", [probes])
    report = dict(characters=len(points), fontBytes=sum(f["bytes"] for f in coverage["faces"]),
                  sources=sources, errors=sum(bool(s["missing"]) for s in sources.values()),
                  scope="Exact shipped cmaps; built-in and six local APK XML/INI texts; NFC; control/private-use characters excluded")
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=True))
    assert report["errors"] == 0, "Inspect missing code points before shipping"


if __name__ == "__main__":
    main()
