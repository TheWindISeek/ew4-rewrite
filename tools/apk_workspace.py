"""Maintain an extracted EW4 APK and rebuild it without touching the source APK."""

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import zipfile


ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / "apk_work"
TREE = WORK / "game"
BACKUP = WORK / "original"
META = WORK / "source.json"
BUILD = ROOT / "build"
SOURCE = ROOT / "\u6b27\u9646\u6218\u4e894_1.6.6.apk"
SIGNATURE = re.compile(r"META-INF/(?:MANIFEST\.MF|[^/]+\.(?:SF|RSA|DSA|EC))$", re.I)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def save_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")


def safe_name(name):
    parts = PurePosixPath(name).parts
    if not parts or name.startswith("/") or "\\" in name or any(
        p in ("..", ".") or ":" in p or p.endswith((" ", ".")) for p in parts
    ):
        raise ValueError(f"Unsafe APK entry: {name}")
    return parts


def initialize():
    if WORK.exists():
        raise RuntimeError("apk_work already exists; initialization never overwrites it")
    entries = []
    with zipfile.ZipFile(SOURCE) as archive:
        names = [i.filename for i in archive.infolist() if not i.is_dir()]
        if len({n.casefold() for n in names}) != len(names):
            raise ValueError("Duplicate or case-colliding APK entries")
        for name in names:
            safe_name(name)
        # Validate every member before creating the persistent workspace.
        if archive.testzip() is not None:
            raise ValueError("Source APK failed CRC verification")
        for info in archive.infolist():
            if info.is_dir():
                continue
            data = archive.read(info)
            path = BACKUP.joinpath(*safe_name(info.filename))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            entries.append({
                "name": info.filename,
                "size": len(data),
                "sha256": digest(data),
                "compression": info.compress_type,
                "date_time": info.date_time,
                "external_attr": info.external_attr,
            })
    shutil.copytree(BACKUP, TREE)
    metadata = {"source": SOURCE.name, "sha256": digest(SOURCE.read_bytes()), "entries": entries}
    save_json(META, metadata)
    for entry in entries:
        for folder in (BACKUP, TREE):
            if digest((folder / entry["name"]).read_bytes()) != entry["sha256"]:
                raise ValueError(f"Extracted file mismatch: {entry['name']}")
    save_json(WORK / "extraction_check.json", {
        "entries_per_tree": len(entries), "verified_trees": 2, "errors": 0,
        "source_sha256": metadata["sha256"],
    })
    print(f"Extracted and verified {len(entries)} files in each tree; source unchanged")


def run(args):
    subprocess.run([str(arg) for arg in args], check=True)


