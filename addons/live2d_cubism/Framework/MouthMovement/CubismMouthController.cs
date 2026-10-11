/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core;

namespace Live2D.Cubism.Framework.MouthMovement
{
    /// <summary>
    /// Mouth movement controller. Mirrors Unity CubismMouthController:
    /// default blend is Multiply, default MouthOpening is 1, applied via BlendToValue.
    /// </summary>
    public class CubismMouthController
    {
        public string[] MouthParameterIds { get; set; }
        public CubismParameterBlendMode BlendMode { get; set; } = CubismParameterBlendMode.Multiply;
        public float MouthOpening { get; set; } = 1f;

        private CubismParameter[] _parameters;
        private CubismParameter[] _mouthParameters;

        public void SetParameters(CubismParameter[] parameters)
        {
            _parameters = parameters;
            ResolveMouthParameters();
        }

        private void ResolveMouthParameters()
        {
            if (_parameters == null || MouthParameterIds == null)
            {
                _mouthParameters = null;
                return;
            }

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
            if (_mouthParameters == null)
                return;

            _mouthParameters.BlendToValue(BlendMode, MouthOpening);
        }
    }
}
