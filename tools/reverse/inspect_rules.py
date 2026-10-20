"""Extract native constant tables and direct skill references as research evidence."""

import json
from pathlib import Path
import re
import struct
import sys

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from elftools.elf.elffile import ELFFile

ROOT = Path(__file__).resolve().parents[2]
NATIVE = ROOT / "reverse/native"


def main():
    with (ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so").open("rb") as stream:
        elf = ELFFile(stream)

        def read(address, size):
            for segment in elf.iter_segments():
                start = segment["p_vaddr"]
                if segment["p_type"] == "PT_LOAD" and start <= address and address + size <= start + segment["p_filesz"]:
                    stream.seek(segment["p_offset"] + address - start)
                    return stream.read(size)
            raise ValueError(f"Unmapped native range {address:x}+{size}")

        tables = {
            "recruit_action_by_unit_grade": list(struct.unpack("<66I", read(0x195670, 66 * 4))),
            "attack_count_health_thresholds": list(struct.unpack("<6i", read(0x194b90, 24))),
            "weapon_category_multipliers": list(struct.unpack("<30f", read(0x197140, 120))),
            "combat_constants_181f28": list(struct.unpack("<4f", read(0x81f28, 16))),
            "commander_rank_xp": list(struct.unpack("<14i", read(0x194da0, 56))),
            "commander_nobility_xp": list(struct.unpack("<9i", read(0x194e20, 36))),
            "commander_rank_health_bonus": list(struct.unpack("<15i", read(0x195780, 60))),
            "commander_nobility_healing": list(struct.unpack("<10i", read(0x1957c0, 40))),
            "war_experience_multipliers": list(struct.unpack("<2f", read(0x56f54, 8))),
            "economic_multipliers": list(struct.unpack("<3f", read(0x579ec, 12))),
        }
    refs = []
    for path in sorted((NATIVE / "decompiled").glob("*.c")):
        code = path.read_text(encoding="utf-8")
        for match in re.finditer(r"FUN_001570f8\([^;]*?,\s*(0x[0-9a-f]+|[0-9]+)\)", code):
            refs.append({"function": path.stem, "skill_id": int(match[1], 0),
                         "line": code.count("\n", 0, match.start()) + 1})
    result = {"warning": "Static evidence; skill names and unobserved branches are not inferred.",
              "tables": tables, "direct_skill_calls": refs}
    (NATIVE / "rules_inventory.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"tables": len(tables), "direct_skill_calls": len(refs),
                      "distinct_direct_skill_ids": len({r['skill_id'] for r in refs})}))


if __name__ == "__main__":
    main()
