#!/usr/bin/env python3
"""Compare Native and Godot synthetic Cubism alpha-blend RGBA8 images."""

import json
import math
import struct
import sys
import zlib
from pathlib import Path

WIDTH = 96
HEIGHT = 96
MODES = ("over", "atop", "out", "conjoint_over", "disjoint_over")


def png(path: Path, width: int, height: int, rgba: bytes) -> None:
    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    rows = b"".join(b"\0" + rgba[y * width * 4:(y + 1) * width * 4] for y in range(height))
    path.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(rows, 9))
        + chunk(b"IEND", b"")
    )


def join(images: list[bytes]) -> bytes:
    result = bytearray(WIDTH * len(images) * HEIGHT * 4)
    output_width = WIDTH * len(images)
    for panel, pixels in enumerate(images):
        for y in range(HEIGHT):
            source = y * WIDTH * 4
            target = (y * output_width + panel * WIDTH) * 4
            result[target:target + WIDTH * 4] = pixels[source:source + WIDTH * 4]
    return bytes(result)


def main() -> int:
    if len(sys.argv) != 4:
        print("usage: compare_alpha_blend_pixels.py NATIVE_DIR GODOT_DIR ARTIFACT_DIR", file=sys.stderr)
        return 2
    native_dir, godot_dir, artifact_dir = map(Path, sys.argv[1:])
    artifact_dir.mkdir(parents=True, exist_ok=True)
    expected_size = WIDTH * HEIGHT * 4
    native_images, godot_images, diff_images = [], [], []
    metrics = {}

    for mode in MODES:
        native = (native_dir / f"{mode}.rgba").read_bytes()
        godot = (godot_dir / f"{mode}.rgba").read_bytes()
        if len(native) != expected_size or len(godot) != expected_size:
            raise ValueError(f"{mode}: expected {expected_size} bytes")
        differences = [abs(a - b) for a, b in zip(native, godot)]
        squared = sum(value * value for value in differences)
        mean = sum(differences) / len(differences)
        maximum = max(differences)
        psnr = math.inf if squared == 0 else 10 * math.log10(255 * 255 / (squared / len(differences)))
        metrics[mode] = {"pixels": WIDTH * HEIGHT, "max_8bit": maximum, "mean_8bit": mean, "psnr_db": psnr}
        native_images.append(native)
        godot_images.append(godot)
        amplified = bytearray(expected_size)
        for pixel in range(WIDTH * HEIGHT):
            for channel in range(3):
                amplified[pixel * 4 + channel] = min(255, differences[pixel * 4 + channel] * 16)
            amplified[pixel * 4 + 3] = 255
        diff_images.append(bytes(amplified))

    output_width = WIDTH * len(MODES)
    png(artifact_dir / "alpha-blend-native.png", output_width, HEIGHT, join(native_images))
    png(artifact_dir / "alpha-blend-godot.png", output_width, HEIGHT, join(godot_images))
    png(artifact_dir / "alpha-blend-diff-16x.png", output_width, HEIGHT, join(diff_images))
    (artifact_dir / "alpha-blend-metrics.json").write_text(json.dumps(metrics, indent=2, allow_nan=True) + "\n")

    lines = [
        "# Isolated Cubism 5 alpha blend pixel comparison", "",
        "Native and Godot each rendered the same 96×96 RGBA input grid without offscreen groups.",
        "The five panels are Over, Atop, Out, Conjoint Over, and Disjoint Over.", "",
        "| Mode | Pixels | Maximum error | Mean error | PSNR |", "|---|---:|---:|---:|---:|",
    ]
    for mode in MODES:
        item = metrics[mode]
        psnr_text = "∞" if math.isinf(item["psnr_db"]) else f'{item["psnr_db"]:.6f} dB'
        lines.append(f'| {mode} | {item["pixels"]} | {item["max_8bit"]}/255 | {item["mean_8bit"]:.8f}/255 | {psnr_text} |')
    lines += ["", "The difference image is amplified 16×.", ""]
    (artifact_dir / "alpha-blend-pixel-report.md").write_text("\n".join(lines))

    print(json.dumps(metrics, indent=2, allow_nan=True))
    return 1 if any(item["max_8bit"] > 2 for item in metrics.values()) else 0


if __name__ == "__main__":
    raise SystemExit(main())
