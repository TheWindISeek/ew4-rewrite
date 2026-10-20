"""Check recovered formats against the original corpus without modifying inputs."""

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from ew4_formats import BattleFile, BitmapFont, FormatError, Pvr4444, TerrainMap, TextureIndex

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "reverse/reports")
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "reverse"):
        raise ValueError("Audit output must remain in project reverse directory")
    output.mkdir(parents=True, exist_ok=True)
    source = ROOT / "apk_work/original/assets"
    counts = Counter()
    errors = []
    battles = {}
    input_hashes = {}
    jobs = [(p, BattleFile.from_bytes, "battle") for p in sorted(source.glob("*.btl"))]
    jobs += [(p, BitmapFont.from_bytes, "font") for p in sorted(source.glob("*.fnt"))]
    jobs += [(source / (name + ".bin"), TerrainMap.from_bytes, "terrain") for name in ("europe", "america")]
    for name in ("map1", "map2"):
        for suffix, scale in (("", 1), ("_hd", 2)):
            jobs.append((source / f"{name}{suffix}.bin", lambda b, s=scale: TextureIndex.from_bytes(b, s), "texture_index"))
    jobs += [(p, Pvr4444.from_bytes, "pvr") for p in sorted((source / "map").glob("*.pvr"))]
    for path, decode, kind in jobs:
        data = path.read_bytes()
        relative = path.relative_to(source).as_posix()
        input_hashes[relative] = hashlib.sha256(data).hexdigest()
        try:
            model = decode(data)
            if model.to_bytes() != data:
                raise ValueError("Lossless round trip failed")
            counts[kind] += 1
            if isinstance(model, BattleFile):
                battles[relative] = {"header": model.header, "sections": model.summary()}
            for malformed in (data[:-1], data + b"\0", b""):
                try:
                    decode(malformed)
                except FormatError:
                    counts["malformed_rejected"] += 1
                else:
                    raise ValueError("Accepted a truncated or trailing-byte resource")
            if isinstance(model, Pvr4444) and path.name in ("map1_1.pvr", "map1_102.pvr", "map2_1@2x.pvr"):
                if Pvr4444.from_image(model.to_image(), data).to_bytes() != data:
                    raise ValueError("PVR pixel decode/encode changed original")
                counts["pixel_roundtrip"] += 1
        except (ValueError, TypeError) as error:
            errors.append({"file": relative, "error": str(error)})
    report = {"counts": dict(counts), "errors": errors, "source_sha256": input_hashes,
              "limitations": ["Round trips verify structure, not complete gameplay semantics.",
                              "BTL opaque sections remain uninterpreted and are preserved byte for byte."]}
    (output / "format_audit.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    (output / "battle_sections.json").write_text(json.dumps(battles, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"counts": dict(counts), "errors": errors}, ensure_ascii=True))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
