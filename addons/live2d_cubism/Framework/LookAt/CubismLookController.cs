/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core;
using Godot;

namespace Live2D.Cubism.Framework.LookAt
{
    /// <summary>
    /// Look direction axis. Mirrors Unity CubismLookAxis.
    /// </summary>
    public enum CubismLookAxis
    {
        X,
        Y,
        Z
    }

    /// <summary>
    /// Look-at parameter binding.
    /// </summary>
    public class CubismLookParameter
    {
        public string ParameterId { get; set; }
        public CubismLookAxis Axis { get; set; }
        public float Factor { get; set; } = 1f;
    }

    /// <summary>
    /// Look-at controller. Mirrors Unity CubismLookController.
    /// Drives look parameters based on a target position relative to a center.
    /// </summary>
    public class CubismLookController
    {
        public CubismParameterBlendMode BlendMode { get; set; } = CubismParameterBlendMode.Additive;
        public float Damping { get; set; } = 0.15f;

        public CubismLookParameter[] LookParameters { get; set; }

        /// <summary>
        /// Target position in model-local space (values typically -1..1).
        /// </summary>
        public Vector3 TargetPosition { get; set; }

        /// <summary>
        /// Center offset (mirrors Unity CubismLookCenterTransform).
        /// </summary>
        public Vector3 CenterPosition { get; set; }

        private CubismParameter[] _parameters;
        private Vector3 _lastPosition;

        public void SetParameters(CubismParameter[] parameters)
        {
            _parameters = parameters;
        }

        public void Update(float deltaTime)
        {
            if (_parameters == null || LookParameters == null) return;

            var targetDir = TargetPosition - CenterPosition;

            // Damping (same as Unity's Mathf.Lerp-based damping)
            var t = 1f - Mathf.Pow(Damping, deltaTime);
            _lastPosition = _lastPosition.Lerp(targetDir, t);

            foreach (var lp in LookParameters)
            {
                var param = FindParameter(lp.ParameterId);
                if (param == null) continue;

                float value = lp.Axis switch
                {
                    CubismLookAxis.X => _lastPosition.X * lp.Factor,
                    CubismLookAxis.Y => _lastPosition.Y * lp.Factor,
                    CubismLookAxis.Z => _lastPosition.Z * lp.Factor,
                    _ => 0f
                };

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
