"""Index original UI layouts and archived observations without modifying source assets."""

import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "apk_work/original/assets"
SUITE = ROOT / "build/tests/frontend_reference_20261018"
OBSERVED = {
    "form_mainmenu": ["main_menu"], "form_option": ["settings"],
    "form_screen_zoom": ["screen_size"], "form_selconquest": ["conquest_menu"],
    "form_conquestlist": ["conquest_1798_countries"], "form_roundturn": ["round_start"],
    "form_game": ["battlefield", "unit_selected", "city_selected", "upgrade_quote", "training_quote"],
    "form_unitinfo": ["unit_info"], "form_generalinfo": ["general_info", "skill_tooltip"],
    "form_defense": ["defense"], "form_deployitem": ["deploy_item"],
    "form_recruitunit": ["recruit_unit"], "form_pause": ["pause"], "form_save": ["save_slots"],
}
EXCLUDED = {"form_buymedal", "form_selectpurchase", "form_servicedialog"}


def main():
    layouts = ET.parse(SOURCE / "layout-568h.xml").getroot()
    records = []
    for layout in layouts:
        name = layout.attrib["id"]
        shots = [s for s in OBSERVED.get(name, []) if (SUITE / (s + ".png")).exists()]
        records.append({"id": name, "attributes": layout.attrib,
                        "status": "excluded_payment" if name in EXCLUDED else "observed" if shots else "pending",
                        "screenshots": shots,
                        "elements": [dict(e.attrib) for e in layout.iter("Element")]})
    report = {"source": "apk_work/original/assets/layout-568h.xml", "layouts": records,
              "counts": {s: sum(r["status"] == s for r in records) for s in ("observed", "pending", "excluded_payment")}}
    (SUITE / "inventory.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(report["counts"]))


if __name__ == "__main__":
    main()
