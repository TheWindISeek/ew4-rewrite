"""Audit decoded original unit fields and lossless BILE animation hierarchies."""

from collections import Counter
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from ew4_formats import BattleFile
from ew4_state_formats import ArmyRecord, BileLibrary

ROOT = Path(__file__).resolve().parents[2]


def main():
    assets = ROOT / "apk_work/original/assets"
    counts, grades, unit_ids = Counter(), Counter(), Counter()
    errors, animations = [], []
    army_definitions = ET.parse(assets / "def_army.xml").getroot()
    definitions = {(c.get("name"), a.get("name"), int(a.get("grade"))) for c in army_definitions for a in c.findall("army")}
    cards = ET.parse(assets / "def_card.xml").getroot()
    recruit_cards = [c.attrib for c in cards if c.get("type") in ("recruit", "construct") and c.get("army")]
    for path in sorted(assets.glob("*.btl")):
        battle = BattleFile.from_bytes(path.read_bytes())
        data = battle.sections["armies"]
        for offset in range(0, len(data), 32):
            fields = ArmyRecord(data[offset:offset + 32]).fields()
            grades[fields["grade"]] += 1
            unit_ids[fields["unit_id"]] += 1
            counts["army_records"] += 1
            if not 0 <= fields["unit_id"] < 22 or not 0 <= fields["grade"] < 3:
                errors.append({"file": path.name, "record": offset // 32, "error": "Unexpected unit/grade", "fields": fields})
        counts["battle_files"] += 1
    for path in sorted(assets.glob("*.bin")):
        data = path.read_bytes()
        if data[:4] != b"BILE":
            continue
        library = BileLibrary.from_bytes(data)
        if library.to_bytes() != data:
            errors.append({"file": path.name, "error": "Animation byte roundtrip differs"})
        hierarchy = library.hierarchy()
        animations.append({"file": path.name, "counts": hierarchy["counts"], "fps": hierarchy["fps"]})
        counts["animation_files"] += 1
        counts["animation_items"] += len(hierarchy["items"])
    result = {"counts": dict(counts), "unit_ids": dict(unit_ids), "grades": dict(grades),
              "unit_definition_keys": len(definitions), "recruit_cards": recruit_cards,
              "animations": animations, "errors": errors,
              "save_validation": "Native-derived synthetic tests only; no device save corpus."}
    (ROOT / "reverse/reports/state_format_audit.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"counts": dict(counts), "errors": len(errors)}))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
