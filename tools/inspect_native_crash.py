"""Read-only ARM64 disassembly near EW4 crash addresses; never patch native code."""

import argparse
import hashlib
from pathlib import Path
import sys

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from capstone import Cs, CS_ARCH_ARM64, CS_MODE_ARM
from elftools.elf.elffile import ELFFile

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("addresses", nargs="+", type=lambda x: int(x, 16))
    args = parser.parse_args()
    library = ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so"
    decoder = Cs(CS_ARCH_ARM64, CS_MODE_ARM)
    lines = ["Library SHA256: " + hashlib.sha256(library.read_bytes()).hexdigest()]
    with library.open("rb") as stream:
        elf = ELFFile(stream)
        for address in args.addresses:
            start, end = address - 0x100, address + 0x64
            segment = next(s for s in elf.iter_segments()
                           if s["p_type"] == "PT_LOAD" and s["p_vaddr"] <= start
                           and end <= s["p_vaddr"] + s["p_filesz"])
            stream.seek(segment["p_offset"] + start - segment["p_vaddr"])
            data = stream.read(end - start)
            lines.append(f"\nAddress 0x{address:x}")
            lines.extend(f"{'>>' if i.address == address else '  '} {i.address:08x}  {i.mnemonic:8} {i.op_str}"
                         for i in decoder.disasm(data, start))
    output = ROOT / "build/tests/phone_crash/native-disassembly.txt"
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