def build(args):
    metadata = json.loads(META.read_text(encoding="utf-8"))
    original = {entry["name"]: entry for entry in metadata["entries"]}
    files = {p.relative_to(TREE).as_posix(): p for p in TREE.rglob("*") if p.is_file()}
    if any(p.is_symlink() or not p.resolve().is_relative_to(TREE.resolve()) for p in files.values()):
        raise ValueError("Working tree contains linked files outside the tree")
    restored = set()
    for rule in args.restore_original:
        safe_name(rule.rstrip("/"))
        matches = [name for name in original
                   if name == rule or (rule.endswith("/") and name.startswith(rule))]
        if not matches:
            raise ValueError("No original entries match: " + rule)
        for name in matches:
            path = BACKUP / name
            if path.is_symlink() or not path.resolve().is_relative_to(BACKUP.resolve()):
                raise ValueError("Unsafe original entry: " + name)
            if not path.is_file() or digest(path.read_bytes()) != original[name]["sha256"]:
                raise ValueError("Original backup differs from source: " + name)
            files[name] = path
            restored.add(name)
    if not {"AndroidManifest.xml", "classes.dex", "resources.arsc"}.issubset(files):
        raise ValueError("Working tree is missing required APK files")
    BUILD.mkdir(exist_ok=True)
    output_name = args.name
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]*", output_name):
        raise ValueError("Invalid build name")
    unsigned = BUILD / f"{output_name}-unsigned.apk"
    changed, added, skipped = [], [], []
    expected = {}
    with zipfile.ZipFile(unsigned, "w", allowZip64=False) as archive:
        for name in sorted(files):
            safe_name(name)
            if SIGNATURE.fullmatch(name):
                skipped.append(name)
                continue
            old = original.get(name)
            data = files[name].read_bytes()
            sha = digest(data)
            expected[name] = sha
            if old is None:
                added.append(name)
            elif sha != old["sha256"]:
                changed.append(name)
            info = zipfile.ZipInfo(name, tuple(old["date_time"]) if old else (2026, 1, 1, 0, 0, 0))
            info.compress_type = old["compression"] if old else zipfile.ZIP_DEFLATED
            info.external_attr = old["external_attr"] if old else 0o100644 << 16
            archive.writestr(info, data)
    final = unsigned
    signed = False
    if args.sign:
        if not args.android_tools:
            raise ValueError("--sign requires --android-tools (official Android build-tools directory)")
        android = args.android_tools.resolve()
        aligner = android / "zipalign.exe"
        signer = android / "lib" / "apksigner.jar"
        if not aligner.is_file() or not signer.is_file():
            raise ValueError("zipalign.exe or lib/apksigner.jar is missing")
        key = args.keystore.resolve()
        if not key.exists():
            key.parent.mkdir(parents=True, exist_ok=True)
            run(["keytool", "-genkeypair", "-keystore", key, "-storepass", "android",
                 "-keypass", "android", "-alias", "ew4-test", "-dname", "CN=EW4 Local Test",
                 "-keyalg", "RSA", "-keysize", "2048", "-validity", "10000", "-storetype", "JKS"])
        aligned = BUILD / f"{output_name}-aligned.apk"
        run([aligner, "-P", "16", "-f", "4", unsigned, aligned])
        final = BUILD / f"{output_name}.apk"
        run(["java", "-jar", signer, "sign", "--ks", key, "--ks-key-alias", "ew4-test",
             "--ks-pass", "pass:android", "--key-pass", "pass:android", "--out", final, aligned])
        run(["java", "-jar", signer, "verify", "--verbose", final])
        run([aligner, "-c", "-P", "16", "4", final])
        signed = True
    with zipfile.ZipFile(final) as archive:
        if archive.testzip() is not None:
            raise ValueError("Rebuilt APK failed CRC verification")
        actual = {i.filename for i in archive.infolist() if not i.is_dir() and not SIGNATURE.fullmatch(i.filename)}
        if actual != set(expected):
            raise ValueError("Rebuilt APK file list differs from the working tree")
        for name, sha in expected.items():
            if digest(archive.read(name)) != sha:
                raise ValueError(f"Rebuilt APK content mismatch: {name}")
    removed = sorted(set(original) - set(files))
    save_json(BUILD / ("build_report.json" if output_name == "ew4-dev" else f"{output_name}-report.json"), {
        "apk": str(final.relative_to(ROOT)), "sha256": digest(final.read_bytes()),
        "signed_with_local_test_key": signed, "runtime_tested": False,
        "verified_entries": len(expected), "errors": 0,
        "changed": changed, "added": added, "removed": removed, "discarded_old_signatures": skipped,
        "restored_original_entries": sorted(restored),
        "source_apk_unchanged": digest(SOURCE.read_bytes()) == metadata["sha256"],
    })
    print(f"Built {final}; {len(expected)} entries verified; changed={len(changed)}, added={len(added)}, removed={len(removed)}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("init")
    command = commands.add_parser("build")
    command.add_argument("--name", default="ew4-dev")
    command.add_argument("--restore-original", action="append", default=[], metavar="PATH",
                         help="Use an original file or directory prefix ending in / for this build only")
    command.add_argument("--sign", action="store_true")
    command.add_argument("--android-tools", type=Path)
    command.add_argument("--keystore", type=Path, default=Path.home() / ".codex" / "keys" / "ew4-test.jks")
    args = parser.parse_args()
    if args.command == "init":
        initialize()
    else:
        build(args)


if __name__ == "__main__":
    main()
