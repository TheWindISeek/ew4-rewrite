"""Recovered save envelope, unit records, and BILE hierarchy for research/import.

Save support is derived from native readers/writers, with synthetic verification;
no device save corpus has been supplied. Unknown serialized bytes are retained.
"""

from dataclasses import dataclass
import math
import struct

from ew4_formats import BattleFile, require


@dataclass(frozen=True)
class SaveFile:
    header: bytes
    battle: BattleFile

    @classmethod
    def from_bytes(cls, data):
        require(len(data) >= 104, "Truncated save envelope")
        require(data[:4] == b"YSAE", "Unknown save magic")
        require(struct.unpack_from("<i", data, 4)[0] == 1, "Unsupported save version")
        return cls(data[:104], BattleFile.from_bytes(data[104:], legion_stride=428))

    def to_bytes(self):
        require(len(self.header) == 104 and self.battle.legion_stride == 428, "Invalid save layout")
        data = self.header + self.battle.to_bytes()
        self.from_bytes(data)
        return data

    def metadata(self):
        return {"mode": struct.unpack_from("<i", self.header, 8)[0],
                "player_country_raw": self.header[12:48].hex(),
                "branch": struct.unpack_from("<i", self.header, 48)[0],
                "camera_raw": list(struct.unpack_from("<3I", self.header, 52)),
                "active_country_index": struct.unpack_from("<i", self.header, 64)[0],
                "round": struct.unpack_from("<i", self.header, 68)[0],
                "collected_medals": struct.unpack_from("<i", self.header, 72)[0],
                "saved_local_date": list(struct.unpack_from("<5i", self.header, 76)),
                "warzone": struct.unpack_from("<i", self.header, 96)[0],
                "scenario": struct.unpack_from("<i", self.header, 100)[0]}


@dataclass(frozen=True)
class ArmyRecord:
    raw: bytes

    def __post_init__(self):
        require(len(self.raw) == 32, "Army record must contain 32 bytes")

    def fields(self):
        layout = (("cell_id", 0, "h"), ("unit_id", 2, "b"), ("grade", 3, "b"),
                  ("health", 4, "h"), ("max_health", 6, "h"), ("experience", 8, "I"),
                  ("rank", 12, "b"), ("movement", 13, "b"), ("base_morale", 14, "b"),
                  ("morale_turns", 15, "b"), ("commander_id", 16, "h"),
                  ("flag58", 18, "B"), ("objective", 19, "b"), ("state64", 20, "B"),
                  ("facing_positive", 21, "B"), ("transport_flag", 22, "B"),
                  ("acted", 23, "B"), ("flag5b", 24, "B"), ("flag5c", 25, "B"),
                  ("state70", 26, "b"), ("construction_turns", 27, "b"),
                  ("status_round", 28, "h"), ("status_id", 30, "B"), ("state4c", 31, "b"))
        return {name: struct.unpack_from("<" + fmt, self.raw, offset)[0] for name, offset, fmt in layout}


@dataclass(frozen=True)
class BileChunk:
    tag: bytes
    data: bytes


