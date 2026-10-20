"""Boundary checks for recovered resource formats and atlas sampling."""

from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from PIL import Image, ImageDraw
from ew4_formats import BattleFile, BitmapFont, FormatError, TerrainMap, TextureIndex


class FormatBoundaries(unittest.TestCase):
    def test_native_texture_gutters_and_step(self):
        atlas = Image.new("RGBA", (256, 256), "red")
        draw = ImageDraw.Draw(atlas)
        draw.rectangle((65, 1, 126, 62), fill="green")
        draw.rectangle((129, 1, 190, 62), fill="yellow")
        index = TextureIndex(63, 62, 1, (1, 2))
        image = index.render(lambda _: atlas)
        self.assertEqual(image.getpixel((61, 61)), (0, 128, 0, 255))
        self.assertEqual(image.getpixel((62, 61)), (255, 255, 0, 255))
        self.assertTrue(all(color != (255, 0, 0, 255) for _, color in image.getcolors()))

    def test_hd_texture_gutters(self):
        index = TextureIndex(124, 124, 2, (0,))
        self.assertEqual(index.atlas_region(0), (1, (2, 2, 126, 126)))
        self.assertEqual(index.atlas_region(16), (2, (2, 2, 126, 126)))

    def test_transparent_sentinel_does_not_load_atlas(self):
        image = TextureIndex(62, 62, 1, (-1,)).render(
            lambda _: self.fail("Transparent tile attempted an atlas read"))
        self.assertIsNone(image.getbbox())

    def test_dimension_and_texture_count_errors(self):
        for width, height in ((0, 62), (-1, 62), (2**31 - 1, 62)):
            with self.assertRaises(FormatError):
                TextureIndex.from_bytes(struct.pack("<3i", width, height, 0))
        with self.assertRaises(FormatError):
            TextureIndex.from_bytes(struct.pack("<3i", 124, 62, 0))
        with self.assertRaises(FormatError):
            TextureIndex.from_bytes(struct.pack("<3i", 62, 62, -2))

    def test_font_aliases_and_signed_offsets_survive(self):
        font = BitmapFont(13, ((0xffe5, 0, 0, 4, 8, -2, -3, 5),
                               (0xffe5, 4, 0, 4, 8, -2, -3, 5)))
        self.assertEqual(BitmapFont.from_bytes(font.to_bytes()), font)

    def test_terrain_offsets_are_signed(self):
        record = bytes((3, 2, 249, 251, 11, 255, 0, 0, 1, 0, 9, 10))
        data = struct.pack("<4s3i", b"YSAE", 1, 1, 1) + record
        terrain = TerrainMap.from_bytes(data)
        self.assertEqual(terrain.cells[0].background_dx, -7)
        self.assertEqual(terrain.cells[0].background_dy, -5)
        self.assertEqual(terrain.to_bytes(), data)

    def test_battle_unknown_bytes_survive(self):
        header = [0] * 18
        header[0], header[4], header[5], header[14] = 2, 1, 1, 1
        data = struct.pack("<18i", *header) + b"\xff" + bytes(range(108))
        battle = BattleFile.from_bytes(data)
        self.assertEqual(battle.sections["opaque108"], bytes(range(108)))
        self.assertEqual(battle.to_bytes(), data)
        header[14] = -1
        with self.assertRaises(FormatError):
            BattleFile.from_bytes(struct.pack("<18i", *header) + b"\xff")


if __name__ == "__main__":
    unittest.main()
