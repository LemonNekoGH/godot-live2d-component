#!/usr/bin/env bash
# Record one CUBISM_DEMO_MODE (or all of them) with Godot Movie Maker.
#
# Movie Maker advances the engine on a fixed clock (--fixed-fps), so the
# output frame rate stays at 60 even when the software renderer is slow.
# --quit-after is a frame count. The AVI is then transcoded to H.264 mp4.
#
# Usage:
#   ./tools/record_feature.sh <mode> [out.mp4]
#   ./tools/record_feature.sh all [out_dir]
#
# Modes: static motion expression blink physics pose lipsync lookat
#        idle0..idle8 tapbody0  (one Hiyori motion3 each)
#        motions  (all Idle + TapBody)
#        blend alpha async
#
# Environment:
#   CUBISM_RECORD_SECONDS   duration in simulated seconds (default 8)
#   CUBISM_RECORD_FPS       movie fps (default 60)
#   CUBISM_RECORD_BG        demo background (default green)
#   GODOT                   godot binary (default: godot)

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

MODE="${1:-}"
if [[ -z "$MODE" ]]; then
  echo "Usage: $0 <static|motion|expression|blink|physics|pose|lipsync|lookat|all> [out.mp4|out_dir]" >&2
  exit 2
fi

export DISPLAY="${DISPLAY:-:1}"
GODOT="${GODOT:-godot}"
FPS="${CUBISM_RECORD_FPS:-60}"
SECONDS_LEN="${CUBISM_RECORD_SECONDS:-8}"
FRAMES=$((SECONDS_LEN * FPS))
BG="${CUBISM_RECORD_BG:-green}"

artifact_name() {
  case "$1" in
    static) echo "feature-static-render.mp4" ;;
    motion) echo "feature-motion-idle.mp4" ;;
    expression) echo "feature-expression-smile.mp4" ;;
    blink) echo "feature-eye-blink.mp4" ;;
    physics) echo "feature-physics.mp4" ;;
    pose) echo "feature-pose-tapbody.mp4" ;;
    lipsync) echo "feature-lipsync.mp4" ;;
    lookat) echo "feature-lookat.mp4" ;;
    idle0) echo "feature-motion-idle0-m01.mp4" ;;
    idle1) echo "feature-motion-idle1-m02.mp4" ;;
    idle2) echo "feature-motion-idle2-m03.mp4" ;;
    idle3) echo "feature-motion-idle3-m05.mp4" ;;
    idle4) echo "feature-motion-idle4-m06.mp4" ;;
    idle5) echo "feature-motion-idle5-m07.mp4" ;;
    idle6) echo "feature-motion-idle6-m08.mp4" ;;
    idle7) echo "feature-motion-idle7-m09.mp4" ;;
    idle8) echo "feature-motion-idle8-m10.mp4" ;;
    tapbody0) echo "feature-motion-tapbody0-m04.mp4" ;;
    blend) echo "feature-blend-modes.mp4" ;;
    alpha) echo "feature-alpha-blend-modes.mp4" ;;
    async) echo "feature-async-update.mp4" ;;
    *) echo "feature-$1.mp4" ;;
  esac
}

default_dir() {
  if [[ -d /opt/cursor/artifacts ]]; then
    echo /opt/cursor/artifacts
  else
    echo "$ROOT/recordings"
  fi
}

encode_mp4() {
  local avi="$1"
  local mp4="$2"
  ffmpeg -y -i "$avi" -an -c:v libx264 -pix_fmt yuv420p \
    -preset medium -crf 20 -movflags +faststart "$mp4" >/tmp/ffmpeg-record-feature.log 2>&1

  local size
  size="$(stat -c%s "$mp4" 2>/dev/null || stat -f%z "$mp4")"
  local limit=$((15 * 1024 * 1024))
  if (( size > limit )); then
    echo "  mp4 is $size bytes, re-encoding at crf 26 to stay under 15 MB"
    ffmpeg -y -i "$avi" -an -c:v libx264 -pix_fmt yuv420p \
      -preset medium -crf 26 -movflags +faststart "$mp4" >/tmp/ffmpeg-record-feature.log 2>&1
  fi
}

