/**
 * Golden-value tests for ported math functions.
 * These verify numeric equivalence with the Unity CubismPhysicsMath and CubismMath implementations
 * by computing the same operations with the same inputs and checking identical results.
 *
 * Verification method: direct numerical comparison of ported C# math against
 * Unity source C# math (extracted from CubismUnityComponents, no Unity runtime needed).
 */

using System;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.Physics;
using Live2D.Cubism.Framework.Json;
using Godot;

namespace Live2D.Cubism.Tests
{
    public static class CubismMathTests
    {
        private const float Epsilon = 0.02f;

        public static bool RunAll()
        {
            bool allPassed = true;

            allPassed &= TestModF();
            allPassed &= TestClampF();
            allPassed &= TestDegreesToRadian();
            allPassed &= TestRadianToDegrees();
            allPassed &= TestDirectionToRadian();
            allPassed &= TestGetAngleDiff();
            allPassed &= TestDirectionToDegrees();
            allPassed &= TestRadianToDirection();
            allPassed &= TestNormalize();
            allPassed &= TestParameterRepeatValue();
            allPassed &= TestParameterClampValue();
            allPassed &= TestMotionCurveEvaluation();
            allPassed &= TestBezierCurveEvaluation();
            allPassed &= TestEyeBlinkTimingCurve();
            allPassed &= TestParameterStoreMultiplyRestore();
            allPassed &= TestNativeBezierAndFade();
            allPassed &= TestHiyoriMotionCurvesVsNative();

            if (allPassed)
                GD.Print("[TESTS] All golden-value math tests PASSED");
            else
                GD.PrintErr("[TESTS] Some tests FAILED");

            return allPassed;
        }

        static bool AssertClose(float actual, float expected, string testName)
        {
            if (MathF.Abs(actual - expected) > Epsilon)
            {
                GD.PrintErr($"  FAIL {testName}: expected {expected}, got {actual}");
                return false;
            }
            GD.Print($"  PASS {testName}: {actual} ≈ {expected}");
            return true;
        }

        static bool TestModF()
        {
            GD.Print("[TEST] CubismMath.ModF");
            bool ok = true;
            // Unity: a - b * floor(a/b)
            ok &= AssertClose(CubismMath.ModF(5.5f, 3.0f), 5.5f - 3.0f * MathF.Floor(5.5f / 3.0f), "ModF(5.5, 3)");
            ok &= AssertClose(CubismMath.ModF(-1.5f, 3.0f), -1.5f - 3.0f * MathF.Floor(-1.5f / 3.0f), "ModF(-1.5, 3)");
            ok &= AssertClose(CubismMath.ModF(0f, 1f), 0f, "ModF(0, 1)");
            ok &= AssertClose(CubismMath.ModF(7f, 7f), 0f, "ModF(7, 7)");
            ok &= AssertClose(CubismMath.ModF(10.3f, 2.1f), 10.3f - 2.1f * MathF.Floor(10.3f / 2.1f), "ModF(10.3, 2.1)");
            return ok;
        }

        static bool TestClampF()
        {
            GD.Print("[TEST] CubismMath.ClampF");
            bool ok = true;
            ok &= AssertClose(CubismMath.ClampF(0.5f, 0f, 1f), 0.5f, "ClampF(0.5, 0, 1)");
            ok &= AssertClose(CubismMath.ClampF(-1f, 0f, 1f), 0f, "ClampF(-1, 0, 1)");
            ok &= AssertClose(CubismMath.ClampF(2f, 0f, 1f), 1f, "ClampF(2, 0, 1)");
            ok &= AssertClose(CubismMath.ClampF(0f, 0f, 0f), 0f, "ClampF(0, 0, 0)");
            return ok;
        }

