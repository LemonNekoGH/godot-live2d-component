/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Live2D.Cubism.Core;

namespace Live2D.Cubism.Framework.Json
{
    /// <summary>
    /// Parses motion3.json files. Mirrors Unity's CubismMotion3Json.
    /// </summary>
    public sealed class CubismMotion3Json
    {
        [JsonPropertyName("Version")] public int Version { get; set; }

        [JsonPropertyName("Meta")] public SerializableMeta Meta { get; set; }

        [JsonPropertyName("Curves")] public SerializableCurve[] Curves { get; set; }

        [JsonPropertyName("UserData")] public SerializableUserData[] UserData { get; set; }

        public class SerializableMeta
        {
            [JsonPropertyName("Duration")] public float Duration { get; set; }
            [JsonPropertyName("Fps")] public float Fps { get; set; }
            [JsonPropertyName("Loop")] public bool Loop { get; set; }
            [JsonPropertyName("AreBeziersRestricted")] public bool AreBeziersRestricted { get; set; }
            [JsonPropertyName("FadeInTime")] public float FadeInTime { get; set; } = -1f;
            [JsonPropertyName("FadeOutTime")] public float FadeOutTime { get; set; } = -1f;
            [JsonPropertyName("CurveCount")] public int CurveCount { get; set; }
            [JsonPropertyName("TotalSegmentCount")] public int TotalSegmentCount { get; set; }
            [JsonPropertyName("TotalPointCount")] public int TotalPointCount { get; set; }
            [JsonPropertyName("UserDataCount")] public int UserDataCount { get; set; }
            [JsonPropertyName("TotalUserDataSize")] public int TotalUserDataSize { get; set; }
        }

        public class SerializableCurve
        {
            [JsonPropertyName("Target")] public string Target { get; set; }
            [JsonPropertyName("Id")] public string Id { get; set; }
            [JsonPropertyName("FadeInTime")] public float FadeInTime { get; set; } = -1f;
            [JsonPropertyName("FadeOutTime")] public float FadeOutTime { get; set; } = -1f;
            [JsonPropertyName("Segments")] public float[] Segments { get; set; }
        }

        public class SerializableUserData
        {
            [JsonPropertyName("Time")] public float Time { get; set; }
            [JsonPropertyName("Value")] public string Value { get; set; }
        }

        /// <summary>
        /// Segment types matching Live2D spec.
        /// </summary>
        public const int SegmentTypeLinear = 0;
        public const int SegmentTypeBezier = 1;
        public const int SegmentTypeStepped = 2;
        public const int SegmentTypeInverseStepped = 3;

        public static CubismMotion3Json LoadFrom(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;
            return JsonSerializer.Deserialize(jsonText, CubismJsonContext.Default.CubismMotion3Json);
        }

        public static CubismMotion3Json LoadAtPath(string path)
        {
            var text = CubismAssetIo.ReadAllText(path);
            return text != null ? LoadFrom(text) : null;
        }

        /// <summary>
        /// Evaluates a motion3 curve at <paramref name="time"/>.
        /// Matches Native CubismMotion.cpp EvaluateCurve / segment evaluators.
        /// Hiyori motions set AreBeziersRestricted=false, so bezier uses Cardano
        /// (BezierEvaluateCardanoInterpretation), not a 1D time binary search.
        /// </summary>
        public static float EvaluateCurve(float[] segments, float time)
        {
            return EvaluateCurve(segments, time, areBeziersRestricted: false);
        }

