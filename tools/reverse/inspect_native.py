"""Record native ARM64 evidence and RTTI links without loading the library."""

import hashlib
import json
from pathlib import Path
import re
import struct
import sys

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from capstone import Cs, CS_ARCH_ARM64, CS_MODE_ARM
from elftools.elf.elffile import ELFFile

ROOT = Path(__file__).resolve().parents[2]
LIBRARY = ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so"
OUTPUT = ROOT / "reverse/native"
RANGES = {
    "texture_loading": (0x94e18, 0x95110),
    "terrain_loading": (0x84a0c, 0x84acc),
    "battle_layout": (0x7cb80, 0x7cd14),
    "battle_legion_stride": (0x7d058, 0x7d064),
    "font_loading": (0xcd3f4, 0xcd460),
    "ai_dependencies": (0xa7984, 0xa7a0c),
    "damage_random_roll": (0x818dc, 0x819a0),
    "damage_shared_helpers": (0x3a278, 0x3a298),
    "random_bounded_integer": (0xef754, 0xef79c),
    "path_capacity": (0x8dbb8, 0x8dbd4),
}


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    data = LIBRARY.read_bytes()
    with LIBRARY.open("rb") as stream:
        elf = ELFFile(stream)
        text = elf.get_section_by_name(".text")
        base, code = text["sh_addr"], text.data()
        decoder = Cs(CS_ARCH_ARM64, CS_MODE_ARM)
        lines = ["SHA256 " + hashlib.sha256(data).hexdigest()]
        for name, (start, end) in RANGES.items():
            lines.append("\n" + name)
            lines.extend(f"{a:08x} {m:8} {o}" for a, _, m, o in
                         decoder.disasm_lite(code[start - base:end - base], start))
        relocations = {}
        for section in elf.iter_sections():
            if section["sh_type"] == "SHT_RELA":
                for relocation in section.iter_relocations():
                    if relocation["r_info_sym"] == 0:
                        relocations[relocation["r_offset"]] = relocation["r_addend"]
        classes = {}
        for match in re.finditer(rb"[0-9]+C(?:EntityActionAI|EntityBattle|SceneGame|GameSystem)\x00", data):
            name = match.group()[:-1].decode("ascii")
            class_tables = []
            for pointer, target in relocations.items():
                if target != match.start():
                    continue
                typeinfo = pointer - 8
                for reference, value in relocations.items():
                    if value != typeinfo:
                        continue
                    methods = []
                    cursor = reference + 8
                    while cursor in relocations and base <= relocations[cursor] < base + len(code):
                        methods.append(hex(relocations[cursor]))
                        cursor += 8
                    if methods:
                        class_tables.append({"typeinfo": hex(typeinfo), "vtable": hex(reference + 8), "methods": methods})
            classes[name] = class_tables
        exports = {s.name: hex(s["st_value"]) for s in elf.get_section_by_name(".dynsym").iter_symbols()
                   if s.name.startswith("Java_")}
        report = {"library_sha256": hashlib.sha256(data).hexdigest(), "jni": exports, "classes": classes,
                  "texture_visible_step_float": struct.unpack_from("<f", code, 0x9540c - base)[0],
                  "texture_atlas_slot_step_float": struct.unpack_from("<f", code, 0x95410 - base)[0],
                  "limitations": "RTTI reconstructs class associations, not the semantics of every method."}
    (OUTPUT / "evidence.asm").write_text("\n".join(lines) + "\n", encoding="utf-8")
    (OUTPUT / "inventory.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"ranges": len(RANGES), "classes": len(classes), "jni_exports": len(exports),
                      "visible_step": report["texture_visible_step_float"]}))


if __name__ == "__main__":
    main()
