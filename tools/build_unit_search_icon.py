"""Keep the undo button's square gray background and draw a white location symbol."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
source = Image.open(root / "rewrite/data/client/ui/image_recruit_hd/button_return.png").convert("RGBA")
factor = 4
# Remove the return arrow, interpolating the original gray gradient between clean columns.
# All surrounding pixels, including the square border and transparent margin, stay intact.
button = source.copy()
for y in range(28, 65):
    left, right = source.getpixel((3, y)), source.getpixel((51, y))
    for x in range(4, 51):
        t = (x - 3) / 48
        button.putpixel((x, y), tuple(round(a + (b - a) * t) for a, b in zip(left, right)))
symbol = Image.new("RGBA", (source.width * factor, source.height * factor))
draw = ImageDraw.Draw(symbol)
cx, cy = 27, 46

def box(x0, y0, x1, y1):
    return tuple(round(v * factor) for v in (x0, y0, x1, y1))

ink = (230, 230, 230, 255)
draw.ellipse(box(cx - 10, cy - 10, cx + 10, cy + 10), outline=ink, width=3 * factor)
for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
    draw.line(box(cx + dx * 7, cy + dy * 7, cx + dx * 16, cy + dy * 16), fill=ink, width=3 * factor)
draw.ellipse(box(cx - 3, cy - 3, cx + 3, cy + 3), fill=ink)
button = Image.alpha_composite(button, symbol.resize(source.size, Image.Resampling.LANCZOS))
button.save(root / "rewrite/Ew4.Web/wwwroot/unit-search.png")
print(f"Original-style unit search icon: {source.width}x{source.height}")