        public static float EvaluateCurve(float[] segments, float time, bool areBeziersRestricted)
        {
            if (segments == null || segments.Length < 2)
                return 0f;

            var p0t = segments[0];
            var p0v = segments[1];

            int i = 2;
            while (i < segments.Length)
            {
                int segType = (int)segments[i];
                i++;

                switch (segType)
                {
                    case SegmentTypeLinear:
                    {
                        var p1t = segments[i];
                        var p1v = segments[i + 1];
                        i += 2;
                        if (time <= p1t)
                            return LinearEvaluate(p0t, p0v, p1t, p1v, time);
                        p0t = p1t;
                        p0v = p1v;
                        break;
                    }
                    case SegmentTypeBezier:
                    {
                        var c1t = segments[i];
                        var c1v = segments[i + 1];
                        var c2t = segments[i + 2];
                        var c2v = segments[i + 3];
                        var p3t = segments[i + 4];
                        var p3v = segments[i + 5];
                        i += 6;
                        if (time <= p3t)
                        {
                            return areBeziersRestricted
                                ? BezierEvaluateRestricted(p0t, p0v, c1t, c1v, c2t, c2v, p3t, p3v, time)
                                : BezierEvaluateCardano(p0t, p0v, c1t, c1v, c2t, c2v, p3t, p3v, time);
                        }
                        p0t = p3t;
                        p0v = p3v;
                        break;
                    }
                    case SegmentTypeStepped:
                    {
                        var p1t = segments[i];
                        var p1v = segments[i + 1];
                        i += 2;
                        if (time <= p1t)
                            return p0v;
                        p0t = p1t;
                        p0v = p1v;
                        break;
                    }
                    case SegmentTypeInverseStepped:
                    {
                        var p1t = segments[i];
                        var p1v = segments[i + 1];
                        i += 2;
                        if (time <= p1t)
                            return p1v;
                        p0t = p1t;
                        p0v = p1v;
                        break;
                    }
                    default:
                        return p0v;
                }
            }

            return p0v;
        }

        public static float LinearEvaluate(float t0, float v0, float t1, float v1, float time)
        {
            var t = (time - t0) / (t1 - t0);
            if (t < 0.0f) t = 0.0f;
            return v0 + (v1 - v0) * t;
        }

        private struct MotionPoint
        {
            public float Time;
            public float Value;
        }

        private static MotionPoint LerpPoints(MotionPoint a, MotionPoint b, float t)
        {
            return new MotionPoint
            {
                Time = a.Time + ((b.Time - a.Time) * t),
                Value = a.Value + ((b.Value - a.Value) * t)
            };
        }

        private static float BezierValue(MotionPoint p0, MotionPoint p1, MotionPoint p2, MotionPoint p3, float t)
        {
            var p01 = LerpPoints(p0, p1, t);
            var p12 = LerpPoints(p1, p2, t);
            var p23 = LerpPoints(p2, p3, t);
            var p012 = LerpPoints(p01, p12, t);
            var p123 = LerpPoints(p12, p23, t);
            return LerpPoints(p012, p123, t).Value;
        }

        /// <summary>Native BezierEvaluate (restricted / old-curve): t is linear in time.</summary>
        public static float BezierEvaluateRestricted(
            float t0, float v0, float c1t, float c1v, float c2t, float c2v, float t3, float v3, float time)
        {
            var t = (time - t0) / (t3 - t0);
            if (t < 0.0f) t = 0.0f;
            return BezierValue(
                new MotionPoint { Time = t0, Value = v0 },
                new MotionPoint { Time = c1t, Value = c1v },
                new MotionPoint { Time = c2t, Value = c2v },
                new MotionPoint { Time = t3, Value = v3 },
                t);
        }

        /// <summary>Native BezierEvaluateCardanoInterpretation.</summary>
        public static float BezierEvaluateCardano(
            float t0, float v0, float c1t, float c1v, float c2t, float c2v, float t3, float v3, float time)
        {
            var a = t3 - 3.0f * c2t + 3.0f * c1t - t0;
            var b = 3.0f * c2t - 6.0f * c1t + 3.0f * t0;
            var c = 3.0f * c1t - 3.0f * t0;
            var d = t0 - time;
            var t = CubismMath.CardanoAlgorithmForBezier(a, b, c, d);
            return BezierValue(
                new MotionPoint { Time = t0, Value = v0 },
                new MotionPoint { Time = c1t, Value = c1v },
                new MotionPoint { Time = c2t, Value = c2v },
                new MotionPoint { Time = t3, Value = v3 },
                t);
        }
    }
}