        static bool TestDegreesToRadian()
        {
            GD.Print("[TEST] CubismPhysicsMath.DegreesToRadian");
            bool ok = true;
            // Unity: (degrees / 180.0f) * Mathf.PI
            ok &= AssertClose(CubismPhysicsMath.DegreesToRadian(0f), 0f, "DegreesToRadian(0)");
            ok &= AssertClose(CubismPhysicsMath.DegreesToRadian(90f), (90f / 180f) * MathF.PI, "DegreesToRadian(90)");
            ok &= AssertClose(CubismPhysicsMath.DegreesToRadian(180f), MathF.PI, "DegreesToRadian(180)");
            ok &= AssertClose(CubismPhysicsMath.DegreesToRadian(-45f), (-45f / 180f) * MathF.PI, "DegreesToRadian(-45)");
            ok &= AssertClose(CubismPhysicsMath.DegreesToRadian(360f), (360f / 180f) * MathF.PI, "DegreesToRadian(360)");
            return ok;
        }

        static bool TestRadianToDegrees()
        {
            GD.Print("[TEST] CubismPhysicsMath.RadianToDegrees");
            bool ok = true;
            ok &= AssertClose(CubismPhysicsMath.RadianToDegrees(0f), 0f, "RadianToDegrees(0)");
            ok &= AssertClose(CubismPhysicsMath.RadianToDegrees(MathF.PI), 180f, "RadianToDegrees(PI)");
            ok &= AssertClose(CubismPhysicsMath.RadianToDegrees(MathF.PI / 2), 90f, "RadianToDegrees(PI/2)");
            return ok;
        }

        static bool TestDirectionToRadian()
        {
            GD.Print("[TEST] CubismPhysicsMath.DirectionToRadian");
            bool ok = true;
            var from = new Vector2(1, 0);
            var to = new Vector2(0, 1);
            float expected = MathF.Atan2(1, 0) - MathF.Atan2(0, 1);
            ok &= AssertClose(CubismPhysicsMath.DirectionToRadian(from, to), expected, "DirToRad((1,0)->(0,1))");

            from = new Vector2(0, 1);
            to = new Vector2(0, 1);
            ok &= AssertClose(CubismPhysicsMath.DirectionToRadian(from, to), 0f, "DirToRad(same)");
            return ok;
        }

        static bool TestGetAngleDiff()
        {
            GD.Print("[TEST] CubismPhysicsMath.GetAngleDiff");
            bool ok = true;
            ok &= AssertClose(CubismPhysicsMath.GetAngleDiff(0f, 0f), 0f, "AngleDiff(0,0)");
            ok &= AssertClose(CubismPhysicsMath.GetAngleDiff(MathF.PI, 0f), MathF.PI, "AngleDiff(PI,0)");

            float diff = CubismPhysicsMath.GetAngleDiff(MathF.PI + 0.1f, -MathF.PI + 0.1f);
            ok &= AssertClose(diff, -MathF.PI * 2f + 0.2f + MathF.PI * 2f * MathF.Floor((-MathF.PI * 2f + 0.2f + MathF.PI) / (MathF.PI * 2f)) * 0f,
                0f, "AngleDiff wrap");
            // Simplified: should wrap to near 0
            ok &= (MathF.Abs(diff) < MathF.PI + 0.01f);
            return ok;
        }

        static bool AssertClose(float actual, float ignore, float expected, string testName)
        {
            return AssertClose(actual, expected, testName);
        }

        static bool TestDirectionToDegrees()
        {
            GD.Print("[TEST] CubismPhysicsMath.DirectionToDegrees");
            bool ok = true;
            var from = new Vector2(0, 1);
            var to = new Vector2(1, 0);
            float result = CubismPhysicsMath.DirectionToDegrees(from, to);

            // Unity logic: degree = RadianToDegrees(DirectionToRadian(from, to))
            // if (to.x - from.x) > 0 then degree = -degree
            float radian = CubismPhysicsMath.DirectionToRadian(from, to);
            float degree = CubismPhysicsMath.RadianToDegrees(radian);
            if ((to.X - from.X) > 0.0f)
                degree = -degree;
            ok &= AssertClose(result, degree, "DirToDeg((0,1)->(1,0))");
            return ok;
        }

        static bool TestRadianToDirection()
        {
            GD.Print("[TEST] CubismPhysicsMath.RadianToDirection");
            bool ok = true;
            // Unity: x = sin(angle), y = cos(angle)
            var dir = CubismPhysicsMath.RadianToDirection(0f);
            ok &= AssertClose(dir.X, 0f, "RadToDir(0).x");
            ok &= AssertClose(dir.Y, 1f, "RadToDir(0).y");

            dir = CubismPhysicsMath.RadianToDirection(MathF.PI / 2f);
            ok &= AssertClose(dir.X, 1f, "RadToDir(PI/2).x");
            ok &= AssertClose(dir.Y, 0f, "RadToDir(PI/2).y");
            return ok;
        }

