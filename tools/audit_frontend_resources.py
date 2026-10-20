"""Index source resources and evidence of use; unresolved is never called unused."""
import csv
import fnmatch
import json
from collections import Counter, defaultdict
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "apk_work/original/assets"
OUTPUT = ROOT / "reverse/reports/frontend_resources.json"
ART = {".png", ".pkm", ".pvr", ".webp", ".fnt", ".wav", ".mp3"}


def main():
    nodes, references, errors, animation_gaps = {}, [], [], []
    paths = {p.relative_to(SOURCE).as_posix(): p for p in SOURCE.rglob("*") if p.is_file()}
    by_name = defaultdict(list)
    for name, path in paths.items():
        by_name[path.name].append(name)
        nodes[name] = {"kind": "file", "size": path.stat().st_size,
                       "art": path.suffix.lower() in ART, "evidence": []}
    sprites, layouts, native = defaultdict(list), {}, []
    xml = {}
    for name, path in paths.items():
        if path.suffix != ".xml":
            continue
        text = path.read_text(encoding="utf-8-sig")
        try:
            doc = ET.fromstring(text if not text.lstrip().startswith("<Texture") else "<atlas>" + text + "</atlas>")
        except ET.ParseError as error:
            errors.append({"file": name, "error": str(error)})
            continue
        xml[name] = doc
        if doc.tag == "atlas":
            for image in doc.findall("Images/Image"):
                key = name + "::" + image.attrib["name"]
                nodes[key] = {"kind": "sprite", "atlas": name,
                              "rect": dict(image.attrib), "evidence": []}
                sprites[image.attrib["name"]].append(key)
            for texture in doc.findall("Texture"):
                target = texture.attrib.get("name") or texture.attrib.get("file")
                if target:
                    references.append((name, target, "atlas-texture"))
        for layout in doc.findall("Layout"):
            key = name + "::" + layout.attrib.get("id", "")
            nodes[key] = {"kind": "layout", "attributes": dict(layout.attrib), "evidence": []}
            layouts.setdefault(layout.attrib.get("id"), []).append(key)

    def add(source, value, relation):
        targets = list(by_name.get(value, [])) + list(sprites.get(value, [])) + list(layouts.get(value, []))
        if not targets and "%" in value:
            pattern = re.sub(r"%[-+0-9.]*[dis]", "*", value)
            targets = [n for n in by_name if fnmatch.fnmatchcase(n, pattern)]
            targets = [t for n in targets for t in by_name[n]] + [t for n in sprites if fnmatch.fnmatchcase(n, pattern) for t in sprites[n]]
            relation += "-pattern"
        ambiguous = len(targets) > 1 and relation not in ("font", "atlas-texture")
        for target in targets:
            nodes[target]["evidence"].append({"source": source, "relation": relation, "value": value,
                                          "candidate": ambiguous or relation.endswith("-pattern")})
        return targets

    for name, doc in xml.items():
        def walk(element, owner):
            if element.tag == "Layout":
                owner = name + "::" + element.attrib.get("id", "")
            if element.tag == "Image" and "refx" in element.attrib:
                return
            for attr, value in element.attrib.items():
                if attr == "id":
                    continue
                add(owner, value, "xml:" + element.tag + "@" + attr)
                # Fonts are referenced by stem and backed by FNT + image resources.
                if attr == "font":
                    add(owner, value + ".fnt", "font")
            for child in element:
                walk(child, owner)
        walk(doc, name)
    for source, value, relation in references:
        add(source, value, relation)
    fonts = xml.get("font_resource.xml")
    if fonts is not None:
        for device in fonts:
            for mode in device:
                for font in mode:
                    stem = font.attrib["name"]
                    for language in ("cn", "en") if font.attrib.get("lang") == "true" else (None,):
                        variant = stem + ("_" + language if language else "") + ("_hd" if font.attrib.get("filehd") == "true" else "")
                        for suffix in (".fnt", ".png"):
                            add("font_resource.xml::" + device.tag + "/" + mode.tag, variant + suffix, "font-config")

    for path in sorted((ROOT / "rewrite/data/legacy/animations").glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        library = path.stem
        animation_sprites = {s["name"] for s in data["atlas"]["sprites"]}
        referenced = {child for item in data["items"] if not item["kind"]
                      for layer in item["layers"] for frame in layer for _, child in frame["indices"] if child >= 0}
        atlas_name = library + ".xml"
        for index, item in enumerate(data["items"]):
            if item["kind"]:
                name = item["name"] + ".png"
                if name not in animation_sprites:
                    animation_gaps.append({"animation": library, "missingSprite": name,
                                           "referencedByComposite": index in referenced})
                else:
                    key = atlas_name + "::" + name
                    if key not in nodes:
                        errors.append({"animation": library, "missingAtlasEntry": key})
                    else:
                        nodes[key]["evidence"].append({"source": library + ".bin::" + item["name"],
                            "relation": "animation-leaf", "value": name, "candidate": False})
        add(library + ".bin", library + ".xml", "animation-atlas")
    refs = ROOT / "reverse/native/frontend_resource_refs.tsv"
    if refs.exists():
        with refs.open(encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream, delimiter="\t"):
                targets = add("native:" + row["function"], row["resource"], "native-reference" if row["function"] else "native-string-only")
                native.append({**row, "targets": targets})

    families = defaultdict(list)
    for name in paths:
        base = re.sub(r"(_hd[0-9]*|@2x|_ipad)(?=\.)", "", name, flags=re.I)
        families[base].append(name)
    for family in families.values():
        if len(family) > 1:
            for name in family:
                nodes[name]["variants"] = [n for n in family if n != name]
            for name in family:
                for variant in family:
                    if name != variant and any(not e.get("candidate") and e["relation"] != "native-string-only" for e in nodes[variant]["evidence"]):
                        nodes[name]["evidence"].append({"source": variant, "relation": "resolution-variant", "value": name, "candidate": True})
    status = Counter()
    for name, node in nodes.items():
        usages = [e for e in node["evidence"] if e["relation"] != "native-string-only"]
        node["status"] = "referenced" if any(not e.get("candidate") for e in usages) else "candidate" if usages else "unresolved"
        # This is only a narrow payment-art candidate label, not a deletion rule.
        if re.search(r"iapp|form_buymedal|form_selectpurchase", name, re.I):
            node["paymentReview"] = True
        status[(node["kind"], node["status"])] += 1
    report = {"schema": 1, "counts": {kind + ":" + state: count for (kind, state), count in sorted(status.items())},
              "sourceFiles": len(paths), "nativeReferences": len(native), "errors": errors,
              "animationGaps": animation_gaps,
              "scope": "Static resource-use evidence, not proof of complete display semantics or runtime reachability. Unresolved does not mean unused. Pattern references are candidates.",
              "nodes": nodes, "native": native}
    OUTPUT.write_text(json.dumps(report, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    summary = {k: report[k] for k in ("counts", "sourceFiles", "nativeReferences")}
    summary["errors"] = len(errors)
    summary["animationGaps"] = len(animation_gaps)
    summary["referencedAnimationGaps"] = sum(g["referencedByComposite"] for g in animation_gaps)
    summary["artStatus"] = dict(Counter(node["status"] for node in nodes.values() if node.get("art")))
    (ROOT / "reverse/reports/frontend_resources_summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
