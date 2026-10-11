/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core;
using Godot;

namespace Live2D.Cubism.Framework.MouthMovement
{
    /// <summary>
    /// Mouth movement controller. Mirrors Unity CubismMouthController.
    /// Drives mouth parameters with an input value (0–1).
    /// </summary>
    public class CubismMouthController
    {
        public string[] MouthParameterIds { get; set; }
        public CubismParameterBlendMode BlendMode { get; set; } = CubismParameterBlendMode.Additive;
        public float MouthOpening { get; set; }

        private CubismParameter[] _parameters;
        private CubismParameter[] _mouthParameters;

        public void SetParameters(CubismParameter[] parameters)
        {
            _parameters = parameters;
            ResolveMouthParameters();
        }

        private void ResolveMouthParameters()
        {
            if (_parameters == null || MouthParameterIds == null) return;

            var list = new System.Collections.Generic.List<CubismParameter>();
            foreach (var id in MouthParameterIds)
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
            _mouthParameters = list.ToArray();
        }

        public void Update(float deltaTime)
        {
            if (_mouthParameters == null) return;

            foreach (var param in _mouthParameters)
            {
                switch (BlendMode)
                {
                    case CubismParameterBlendMode.Override:
                        param.Value = MouthOpening;
                        break;
                    case CubismParameterBlendMode.Additive:
                        param.Value += MouthOpening;
                        break;
                    case CubismParameterBlendMode.Multiply:
                        param.Value *= MouthOpening;
                        break;
                }
            }
        }
    }
}