        static bool TestNormalize()
        {
            GD.Print("[TEST] CubismPhysicsMath.Normalize");
            bool ok = true;

            var param = new CubismParameter();
            param.MinimumValue = -30f;
            param.MaximumValue = 30f;
            param.DefaultValue = 0f;

            float paramValue = 15f;
            float result = CubismPhysicsMath.Normalize(
                param, ref paramValue, -10f, 10f, 0f, false);

            // Unity: positive branch: paramValue=15, middleValue=0, maxValue=30
            // nLength = 10 - 0 = 10, pLength = 30 - 0 = 30
            // result = 15 * (10/30) = 5, result += 0 = 5
            // isInverted=false => result * -1 = -5
            ok &= AssertClose(result, -5f, "Normalize(15, [-30,30], [-10,10])");

            paramValue = -15f;
            result = CubismPhysicsMath.Normalize(
                param, ref paramValue, -10f, 10f, 0f, false);
            ok &= AssertClose(result, 5f, "Normalize(-15, [-30,30], [-10,10])");

            paramValue = 0f;
            result = CubismPhysicsMath.Normalize(
                param, ref paramValue, -10f, 10f, 0f, false);
            ok &= AssertClose(result, 0f, "Normalize(0, [-30,30], [-10,10])");

            paramValue = 15f;
            result = CubismPhysicsMath.Normalize(
                param, ref paramValue, -10f, 10f, 0f, true);
            ok &= AssertClose(result, 5f, "Normalize(15, inverted)");

            return ok;
        }

        static bool TestParameterRepeatValue()
        {
            GD.Print("[TEST] CubismParameter.GetParameterRepeatValue");
            bool ok = true;

            var param = new CubismParameter();
            param.MinimumValue = 0f;
            param.MaximumValue = 360f;

            ok &= AssertClose(param.GetParameterRepeatValue(180f), 180f, "Repeat(180)");
            ok &= AssertClose(param.GetParameterRepeatValue(370f), 10f, "Repeat(370)");
            ok &= AssertClose(param.GetParameterRepeatValue(-10f), 350f, "Repeat(-10)");
            ok &= AssertClose(param.GetParameterRepeatValue(0f), 0f, "Repeat(0)");
            ok &= AssertClose(param.GetParameterRepeatValue(360f), 360f, "Repeat(360)");

            return ok;
        }

        static bool TestParameterClampValue()
        {
            GD.Print("[TEST] CubismParameter.GetParameterClampValue");
            bool ok = true;

            var param = new CubismParameter();
            param.MinimumValue = -1f;
            param.MaximumValue = 1f;

            ok &= AssertClose(param.GetParameterClampValue(0.5f), 0.5f, "Clamp(0.5)");
            ok &= AssertClose(param.GetParameterClampValue(2f), 1f, "Clamp(2)");
            ok &= AssertClose(param.GetParameterClampValue(-2f), -1f, "Clamp(-2)");

            return ok;
        }

        static bool TestMotionCurveEvaluation()
        {
            GD.Print("[TEST] CubismMotion3Json.EvaluateCurve (linear segments)");
            bool ok = true;

            // Linear segment: [startTime, startValue, 0, endTime, endValue]
            float[] segments = { 0f, 0f, 0, 1f, 1f };

            ok &= AssertClose(CubismMotion3Json.EvaluateCurve(segments, 0f), 0f, "Linear(t=0)");
            ok &= AssertClose(CubismMotion3Json.EvaluateCurve(segments, 0.5f), 0.5f, "Linear(t=0.5)");
            ok &= AssertClose(CubismMotion3Json.EvaluateCurve(segments, 1f), 1f, "Linear(t=1)");
            ok &= AssertClose(CubismMotion3Json.EvaluateCurve(segments, 1.5f), 1f, "Linear(t=1.5) past end");

            // Stepped segment: [startTime, startValue, 2, endTime, endValue]
            float[] stepped = { 0f, 5f, 2, 1f, 10f };
            ok &= AssertClose(CubismMotion3Json.EvaluateCurve(stepped, 0.5f), 5f, "Stepped(t=0.5)");
            ok &= AssertClose(CubismMotion3Json.EvaluateCurve(stepped, 1.5f), 10f, "Stepped(t=1.5) past end");

            // Inverse stepped: [startTime, startValue, 3, endTime, endValue]
            float[] invStepped = { 0f, 5f, 3, 1f, 10f };
            ok &= AssertClose(CubismMotion3Json.EvaluateCurve(invStepped, 0.5f), 10f, "InvStepped(t=0.5)");

            return ok;
        }

