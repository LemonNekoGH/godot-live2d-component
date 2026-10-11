/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Live2D.Cubism.Framework.Json
{
    /// <summary>
    /// Parses motion3.json files. Mirrors Unity's CubismMotion3Json.
    /// </summary>
    public sealed class CubismMotion3Json
    {
        public int Version { get; set; }

        public SerializableMeta Meta { get; set; }

        public SerializableCurve[] Curves { get; set; }

        public SerializableUserData[] UserData { get; set; }

        public class SerializableMeta
        {
            [JsonProperty("Duration")] public float Duration { get; set; }
            [JsonProperty("Fps")] public float Fps { get; set; }
            [JsonProperty("Loop")] public bool Loop { get; set; }
            [JsonProperty("AreBeziersRestricted")] public bool AreBeziersRestricted { get; set; }
            [JsonProperty("FadeInTime")] public float FadeInTime { get; set; } = -1f;
            [JsonProperty("FadeOutTime")] public float FadeOutTime { get; set; } = -1f;
            [JsonProperty("CurveCount")] public int CurveCount { get; set; }
            [JsonProperty("TotalSegmentCount")] public int TotalSegmentCount { get; set; }
            [JsonProperty("TotalPointCount")] public int TotalPointCount { get; set; }
            [JsonProperty("UserDataCount")] public int UserDataCount { get; set; }
            [JsonProperty("TotalUserDataSize")] public int TotalUserDataSize { get; set; }
        }

        public class SerializableCurve
        {
            [JsonProperty("Target")] public string Target { get; set; }
            [JsonProperty("Id")] public string Id { get; set; }
            [JsonProperty("FadeInTime")] public float FadeInTime { get; set; } = -1f;
            [JsonProperty("FadeOutTime")] public float FadeOutTime { get; set; } = -1f;
            [JsonProperty("Segments")] public float[] Segments { get; set; }
        }

        public class SerializableUserData
        {
            [JsonProperty("Time")] public float Time { get; set; }
            [JsonProperty("Value")] public string Value { get; set; }
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
            return JsonConvert.DeserializeObject<CubismMotion3Json>(jsonText);
        }

        public static CubismMotion3Json LoadAtPath(string path)
        {
            if (!File.Exists(path)) return null;
            return LoadFrom(File.ReadAllText(path));
        }

        /// <summary>
        /// Evaluates a curve at a given time using the segment data.
        /// Mirrors the Unity SDK's segment evaluation exactly.
        /// </summary>
        public static float EvaluateCurve(float[] segments, float time)
        {
            if (segments == null || segments.Length < 2)
                return 0f;

            float lastPointTime = segments[0];
            float lastPointValue = segments[1];

            int i = 2;
            while (i < segments.Length)
            {
                int segType = (int)segments[i];
                i++;

                switch (segType)
                {
                    case SegmentTypeLinear:
                    {
                        float nextTime = segments[i];
                        float nextValue = segments[i + 1];
                        i += 2;

                        if (time <= nextTime)
                        {
                            float t = (time - lastPointTime) / (nextTime - lastPointTime);
                            return lastPointValue + (nextValue - lastPointValue) * t;
                        }

                        lastPointTime = nextTime;
                        lastPointValue = nextValue;
                        break;
                    }
                    case SegmentTypeBezier:
                    {
                        float cx1 = segments[i], cy1 = segments[i + 1];
                        float cx2 = segments[i + 2], cy2 = segments[i + 3];
                        float nextTime = segments[i + 4], nextValue = segments[i + 5];
                        i += 6;

                        if (time <= nextTime)
                        {
                            float t = BezierFindT(lastPointTime, cx1, cx2, nextTime, time);
                            return BezierEvaluate(lastPointValue, cy1, cy2, nextValue, t);
                        }

                        lastPointTime = nextTime;
                        lastPointValue = nextValue;
                        break;
                    }
                    case SegmentTypeStepped:
                    {
                        float nextTime = segments[i];
                        float nextValue = segments[i + 1];
                        i += 2;

                        if (time <= nextTime)
                        {
                            return lastPointValue;
                        }

                        lastPointTime = nextTime;
                        lastPointValue = nextValue;
                        break;
                    }
                    case SegmentTypeInverseStepped:
                    {
                        float nextTime = segments[i];
                        float nextValue = segments[i + 1];
                        i += 2;

                        if (time <= nextTime)
                        {
                            return nextValue;
                        }

                        lastPointTime = nextTime;
                        lastPointValue = nextValue;
                        break;
                    }
                    default:
                        return lastPointValue;
                }
            }

            return lastPointValue;
        }

        private static float BezierFindT(float p0, float p1, float p2, float p3, float target)
        {
            const float epsilon = 0.01f;
            const int maxIterations = 20;

            float lo = 0f, hi = 1f;
            float t = 0.5f;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                float v = BezierEvaluate(p0, p1, p2, p3, t);
                if (MathF.Abs(v - target) < epsilon) break;

                if (v < target) lo = t;
                else hi = t;
                t = (lo + hi) * 0.5f;
            }

            return t;
        }

        private static float BezierEvaluate(float p0, float p1, float p2, float p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 +
                   3f * u * u * t * p1 +
                   3f * u * t * t * p2 +
                   t * t * t * p3;
        }
    }
}
