"""Save screenshots and app-specific diagnostics from the isolated EW4 emulator."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess


ROOT = Path(__file__).resolve().parents[1]
ADB = Path(r"D:\Program Files\Netease\MuMu Player 12\nx_main\adb.exe")
PACKAGE = "com.easytech.ew4oldfriends"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("label")
    parser.add_argument("--serial", default="127.0.0.1:16416")
    parser.add_argument("--suite", default="baseline")
    parser.add_argument("--apk", type=Path, default=ROOT / "build/ew4-dev.apk")
    args = parser.parse_args()
    if not args.label.replace("_", "").replace("-", "").isalnum():
        parser.error("label must be alphanumeric, underscores or hyphens")
    if not args.suite.replace("_", "").replace("-", "").isalnum():
        parser.error("suite must be alphanumeric, underscores or hyphens")
    output = ROOT / "build" / "tests" / args.suite
    output.mkdir(parents=True, exist_ok=True)

    def adb(*command):
        return subprocess.run([str(ADB), "-s", args.serial, *command], check=True, capture_output=True).stdout

    png = adb("exec-out", "screencap", "-p")
    if not png.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("ADB did not return a PNG screenshot")
    (output / f"{args.label}.png").write_bytes(png)
    pid = adb("shell", "pidof", PACKAGE).decode().strip()
    logs = adb("logcat", "-d", f"--pid={pid}", "-t", "120").decode("utf-8", errors="replace")
    (output / f"{args.label}.log").write_text(logs, encoding="utf-8")
    record = {
        "time_utc": datetime.now(timezone.utc).isoformat(),
        "serial": args.serial, "package": PACKAGE, "pid": pid,
        "apk_sha256": hashlib.sha256(args.apk.read_bytes()).hexdigest(),
        "screenshot": f"{args.label}.png", "app_log": f"{args.label}.log",
    }
    (output / f"{args.label}.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(f"Saved {args.label}: screenshot and app-specific diagnostics; process {pid} alive")


if __name__ == "__main__":
    main()
