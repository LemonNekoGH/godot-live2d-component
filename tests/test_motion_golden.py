#!/usr/bin/env python3
"""Golden motion test: generate Native + Godot dumps at test time and diff them.

CSVs are not committed (they are numbers derived from the Live2D sample).
Requires a local Cubism Native SDK and the Godot dump path used by dump_motions.sh.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# Cardano matches Native for identical coefficients. Remaining ~1e-3 abs error
# is CubismJson vs Newtonsoft decimal→float on near-degenerate unrestricted
# beziers (a ~ 1e-3). Native V2 loop wrap is excluded separately.
THRESHOLD = 2e-3


def main() -> int:
    sdk = os.environ.get("CUBISM_NATIVE_SDK", "/tmp/cubism_native/CubismSdkForNative-5-r.5")
    model = os.environ.get(
        "CUBISM_MODEL", str(ROOT / "demo/models/Hiyori/Hiyori.model3.json")
    )
    if not Path(sdk).is_dir():
        print("SKIP: Cubism Native SDK not at", sdk)
        return 0
    if not Path(model).is_file():
        print("SKIP: Hiyori model not at", model)
        return 0

    out = Path(os.environ.get("CUBISM_DUMP_OUT", "/tmp/cubism-motion-dumps"))
    subprocess.check_call([str(ROOT / "tools/dump_motions.sh"), str(out)], cwd=ROOT)

    summary_path = out / "compare-params.json"
    if not summary_path.is_file():
        print("FAIL: missing", summary_path)
        return 1

    summaries = json.loads(summary_path.read_text())
    failed = []
    print(f"golden threshold={THRESHOLD:g} (params, t < Duration)")
    for row in summaries:
        print(f"  {row['file']:16s} max={row['global_max']:.6g} over={row['n_over']}")
        if row["global_max"] > THRESHOLD or row["n_over"]:
            failed.append(row["file"])
    if failed:
        print("FAIL:", ", ".join(failed))
        return 1
    print("PASS all motions")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
