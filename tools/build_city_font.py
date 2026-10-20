"""Extend the original city bitmap fonts with an apostrophe, preserving A-Z."""

from pathlib import Path
import struct

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
RECORD = struct.Struct("<3H2B2bH")


def read_font(data):
    count, height = struct.unpack_from("<2i", data)
    if len(data) != 8 + count * RECORD.size:
        raise ValueError("Unexpected city font layout")
    return height, [RECORD.unpack_from(data, 8 + i * RECORD.size) for i in range(count)]


def build_city_fonts(assets, output):
    for suffix, scale in [("", 1), ("_hd", 2)]:
        name = f"font_city{suffix}"
        original = ROOT / "apk_work/original/assets"
        height, records = read_font((original / f"{name}.fnt").read_bytes())
        source = Image.open(original / f"{name}.png").convert("RGBA")
        atlas = Image.new("RGBA", (source.width, source.height * 2))
        atlas.paste(source, (0, 0))
        x, y = 2 * scale, source.height + 2 * scale
        glyph = Image.new("RGBA", (7 * scale, 8 * scale))
        draw = ImageDraw.Draw(glyph)
        points = [(2, 0), (5, 0), (5, 3), (3, 6), (1, 6), (2, 3)]
        draw.polygon([(px * scale, py * scale) for px, py in points],
                     fill="white", outline="black", width=scale)
        atlas.paste(glyph, (x, y))
        records.append((39, x, y, glyph.width, glyph.height, -scale, -2 * scale, 5 * scale))
        records.sort(key=lambda record: record[0])
        data = struct.pack("<2i", len(records), height) + b"".join(RECORD.pack(*r) for r in records)
        (assets / f"{name}.fnt").write_bytes(data)
        atlas.save(assets / f"{name}.png")
        sample = Image.new("RGBA", (100 * scale, 30 * scale), (68, 102, 136, 255))
        cursor = 8 * scale
        lookup = {chr(r[0]): r for r in records}
        for char in "XI'AN":
            _, sx, sy, w, h, dx, dy, advance = lookup[char]
            sample.alpha_composite(atlas.crop((sx, sy, sx + w, sy + h)),
                                   (cursor + dx, 8 * scale + dy))
            cursor += advance
        sample.resize((400, 120), Image.Resampling.NEAREST).save(output / f"xian_font_check{suffix}.png")
