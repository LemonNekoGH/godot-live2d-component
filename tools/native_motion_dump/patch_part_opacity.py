#!/usr/bin/env python3
"""Rewrite Native CubismMotion PartOpacity to write parts (Unity).

Official DoUpdateParameters looks PartOpacity ids up with GetParameterIndex
and SetParameterValue, which misses part ids. Unity AnimationClip binds those
curves to CubismPart.Opacity. The harness follows Unity so dumps are comparable.
"""

from __future__ import annotations

import sys
from pathlib import Path

OLD = """    for (; c < _motionData->CurveCount && curves[c].Type == CubismMotionCurveTarget_PartOpacity; ++c)
    {
        // Find parameter index.
        parameterIndex = model->GetParameterIndex(curves[c].Id);

        // Skip curve evaluation if no value in sink.
        if (parameterIndex == -1)
        {
            continue;
        }

        // Evaluate curve and apply value.
        value = EvaluateCurve(_motionData, c, time, isCorrection, duration);

        model->SetParameterValue(parameterIndex, value);
    }
"""

NEW = """    for (; c < _motionData->CurveCount && curves[c].Type == CubismMotionCurveTarget_PartOpacity; ++c)
    {
        // Unity writes PartOpacity onto parts. Official Native uses GetParameterIndex.
        const csmInt32 partIndex = model->GetPartIndex(curves[c].Id);
        if (partIndex == -1)
        {
            continue;
        }

        value = EvaluateCurve(_motionData, c, time, isCorrection, duration);
        model->SetPartOpacity(partIndex, value);
    }
"""


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: patch_part_opacity.py <src CubismMotion.cpp> <dst>", file=sys.stderr)
        return 2
    src = Path(sys.argv[1]).read_text()
    if OLD not in src:
        print("PartOpacity block not found in", sys.argv[1], file=sys.stderr)
        return 1
    Path(sys.argv[2]).write_text(src.replace(OLD, NEW, 1))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
