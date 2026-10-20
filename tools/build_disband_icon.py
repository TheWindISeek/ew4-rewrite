"""Derive the disband tool from the original round info button and close mark."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

root = Path(__file__).resolve().parents[1]
button = Image.open(root / "rewrite/data/client/ui/image_recruit_hd/button_info.png").convert("RGBA")
close = Image.open(root / "rewrite/data/client/ui/image_ui_hd/button_close.png").convert("RGBA")
center = (button.width - 1) / 2
draw = ImageDraw.Draw(button)
fill = button.getpixel((18, int(center)))
draw.ellipse((center - 21, center - 21, center + 21, center + 21), fill=fill)
mask = close.getchannel("A")
mask.putdata([a if min(r, g, b) > 190 else 0 for r, g, b, a in close.get_flattened_data()])
mask = mask.crop(mask.getbbox()).resize((25, 25), Image.Resampling.LANCZOS).filter(ImageFilter.MaxFilter(3))
mark = Image.new("RGBA", mask.size, (128, 128, 128, 255))
button.paste(mark, ((button.width - mask.width) // 2, (button.height - mask.height) // 2), mask)
button.save(root / "rewrite/Ew4.Web/wwwroot/disband.png")
print(f"Original-style disband icon: {button.width}x{button.height}")
