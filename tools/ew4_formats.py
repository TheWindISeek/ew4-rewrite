"""Strict, lossless EW4 resource codecs, independent of the Android runtime."""

from dataclasses import dataclass
import struct

TEXTURE_STEP = 62
GLYPH_RECORD = struct.Struct("<3H2B2bH")
CELL_RECORD = struct.Struct("<2B2b2B2b4B")
BATTLE_SECTIONS = (
    ("legions", 6, 180),
    ("ownership", None, 1),
    ("buildings", 7, 16),
    ("armies", 8, 32),
    ("opaque8", 16, 8),
    ("opaque116", 10, 116),
    ("opaque108", 14, 108),
    ("opaque44", 9, 44),
)


class FormatError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise FormatError(message)


def area(width, height):
    require(0 < width <= 32768 and 0 < height <= 32768, "Invalid resource dimensions")
    require(width * height <= 100_000_000, "Resource dimensions exceed reader limit")
    return width * height


@dataclass(frozen=True)
class TerrainCell:
    background_kind: int
    background_index: int
    background_dx: int
    background_dy: int
    foreground_kind: int
    foreground_index: int
    foreground_dx: int
    foreground_dy: int
    river_flags: int
    land_flags: int
    unknown10: int
    unknown11: int

    def to_bytes(self):
        return CELL_RECORD.pack(*self.__dict__.values())


@dataclass(frozen=True)
class TerrainMap:
    width: int
    height: int
    cells: tuple[TerrainCell, ...]

    @classmethod
    def from_bytes(cls, data):
        require(len(data) >= 16, "Truncated terrain header")
        magic, version, width, height = struct.unpack_from("<4s3i", data)
        require(magic == b"YSAE" and version == 1, "Unsupported terrain header")
        count = area(width, height)
        require(len(data) == 16 + count * 12, "Terrain length differs from dimensions")
        return cls(width, height, tuple(TerrainCell(*v) for v in CELL_RECORD.iter_unpack(data[16:])))

    def to_bytes(self):
        require(len(self.cells) == area(self.width, self.height), "Terrain cell count mismatch")
        return struct.pack("<4s3i", b"YSAE", 1, self.width, self.height) + b"".join(c.to_bytes() for c in self.cells)

    def centre(self, col, row):
        require(0 <= col < self.width and 0 <= row < self.height, "Terrain coordinate outside grid")
        return col * 64 + 32 * (row % 2), row * 54