@dataclass(frozen=True)
class BileLibrary:
    header: bytes
    chunks: tuple[BileChunk, ...]

    @classmethod
    def from_bytes(cls, data):
        require(len(data) >= 20, "Truncated BILE header")
        magic, version, payload_size, offset, count, fps = struct.unpack_from("<4sIIHHf", data)
        require(magic == b"BILE" and version == 3 and offset == 20, "Unsupported BILE header")
        require(payload_size == len(data) - 20 and math.isfinite(fps) and fps > 0, "Invalid BILE size/rate")
        chunks = []
        for _ in range(count):
            require(offset + 8 <= len(data), "Truncated BILE chunk")
            tag, size = struct.unpack_from("<4sI", data, offset)
            require(size >= 8 and offset + size <= len(data), "Invalid BILE chunk size")
            chunks.append(BileChunk(tag, data[offset:offset + size]))
            offset += size
        require(offset == len(data), "Unrecognized BILE trailer")
        library = cls(data[:20], tuple(chunks))
        library.hierarchy()
        return library

    def to_bytes(self):
        data = self.header + b"".join(c.data for c in self.chunks)
        self.from_bytes(data)
        return data

    def hierarchy(self):
        blocks = {c.tag: c.data for c in self.chunks}
        require(len(blocks) == len(self.chunks), "Duplicate BILE chunks")
        required = {b"BELE": 44, b"BXDI": 8, b"BMRF": 8, b"BYAL": 8, b"BMTI": 56}
        arrays = {}
        for tag, stride in required.items():
            require(tag in blocks and len(blocks[tag]) >= 16, "Missing BILE array " + tag.decode())
            count = struct.unpack_from("<I", blocks[tag], 8)[0]
            require(len(blocks[tag]) == 16 + count * stride, "BILE record length mismatch " + tag.decode())
            arrays[tag] = [blocks[tag][16 + i * stride:16 + (i + 1) * stride] for i in range(count)]
        require(b"BRTS" in blocks and len(blocks[b"BRTS"]) >= 12, "Missing BILE strings")
        strings = blocks[b"BRTS"][12:]
        layer_offset = frame_offset = index_offset = 0
        items = []
        for record in arrays[b"BMTI"]:
            words = struct.unpack("<14I", record)
            name_offset, layer_count = words[1], words[8]
            require(name_offset < len(strings), "BILE name outside string block")
            end = strings.find(b"\0", name_offset)
            require(end >= 0, "Unterminated BILE name")
            name = strings[name_offset:end].decode("utf-8", errors="strict")
            require(layer_offset + layer_count <= len(arrays[b"BYAL"]), "BILE layer range overflow")
            layers = []
            for layer in arrays[b"BYAL"][layer_offset:layer_offset + layer_count]:
                frame_count = struct.unpack_from("<I", layer)[0]
                require(frame_offset + frame_count <= len(arrays[b"BMRF"]), "BILE frame range overflow")
                frames = []
                for frame in arrays[b"BMRF"][frame_offset:frame_offset + frame_count]:
                    key, index_count = struct.unpack_from("<2H", frame)
                    require(index_offset + index_count <= len(arrays[b"BXDI"]), "BILE index range overflow")
                    indices = [struct.unpack("<2i", r) for r in arrays[b"BXDI"][index_offset:index_offset + index_count]]
                    frames.append({"key": key, "indices": indices})
                    index_offset += index_count
                frame_offset += frame_count
                layers.append(frames)
            layer_offset += layer_count
            items.append({"id": words[0], "name": name, "kind": words[6], "layers": layers})
        require(layer_offset == len(arrays[b"BYAL"]) and frame_offset == len(arrays[b"BMRF"])
                and index_offset == len(arrays[b"BXDI"]), "Unreferenced BILE hierarchy records")
        return {"fps": struct.unpack_from("<f", self.header, 16)[0],
                "counts": {tag.decode(): len(records) for tag, records in arrays.items()}, "items": items}

    def normalized(self):
        hierarchy = self.hierarchy()
        blocks = {c.tag: c.data for c in self.chunks}
        elements = []
        for offset in range(16, len(blocks[b"BELE"]), 44):
            record = blocks[b"BELE"][offset:offset + 44]
            transform = list(struct.unpack_from("<6f", record))
            alpha = struct.unpack_from("<f", record, 24)[0]
            require(all(math.isfinite(v) for v in [*transform, alpha]), "Nonfinite BILE transform")
            elements.append({"transform": transform, "alpha": alpha,
                             "start_frame": struct.unpack_from("<i", record, 28)[0],
                             "playback_mode": struct.unpack_from("<i", record, 32)[0],
                             "unknown_tail": record[36:].hex()})
        items = hierarchy["items"]
        for index, item in enumerate(items):
            offset = 16 + index * 56
            record = blocks[b"BMTI"][offset:offset + 56]
            item["pivot"] = list(struct.unpack_from("<2f", record, 8))
            item["duration"] = struct.unpack_from("<i", record, 28)[0]
            for layer in item["layers"]:
                for frame in layer:
                    for element_index, child_index in frame["indices"]:
                        require(-1 <= element_index < len(elements), "BILE element reference out of range")
                        require(-1 <= child_index < len(items), "BILE child reference out of range")
        return {"schema_version": 1, "fps": hierarchy["fps"], "items": items, "elements": elements}
