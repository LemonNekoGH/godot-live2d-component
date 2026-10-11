#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SDK="${CUBISM_NATIVE_SDK:-/tmp/cubism_native/CubismSdkForNative-5-r.5}"
GODOT="${GODOT:-godot}"
ARTIFACT_DIR="${1:-$ROOT/.godot/alpha-blend-pixel-test/artifacts}"
WORK_DIR="$ROOT/.godot/alpha-blend-pixel-test"
NATIVE_DIR="$WORK_DIR/native"
GODOT_DIR="$WORK_DIR/godot"
BUILD_DIR="$ROOT/tools/native_motion_dump/build"

if [[ ! -d "$SDK/Core" ]]; then
  echo "Cubism Native SDK not found: $SDK" >&2
  exit 2
fi

mkdir -p "$NATIVE_DIR" "$GODOT_DIR" "$ARTIFACT_DIR"
cmake -S "$ROOT/tools/native_motion_dump" -B "$BUILD_DIR" -DCUBISM_NATIVE_SDK="$SDK"
cmake --build "$BUILD_DIR" --target native_alpha_blend_dump --parallel
"$BUILD_DIR/native_alpha_blend_dump" "$NATIVE_DIR"
CUBISM_ALPHA_DUMP_DIR="$GODOT_DIR" "$GODOT" --headless --path "$ROOT" demo/scenes/demo.tscn
python3 "$ROOT/tests/compare_alpha_blend_pixels.py" "$NATIVE_DIR" "$GODOT_DIR" "$ARTIFACT_DIR"