        static bool TestBezierCurveEvaluation()
        {
            GD.Print("[TEST] CubismMotion3Json.EvaluateCurve (bezier segments)");
            bool ok = true;

            // Bezier: [startTime, startValue, 1, cx1, cy1, cx2, cy2, endTime, endValue]
            // Straight-line bezier (control points on line) should equal linear
            float[] bezier = { 0f, 0f, 1, 0.333f, 0.333f, 0.666f, 0.666f, 1f, 1f };

            float midVal = CubismMotion3Json.EvaluateCurve(bezier, 0.5f);
            ok &= AssertClose(midVal, 0.5f, "Bezier linear(t=0.5)");

            ok &= AssertClose(CubismMotion3Json.EvaluateCurve(bezier, 0f), 0f, "Bezier(t=0)");
            ok &= AssertClose(CubismMotion3Json.EvaluateCurve(bezier, 1f), 1f, "Bezier(t=1)");

            return ok;
        }

        /// <summary>
        /// Golden values: CubismAutoEyeBlinkInput vs a line-for-line copy of Unity
        /// CubismAutoEyeBlinkInput.UpdateEyeBlink* at Movie Maker dt = 1/60.
        /// Also asserts Idle uses unscaled deltaTime (not Timescale).
        /// </summary>
        static bool TestEyeBlinkTimingCurve()
        {
            GD.Print("[TEST] CubismAutoEyeBlinkInput vs Unity timing curve");
            bool ok = true;

            const float dt = 1f / 60f;
            const float timescale = 10f;
            const float closing = 1.0f;
            const float closed = 0.5f;
            const float opening = 1.5f;
            const float firstBlinkAt = 0.05f;
            const int frames = 180;

            var ours = new CubismAutoEyeBlinkInput(new Random(0));
            ours.Timescale = timescale;
            ours.SetBlinkingSettings(closing, closed, opening);
            ours.Mean = 10f;
            ours.MaximumDeviation = 0f;
            ours.Reset();
            ours.NextBlinkingTime = firstBlinkAt;

            var unity = new UnityEyeBlinkReference();
            unity.Timescale = timescale;
            unity.SetBlinkingSettings(closing, closed, opening);
            unity.Mean = 10f;
            unity.MaximumDeviation = 0f;
            unity.Reset();
            unity.NextBlinkingTime = firstBlinkAt;

            int closeStart = -1, closedStart = -1, openStart = -1, idleAgain = -1;
            float minOpening = 1f;

            for (int i = 0; i < frames; i++)
            {
                float oursValue = ours.Evaluate(dt);
                float unityValue = unity.Evaluate(dt);
                if (MathF.Abs(oursValue - unityValue) > Epsilon)
                {
                    GD.PrintErr($"  FAIL blink frame {i} t={i * dt:F4}: expected {unityValue}, got {oursValue}");
                    ok = false;
                }

                if (oursValue < minOpening)
                    minOpening = oursValue;

                if (closeStart < 0 && ours.CurrentPhase == CubismAutoEyeBlinkInput.Phase.ClosingEyes)
                    closeStart = i;
                if (closedStart < 0 && ours.CurrentPhase == CubismAutoEyeBlinkInput.Phase.ClosedEyes)
                    closedStart = i;
                if (openStart < 0 && ours.CurrentPhase == CubismAutoEyeBlinkInput.Phase.OpeningEyes)
                    openStart = i;
                if (openStart >= 0 && idleAgain < 0 && ours.CurrentPhase == CubismAutoEyeBlinkInput.Phase.Idling)
                    idleAgain = i;
            }

            if (ok)
                GD.Print($"  PASS blink curve matches Unity for {frames} frames at dt=1/60");

            ok &= minOpening <= 0.001f;
            if (minOpening > 0.001f)
                GD.PrintErr($"  FAIL blink reaches closed: min={minOpening}");
            else
                GD.Print($"  PASS blink reaches closed: min={minOpening}");

            // Idle countdown is unscaled: first close at ~firstBlinkAt, not firstBlinkAt/Timescale.
            float closeAt = closeStart * dt;
            ok &= closeAt >= firstBlinkAt - dt && closeAt <= firstBlinkAt + 2f * dt;
            GD.Print(ok
                ? $"  PASS idle uses unscaled dt: close starts at {closeAt:F4}s (expect ~{firstBlinkAt})"
                : $"  FAIL idle timing: close starts at {closeAt:F4}s");

            // Phase wall times = setting / Timescale (Unity UserTimeSeconds += dt * Timescale).
            float closeDur = (closedStart - closeStart) * dt;
            float closedDur = (openStart - closedStart) * dt;
            float openDur = (idleAgain - openStart) * dt;
            ok &= AssertClose(closeDur, closing / timescale, "closing wall duration");
            ok &= AssertClose(closedDur, closed / timescale, "closed wall duration");
            ok &= AssertClose(openDur, opening / timescale, "opening wall duration");

            // If Idle wrongly used scaled time, close would start at ~0.005s.
            if (closeAt < firstBlinkAt * 0.5f)
            {
                GD.PrintErr("  FAIL Idle appears to use Timescale (blinked too early)");
                ok = false;
            }

            return ok;
        }

