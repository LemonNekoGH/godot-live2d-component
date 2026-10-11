/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

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
    }
}