record_one() {
  local mode="$1"
  local out_mp4="$2"
  local tmp_dir
  tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/cubism-movie.XXXXXX")"
  local avi="$tmp_dir/${mode}.avi"

  mkdir -p "$(dirname "$out_mp4")"

  echo "=== Movie Maker: mode=$mode fps=$FPS frames=$FRAMES -> $out_mp4 ==="

  export LD_LIBRARY_PATH="$ROOT/native:${LD_LIBRARY_PATH:-}"
  export CUBISM_DEMO_MODE="$mode"
  export CUBISM_HUD=0
  export CUBISM_BG="$BG"
  unset CUBISM_SCREENSHOT || true
  unset CUBISM_QUIT_AFTER || true
  unset CUBISM_DUMP_DIR || true

  # --quit-after is a frame count when used with --write-movie.
  # --fixed-fps is forced by Movie Maker; we set it explicitly anyway.
  # --disable-vsync lets the writer run faster than wall-clock 60 fps.
  local display_args=()
  if [[ "$(uname)" != "Darwin" ]]; then
    display_args=(--display-driver x11)
  fi
  "$GODOT" --path "$ROOT" \
    --write-movie "$avi" \
    --fixed-fps "$FPS" \
    --quit-after "$FRAMES" \
    --disable-vsync \
    "${display_args[@]}" \
    demo/scenes/demo.tscn

  if [[ ! -s "$avi" ]]; then
    echo "Godot did not write $avi" >&2
    rm -rf "$tmp_dir"
    return 1
  fi

  encode_mp4 "$avi" "$out_mp4"
  rm -rf "$tmp_dir"

  ffprobe -v error -select_streams v:0 \
    -show_entries stream=width,height,r_frame_rate,avg_frame_rate,nb_frames \
    -show_entries format=duration,size \
    -of default=nw=1 "$out_mp4"
  echo "Wrote $out_mp4"
}

MODES=(static motion expression blink physics pose lipsync lookat)
MOTION_MODES=(idle0 idle1 idle2 idle3 idle4 idle5 idle6 idle7 idle8 tapbody0)
EXTRA_MODES=(blend alpha async)

is_known() {
  local m="$1"
  for x in "${MODES[@]}" "${MOTION_MODES[@]}" "${EXTRA_MODES[@]}"; do
    [[ "$x" == "$m" ]] && return 0
  done
  return 1
}

if [[ "$MODE" == "motions" ]]; then
  OUT_DIR="${2:-$(default_dir)}"
  mkdir -p "$OUT_DIR"
  # Longest Hiyori clip is 8.57s + fade; keep 10s so the motion finishes.
  SECONDS_LEN="${CUBISM_RECORD_SECONDS:-10}"
  FRAMES=$((SECONDS_LEN * FPS))
  for m in "${MOTION_MODES[@]}"; do
    record_one "$m" "$OUT_DIR/$(artifact_name "$m")"
  done
  exit 0
fi

if [[ "$MODE" == "all" ]]; then
  OUT_DIR="${2:-$(default_dir)}"
  mkdir -p "$OUT_DIR"
  for m in "${MODES[@]}"; do
    record_one "$m" "$OUT_DIR/$(artifact_name "$m")"
  done
  exit 0
fi

if ! is_known "$MODE"; then
  echo "Unknown mode: $MODE" >&2
  echo "Expected: ${MODES[*]} ${MOTION_MODES[*]} ${EXTRA_MODES[*]} motions or all" >&2
  exit 2
fi

OUT="${2:-$(default_dir)/$(artifact_name "$MODE")}"
record_one "$MODE" "$OUT"
