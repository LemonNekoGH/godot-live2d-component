/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core;

namespace Live2D.Cubism.Framework
{
    /// <summary>
    /// Eye-blink parameter controller. Mirrors Unity CubismEyeBlinkController:
    /// applies <see cref="EyeOpening"/> via <see cref="CubismParameterExtensionMethods.BlendToValue"/>.
    /// Timing is owned by <see cref="AutoInput"/> (Unity CubismAutoEyeBlinkInput).
    /// </summary>
    public class CubismEyeBlinkController
    {
        public string[] EyeBlinkParameterIds { get; set; }

        public CubismParameterBlendMode BlendMode { get; set; } = CubismParameterBlendMode.Multiply;

        public float EyeOpening { get; set; } = 1f;

        public bool EnableAutoInput { get; set; } = true;

        public CubismAutoEyeBlinkInput AutoInput { get; } = new();

        private CubismParameter[] _parameters;
        private CubismParameter[] _eyeBlinkParameters;

        public void SetParameters(CubismParameter[] parameters)
        {
            _parameters = parameters;
            ResolveEyeBlinkParameters();
            AutoInput.Reset();
            EyeOpening = 1f;
        }

        private void ResolveEyeBlinkParameters()
        {
            if (_parameters == null || EyeBlinkParameterIds == null)
            {
                _eyeBlinkParameters = null;
                return;
            }

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

        public void Update(float deltaTime)
        {
            if (_eyeBlinkParameters == null || _eyeBlinkParameters.Length == 0)
                return;

            if (EnableAutoInput)
                EyeOpening = AutoInput.Evaluate(deltaTime);

            _eyeBlinkParameters.BlendToValue(BlendMode, EyeOpening);
        }
    }
}
