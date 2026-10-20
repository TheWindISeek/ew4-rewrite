"""Build a separate EW4 East Asia map resource prototype; never alter input APKs."""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import io
import json
import math
from pathlib import Path
import struct
import sys
import urllib.request
import xml.etree.ElementTree as ET
from zipfile import ZIP_DEFLATED, ZipFile

DEPENDENCIES = Path.home() / ".codex/tools/ew4-inspect-deps"
sys.path.insert(0, str(DEPENDENCIES))

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import shapefile
from shapely import transform as transform_geometry
from shapely.geometry import LineString, Point, box, shape
from shapely.ops import unary_union

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "maps/eastasia_v5"
CACHE = ROOT / "maps/source_cache"
WIDTH, HEIGHT = 4993, 3583
GRID_W, GRID_H = 79, 68
SEA = (68, 102, 136)
SOURCES = {
    "land": "https://naturalearth.s3.amazonaws.com/10m_physical/ne_10m_land.zip",
    "rivers": "https://naturalearth.s3.amazonaws.com/10m_physical/ne_10m_rivers_lake_centerlines.zip",
    "lakes": "https://naturalearth.s3.amazonaws.com/10m_physical/ne_10m_lakes.zip",
}
RIVER_WORDS = (
    "yangtze", "chang jiang", "huang", "yellow", "amur", "heilong", "sungari",
    "songhua", "ussuri", "mekong", "lancang", "salween", "nu jiang", "irrawaddy",
    "liao", "pearl", "xi jiang", "xijiang", "min jiang", "han-shui", "han shui",
    "yalu", "umen", "huai", "wei he", "brahmaputra", "tsangpo",
    "tone", "ishikari", "shinano", "nakdong", "han-gang",
)
EXACT_RIVERS = {"xi", "bei", "dong", "min", "han", "wei", "fen", "hai", "yongding"}
CITIES = {
    "Beijing": (116.40, 39.90), "Shanghai": (121.47, 31.23),
    "Nanjing": (118.80, 32.06), "Wuhan": (114.31, 30.59),
    "Guangzhou": (113.26, 23.13), "Chengdu": (104.07, 30.57),
    "Chongqing": (106.55, 29.56), "Kunming": (102.83, 24.88),
    "Xi'an": (108.94, 34.34), "Shenyang": (123.43, 41.80),
    "Harbin": (126.64, 45.76), "Urumqi": (87.62, 43.83),
    "Lhasa": (91.14, 29.65), "Taipei": (121.57, 25.03),
    "Haikou": (110.20, 20.04), "Seoul": (126.98, 37.57),
    "Tokyo": (139.69, 35.69), "Ulaanbaatar": (106.91, 47.92),
    "Vladivostok": (131.89, 43.12),
}


def log(message):
    print(message, flush=True)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def fetch_source(item):
    name, url = item
    path = CACHE / (url.rsplit("/", 1)[1])
    if not path.exists():
        request = urllib.request.Request(url, headers={"User-Agent": "EW4-EastAsia-map-research/1.0"})
        with urllib.request.urlopen(request, timeout=90) as response:
            data = response.read(64 * 1024 * 1024 + 1)
        if len(data) > 64 * 1024 * 1024:
            raise ValueError("Unexpected source size")
        with ZipFile(io.BytesIO(data)) as archive:
            if not any(n.endswith(".shp") for n in archive.namelist()):
                raise ValueError("Source does not contain a shapefile")
        path.write_bytes(data)
    with ZipFile(path) as archive:
        error = archive.testzip()
        if error:
            raise ValueError(f"Corrupt source member: {error}")
    return {"kind": name, "url": url, "path": str(path.relative_to(ROOT)),
            "sha256": sha(path.read_bytes()), "license": "Natural Earth public domain"}


