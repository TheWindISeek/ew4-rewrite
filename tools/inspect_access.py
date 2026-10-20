"""Record evidence about resource access without patching either APK."""

import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[1]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def inspect(path):
    with zipfile.ZipFile(path) as archive:
        global_data = ET.fromstring(archive.read("assets/global_data.xml"))
        values = {}
        for group in ("GameProp", "SaveDatas"):
            values[group] = {
                node.attrib["name"]: node.attrib.get("value")
                for node in global_data.findall(f"{group}/data")
                if node.attrib["name"] in (
                    "Medal", "UnlockSlot", "CollectMedal", "AwardMedal", "Victory", "ObtainPrincess"
                )
            }
        libs = {name: archive.read(name) for name in archive.namelist() if name.endswith(".so")}
        summary = {
            "apk": path.name,
            "xml_defaults": values,
            "native_libraries": {name: {"size": len(data), "sha256": sha(data)} for name, data in libs.items()},
        }
        return summary, libs


def main():
    base_path = ROOT / "\u6b27\u9646\u6218\u4e894_1.6.6.apk"
    reference_path = ROOT / "\u5927\u93d6\u6218 \u8001\u53cb\u7248_1.6.6(1).apK"
    base, base_libs = inspect(base_path)
    reference, ref_libs = inspect(reference_path)
    first = base_libs["lib/arm64-v8a/lib64ew4_Sagiri.so"]
    second = ref_libs["lib/arm64-v8a/lib64daz1_Sagiri.so"]
    runs = []
    for offset, (left, right) in enumerate(zip(first, second)):
        if left == right:
            continue
        if runs and runs[-1]["offset"] + runs[-1]["length"] == offset:
            runs[-1]["length"] += 1
        else:
            runs.append({"offset": offset, "length": 1})
    report = {
        "base": base, "reference": reference,
        "native_comparison": {
            "same_length": len(first) == len(second),
            "differing_bytes": sum(item["length"] for item in runs),
            "contiguous_ranges": runs,
        },
        "conclusion": "Reference XML medal default is zero; native libraries differ. Exact unlock implementation remains unverified.",
        "native_code_modified_by_this_inspection": False,
        "runtime_tested": False,
    }
    output = ROOT / "apk_work" / "access_inspection.json"
    output.write_text(json.dumps(report, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    print(f"Access inspection saved: {len(runs)} native difference ranges; no patches applied")


if __name__ == "__main__":
    main()
