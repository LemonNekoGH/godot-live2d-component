#!/usr/bin/env bash
# Dump Native + Godot Hiyori motions and diff them.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SDK="${CUBISM_NATIVE_SDK:-/tmp/cubism_native/CubismSdkForNative-5-r.5}"
MODEL="${CUBISM_MODEL:-$ROOT/demo/models/Hiyori/Hiyori.model3.json}"
OUT="${1:-/tmp/cubism-motion-dumps}"
NATIVE_OUT="$OUT/native"
GODOT_OUT="$OUT/godot"
mkdir -p "$NATIVE_OUT" "$GODOT_OUT"

DUMP_BIN="$ROOT/tools/native_motion_dump/build/native_motion_dump"
if [[ ! -x "$DUMP_BIN" ]]; then
  cmake -S "$ROOT/tools/native_motion_dump" -B "$ROOT/tools/native_motion_dump/build"
  cmake --build "$ROOT/tools/native_motion_dump/build" -j"$(nproc)"
fi

echo "=== Native dump ==="
"$DUMP_BIN" --model "$MODEL" --out "$NATIVE_OUT" --seconds 10 --fps 60

echo "=== Godot dump ==="
export DISPLAY="${DISPLAY:-:1}"
export LD_LIBRARY_PATH="$ROOT/native:${LD_LIBRARY_PATH:-}"
export CUBISM_DUMP_DIR="$GODOT_OUT"
export CUBISM_HUD=0
unset CUBISM_DEMO_MODE || true
godot --path "$ROOT" --headless --quit-after 60 demo/scenes/demo.tscn

echo "=== Diff ==="
python3 "$ROOT/tools/compare_motion_dumps.py" "$NATIVE_OUT" "$GODOT_OUT" --threshold 1e-4