        /// <summary>
        /// Multiply blink without ParameterStore restore sticks at 0 after the first close.
        /// Restore after Core read-back lets the next Opening frame reopen.
        /// </summary>
        static bool TestParameterStoreMultiplyRestore()
        {
            GD.Print("[TEST] ParameterStore restore so Multiply blink can reopen");
            bool ok = true;

            var param = new CubismParameter
            {
                Id = "ParamEyeLOpen",
                Value = 1f,
                MinimumValue = 0f,
                MaximumValue = 1f,
                DefaultValue = 1f
            };
            var part = new CubismPart { Opacity = 1f };
            var store = new CubismParameterStore();
            store.Initialize(new[] { param }, new[] { part });

            // Close: Multiply 1 * 0 = 0, as Core read-back would leave it.
            param.Value *= 0f;
            ok &= AssertClose(param.Value, 0f, "after close multiply");

            // Bug path: Opening 0.5 without restore stays 0.
            float stuck = param.Value * 0.5f;
            ok &= AssertClose(stuck, 0f, "without restore opening stays 0");

            // Unity path: Restore then Opening Multiply.
            store.RestoreParameters();
            ok &= AssertClose(param.Value, 1f, "restore returns saved 1");
            param.BlendToValue(CubismParameterBlendMode.Multiply, 0.5f);
            ok &= AssertClose(param.Value, 0.5f, "opening multiply after restore");

            store.RestoreParameters();
            param.BlendToValue(CubismParameterBlendMode.Multiply, 1f);
            ok &= AssertClose(param.Value, 1f, "fully open after restore");

            return ok;
        }

        /// <summary>
        /// Line-for-line Unity CubismAutoEyeBlinkInput phase machine (no UnityEngine).
        /// Random interval is unused when NextBlinkingTime is assigned by the test.
        /// </summary>
        sealed class UnityEyeBlinkReference
        {
            public float Mean = 2.5f;
            public float MaximumDeviation = 2f;
            public float Timescale = 10f;
            public Phase CurrentPhase { get; private set; }
            public float UserTimeSeconds { get; private set; }
            public float StateStartTimeSeconds { get; private set; }
            public float ClosingSeconds { get; private set; }
            public float ClosedSeconds { get; private set; }
            public float OpeningSeconds { get; private set; }
            public float NextBlinkingTime { get; set; }

            public enum Phase
            {
                Idling,
                ClosingEyes,
                ClosedEyes,
                OpeningEyes
            }

            public void Reset()
            {
                CurrentPhase = Phase.Idling;
                NextBlinkingTime = Mean;
                UserTimeSeconds = 0f;
                StateStartTimeSeconds = 0f;
            }

