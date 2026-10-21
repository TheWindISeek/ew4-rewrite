"""Check a standalone Web publish against the current runtime and author docs."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("directory")
parser.add_argument("--report", required=True)
args = parser.parse_args()
web = ROOT / args.directory
checks = 0


def check(condition, message):
    global checks
    assert condition, message
    checks += 1


for name in ("Ew4.Web.exe", "MoonSharp.Interpreter.dll", "hostfxr.dll", "coreclr.dll",
             "Start-Web.cmd", "Open-Web-When-Ready.cmd"):
    check((web / name).is_file(), "Missing standalone dependency: " + name)
check(not (web / "saves").exists(), "Player saves must not be distributed")
for source_dir, destination in (("rewrite/Ew4.Web/wwwroot", "wwwroot"),
                                ("rewrite/mod-kit/docs", "docs"),
                                ("rewrite/mod-kit/examples", "examples")):
    for source in (ROOT / source_dir).rglob("*"):
        if source.is_file():
            target = web / destination / source.relative_to(ROOT / source_dir)
            check(target.is_file() and target.read_bytes() == source.read_bytes(), "Stale file: " + str(target))
index = json.loads((ROOT / "rewrite/data/runtime-content.json").read_text(encoding="utf-8"))
for entry in index["files"]:
    data = (web / "data" / entry["path"]).read_bytes()
    check(hashlib.sha256(data).hexdigest() == entry["sha256"], "Runtime hash: " + entry["path"])
coverage = json.loads((web / "data/client/fonts/coverage.json").read_text(encoding="utf-8"))
expected = {face["file"] for face in coverage["faces"]}
actual = {p.relative_to(web / "data/client/fonts").as_posix() for p in (web / "data/client/fonts").rglob("*.woff2")}
check(expected == actual, "Missing or superseded fonts in publish")
for face in coverage["faces"]:
    check(hashlib.sha256((web / "data/client/fonts" / face["file"]).read_bytes()).hexdigest() == face["sha256"], "Font hash: " + face["file"])
report = dict(checks=checks, errors=0, directory=args.directory, playerSaves=False,
              scope="Exact frontend, runtime hashes, font inventory, author docs/examples, standalone dependencies")
(ROOT / args.report).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report))
