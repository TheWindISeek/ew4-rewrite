"""Compare recovered formulas with isolated original ARM64 functions in memory.

This is not an Android APK, phone, graphics, or complete-game compatibility test.
"""

from collections import Counter
import json
from pathlib import Path
import random
import struct
import sys

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from elftools.elf.elffile import ELFFile
from unicorn import Uc, UC_ARCH_ARM64, UC_MODE_ARM, UC_HOOK_CODE
from unicorn import arm64_const as registers

ROOT = Path(__file__).resolve().parents[2]
SCRATCH = 0x5000000
STACK = 0x4000000
RETURN = 0x6000000
RNG_STATE = 0x221858
HEALTH_THRESHOLDS = (5, 15, 30, 50, 65, 80)


class NativeProbe:
    def __init__(self):
        self.cpu = Uc(UC_ARCH_ARM64, UC_MODE_ARM)
        with (ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so").open("rb") as stream:
            elf = ELFFile(stream)
            segments = [s for s in elf.iter_segments() if s["p_type"] == "PT_LOAD"]
            end = max(s["p_vaddr"] + s["p_memsz"] for s in segments)
            self.cpu.mem_map(0, (end + 4095) & ~4095)
            for segment in segments:
                self.cpu.mem_write(segment["p_vaddr"], segment.data())
            for section in elf.iter_sections():
                if section["sh_type"] == "SHT_RELA":
                    for relocation in section.iter_relocations():
                        if relocation["r_info_type"] == 1027:
                            self.write(relocation["r_offset"], "Q", relocation["r_addend"])
        self.cpu.mem_map(STACK, 0x10000)
        self.cpu.mem_map(SCRATCH, 0x10000)
        self.cpu.mem_map(RETURN, 4096)
        self.cpu.mem_map(RETURN + 0x10000, 4096)
        self.import_stubs = 0

    def hook_import(self, name, callback):
        """Route an ELF import through a named, observable host stub."""
        address = RETURN + 0x10000 + self.import_stubs * 4
        self.import_stubs += 1
        if self.import_stubs > 1024:
            raise ValueError("Import stub page exhausted")
        found = False
        with (ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so").open("rb") as stream:
            elf = ELFFile(stream)
            for section in elf.iter_sections():
                if section["sh_type"] != "SHT_RELA":
                    continue
                symbols = elf.get_section(section["sh_link"])
                for relocation in section.iter_relocations():
                    if relocation["r_info_sym"] and symbols.get_symbol(relocation["r_info_sym"]).name == name:
                        self.write(relocation["r_offset"], "Q", address)
                        found = True
        if not found:
            raise ValueError(f"ELF import not found: {name}")
        return self.hook(address, callback)

    def hook(self, address, callback, *, floating=False):
        def dispatch(cpu, pc, size, user_data):
            args = [cpu.reg_read(getattr(registers, f"UC_ARM64_REG_X{i}")) for i in range(8)]
            result = callback(*args)
            if floating:
                cpu.reg_write(registers.UC_ARM64_REG_S0, struct.unpack("<I", struct.pack("<f", result))[0])
            else:
                cpu.reg_write(registers.UC_ARM64_REG_X0, result & ((1 << 64) - 1))
            cpu.reg_write(registers.UC_ARM64_REG_PC, cpu.reg_read(registers.UC_ARM64_REG_X30))
        return self.cpu.hook_add(UC_HOOK_CODE, dispatch, begin=address, end=address)

    def write(self, address, fmt, *values):
        self.cpu.mem_write(address, struct.pack("<" + fmt, *values))

    def read(self, address, fmt):
        return struct.unpack("<" + fmt, self.cpu.mem_read(address, struct.calcsize("<" + fmt)))

    def call(self, address, *arguments):
        for index in range(8):
            self.cpu.reg_write(getattr(registers, f"UC_ARM64_REG_X{index}"),
                               (arguments[index] if index < len(arguments) else 0) & ((1 << 64) - 1))
        self.cpu.reg_write(registers.UC_ARM64_REG_SP, STACK + 0xf000)
        self.cpu.reg_write(registers.UC_ARM64_REG_X30, RETURN)
        self.cpu.emu_start(address, RETURN, count=100_000)
        if self.cpu.reg_read(registers.UC_ARM64_REG_PC) != RETURN:
            raise RuntimeError(f"Native function {address:x} exceeded instruction budget")
        value = self.cpu.reg_read(registers.UC_ARM64_REG_X0) & 0xffffffff
        return value if value < 0x80000000 else value - 0x100000000


def expected_neighbor(width, height, cell, direction):
    col, row = cell % width, cell // width
    offsets = ((1, 0), (row % 2, 1), (row % 2 - 1, 1),
               (-1, 0), (row % 2 - 1, -1), (row % 2, -1))
    dx, dy = offsets[direction]
    col, row = col + dx, row + dy
    return col + row * width if 0 <= col < width and 0 <= row < height else -1


def expected_distance(col1, row1, col2, row2):
    q1, q2 = col1 - (row1 - row1 % 2) // 2, col2 - (row2 - row2 % 2) // 2
    dq, dr = q2 - q1, row2 - row1
    return max(abs(dq), abs(dr), abs(dq + dr))


def expected_attack_count(health, maximum, grade, dense_fire=False):
    percent = health * 100 // maximum if maximum else 0
    if dense_fire:
        percent = 100
    for index in range(grade + 3, max(-1, grade - 4), -1):
        if HEALTH_THRESHOLDS[index] < percent:
            return index + 2
    return 1


def main():
    probe = NativeProbe()
    rng = random.Random(1946)
    counts = Counter()
    errors = []

    def check(group, actual, expected, inputs):
        counts[group] += 1
        if actual != expected:
            errors.append({"group": group, "actual": actual, "expected": expected, "inputs": inputs})
            if len(errors) >= 10:
                raise AssertionError(errors)

    map_object = SCRATCH
    for width, height in ((1, 1), (2, 3), (55, 76), (79, 68), (130, 90)):
        probe.write(map_object + 0x100, "2i", width, height)
        for _ in range(100):
            col, row = rng.randrange(-1, width + 1), rng.randrange(-1, height + 1)
            expected = col + row * width if 0 <= col < width and 0 <= row < height else -1
            check("grid_index", probe.call(0x851c4, map_object, col, row), expected, [width, height, col, row])
            cell = rng.randrange(width * height)
            for direction in range(6):
                check("hex_neighbors", probe.call(0x859e0, map_object, cell, direction),
                      expected_neighbor(width, height, cell, direction), [width, height, cell, direction])
    for _ in range(1000):
        points = [rng.randrange(200) for _ in range(4)]
        check("hex_distance", probe.call(0x869ec, map_object, *points), expected_distance(*points), points)

    unit, definition, general, general_definition = (SCRATCH + i * 0x1000 for i in range(1, 5))
    probe.write(unit + 8, "Q", definition)
    probe.write(general + 8, "Q", general_definition)
    probe.write(general + 0x44, "2i", -1, -1)
    for grade in range(3):
        probe.write(definition + 0x10, "i", grade)
        for maximum in (0, 60, 90, 110, 165):
            for health in range(maximum + 1):
                probe.write(unit + 0x20, "2i", health, maximum)
                for mode in ("no_general", "general", "dense_infantry", "dense_cavalry"):
                    probe.write(unit + 0x18, "Q", 0 if mode == "no_general" else general)
                    probe.write(definition + 0xc, "i", int(mode == "dense_cavalry"))
                    probe.write(general + 0x40, "i", int(mode.startswith("dense")))
                    probe.write(general_definition + 0x34, "i", 15)
                    check("attack_count", probe.call(0x5318c, unit),
                          expected_attack_count(health, maximum, grade, mode == "dense_infantry"),
                          [health, maximum, grade, mode])
    for seed in (0, 1, 1946, 0xffffffff, 0xffffffffffffffff):
        probe.write(RNG_STATE, "Q", seed)
        for bound in (0, 1, 2, 5, 10, 100, 32768):
            if bound:
                seed = (seed * 214013 + 2531011) & ((1 << 64) - 1)
                expected = ((seed >> 16) & 32767) % bound
            else:
                expected = 0
            check("native_random", probe.call(0xef754, bound), expected, [seed, bound])
            check("native_random_state", probe.read(RNG_STATE, "Q")[0], seed, [bound])

    report = {"method": "Original ARM64 bytes executed by Unicorn; no APK or phone involved.",
              "scope": "Pure coordinate, attack-count, and RNG functions; not complete combat or movement legality.",
              "counts": dict(counts), "errors": errors}
    output = ROOT / "reverse/reports/native_rule_validation.json"
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"counts": dict(counts), "errors": len(errors)}))
    if errors:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