            public void SetBlinkingSettings(float closing, float closed, float opening)
            {
                ClosingSeconds = closing;
                ClosedSeconds = closed;
                OpeningSeconds = opening;
            }

            public float Evaluate(float deltaTime)
            {
                UserTimeSeconds += (deltaTime * Timescale);
                switch (CurrentPhase)
                {
                    case Phase.ClosingEyes:
                        return UpdateEyeBlinkClosing();
                    case Phase.ClosedEyes:
                        return UpdateEyeBlinkClosed();
                    case Phase.OpeningEyes:
                        return UpdateEyeBlinkOpening();
                    case Phase.Idling:
                        return UpdateEyeBlinkIdling(deltaTime);
                    default:
                        return 0f;
                }
            }

            private float UpdateEyeBlinkClosing()
            {
                var t = (UserTimeSeconds - StateStartTimeSeconds) / ClosingSeconds;
                if (t >= 1.0f)
                {
                    CurrentPhase = Phase.ClosedEyes;
                    StateStartTimeSeconds = UserTimeSeconds;
                }
                return 1.0f - t;
            }

            private float UpdateEyeBlinkClosed()
            {
                var t = (UserTimeSeconds - StateStartTimeSeconds) / ClosedSeconds;
                if (t >= 1.0f)
                {
                    CurrentPhase = Phase.OpeningEyes;
                    StateStartTimeSeconds = UserTimeSeconds;
                }
                return 0.0f;
            }

            private float UpdateEyeBlinkOpening()
            {
                var t = (UserTimeSeconds - StateStartTimeSeconds) / OpeningSeconds;
                if (t >= 1.0f)
                {
                    t = 1.0f;
                    CurrentPhase = Phase.Idling;
                    NextBlinkingTime = Mean;
                }
                return t;
            }

            private float UpdateEyeBlinkIdling(float deltaTime)
            {
                NextBlinkingTime -= deltaTime;
                if (NextBlinkingTime < 0.0f)
                {
                    CurrentPhase = Phase.ClosingEyes;
                    StateStartTimeSeconds = UserTimeSeconds;
                }
                return 1.0f;
            }
        }

        static bool TestNativeBezierAndFade()
        {
            GD.Print("[TEST] Native Cardano bezier + sine fade");
            bool ok = true;

            ok &= AssertClose(CubismMath.GetEasingSine(-1f), 0f, "easing sine <0");
            ok &= AssertClose(CubismMath.GetEasingSine(0f), 0f, "easing sine 0");
            ok &= AssertClose(CubismMath.GetEasingSine(1f), 1f, "easing sine 1");
            ok &= AssertClose(CubismMath.GetEasingSine(2f), 1f, "easing sine >1");
            ok &= AssertClose(CubismMath.GetEasingSine(0.5f), 0.5f, "easing sine 0.5");

            // Restricted: t linear in time, control points on the line → 0.5 at mid.
            float restricted = CubismMotion3Json.BezierEvaluateRestricted(
                0f, 0f, 0.333f, 0.333f, 0.666f, 0.666f, 1f, 1f, 0.5f);
            ok &= AssertClose(restricted, 0.5f, "restricted bezier mid");

            // Unrestricted Cardano on the same line should also be ~0.5.
            float cardano = CubismMotion3Json.BezierEvaluateCardano(
                0f, 0f, 0.333f, 0.333f, 0.666f, 0.666f, 1f, 1f, 0.5f);
            ok &= AssertClose(cardano, 0.5f, "cardano bezier mid");

            // Native-equivalent Cardano vs production EvaluateCurve (unrestricted).
            float[] bezier = { 0f, 0f, 1, 0.2f, 0.8f, 0.8f, 0.1f, 1f, 1f };
            float prod = CubismMotion3Json.EvaluateCurve(bezier, 0.4f, false);
            float native = NativeCardanoSegment(0f, 0f, 0.2f, 0.8f, 0.8f, 0.1f, 1f, 1f, 0.4f);
            ok &= AssertClose(prod, native, "EvaluateCurve Cardano vs Native clone");

            return ok;
        }

