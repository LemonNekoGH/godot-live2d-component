/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Godot;
using Live2D.Cubism.Core;

namespace Live2D.Cubism.Framework.Physics
{
    /// <summary>
    /// Math utilities for physics. Mirrors Unity CubismPhysicsMath exactly.
    /// All math is identical; only UnityEngine types replaced with Godot equivalents.
    /// </summary>
    internal static class CubismPhysicsMath
    {
        public static float DegreesToRadian(float degrees)
        {
            return (degrees / 180.0f) * Mathf.Pi;
        }

        public static float RadianToDegrees(float radian)
        {
            return (radian * 180.0f) / Mathf.Pi;
        }

        public static float DirectionToRadian(Vector2 from, Vector2 to)
        {
            var q1 = Mathf.Atan2(to.Y, to.X);
            var q2 = Mathf.Atan2(from.Y, from.X);
            return GetAngleDiff(q1, q2);
        }

        public static float GetAngleDiff(float q1, float q2)
        {
            var ret = q1 - q2;

            while (ret < -Mathf.Pi)
                ret += (Mathf.Pi * 2.0f);

            while (ret > Mathf.Pi)
                ret -= (Mathf.Pi * 2.0f);

            return ret;
        }

        public static float DirectionToDegrees(Vector2 from, Vector2 to)
        {
            var radian = DirectionToRadian(from, to);
            var degree = RadianToDegrees(radian);

            if ((to.X - from.X) > 0.0f)
                degree = -degree;

            return degree;
        }

        public static Vector2 RadianToDirection(float totalAngle)
        {
            return new Vector2(Mathf.Sin(totalAngle), Mathf.Cos(totalAngle));
        }

        private static float GetRangeValue(float min, float max)
        {
            var maxValue = Mathf.Max(min, max);
            var minValue = Mathf.Min(min, max);
            return Mathf.Abs(maxValue - minValue);
        }

        private static float GetDefaultValue(float min, float max)
        {
            var minValue = Mathf.Min(min, max);
            return minValue + (GetRangeValue(min, max) / 2.0f);
        }

        /// <summary>
        /// Normalizes a parameter value. Mirrors Unity CubismPhysicsMath.Normalize exactly.
        /// </summary>
        public static float Normalize(
            CubismParameter parameter,
            ref float parameterValue,
            float normalizedMinimum,
            float normalizedMaximum,
            float normalizedDefault,
            bool isInverted = false)
        {
            var result = 0.0f;

            var maxValue = Mathf.Max(parameter.MaximumValue, parameter.MinimumValue);
            if (maxValue < parameterValue)
                parameterValue = maxValue;

            var minValue = Mathf.Min(parameter.MaximumValue, parameter.MinimumValue);
            if (minValue > parameterValue)
                parameterValue = minValue;

            var minNormValue = Mathf.Min(normalizedMinimum, normalizedMaximum);
            var maxNormValue = Mathf.Max(normalizedMinimum, normalizedMaximum);
            var middleNormValue = normalizedDefault;

            var middleValue = GetDefaultValue(minValue, maxValue);
            var paramValue = parameterValue - middleValue;

            switch ((int)Mathf.Sign(paramValue))
            {
                case 1:
                {
                    var nLength = maxNormValue - middleNormValue;
                    var pLength = maxValue - middleValue;
                    if (pLength != 0.0f)
                    {
                        result = paramValue * (nLength / pLength);
                        result += middleNormValue;
                    }
                    break;
                }
                case -1:
                {
                    var nLength = minNormValue - middleNormValue;
                    var pLength = minValue - middleValue;
                    if (pLength != 0.0f)
                    {
                        result = paramValue * (nLength / pLength);
                        result += middleNormValue;
                    }
                    break;
                }
                case 0:
                {
                    result = middleNormValue;
                    break;
                }
            }

            return isInverted ? result : (result * -1.0f);
        }
    }
}
