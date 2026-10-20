"""Capture original border quads and coast-fill vertices at the renderer boundary.

The original ARM64 functions execute in Unicorn. Resource IO and graphics calls
are intercepted; this establishes draw submissions, not Android pixel parity.
"""
import hashlib
import json
import random
import struct

from validate_native_rules import NativeProbe, ROOT, SCRATCH
from unicorn import arm64_const as registers
from PIL import Image


def main():
    p = NativeProbe()
    renderer, tile, neighbors, country, resources, texture = (
        SCRATCH, SCRATCH + 0x9000, SCRATCH + 0xa000,
        SCRATCH + 0xb000, SCRATCH + 0xc000, SCRATCH + 0xd000)
    scale_address = p.read(0x220e90, "Q")[0]
    p.write(renderer + 0x10, "2Q", resources, SCRATCH + 0xe000)
    p.write(tile + 8, "3i", 0, 0, 0)
    p.write(tile + 0x14, "B", 1)
    p.write(tile + 0x68, "Q", country)
    draws = []
    present = [True] * 6

    def neighbor(_, cell, direction, *args):
        return neighbors + direction * 0x100 if present[direction] else 0

    def guard_release(address, *args):
        p.write(address, "B", 1)
        return 0

    def capture(_, vertices, *args):
        draws.append([list(p.read(vertices + i * 24, "2fI4x2f")) for i in range(4)])
        return 0

    def capture_triangle(_, vertices, *args):
        draws.append([list(p.read(vertices + i * 24, "2fI4x2f")) for i in range(3)])
        return 0

    p.hook_import("__cxa_guard_acquire", lambda *args: 1)
    p.hook_import("__cxa_guard_release", guard_release)
    p.hook(0x85aa8, neighbor)
    for address in (0xc0568, 0xc05f0, 0xbf0f8, 0xbf154, 0xbf130, 0xef88c):
        p.hook(address, lambda *args: 0)
    p.hook(0xc00d0, lambda *args: resources)
    p.hook(0xc2940, lambda *args: texture)
    p.hook(0x2f09c, lambda *args: 0)
    p.hook(0x938a4, lambda *args: 0)
    p.hook(0xbf1a4, lambda *args: 1)
    p.hook(0x26e30, lambda *args: SCRATCH + 0xf000)

    def read_empty(_, destination, size, *args):
        p.cpu.mem_write(destination, bytes(size))
        return 0

    p.hook(0xbf640, read_empty)
    p.hook(0xc0ac4, capture)
    p.hook(0xc0a1c, capture_triangle)
    borders = {}
    checks = 0
    for ratio in (1, 2):
        p.write(scale_address, "f", ratio)
        # c8660 pads PNG storage to powers of two, then halves HD logical sizes.
        with Image.open(ROOT / "apk_work/original/assets/image_boundary.png") as bitmap:
            p.write(texture + 4, "2i", *(1 << (size - 1).bit_length() for size in bitmap.size))
        for register in (registers.UC_ARM64_REG_S0, registers.UC_ARM64_REG_S1,
                         registers.UC_ARM64_REG_S2, registers.UC_ARM64_REG_S3):
            p.cpu.reg_write(register, 0)
        p.call(0x94da8, renderer, 1)
        cases = []
        for mask in range(64):
            for direction in range(6):
                address = neighbors + direction * 0x100
                x, y = ((64, 0), (32, 54), (-32, 54), (-64, 0), (-32, -54), (32, -54))[direction]
                p.write(address + 8, "3i", direction + 1, x, y)
                p.write(address + 0x14, "B", 1)
                p.write(address + 0x1c, "B", 0)
                p.write(address + 0x68, "Q", country + (8 if mask & (1 << direction) else 0))
            draws.clear()
            p.call(0x95414, renderer, tile)
            assert len(draws) == mask.bit_count(), (ratio, mask, len(draws))
            cases.append(json.loads(json.dumps(draws)))
            checks += 1
        borders[str(ratio)] = cases
    # Native coastal fill uses wider cells and interpolates ownership into sea.
    p.write(scale_address, "f", 1)
    draws.clear()
    p.call(0x961b4, renderer, tile, 0xaabbccdd)
    land = json.loads(json.dumps(draws))
    assert len(land) == 4
    p.write(country + 0x50, "I", 0x8899aabb)
    for direction in range(6):
        p.write(neighbors + direction * 0x100 + 0x68, "Q", country)
    draws.clear()
    p.call(0x9641c, renderer, tile)
    sea = json.loads(json.dumps(draws))
    assert len(sea) == 6
    coast_cases = []
    rng = random.Random(20261018)
    for case in range(256):
        states = [rng.randrange(6) for _ in range(6)]
        descriptions = []
        for direction, state in enumerate(states):
            address = neighbors + direction * 0x100
            present[direction] = state != 0
            active, ocean = state != 2, state == 1
            color = (0x8899aabb if state == 4 else 0x99cc7722 if state == 5 else 0)
            owner = country + (0x200 if state == 5 else 0) if color else 0
            p.write(address + 0x14, "B", active)
            p.write(address + 0x1c, "B", ocean)
            p.write(address + 0x68, "Q", owner)
            if owner:
                p.write(owner + 0x50, "I", color)
            descriptions.append({"active": active, "sea": ocean, "color": color} if present[direction] else None)
        draws.clear()
        p.call(0x9641c, renderer, tile)
        coast_cases.append({"neighbors": descriptions, "triangles": json.loads(json.dumps(draws))})
    # Observe the marker and sprite placement functions with distinct handles.
    marker_draws = []
    def sprite_draw(handle, *args):
        x = struct.unpack('<f', struct.pack('<I', p.cpu.reg_read(registers.UC_ARM64_REG_S0)))[0]
        y = struct.unpack('<f', struct.pack('<I', p.cpu.reg_read(registers.UC_ARM64_REG_S1)))[0]
        marker_draws.append([handle, x, y])
        return 0
    p.hook(0xc31c8, sprite_draw)
    for i in range(3):
        p.write(renderer + 0x298 + i * 8, 'Q', i + 1)
    for i in range(1, 4):
        p.call(0x60da0, renderer, i)
    assert [v[0] for v in marker_draws] == [1, 2, 3]
    library = ROOT / "apk_work/original/lib/arm64-v8a/lib64ew4_Sagiri.so"
    data = {"schema": 1, "sourceSha256": hashlib.sha256(library.read_bytes()).hexdigest(),
            "functions": {"border": "95414", "borderUV": "94da8", "ownedFill": "961b4", "coastFill": "9641c"},
            "borders": borders, "ownedFill": land, "coastFill": sea, "coastCases": coast_cases,
            "borderStorage": {"logicalWidth": 128, "logicalHeight": 64, "hdWidth": 256, "hdHeight": 128},
            "facilitySprites": ["marker_trade.png", "marker_shop.png", "marker_bar.png"],
            "scope": "Native draw submissions; graphics driver and Android rendering not executed."}
    output = ROOT / "reverse/reports/frontend_draw_vectors.json"
    output.write_text(json.dumps(data, separators=(",", ":")) + "\n", encoding="utf-8")
    print(json.dumps({"borderMasks": checks, "ownedTriangles": len(land), "coastTriangles": len(sea), "mixedCoasts": len(coast_cases), "errors": 0}))


if __name__ == "__main__":
    main()
