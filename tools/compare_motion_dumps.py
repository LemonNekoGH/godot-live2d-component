#!/usr/bin/env python3
"""Diff Native vs Godot motion CSVs. Prints max abs error per parameter per motion."""

from __future__ import annotations

import argparse
import csv
import math
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


def compare(native: Path, godot: Path, threshold: float) -> dict:
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
    maxima: dict[str, tuple[float, int]] = {}
    over = []
    for col in common:
        if col in ("frame", "time"):
            continue
        ni, gi = nidx[col], gidx[col]
        mx = 0.0
        at = 0
        for f in range(nframes):
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
        "over": over,
        "maxima": maxima,
        "worst": worst,
        "n_over": len(over),
        "global_max": max((v[0] for v in maxima.values()), default=0.0),
    }


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("native_dir")
    p.add_argument("godot_dir")
    p.add_argument("--threshold", type=float, default=1e-4)
    p.add_argument("--top", type=int, default=8)
    args = p.parse_args()
    ndir = Path(args.native_dir)
    gdir = Path(args.godot_dir)
    files = sorted(ndir.glob("*.csv"))
    if not files:
        print(f"no CSVs in {ndir}", file=sys.stderr)
        return 2

    rc = 0
    print(f"threshold={args.threshold:g}")
    for nf in files:
        gf = gdir / nf.name
        if not gf.exists():
            print(f"MISSING godot {nf.name}")
            rc = 1
            continue
        r = compare(nf, gf, args.threshold)
        print(f"{r['file']:16s} frames={r['frames']:4d}  max={r['global_max']:.6g}  "
              f"over={r['n_over']}")
        for mx, at, col in r["over"][: args.top]:
            print(f"    {mx:.6g} @frame {at}  {col}")
        if r["n_over"]:
            rc = 1
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
