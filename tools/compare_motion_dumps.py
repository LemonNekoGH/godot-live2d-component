#!/usr/bin/env python3
"""Diff Native vs Godot motion CSVs. Prints max abs error per parameter per motion.

Loop wrap: Unity / this port wrap at motion Duration. Native V2 uses
Duration+1/fps and CorrectEndPoint. Pass --model3 to exclude t >= Duration
from the pass/fail (those frames are a documented Native≠Unity difference).
"""

from __future__ import annotations

import argparse
import csv
import json
import sys
from pathlib import Path


def load_csv(path: Path) -> tuple[list[str], list[list[float]]]:
    with path.open() as f:
        reader = csv.reader(f)
        header = next(reader)
        rows = []
        for row in reader:
            rows.append([float(x) for x in row])
    return header, rows


def load_job_durations(model3: Path) -> dict[str, float]:
    data = json.loads(model3.read_text())
    motions = data.get("FileReferences", {}).get("Motions", {})
    home = model3.parent
    out: dict[str, float] = {}
    for group, items in motions.items():
        for i, item in enumerate(items):
            motion_path = home / item["File"]
            meta = json.loads(motion_path.read_text())["Meta"]
            out[f"{group}_{i}"] = float(meta["Duration"])
    return out


def compare(
    native: Path,
    godot: Path,
    threshold: float,
    params_only: bool,
    max_time: float | None,
) -> dict:
    nh, nr = load_csv(native)
    gh, gr = load_csv(godot)
    if nh != gh:
        missing_n = set(nh) - set(gh)
        missing_g = set(gh) - set(nh)
        print(f"  HEADER MISMATCH {native.name}: native-only={len(missing_n)} godot-only={len(missing_g)}")
        common = [c for c in nh if c in gh]
    else:
        common = nh

    nidx = {c: i for i, c in enumerate(nh)}
    gidx = {c: i for i, c in enumerate(gh)}
    nframes = min(len(nr), len(gr))
    time_i = nidx.get("time", 1)

    frames = []
    for f in range(nframes):
        t = nr[f][time_i]
        if max_time is not None and t >= max_time - 1e-9:
            continue
        frames.append(f)

    maxima: dict[str, tuple[float, int]] = {}
    over = []
    for col in common:
        if col in ("frame", "time"):
            continue
        if params_only and not col.startswith("P:"):
            continue
        ni, gi = nidx[col], gidx[col]
        mx = 0.0
        at = 0
        for f in frames:
            err = abs(nr[f][ni] - gr[f][gi])
            if err > mx:
                mx = err
                at = f
        maxima[col] = (mx, at)
        if mx > threshold:
            over.append((mx, at, col))
    over.sort(reverse=True)
    worst = over[0] if over else (0.0, 0, "")
    return {
        "file": native.name,
        "frames": nframes,
        "compared_frames": len(frames),
        "over": over,
        "maxima": maxima,
        "worst": worst,
        "n_over": len(over),
        "global_max": max((v[0] for v in maxima.values()), default=0.0),
        "max_time": max_time,
    }


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("native_dir")
    p.add_argument("godot_dir")
    p.add_argument("--threshold", type=float, default=1e-4)
    p.add_argument("--top", type=int, default=8)
    p.add_argument("--params-only", action="store_true")
    p.add_argument("--model3", default="", help="Hiyori.model3.json; exclude Native V2 loop-gap frames")
    p.add_argument("--json-out", default="")
    args = p.parse_args()
    ndir = Path(args.native_dir)
    gdir = Path(args.godot_dir)
    files = sorted(ndir.glob("*.csv"))
    if not files:
        print(f"no CSVs in {ndir}", file=sys.stderr)
        return 2

    durations = load_job_durations(Path(args.model3)) if args.model3 else {}

    rc = 0
    summaries = []
    print(f"threshold={args.threshold:g} params_only={int(args.params_only)} "
          f"exclude_loop_wrap={int(bool(durations))}")
    for nf in files:
        gf = gdir / nf.name
        if not gf.exists():
            print(f"MISSING godot {nf.name}")
            rc = 1
            continue
        max_time = durations.get(nf.stem)
        r = compare(nf, gf, args.threshold, args.params_only, max_time)
        print(f"{r['file']:16s} frames={r['frames']:4d} compared={r['compared_frames']:4d}  "
              f"max={r['global_max']:.6g}  over={r['n_over']}")
        for mx, at, col in r["over"][: args.top]:
            print(f"    {mx:.6g} @frame {at}  {col}")
        if r["n_over"]:
            rc = 1
        summaries.append({
            "file": r["file"],
            "frames": r["frames"],
            "compared_frames": r["compared_frames"],
            "max_time": r["max_time"],
            "global_max": r["global_max"],
            "n_over": r["n_over"],
            "over": [{"max": mx, "frame": at, "column": col} for mx, at, col in r["over"]],
        })

    if args.json_out:
        Path(args.json_out).write_text(json.dumps(summaries, indent=2))
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
