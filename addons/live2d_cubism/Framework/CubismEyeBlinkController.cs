/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core;
using Godot;

namespace Live2D.Cubism.Framework
{
    /// <summary>
    /// Automatic eye blink controller. Mirrors Unity CubismEyeBlinkController + CubismAutoEyeBlinkInput.
    ///
    /// Timing (mirrors Unity exactly):
    /// - Mean blink interval: ~3.5 seconds (random 2.0–6.0s)
    /// - Close phase: 0.1s
    /// - Closed phase: 0.05s
    /// - Open phase: 0.15s
    /// </summary>
    public class CubismEyeBlinkController
    {
        public string[] EyeBlinkParameterIds { get; set; }

        public CubismParameterBlendMode BlendMode { get; set; } = CubismParameterBlendMode.Multiply;

        private CubismParameter[] _parameters;
        private CubismParameter[] _eyeBlinkParameters;

        private float _blinkValue = 1f;
        private float _nextBlinkTime;
        private float _stateTimer;
        private BlinkState _state = BlinkState.Idle;
        private readonly RandomNumberGenerator _rng = new();

        private const float ClosingDuration = 0.1f;
        private const float ClosedDuration = 0.05f;
        private const float OpeningDuration = 0.15f;

        private enum BlinkState
        {
            Idle,
            Closing,
            Closed,
            Opening
        }

        public void SetParameters(CubismParameter[] parameters)
        {
            _parameters = parameters;
            _nextBlinkTime = GetNextBlinkInterval();
            ResolveEyeBlinkParameters();
        }

        private void ResolveEyeBlinkParameters()
        {
            if (_parameters == null || EyeBlinkParameterIds == null) return;

            var list = new System.Collections.Generic.List<CubismParameter>();
            foreach (var id in EyeBlinkParameterIds)
            {
                for (int i = 0; i < _parameters.Length; i++)
                {
                    if (_parameters[i].Id == id)
                    {
                        list.Add(_parameters[i]);
                        break;
                    }
                }
            }
            _eyeBlinkParameters = list.ToArray();
        }

        private float GetNextBlinkInterval()
        {
            return _rng.RandfRange(2.0f, 6.0f);
        }

        public void Update(float deltaTime)
        {
            if (_eyeBlinkParameters == null || _eyeBlinkParameters.Length == 0) return;

            switch (_state)
            {
                case BlinkState.Idle:
                    _stateTimer += deltaTime;
                    if (_stateTimer >= _nextBlinkTime)
                    {
                        _state = BlinkState.Closing;
                        _stateTimer = 0f;
                    }
                    _blinkValue = 1f;
                    break;

                case BlinkState.Closing:
                    _stateTimer += deltaTime;
                    _blinkValue = 1f - (_stateTimer / ClosingDuration);
                    if (_stateTimer >= ClosingDuration)
                    {
                        _state = BlinkState.Closed;
                        _stateTimer = 0f;
                        _blinkValue = 0f;
                    }
                    break;

                case BlinkState.Closed:
                    _stateTimer += deltaTime;
                    _blinkValue = 0f;
                    if (_stateTimer >= ClosedDuration)
                    {
                        _state = BlinkState.Opening;
                        _stateTimer = 0f;
                    }
                    break;

                case BlinkState.Opening:
                    _stateTimer += deltaTime;
                    _blinkValue = _stateTimer / OpeningDuration;
                    if (_stateTimer >= OpeningDuration)
                    {
                        _state = BlinkState.Idle;
                        _stateTimer = 0f;
                        _nextBlinkTime = GetNextBlinkInterval();
                        _blinkValue = 1f;
                    }
                    break;
            }

            _blinkValue = Mathf.Clamp(_blinkValue, 0f, 1f);

            foreach (var param in _eyeBlinkParameters)
            {
                switch (BlendMode)
                {
                    case CubismParameterBlendMode.Override:
                        param.Value = _blinkValue;
                        break;
                    case CubismParameterBlendMode.Additive:
                        param.Value += _blinkValue;
                        break;
                    case CubismParameterBlendMode.Multiply:
                        param.Value *= _blinkValue;
                        break;
                }
            }
        }
    }
}
