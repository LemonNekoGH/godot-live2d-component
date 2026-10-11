/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using Live2D.Cubism.Core.Unmanaged;

namespace Live2D.Cubism.Core
{
    /// <summary>
    /// Represents a single Cubism model parameter.
    /// Mirrors Unity's CubismParameter MonoBehaviour.
    /// </summary>
    public sealed class CubismParameter
    {
        public string Id { get; internal set; }
        public int UnmanagedIndex { get; internal set; } = -1;
        public float Value;
        public float MinimumValue { get; internal set; }
        public float MaximumValue { get; internal set; }
        public float DefaultValue { get; internal set; }
        public int Type { get; internal set; }

        private bool _isOverriddenUserParameterRepeat;
        private bool _isParameterRepeated;
        private bool _coreRepeat;

        internal void Initialize(CubismUnmanagedParameters unmanaged, int index)
        {
            UnmanagedIndex = index;
            Id = unmanaged.Ids[index];
            MinimumValue = unmanaged.GetMinimumValue(index);
            MaximumValue = unmanaged.GetMaximumValue(index);
            DefaultValue = unmanaged.GetDefaultValue(index);
            Type = unmanaged.GetType(index);
            Value = DefaultValue;
            _coreRepeat = unmanaged.GetRepeat(index) != 0;
        }

        public bool IsRepeat(bool modelOverride)
        {
            if (modelOverride || _isOverriddenUserParameterRepeat)
                return _isParameterRepeated;
            return _coreRepeat;
        }

        /// <summary>
        /// Parameter-only repeat query. Mirrors Unity CubismParameter.IsRepeat when the
        /// model-level override flag is not available on this object.
        /// </summary>
        public bool IsRepeat() => IsRepeat(false);

        /// <summary>
        /// Wraps value to stay within [MinimumValue, MaximumValue] using modular arithmetic.
        /// Mirrors Unity CubismParameter.GetParameterRepeatValue exactly.
        /// </summary>
        public float GetParameterRepeatValue(float value)
        {
            var maxValue = MaximumValue;
            var minValue = MinimumValue;
            var valueSize = maxValue - minValue;

            if (maxValue < value)
            {
                var overValue = CubismMath.ModF(value - maxValue, valueSize);
                if (!float.IsNaN(overValue))
                    value = minValue + overValue;
                else
                    value = maxValue;
            }
            if (value < minValue)
            {
                var overValue = CubismMath.ModF(minValue - value, valueSize);
                if (!float.IsNaN(overValue))
                    value = maxValue - overValue;
                else
                    value = minValue;
            }

            return value;
        }

        /// <summary>
        /// Clamps value to [MinimumValue, MaximumValue].
        /// </summary>
        public float GetParameterClampValue(float value)
        {
            return CubismMath.ClampF(value, MinimumValue, MaximumValue);
        }

        public void SetOverrideFlagForParameterRepeat(bool value) => _isOverriddenUserParameterRepeat = value;
        public bool GetOverrideFlagForParameterRepeat() => _isOverriddenUserParameterRepeat;
        public void SetRepeatFlagForParameterRepeat(bool value) => _isParameterRepeated = value;
        public bool GetRepeatFlagForParameterRepeat() => _isParameterRepeated;
    }

    /// <summary>
    /// Math utilities mirroring Unity's CubismMath.
    /// </summary>
    public static class CubismMath
    {
        public static float ModF(float a, float b)
        {
            return a - b * (float)System.Math.Floor(a / b);
        }

        public static float ClampF(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public const float Epsilon = 0.00001f;
        public const float Pi = 3.14159265358979323846f;

        public static float RangeF(float value, float min, float max) => ClampF(value, min, max);

        /// <summary>Native CubismMath.GetEasingSine / Unity CubismFadeMath.GetEasingSine.</summary>
        public static float GetEasingSine(float value)
        {
            if (value < 0.0f) return 0.0f;
            if (value > 1.0f) return 1.0f;
            return 0.5f - 0.5f * System.MathF.Cos(value * System.MathF.PI);
        }

        /// <summary>Native CubismMath.QuadraticEquation.</summary>
        public static float QuadraticEquation(float a, float b, float c)
        {
            if (MathF.Abs(a) < Epsilon)
            {
                if (MathF.Abs(b) < Epsilon)
                    return -c;
                return -c / b;
            }

            return -(b + MathF.Sqrt(b * b - 4.0f * a * c)) / (2.0f * a);
        }

        /// <summary>Native CubismMath.CardanoAlgorithmForBezier. Used when AreBeziersRestricted is false.</summary>
        public static float CardanoAlgorithmForBezier(float a, float b, float c, float d)
        {
            if (MathF.Abs(a) < Epsilon)
                return RangeF(QuadraticEquation(b, c, d), 0.0f, 1.0f);

            float ba = b / a;
            float ca = c / a;
            float da = d / a;

            float p = (3.0f * ca - ba * ba) / 3.0f;
            float p3 = p / 3.0f;
            float q = (2.0f * ba * ba * ba - 9.0f * ba * ca + 27.0f * da) / 27.0f;
            float q2 = q / 2.0f;
            float discriminant = q2 * q2 + p3 * p3 * p3;

            const float center = 0.5f;
            const float threshold = center + 0.01f;

            if (discriminant < 0.0f)
            {
                float mp3 = -p / 3.0f;
                float mp33 = mp3 * mp3 * mp3;
                float r = MathF.Sqrt(mp33);
                float t = -q / (2.0f * r);
                float cosphi = RangeF(t, -1.0f, 1.0f);
                float phi = MathF.Acos(cosphi);
                float crtr = MathF.Cbrt(r);
                float t1 = 2.0f * crtr;

                float root1 = t1 * MathF.Cos(phi / 3.0f) - ba / 3.0f;
                if (MathF.Abs(root1 - center) < threshold)
                    return RangeF(root1, 0.0f, 1.0f);

                float root2 = t1 * MathF.Cos((phi + 2.0f * MathF.PI) / 3.0f) - ba / 3.0f;
                if (MathF.Abs(root2 - center) < threshold)
                    return RangeF(root2, 0.0f, 1.0f);

                float root3 = t1 * MathF.Cos((phi + 4.0f * MathF.PI) / 3.0f) - ba / 3.0f;
                return RangeF(root3, 0.0f, 1.0f);
            }

            if (discriminant == 0.0f)
            {
                float u1 = q2 < 0.0f ? MathF.Cbrt(-q2) : -MathF.Cbrt(q2);
                float root1 = 2.0f * u1 - ba / 3.0f;
                if (MathF.Abs(root1 - center) < threshold)
                    return RangeF(root1, 0.0f, 1.0f);

                float root2 = -u1 - ba / 3.0f;
                return RangeF(root2, 0.0f, 1.0f);
            }

            float sd = MathF.Sqrt(discriminant);
            float u = MathF.Cbrt(sd - q2);
            float v = MathF.Cbrt(sd + q2);
            return RangeF(u - v - ba / 3.0f, 0.0f, 1.0f);
        }
    }
}
