"""Build a shared outlined city atlas without changing the original fonts."""
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "rewrite/data/client"
NAME = "font_city_labels_hd"


def main():
    path = OUTPUT / "manifest.json"
    manifest = json.loads(path.read_text(encoding="utf-8"))
    names = json.loads((ROOT / "rewrite/Ew4.Web/wwwroot/city-names.json").read_text(encoding="utf-8"))
    original = manifest["fonts"]["font_city_hd"]
    source = Image.open(ROOT / "apk_work/original/assets/font_city_hd.png").convert("RGBA")
    typeface_path = OUTPUT / "fonts/LXGWWenKai-Regular.ttf"
    typeface = ImageFont.truetype(str(typeface_path), 26 * 4)
    chars = sorted(set("".join(names.keys()) + "".join(names.values()) + " .'-"))
    atlas = Image.new("RGBA", (512, ((len(chars) + 15) // 16) * 40))
    glyphs = {}
    for index, char in enumerate(chars):
        x, y = index % 16 * 32, index // 16 * 40
        record = original["glyphs"].get(str(ord(char)))
        if record:
            sx, sy, w, h, dx, dy, advance = record
            glyph = source.crop((sx, sy, sx + w, sy + h))
        elif char.isspace():
            glyph = Image.new("RGBA", (1, 1));w, h, dx, dy, advance = 1, 1, 0, 0, 10
        else:
            mask = Image.new("L", (144, 160))
            draw = ImageDraw.Draw(mask)
            draw.text((16, 0), char, font=typeface, fill=255, anchor="lt")
            bounds = mask.getbbox()
            if bounds is None:
                raise ValueError("Missing city glyph: " + char)
            mask = mask.crop(bounds)
            # All CJK glyphs use the same typeface and cap height; no runtime fallback.
            ratio = min(22 * 4 / mask.height, 23 * 4 / mask.width)
            mask = mask.resize((max(1, round(mask.width * ratio)), max(1, round(mask.height * ratio))), Image.Resampling.LANCZOS)
            padded = Image.new("L", (mask.width + 12, mask.height + 12));padded.paste(mask, (6, 6))
            from PIL import ImageFilter
            edge = padded.filter(ImageFilter.MaxFilter(13))
            glyph = Image.new("RGBA", padded.size, "black");glyph.putalpha(edge)
            white = Image.new("RGBA", padded.size, "white");white.putalpha(padded);glyph.alpha_composite(white)
            glyph = glyph.resize((round(glyph.width / 4), round(glyph.height / 4)), Image.Resampling.LANCZOS)
            w, h = glyph.size;dx, dy, advance = 0, -3, max(w + 1, 25)
        atlas.alpha_composite(glyph, (x, y))
        glyphs[str(ord(char))] = [x, y, w, h, dx, dy, advance]
    atlas_path = OUTPUT / "images" / (NAME + ".png")
    atlas.save(atlas_path)
    manifest["fonts"][NAME] = dict(height=26, preserveColor=True, glyphs=glyphs)
    path.write_text(json.dumps(manifest, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    report = dict(glyphs=len(glyphs), names=len(names), errors=0,
                  atlasSha256=hashlib.sha256(atlas_path.read_bytes()).hexdigest(),
                  typefaceSha256=hashlib.sha256(typeface_path.read_bytes()).hexdigest(),
                  scope="Original English glyph pixels; consistent WenKai Chinese ink, black outline, complete city coverage.")
    (ROOT / "rewrite/reports/frontend_refinement_20261019/city-font.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