class Projection:
    """Spherical Lambert conformal conic, with one scale for both axes."""

    def __init__(self):
        p1, p2, self.lon0, p0 = map(math.radians, (25, 47, 110, 35))
        self.n = math.log(math.cos(p1) / math.cos(p2)) / math.log(
            math.tan(math.pi / 4 + p2 / 2) / math.tan(math.pi / 4 + p1 / 2))
        self.f = math.cos(p1) * math.tan(math.pi / 4 + p1 / 2) ** self.n / self.n
        self.radius = 6371008.8
        self.rho0 = self.radius * self.f / math.tan(math.pi / 4 + p0 / 2) ** self.n
        self.xmin, self.xmax = -3_350_000, 3_250_000
        self.ymin = -2_050_000
        self.ymax = self.ymin + (self.xmax - self.xmin) * HEIGHT / WIDTH

    def xy(self, lon, lat, z=None):
        lon, lat = np.radians(lon), np.radians(lat)
        rho = self.radius * self.f / np.tan(np.pi / 4 + lat / 2) ** self.n
        theta = self.n * (lon - self.lon0)
        return rho * np.sin(theta), self.rho0 - rho * np.cos(theta)

    def pixels(self, lon, lat):
        x, y = self.xy(lon, lat)
        return ((x - self.xmin) * WIDTH / (self.xmax - self.xmin),
                (self.ymax - y) * HEIGHT / (self.ymax - self.ymin))


PROJ = Projection()
CLIP = box(PROJ.xmin, PROJ.ymin, PROJ.xmax, PROJ.ymax)
TEXTURE_STEP = 62
PADDED_SIZE = (math.ceil(WIDTH / TEXTURE_STEP) * TEXTURE_STEP + 2,
               math.ceil(HEIGHT / TEXTURE_STEP) * TEXTURE_STEP + 2)
TERRAIN_MARGIN = 36
TERRAIN_MASK_SIZE = (GRID_W * 64 + 2 * TERRAIN_MARGIN,
                     (GRID_H - 1) * 54 + 2 * TERRAIN_MARGIN + 1)
METRES_PER_PIXEL = (PROJ.xmax - PROJ.xmin) / WIDTH
# Include a filter guard outside the visible atlas margin.
EDGE_CLIP = box(PROJ.xmin - 52 * METRES_PER_PIXEL,
                PROJ.ymax - (TERRAIN_MASK_SIZE[1] - TERRAIN_MARGIN + 16) * METRES_PER_PIXEL,
                PROJ.xmin + (TERRAIN_MASK_SIZE[0] - TERRAIN_MARGIN + 16) * METRES_PER_PIXEL,
                PROJ.ymax + 52 * METRES_PER_PIXEL)


def read_shapes(source, clip=CLIP):
    with ZipFile(CACHE / SOURCES[source].rsplit("/", 1)[1]) as archive:
        names = archive.namelist()
        shp_name = next(n for n in names if n.endswith(".shp"))
        stem = shp_name[:-4]
        reader = shapefile.Reader(
            shp=io.BytesIO(archive.read(stem + ".shp")),
            shx=io.BytesIO(archive.read(stem + ".shx")),
            dbf=io.BytesIO(archive.read(stem + ".dbf")),
            encoding="utf-8", encodingErrors="replace")
        for item in reader.iterShapeRecords():
            bounds = item.shape.bbox
            if bounds[2] < 58 or bounds[0] > 158 or bounds[3] < 9 or bounds[1] > 66:
                continue
            attrs = item.record.as_dict()
            geom = shape(item.shape.__geo_interface__)
            if not geom.is_valid:
                geom = geom.buffer(0)
            # Cut before projection to avoid dateline-spanning world polygons.
            geom = geom.intersection(box(58, 9, 158, 66))
            if geom.is_empty:
                continue
            projected = transform_geometry(geom, PROJ.xy, interleaved=False).intersection(clip)
            if not projected.is_empty:
                yield projected, attrs


def geometries(geometry, kind):
    if geometry.is_empty:
        return
    if geometry.geom_type == kind:
        yield geometry
    elif hasattr(geometry, "geoms"):
        for part in geometry.geoms:
            yield from geometries(part, kind)


def pixel_coords(coords, scale=1, margin=0):
    return [(((x - PROJ.xmin) * WIDTH / (PROJ.xmax - PROJ.xmin) + margin) * scale,
             ((PROJ.ymax - y) * HEIGHT / (PROJ.ymax - PROJ.ymin) + margin) * scale) for x, y in coords]