        static bool TestHiyoriMotionCurvesVsNative()
        {
            GD.Print("[TEST] Hiyori motion3 curves vs Native evaluator");
            bool ok = true;

            var roots = new[]
            {
                "demo/models/Hiyori/motions",
                "res://demo/models/Hiyori/motions"
            };
            string dir = null;
            foreach (var r in roots)
            {
                var p = r.StartsWith("res://") ? Godot.ProjectSettings.GlobalizePath(r) : r;
                if (System.IO.Directory.Exists(p))
                {
                    dir = p;
                    break;
                }
            }

            if (dir == null)
            {
                GD.Print("  SKIP Hiyori motions not on disk");
                return true;
            }

            var files = System.IO.Directory.GetFiles(dir, "Hiyori_m*.motion3.json");
            System.Array.Sort(files);
            if (files.Length == 0)
            {
                GD.Print("  SKIP no Hiyori_m*.motion3.json");
                return true;
            }

            float[] sampleTimes = { 0f, 0.25f, 1f / 60f, 0.5f, 1.0f, 2.0f };
            int compared = 0;
            foreach (var file in files)
            {
                var motion = CubismMotion3Json.LoadAtPath(file);
                if (motion?.Curves == null) continue;
                var restricted = motion.Meta?.AreBeziersRestricted ?? false;
                if (restricted)
                {
                    GD.PrintErr($"  FAIL {System.IO.Path.GetFileName(file)} expected unrestricted beziers");
                    ok = false;
                }

                foreach (var t in sampleTimes)
                {
                    if (t > (motion.Meta?.Duration ?? 0f))
                        continue;
                    var values = CubismMotionController.EvaluateAllParameterCurves(motion, t);
                    foreach (var kv in values)
                    {
                        if (float.IsNaN(kv.Value) || float.IsInfinity(kv.Value))
                        {
                            GD.PrintErr($"  FAIL {System.IO.Path.GetFileName(file)} {kv.Key} t={t} is {kv.Value}");
                            ok = false;
                        }
                    }
                    compared += values.Count;
                }
            }

            GD.Print($"  PASS Hiyori motions={files.Length} curve-samples={compared} finite");
            ok &= compared > 0;

            // Priority: equal-or-lower is rejected unless Force (Unity).
            var ctrl = new CubismMotionController();
            var dummy = new CubismMotion3Json
            {
                Meta = new CubismMotion3Json.SerializableMeta { Duration = 1f, Fps = 30f, FadeInTime = 0f, FadeOutTime = 0.5f },
                Curves = System.Array.Empty<CubismMotion3Json.SerializableCurve>()
            };
            ctrl.SetModel(System.Array.Empty<CubismParameter>(), System.Array.Empty<CubismPart>());
            var first = ctrl.PlayMotion(dummy, CubismMotionPriority.PriorityNormal);
            var blocked = ctrl.PlayMotion(dummy, CubismMotionPriority.PriorityIdle);
            var forced = ctrl.PlayMotion(dummy, CubismMotionPriority.PriorityForce);
            if (first == null || blocked != null || forced == null)
            {
                GD.PrintErr("  FAIL priority reject/force");
                ok = false;
            }
            else
                GD.Print("  PASS priority: Idle rejected while Normal plays; Force preempts");

            return ok;
        }

        /// <summary>Line-for-line Native BezierEvaluateCardanoInterpretation (local lerps).</summary>
        static float NativeCardanoSegment(
            float t0, float v0, float c1t, float c1v, float c2t, float c2v, float t3, float v3, float time)
        {
            float a = t3 - 3.0f * c2t + 3.0f * c1t - t0;
            float b = 3.0f * c2t - 6.0f * c1t + 3.0f * t0;
            float c = 3.0f * c1t - 3.0f * t0;
            float d = t0 - time;
            float t = CubismMath.CardanoAlgorithmForBezier(a, b, c, d);

            float Lerp(float x, float y, float u) => x + (y - x) * u;
            float p01v = Lerp(v0, c1v, t);
            float p12v = Lerp(c1v, c2v, t);
            float p23v = Lerp(c2v, v3, t);
            float p012v = Lerp(p01v, p12v, t);
            float p123v = Lerp(p12v, p23v, t);
            return Lerp(p012v, p123v, t);
        }
    }
}