@dataclass(frozen=True)
class TextureIndex:
    width: int
    height: int
    scale: int
    indices: tuple[int, ...]

    @property
    def step(self):
        return TEXTURE_STEP * self.scale

    @property
    def columns(self):
        return (self.width + self.step - 1) // self.step

    @property
    def rows(self):
        return (self.height + self.step - 1) // self.step

    @classmethod
    def from_bytes(cls, data, scale=1):
        require(scale in (1, 2), "Unsupported texture scale")
        require(len(data) >= 8, "Truncated texture index header")
        width, height = struct.unpack_from("<2i", data)
        area(width, height)
        step = TEXTURE_STEP * scale
        count = ((width + step - 1) // step) * ((height + step - 1) // step)
        require(len(data) == 8 + count * 4, "Texture index length differs from native 62-pixel stepping")
        indices = struct.unpack_from(f"<{count}i", data, 8)
        require(all(i >= -1 for i in indices), "Unknown negative texture sentinel")
        return cls(width, height, scale, indices)

    def to_bytes(self):
        area(self.width, self.height)
        require(self.scale in (1, 2), "Unsupported texture scale")
        require(len(self.indices) == self.columns * self.rows, "Texture index count mismatch")
        require(all(i >= -1 for i in self.indices), "Unknown negative texture sentinel")
        return struct.pack("<2i", self.width, self.height) + struct.pack(f"<{len(self.indices)}i", *self.indices)

    def atlas_region(self, tile_id):
        require(tile_id >= 0, "Transparent sentinel has no atlas region")
        slot = tile_id % 16
        left = (slot % 4) * 64 * self.scale + self.scale
        top = (slot // 4) * 64 * self.scale + self.scale
        return tile_id // 16 + 1, (left, top, left + self.step, top + self.step)

    def render(self, atlas_loader):
        from PIL import Image

        image = Image.new("RGBA", (self.width, self.height))
        atlases = {}
        for i, tile_id in enumerate(self.indices):
            if tile_id == -1:
                continue
            number, region = self.atlas_region(tile_id)
            if number not in atlases:
                atlas = atlas_loader(number)
                require(atlas.size == (256 * self.scale, 256 * self.scale), "Atlas image dimensions mismatch")
                atlases[number] = atlas
            image.paste(atlases[number].crop(region),
                        ((i % self.columns) * self.step, (i // self.columns) * self.step))
        return image


@dataclass(frozen=True)
class Pvr4444:
    header: tuple[int, ...]
    pixels: bytes

    @classmethod
    def from_bytes(cls, data):
        require(len(data) >= 52, "Truncated PVR header")
        h = struct.unpack_from("<13I", data)
        count = area(h[2], h[1])
        require(h[0] == 52 and h[6] == 16 and h[7:11] == (61440, 3840, 240, 15)
                and h[11] == 559044176, "Unsupported PVR encoding")
        require(h[5] == count * 2 and len(data) == 52 + h[5], "PVR pixel length mismatch")
        return cls(h, data[52:])

    def to_bytes(self):
        data = struct.pack("<13I", *self.header) + self.pixels
        self.from_bytes(data)
        return data

    def to_image(self):
        import numpy as np
        from PIL import Image

        words = np.frombuffer(self.pixels, dtype="<u2").reshape(self.header[1], self.header[2])
        return Image.fromarray(np.stack([((words >> s) & 15) * 17 for s in (12, 8, 4, 0)], axis=-1).astype("uint8"))

    @classmethod
    def from_image(cls, image, template):
        import numpy as np

        header = list(cls.from_bytes(template).header)
        pixels = np.asarray(image.convert("RGBA"), dtype=np.uint16)
        words = np.zeros(pixels.shape[:2], dtype="<u2")
        for channel, shift in enumerate((12, 8, 4, 0)):
            words |= ((pixels[:, :, channel] + 8) // 17) << shift
        header[1], header[2], header[5] = image.height, image.width, words.nbytes
        return cls(tuple(header), words.tobytes())


@dataclass(frozen=True)
class BitmapFont:
    line_height: int
    records: tuple[tuple[int, ...], ...]

    @classmethod
    def from_bytes(cls, data):
        require(len(data) >= 8, "Truncated font header")
        count, height = struct.unpack_from("<2i", data)
        require(count >= 0 and height > 0 and len(data) == 8 + count * 12, "Font record count/height mismatch")
        records = tuple(GLYPH_RECORD.iter_unpack(data[8:]))
        # Original multilingual fonts contain duplicate U+FFE5 entries; retain order.
        return cls(height, records)

    @property
    def characters(self):
        return frozenset(chr(r[0]) for r in self.records)

    def to_bytes(self):
        data = struct.pack("<2i", len(self.records), self.line_height) + b"".join(GLYPH_RECORD.pack(*r) for r in self.records)
        self.from_bytes(data)
        return data


@dataclass(frozen=True)
class BattleFile:
    header: tuple[int, ...]
    sections: dict[str, bytes]
    legion_stride: int = 180

    @classmethod
    def from_bytes(cls, data, *, legion_stride=180):
        require(legion_stride in (180, 428), "Unsupported legion record size")
        require(len(data) >= 72, "Truncated battle header")
        header = struct.unpack_from("<18i", data)
        require(header[0] == 2, "Only corpus-verified BTL version 2 is supported")
        count = area(header[4], header[5])
        offset = 72
        sections = {}
        for name, field, stride in BATTLE_SECTIONS:
            if name == "legions":
                stride = legion_stride
            length = count if field is None else header[field] * stride
            require(length >= 0 and offset + length <= len(data), "Invalid/truncated battle section: " + name)
            sections[name] = data[offset:offset + length]
            offset += length
        require(offset == len(data), "Unrecognized battle trailer")
        return cls(header, sections, legion_stride)

    def to_bytes(self):
        require(len(self.header) == 18 and self.header[0] == 2, "Unsupported battle header")
        require(self.legion_stride in (180, 428), "Unsupported legion record size")
        count = area(self.header[4], self.header[5])
        require(set(self.sections) == {s[0] for s in BATTLE_SECTIONS}, "Battle section inventory mismatch")
        for name, field, stride in BATTLE_SECTIONS:
            if name == "legions":
                stride = self.legion_stride
            length = count if field is None else self.header[field] * stride
            require(length >= 0 and len(self.sections[name]) == length, "Battle section/count mismatch: " + name)
        return struct.pack("<18i", *self.header) + b"".join(self.sections[s[0]] for s in BATTLE_SECTIONS)

    def summary(self):
        offset = 72
        result = {}
        for name, _, stride in BATTLE_SECTIONS:
            if name == "legions":
                stride = self.legion_stride
            length = len(self.sections[name])
            result[name] = {"offset": offset, "bytes": length, "records": length // stride,
                            "record_size": stride}
            offset += length
        return result