def raster_polygons(geometry, scale, size=None, margin=0):
    size = size or (WIDTH, HEIGHT)
    mask = Image.new("L", (size[0] * scale, size[1] * scale), 0)
    draw = ImageDraw.Draw(mask)
    for polygon in geometries(geometry, "Polygon"):
        draw.polygon(pixel_coords(polygon.exterior.coords, scale, margin), fill=255)
        for interior in polygon.interiors:
            draw.polygon(pixel_coords(interior.coords, scale, margin), fill=0)
    return mask


def decode_pvr(data):
    header = struct.unpack("<13I", data[:52])
    if header[0] != 52 or header[6] != 16 or header[7:11] != (61440, 3840, 240, 15):
        raise ValueError("Unsupported PVR layout")
    words = np.frombuffer(data[52:], dtype="<u2").reshape(header[1], header[2])
    pixels = np.stack([((words >> s) & 15) * 17 for s in (12, 8, 4, 0)], axis=-1)
    return Image.fromarray(pixels.astype("uint8"))


def encode_pvr(image, template):
    pixels = np.asarray(image.convert("RGBA"), dtype=np.uint16)
    words = np.zeros(pixels.shape[:2], dtype="<u2")
    for channel, shift in enumerate((12, 8, 4, 0)):
        words |= ((pixels[:, :, channel] + 8) // 17) << shift
    header = list(struct.unpack("<13I", template[:52]))
    header[1], header[2] = image.height, image.width
    header[5] = words.nbytes
    return struct.pack("<13I", *header) + words.tobytes()


def quantize(image):
    a = np.asarray(image, dtype=np.uint16)
    return Image.fromarray((((a + 8) // 17) * 17).astype("uint8"))


def render_overlay(land_mask, river_features, scale, margin=0):
    water = Image.eval(land_mask, lambda v: 255 - v)
    overlay = Image.new("RGBA", land_mask.size, SEA + (0,))
    overlay.putalpha(water)
    # An inner and outer contour match the original map's restrained shore outline.
    inner = np.asarray(land_mask) - np.asarray(land_mask.filter(ImageFilter.MinFilter(5)))
    outer = np.asarray(land_mask.filter(ImageFilter.MaxFilter(7))) - np.asarray(land_mask)
    outer_layer = Image.new("RGBA", land_mask.size, (119, 151, 159, 0))
    outer_layer.putalpha(Image.fromarray(outer))
    overlay = Image.alpha_composite(overlay, outer_layer)
    inner_layer = Image.new("RGBA", land_mask.size, (91, 113, 118, 0))
    inner_layer.putalpha(Image.fromarray(inner))
    overlay = Image.alpha_composite(overlay, inner_layer)
    draw = ImageDraw.Draw(overlay)
    for geom, attrs in river_features:
        rank = attrs.get("strokeweig", attrs.get("scalerank", 3))
        try:
            rank = float(rank or 3)
        except (ValueError, TypeError):
            rank = 3
        width = max(3, round(scale * (2.1 if rank >= 0.8 else 1.5)))
        for line in geometries(geom, "LineString"):
            pts = pixel_coords(line.coords, scale, margin)
            if len(pts) < 2:
                continue
            draw.line(pts, fill=(137, 163, 164, 255), width=width + 2 * scale, joint="curve")
            draw.line(pts, fill=(76, 111, 137, 255), width=width, joint="curve")
    return overlay


def land_texture(size):
    with ZipFile(ROOT / "欧陆战争4地图原图.zip") as archive:
        name = next(n for n in archive.namelist() if n.endswith("/陆地填充.png"))
        texture = Image.open(io.BytesIO(archive.read(name))).convert("RGB")
    result = Image.new("RGBA", size)
    for y in range(0, size[1], texture.height):
        for x in range(0, size[0], texture.width):
            result.paste(texture, (x, y))
    return result


def pack_atlases(overlay, scale, template, assets, padded):
    block = 64 * scale
    cols = math.ceil(WIDTH / TEXTURE_STEP)
    rows = math.ceil(HEIGHT / TEXTURE_STEP)
    step = TEXTURE_STEP * scale
    if padded.size != (PADDED_SIZE[0] * scale, PADDED_SIZE[1] * scale):
        raise ValueError("Geographic edge canvas does not match the original atlas index")
    # Native renderer crops one pixel per side and advances 62 pixels per tile.
    padded.paste(overlay, (scale, scale))
    padded.save(OUT / f"eastasia_overlay_padded{scale}x.png")
    indices = np.zeros((rows, cols), dtype="<i4")
    atlas = Image.new("RGBA", (4 * block, 4 * block), SEA + (255,))
    next_id = 1
    atlas_no = 1
    emitted = 0
    errors = []
    suffix = "@2x" if scale == 2 else ""
    reconstructed = Image.new("RGBA", padded.size, SEA + (255,))
    for row in range(rows):
        for col in range(cols):
            patch = padded.crop((col * step, row * step, col * step + block, row * step + block))
            a = np.asarray(patch)
            if not a[:, :, 3].any():
                indices[row, col] = -1
                reconstructed.paste((0, 0, 0, 0), (col * step, row * step,
                                                   col * step + block, row * step + block))
                continue
            if np.all(a == np.array(SEA + (255,), dtype=np.uint8)):
                continue
            if next_id >= 102 * 16:
                raise ValueError("Map needs more than the original 102 atlas files; simplify the outlines")
            wanted_atlas, tile = next_id // 16 + 1, next_id % 16
            if wanted_atlas != atlas_no:
                data = encode_pvr(atlas, template)
                (assets / f"map/map1_{atlas_no}{suffix}.pvr").write_bytes(data)
                emitted += 1
                atlas = Image.new("RGBA", (4 * block, 4 * block), SEA + (255,))
                atlas_no = wanted_atlas
            x, y = (tile % 4) * block, (tile // 4) * block
            atlas.paste(patch, (x, y))
            indices[row, col] = next_id
            reconstructed.paste(quantize(patch), (col * step, row * step))
            next_id += 1
    (assets / f"map/map1_{atlas_no}{suffix}.pvr").write_bytes(encode_pvr(atlas, template))
    emitted += 1
    # Keep the original filename inventory, including unused atlases.
    blank = encode_pvr(Image.new("RGBA", (4 * block, 4 * block), SEA + (255,)), template)
    for number in range(atlas_no + 1, 103):
        (assets / f"map/map1_{number}{suffix}.pvr").write_bytes(blank)
    header = struct.pack("<2i", WIDTH * scale, HEIGHT * scale)
    (assets / ("map1_hd.bin" if scale == 2 else "map1.bin")).write_bytes(header + indices.tobytes())
    # Reopen every saved atlas and use the saved index to reconstruct independently.
    reopened = Image.new("RGBA", padded.size, SEA + (255,))
    decoded = {}
    for row in range(rows):
        for col in range(cols):
            v = int(indices[row, col])
            target = (col * step, row * step)
            if v == -1:
                reopened.paste((0, 0, 0, 0), (*target, target[0] + block, target[1] + block))
            elif v > 0:
                number, tile = v // 16 + 1, v % 16
                if number not in decoded:
                    decoded[number] = decode_pvr((assets / f"map/map1_{number}{suffix}.pvr").read_bytes())
                x, y = tile % 4 * block, tile // 4 * block
                reopened.paste(decoded[number].crop((x, y, x + block, y + block)), target)
    if not np.array_equal(np.asarray(reopened), np.asarray(reconstructed)):
        errors.append("Atlas reconstruction differs")
    expected = np.array(quantize(padded))
    expected[expected[:, :, 3] == 0] = 0
    actual = np.array(reopened)
    actual[actual[:, :, 3] == 0] = 0
    if not np.array_equal(actual, expected):
        errors.append("Saved atlas differs from padded geographic overlay")
    del expected, actual
    if scale == 1:
        preview_edges(reopened)
    crop = reopened.crop((scale, scale, (1 + WIDTH) * scale, (1 + HEIGHT) * scale))
    return crop, {"scale": scale, "texture_blocks": next_id - 1, "active_atlases": emitted,
                  "atlas_inventory": 102, "index_grid": [cols, rows],
                  "visible_tile_size": 62 * scale, "atlas_gutter": scale, "errors": errors}


def preview_edges(overlay):
    image = Image.alpha_composite(land_texture(overlay.size), overlay).convert("RGB")
    sheet = Image.new("RGB", (1400, 720), "white")
    draw = ImageDraw.Draw(sheet)
    for i, (name, x, y) in enumerate([
        ("Northwest", 0, 0), ("Northeast", image.width - 700, 0),
        ("Southwest", 0, image.height - 340),
        ("Southeast", image.width - 700, image.height - 340),
    ]):
        left, top = (i % 2) * 700, (i // 2) * 360
        sheet.paste(image.crop((x, y, x + 700, y + 340)), (left, top + 20))
        draw.text((left + 8, top + 4), name, fill="black")
        for boundary in (1, 1 + WIDTH):
            if x <= boundary < x + 700:
                draw.line((left + boundary - x, top + 20,
                           left + boundary - x, top + 359), fill="red")
        for boundary in (1, 1 + HEIGHT):
            if y <= boundary < y + 340:
                draw.line((left, top + 20 + boundary - y,
                           left + 699, top + 20 + boundary - y), fill="red")
    sheet.save(OUT / "eastasia_edge_check.png")


def terrain(land_mask, rivers, assets):
    mask = np.asarray(land_mask) >= 128
    yy, xx = np.mgrid[-36:36, -32:32]
    in_hex = np.abs(yy + .5) <= 36 - np.abs(xx + .5) * 18 / 32
    fractions = np.zeros((GRID_H, GRID_W), dtype=np.float64)
    data = np.zeros((GRID_H, GRID_W, 12), dtype=np.uint8)
    data[:, :, 1] = 255
    data[:, :, 4] = 11
    data[:, :, 5] = 255
    centres = {}
    for row in range(GRID_H):
        for col in range(GRID_W):
            x, y = col * 64 + (32 if row % 2 else 0), row * 54
            centres[col, row] = (x, y)
            patch = mask[y:y + 72, x + TERRAIN_MARGIN - 32:x + TERRAIN_MARGIN + 32]
            fractions[row, col] = np.count_nonzero(patch & in_hex) / np.count_nonzero(in_hex)
            water = fractions[row, col] <= .5
            data[row, col, 0] = int(water)
            data[row, col, 1] = ((col * 13 + row * 7) % 7) if water else 255
    river_lines = unary_union([g for g, _ in rivers])
    barrier = transform_geometry(river_lines, lambda x, y, z=None: (
        (np.asarray(x) - PROJ.xmin) * WIDTH / (PROJ.xmax - PROJ.xmin),
        (PROJ.ymax - np.asarray(y)) * HEIGHT / (PROJ.ymax - PROJ.ymin)), interleaved=False)
    # Right, lower-right, lower-left and their opposite edge bits.
    links = [(1, 8, 1, 0), (2, 16, None, 1), (4, 32, None, 1)]
    for row in range(GRID_H):
        for col in range(GRID_W):
            for bit, opposite, dx, dy in links:
                if dx is None:
                    dx = (1 if row % 2 else 0) if bit == 2 else (0 if row % 2 else -1)
                nc, nr = col + dx, row + dy
                if not (0 <= nc < GRID_W and nr < GRID_H):
                    continue
                if data[row, col, 0] == 1 or data[nr, nc, 0] == 1:
                    continue
                if LineString([centres[col, row], centres[nc, nr]]).intersects(barrier):
                    data[row, col, 8] |= bit
                    data[nr, nc, 8] |= opposite
    regions_path = ROOT / "maps/terrain_regions.geojson"
    regions = json.loads(regions_path.read_text(encoding="utf-8"))
    projected = []
    for feature in sorted(regions["features"], key=lambda f: f["properties"]["priority"], reverse=True):
        region = transform_geometry(shape(feature["geometry"]), PROJ.xy, interleaved=False)
        properties = feature["properties"]
        if "buffer_km" in properties:
            region = region.buffer(properties["buffer_km"] * 1000)
        projected.append((region, properties["terrain"]))
    for (col, row), (x, y) in centres.items():
        if data[row, col, 0] == 1:
            continue
        p = Point(PROJ.xmin + x / WIDTH * (PROJ.xmax - PROJ.xmin),
                  PROJ.ymax - y / HEIGHT * (PROJ.ymax - PROJ.ymin))
        for region, kind in projected:
            if region.covers(p):
                seed = ((col * 73856093) ^ (row * 19349663)) & 0xffffffff
                density = {3: 68, 4: 60, 6: 72, 7: 80, 8: 48, 9: 90}[kind]
                if seed % 100 >= density:
                    break
                data[row, col, 0] = kind
                # Match the original: typed terrain has a valid background sprite.
                data[row, col, 1] = (col * 13 + row * 17) % (9 if kind == 7 else 6)
                data[row, col, 2] = ((seed >> 8) % 15 - 7) & 255
                data[row, col, 3] = ((seed >> 16) % 11 - 5) & 255
                break
    header = struct.pack("<4s3i", b"YSAE", 1, GRID_W, GRID_H)
    (assets / "europe.bin").write_bytes(header + data.tobytes())
    np.save(OUT / "cell_land_fraction.npy", fractions)
    return data, centres


def preview_terrain(background, data, centres):
    assets = ROOT / "apk_work/original/assets"
    root = ET.fromstring("<root>" + (assets / "tiles.xml").read_text(encoding="utf-8") + "</root>")
    sprites = {element.get("name"): element.attrib for element in root.findall(".//Image")}
    atlas = Image.open(assets / "tiles.png").convert("RGBA")
    kinds = ["land", "wave", "cactus", "cedar", "cypress", "palm", "pine", "desert", "hill", "mountain"]
    image = background.convert("RGBA")
    for (col, row), (x, y) in centres.items():
        cell = data[row, col]
        for offset in (0, 4):
            kind, index = int(cell[offset]), int(cell[offset + 1])
            if kind < 2 or kind > 9 or index == 255:
                continue
            sprite = sprites[f"{kinds[kind]}_{index + 1}.png"]
            sx, sy, sw, sh, rx, ry = [int(sprite[k]) for k in ("x", "y", "w", "h", "refx", "refy")]
            dx, dy = (int(v) if int(v) < 128 else int(v) - 256 for v in cell[offset + 2:offset + 4])
            image.alpha_composite(atlas.crop((sx, sy, sx + sw, sy + sh)), (round(x + dx - rx), round(y + dy - ry)))
    image.convert("RGB").save(OUT / "eastasia_terrain.png")
    image.thumbnail((1997, 1433))
    image.convert("RGB").save(OUT / "eastasia_terrain_preview.png")


def preview_grid(background, data, centres):
    im = background.copy()
    draw = ImageDraw.Draw(im, "RGBA")
    for (col, row), (x, y) in centres.items():
        pts = [(x, y - 36), (x + 32, y - 18), (x + 32, y + 18),
               (x, y + 36), (x - 32, y + 18), (x - 32, y - 18)]
        draw.line(pts + [pts[0]], fill=(30, 40, 45, 55), width=1)
        draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(20, 150, 210, 220) if data[row, col, 0] else (100, 95, 65, 170))
    im.thumbnail((1997, 1433))
    im.save(OUT / "eastasia_grid_check.png")


def preview_places(background):
    im = background.copy()
    im.thumbnail((1997, 1433))
    scale = im.width / WIDTH
    draw = ImageDraw.Draw(im)
    font_path = Path("C:/Windows/Fonts/arial.ttf")
    font = ImageFont.truetype(str(font_path), 16) if font_path.exists() else ImageFont.load_default()
    positions = {}
    for name, (lon, lat) in CITIES.items():
        x, y = PROJ.pixels(lon, lat)
        positions[name] = {"lon": lon, "lat": lat, "pixel": [round(float(x), 2), round(float(y), 2)],
                           "inside": bool(0 <= x < WIDTH and 0 <= y < HEIGHT)}
        if not positions[name]["inside"]:
            continue
        x, y = x * scale, y * scale
        draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill="#7a3431", outline="white", width=1)
        draw.text((x + 6, y - 9), name, font=font, fill="#252b2b", stroke_width=2, stroke_fill="#e4e5df")
    im.save(OUT / "eastasia_places_check.png")
    write_json(OUT / "place_coordinates.json", positions)
    return positions


def main():
    global OUT
    parser = argparse.ArgumentParser()
    parser.add_argument("--fetch-only", action="store_true")
    parser.add_argument("--output", type=Path, default=OUT)
    args = parser.parse_args()
    OUT = args.output.resolve()
    if not OUT.is_relative_to(ROOT / "maps"):
        raise ValueError("Map output must stay within the project maps directory")
    CACHE.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(parents=True, exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        sources = list(pool.map(fetch_source, SOURCES.items()))
    write_json(OUT / "sources.json", sources)
    log("Geographic sources cached and verified")
    if args.fetch_only:
        return
    apk = ROOT / "欧陆战争4_1.6.6.apk"
    source_sha = sha(apk.read_bytes())
    log("Projecting coastline and lakes")
    land = unary_union([g for g, _ in read_shapes("land")])
    lake_features = [(g, a) for g, a in read_shapes("lakes")
                     if g.area > 25_000_000 and a.get("featurecla") != "Reservoir"]
    land = land.difference(unary_union([g for g, _ in lake_features]))
    rivers = []
    for geom, attrs in read_shapes("rivers"):
        names = " ".join(str(attrs.get(k, "") or "") for k in ("name", "name_en", "name_alt", "name_zh")).lower()
        exact_name = str(attrs.get("name_en") or attrs.get("name") or "").lower()
        if exact_name in EXACT_RIVERS or any(word in names for word in RIVER_WORDS):
            rivers.append((geom, attrs))
    log(f"Rendering land, {len(lake_features)} lakes, {len(rivers)} river segments")
    mask_hd = raster_polygons(land, 2)
    mask = mask_hd.resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS)
    mask.save(OUT / "eastasia_land_mask.png")
    overlay_hd = render_overlay(mask_hd, rivers, 2)
    del mask_hd
    overlay = overlay_hd.resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS)
    overlay.save(OUT / "eastasia_shores_rivers.png")
    log("Rendering geographic continuation into the outer texture margin")
    edge_land = unary_union([g for g, _ in read_shapes("land", EDGE_CLIP)])
    edge_lakes = [g for g, a in read_shapes("lakes", EDGE_CLIP)
                  if g.area > 25_000_000 and a.get("featurecla") != "Reservoir"]
    edge_land = edge_land.difference(unary_union(edge_lakes))
    river_ids = {a.get("ne_id") for _, a in rivers}
    edge_rivers = [(g, a) for g, a in read_shapes("rivers", EDGE_CLIP) if a.get("ne_id") in river_ids]
    terrain_mask = raster_polygons(edge_land, 1, size=TERRAIN_MASK_SIZE, margin=TERRAIN_MARGIN)
    terrain_mask.save(OUT / "eastasia_terrain_land_mask.png")
    edge_mask_hd = raster_polygons(edge_land, 2, size=PADDED_SIZE, margin=1)
    edge_overlay_hd = render_overlay(edge_mask_hd, edge_rivers, 2, margin=1)
    del edge_mask_hd, edge_land
    assets = OUT / "assets"
    (assets / "map").mkdir(parents=True, exist_ok=True)
    with ZipFile(apk) as archive:
        standard_template = archive.read("assets/map/map1_1.pvr")
        hd_template = archive.read("assets/map/map1_1@2x.pvr")
        # Hide obsolete European geographic labels in this replacement slot.
        (assets / "maptextpos_europe.xml").write_bytes(b'<Elements/>\n')
        (assets / "def_map.xml").write_bytes(archive.read("assets/def_map.xml"))
    log("Packing normal-resolution texture atlases")
    restored, check_standard = pack_atlases(
        overlay, 1, standard_template, assets,
        edge_overlay_hd.resize(PADDED_SIZE, Image.Resampling.LANCZOS))
    del overlay
    log("Packing high-resolution texture atlases")
    restored_hd, check_hd = pack_atlases(overlay_hd, 2, hd_template, assets, edge_overlay_hd)
    del overlay_hd, edge_overlay_hd
    hd_background = Image.alpha_composite(land_texture(restored_hd.size), restored_hd).convert("RGB")
    hd_background.save(OUT / "eastasia_base_hd.png", compress_level=6)
    del hd_background, restored_hd
    background = Image.alpha_composite(land_texture(restored.size), restored).convert("RGB")
    background.save(OUT / "eastasia_base.png", compress_level=6)
    preview = background.copy()
    preview.thumbnail((1997, 1433))
    preview.save(OUT / "eastasia_preview.png")
    log("Generating basic sea/land cells and symmetric river edge flags")
    cells, centres = terrain(terrain_mask, rivers, assets)
    preview_terrain(background, cells, centres)
    preview_grid(background, cells, centres)
    places = preview_places(background)
    from build_city_font import build_city_fonts
    build_city_fonts(assets, OUT)
    river_names = sorted(set(str(a.get("name_en") or a.get("name") or "") for _, a in rivers))
    errors = check_standard["errors"] + check_hd["errors"]
    if any(not p["inside"] for p in places.values()):
        errors.append("One or more key places outside the map")
    if len((assets / "europe.bin").read_bytes()) != 16 + GRID_W * GRID_H * 12:
        errors.append("Terrain binary size mismatch")
    if sha(apk.read_bytes()) != source_sha:
        errors.append("Input APK changed")
    pack = OUT / "eastasia_map_resources.zip"
    with ZipFile(pack, "w", ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(assets.rglob("*")):
            if path.is_file():
                archive.write(path, path.relative_to(OUT).as_posix())
    report = {
        "status": "resource prototype; game runtime not yet tested",
        "original_apk_sha256": source_sha, "map_slot": 1,
        "pixel_size": [WIDTH, HEIGHT], "hd_pixel_size": [WIDTH * 2, HEIGHT * 2],
        "terrain_grid": [GRID_W, GRID_H], "projection": "spherical Lambert conformal conic",
        "projection_parameters": {"lon0": 110, "lat0": 35, "parallels": [25, 47],
                                  "radius_m": PROJ.radius, "projected_bounds": list(CLIP.bounds)},
        "texture_index_origin_margin": [0, 0],
        "texture_edges": "Native 62-pixel visible tiles with 1-pixel atlas gutters; real sea/land continuation beyond the map rectangle.",
        "terrain_classification": "Land only when more than half of the full hexagon is land; extended geographic mask covers boundary hexagons.",
        "terrain_centres": "x=64*col+32*(row%2), y=54*row; inferred from original format, requires runtime confirmation",
        "textures": [check_standard, check_hd],
        "land_cells": int(np.count_nonzero(cells[:, :, 0] != 1)),
        "sea_cells": int(np.count_nonzero(cells[:, :, 0] == 1)),
        "river_edge_cells": int(np.count_nonzero(cells[:, :, 8])),
        "river_names": river_names, "resource_files": len(list(assets.rglob("*.*"))),
        "terrain_counts": {str(int(k)): int(v) for k, v in zip(*np.unique(cells[:, :, 0], return_counts=True))},
        "terrain_regions_sha256": sha((ROOT / "maps/terrain_regions.geojson").read_bytes()),
        "terrain_accuracy": "Hand-generalized terrain regions at game-grid resolution; not measured elevation or historical vegetation.",
        "historical_geography": "Modern physical baseline; reservoir-class lakes excluded. Period-specific river courses and lake shores still require historical review.",
        "errors": errors,
        "pending": ["Android runtime and map-index origin validation",
                    "Historical city placements and six scenario files", "Ownership and faction colors",
                    "Conquest-selection miniature artwork"],
    }
    write_json(OUT / "validation.json", report)
    write_json(OUT / "manifest.json", [
        {"path": p.relative_to(OUT).as_posix(), "bytes": p.stat().st_size, "sha256": sha(p.read_bytes())}
        for p in sorted(assets.rglob("*")) if p.is_file()])
    log(json.dumps({"output": str(OUT), "errors": errors, "textures": report["textures"],
                    "land_cells": report["land_cells"], "sea_cells": report["sea_cells"]}))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
