"""Collect EW4 crash evidence from an explicitly selected physical Android device."""

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import subprocess


ROOT = Path(__file__).resolve().parents[1]
ADB = Path(r"D:\Program Files\Netease\MuMu Player 12\nx_main\adb.exe")
PACKAGE = "com.easytech.ew4oldfriends"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("label")
    parser.add_argument("--serial", required=True)
    args = parser.parse_args()
    if not re.fullmatch(r"[A-Za-z0-9_-]+", args.label):
        parser.error("Invalid label")
    if args.serial.startswith("emulator-") or ":" in args.serial:
        parser.error("Select the USB physical device, not an emulator")
    output = ROOT / "build/tests/phone_crash"
    output.mkdir(parents=True, exist_ok=True)
    if (output / (args.label + ".json")).exists():
        parser.error("Choose a new label to preserve existing evidence")

    warnings = []

    def adb(*command):
        result = subprocess.run([str(ADB), "-s", args.serial, *command],
                                capture_output=True, timeout=30)
        if result.returncode:
            error = result.stderr.decode("utf-8", errors="replace").strip()
            if command[0] != "logcat" or not result.stdout:
                raise RuntimeError(error or "ADB command failed")
            warnings.append(error or "logcat returned partial output")
        return result.stdout.decode("utf-8", errors="replace")

    properties = {key: adb("shell", "getprop", key).strip() for key in (
        "ro.product.model", "ro.build.version.release", "ro.build.version.sdk",
        "ro.product.cpu.abi", "ro.build.fingerprint")}
    exit_info = adb("shell", "dumpsys", "activity", "exit-info", PACKAGE)
    (output / (args.label + ".exit-info.txt")).write_text(exit_info, encoding="utf-8")
    raw = adb("logcat", "-b", "crash", "-d", "-v", "threadtime", "-t", "500")
    # Native crash dumps use a different PID from the crashed app itself.
    blocks = re.split(r"(?=^.*(?:\*\*\* \*\*\* \*\*\*|FATAL EXCEPTION:).*$)", raw, flags=re.M)
    relevant = [b for b in blocks if PACKAGE in b or "lib64ew4_Sagiri.so" in b]
    crash = "\n".join(relevant)
    (output / (args.label + ".crash.log")).write_text(crash, encoding="utf-8")
    match = re.search(r"timestamp=.*?\bpid=(\d+)", exit_info)
    pid = match.group(1) if match else None
    if pid:
        app_log = adb("logcat", "-b", "all", "-d", "--pid=" + pid,
                      "-v", "threadtime", "-t", "500")
        (output / (args.label + ".app.log")).write_text(app_log, encoding="utf-8")
    record = {"time_utc": datetime.now(timezone.utc).isoformat(),
              "serial": args.serial, "package": PACKAGE, "properties": properties,
              "latest_exit_pid": pid, "matching_crash_blocks": len(relevant),
              "crash_bytes": len(crash.encode("utf-8")), "warnings": warnings}
    (output / (args.label + ".json")).write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(record))


if __name__ == "__main__":
    main()
