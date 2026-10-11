/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core;
using Godot;

namespace Live2D.Cubism.Framework.HarmonicMotion
{
    /// <summary>
    /// Direction of harmonic motion.
    /// </summary>
    public enum CubismHarmonicMotionDirection
    {
        Left,
        Right,
        Centric
    }

    /// <summary>
    /// Configuration for a single harmonic motion parameter.
    /// </summary>
    public class CubismHarmonicMotionParameter
    {
        public string ParameterId { get; set; }
        public int Channel { get; set; }
        public CubismHarmonicMotionDirection Direction { get; set; } = CubismHarmonicMotionDirection.Centric;
        public float NormalizedMinimum { get; set; } = 0f;
        public float NormalizedMaximum { get; set; } = 1f;
        public float NormalizedDefault { get; set; } = 0.5f;
        public float Duration { get; set; } = 3f;
    }

    /// <summary>
    /// Harmonic motion controller. Mirrors Unity CubismHarmonicMotionController exactly.
    /// Generates sinusoidal motion on assigned parameters.
    /// </summary>
    public class CubismHarmonicMotionController
    {
        public CubismParameterBlendMode BlendMode { get; set; } = CubismParameterBlendMode.Additive;
        public int ChannelCount { get; set; } = 1;

        public CubismHarmonicMotionParameter[] HarmonicParameters { get; set; }

        private CubismParameter[] _parameters;
        private float[] _channelTimescales;

        public void SetParameters(CubismParameter[] parameters)
        {
            _parameters = parameters;
            _channelTimescales = new float[ChannelCount];
            for (int i = 0; i < ChannelCount; i++)
                _channelTimescales[i] = 1f;
        }

        public void SetChannelTimescale(int channel, float timescale)
        {
            if (channel >= 0 && channel < _channelTimescales.Length)
                _channelTimescales[channel] = timescale;
        }

        private float _totalTime;

        public void Update(float deltaTime)
        {
            if (_parameters == null || HarmonicParameters == null) return;

            _totalTime += deltaTime;

            foreach (var hp in HarmonicParameters)
            {
                var param = FindParameter(hp.ParameterId);
                if (param == null) continue;

                var channelScale = (hp.Channel >= 0 && hp.Channel < _channelTimescales.Length)
                    ? _channelTimescales[hp.Channel] : 1f;

                var t = (_totalTime * channelScale) / hp.Duration;
                var sinValue = Mathf.Sin(t * Mathf.Pi * 2f);

                float value;
                switch (hp.Direction)
                {
                    case CubismHarmonicMotionDirection.Left:
                        value = Mathf.Lerp(hp.NormalizedDefault, hp.NormalizedMinimum, Mathf.Abs(sinValue));
                        break;
                    case CubismHarmonicMotionDirection.Right:
                        value = Mathf.Lerp(hp.NormalizedDefault, hp.NormalizedMaximum, Mathf.Abs(sinValue));
                        break;
                    case CubismHarmonicMotionDirection.Centric:
                    default:
                        if (sinValue >= 0)
                            value = Mathf.Lerp(hp.NormalizedDefault, hp.NormalizedMaximum, sinValue);
                        else
                            value = Mathf.Lerp(hp.NormalizedDefault, hp.NormalizedMinimum, -sinValue);
                        break;
                }

                var range = param.MaximumValue - param.MinimumValue;
                value = param.MinimumValue + value * range;

                switch (BlendMode)
                {
                    case CubismParameterBlendMode.Override:
                        param.Value = value;
                        break;
                    case CubismParameterBlendMode.Additive:
                        param.Value += value;
                        break;
                    case CubismParameterBlendMode.Multiply:
                        param.Value *= value;
                        break;
                }
            }
        }

        private CubismParameter FindParameter(string id)
        {
            for (int i = 0; i < _parameters.Length; i++)
                if (_parameters[i].Id == id) return _parameters[i];
            return null;
        }
    }
}
