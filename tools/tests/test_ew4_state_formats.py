"""Boundaries for native-derived state envelopes and animation hierarchy."""

from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from ew4_formats import BattleFile, BATTLE_SECTIONS, FormatError
from ew4_state_formats import ArmyRecord, BileLibrary, SaveFile

ROOT = Path(__file__).resolve().parents[2]


def synthetic_save():
    header = [0] * 18
    header[0], header[4], header[5], header[6] = 2, 2, 3, 1
    sections = {name: bytes(6 if field is None else header[field] * (428 if name == "legions" else stride))
                for name, field, stride in BATTLE_SECTIONS}
    envelope = bytearray(104)
    struct.pack_into("<4sii", envelope, 0, b"YSAE", 1, 2)
    struct.pack_into("<3i", envelope, 64, 0, 17, 8)
    return SaveFile(bytes(envelope), BattleFile(tuple(header), sections, 428))


class StateFormatBoundaries(unittest.TestCase):
    def test_save_uses_runtime_legion_stride(self):
        save = synthetic_save()
        data = save.to_bytes()
        self.assertEqual(SaveFile.from_bytes(data), save)
        self.assertEqual(save.battle.summary()["legions"]["record_size"], 428)
        self.assertEqual(save.metadata()["round"], 17)
        with self.assertRaises(FormatError):
            BattleFile.from_bytes(data[104:])

    def test_save_rejects_wrong_envelope_and_partial_payload(self):
        data = synthetic_save().to_bytes()
        for broken in (b"", data[:103], data[:-1], data + b"x", b"NOPE" + data[4:],
                       data[:4] + struct.pack("<i", 2) + data[8:]):
            with self.assertRaises(FormatError):
                SaveFile.from_bytes(broken)

    def test_army_preserves_signed_fields_and_flags(self):
        raw = bytearray(32)
        struct.pack_into("<hbbhhI4bh", raw, 0, 500, 2, 1, 60, 90, 123, 1, 6, -3, 3, -1)
        raw[20], raw[30] = 255, 254
        fields = ArmyRecord(bytes(raw)).fields()
        self.assertEqual((fields["base_morale"], fields["commander_id"]), (-3, -1))
        self.assertEqual((fields["state64"], fields["status_id"]), (255, 254))
        with self.assertRaises(FormatError):
            ArmyRecord(bytes(31))

    def test_original_animation_hierarchy_and_lossless_bytes(self):
        data = (ROOT / "apk_work/original/assets/army_fort.bin").read_bytes()
        library = BileLibrary.from_bytes(data)
        hierarchy = library.hierarchy()
        self.assertEqual(library.to_bytes(), data)
        self.assertEqual(hierarchy["counts"]["BMTI"], 14)
        self.assertEqual(hierarchy["items"][0]["name"], "Building")
        self.assertEqual(sum(len(i["layers"]) for i in hierarchy["items"]), 16)

    def test_animation_rejects_overflow_and_inconsistent_hierarchy(self):
        data = (ROOT / "apk_work/original/assets/army_fort.bin").read_bytes()
        bad_size = bytearray(data)
        struct.pack_into("<I", bad_size, 24, 0xffffffff)
        bad_layers = bytearray(data)
        struct.pack_into("<I", bad_layers, 984 + 32, 0xffffffff)
        for broken in (data[:-1], data + b"x", bytes(bad_size), bytes(bad_layers)):
            with self.assertRaises(FormatError):
                BileLibrary.from_bytes(broken)

    def test_normalized_animation_retains_layout_and_rejects_invalid_refs(self):
        data = (ROOT / "apk_work/original/assets/army_fort.bin").read_bytes()
        library = BileLibrary.from_bytes(data)
        normalized = library.normalized()
        self.assertEqual(normalized["items"][0]["duration"], 2)
        self.assertEqual(normalized["items"][0]["layers"][0][0]["indices"], [(0, 6)])
        self.assertEqual(len(normalized["elements"]), 11)
        for offset, fmt, value in ((536, "i", 999999), (36, "f", float("nan"))):
            broken = bytearray(data)
            struct.pack_into("<" + fmt, broken, offset, value)
            with self.assertRaises(FormatError):
                BileLibrary.from_bytes(broken).normalized()


if __name__ == "__main__":
    unittest.main()
