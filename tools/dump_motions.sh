#!/usr/bin/env bash
# Dump Native + Godot Hiyori motions and diff them.
# CSVs stay under /tmp (or $1); they are derived from the sample and are not committed.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SDK="${CUBISM_NATIVE_SDK:-/tmp/cubism_native/CubismSdkForNative-5-r.5}"
MODEL="${CUBISM_MODEL:-$ROOT/demo/models/Hiyori/Hiyori.model3.json}"
OUT="${1:-/tmp/cubism-motion-dumps}"
NATIVE_OUT="$OUT/native"
GODOT_OUT="$OUT/godot"
mkdir -p "$NATIVE_OUT" "$GODOT_OUT"

if [[ ! -f "$MODEL" ]]; then
  echo "missing model $MODEL" >&2
  exit 2
fi
if [[ ! -d "$SDK" ]]; then
  echo "missing Cubism Native SDK at $SDK (set CUBISM_NATIVE_SDK)" >&2
  exit 2
fi

echo "=== Build native_motion_dump ==="
cmake -S "$ROOT/tools/native_motion_dump" -B "$ROOT/tools/native_motion_dump/build" \
  -DCMAKE_CXX_COMPILER=/usr/bin/g++ \
  -DCUBISM_NATIVE_SDK="$SDK"
cmake --build "$ROOT/tools/native_motion_dump/build" -j"$(nproc)"
DUMP_BIN="$ROOT/tools/native_motion_dump/build/native_motion_dump"

echo "=== Native dump ==="
"$DUMP_BIN" --model "$MODEL" --out "$NATIVE_OUT" --seconds 10 --fps 60

echo "=== Godot dump ==="
export DISPLAY="${DISPLAY:-:1}"
export LD_LIBRARY_PATH="$ROOT/native:${LD_LIBRARY_PATH:-}"
export CUBISM_DUMP_DIR="$GODOT_OUT"
export CUBISM_HUD=0
unset CUBISM_DEMO_MODE || true
godot --path "$ROOT" --headless --quit-after 60 demo/scenes/demo.tscn

echo "=== Diff (params, t < Duration; Native V2 loop gap excluded) ==="
echo "(1e-4 informational; pass/fail uses 2e-3 for CubismJson vs Newtonsoft ulps on degenerate beziers)"
python3 "$ROOT/tools/compare_motion_dumps.py" "$NATIVE_OUT" "$GODOT_OUT" \
  --threshold 1e-4 --params-only --model3 "$MODEL" || true
set +e
python3 "$ROOT/tools/compare_motion_dumps.py" "$NATIVE_OUT" "$GODOT_OUT" \
  --threshold 2e-3 --params-only --model3 "$MODEL" \
  --json-out "$OUT/compare-params.json"
PARAMS_RC=$?
echo "=== Diff (all columns, all frames; informational) ==="
python3 "$ROOT/tools/compare_motion_dumps.py" "$NATIVE_OUT" "$GODOT_OUT" \
  --threshold 1e-4 --top 12 --json-out "$OUT/compare-all.json"
set -e
exit "$PARAMS_RC"
