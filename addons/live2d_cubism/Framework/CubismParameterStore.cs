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
    /// Stores and restores parameter values. Mirrors Unity CubismParameterStore.
    /// Used to save parameter state before framework updates and restore afterwards.
    /// </summary>
    public class CubismParameterStore
    {
        private float[] _savedParameterValues;
        private float[] _savedPartOpacities;
        private CubismParameter[] _parameters;
        private CubismPart[] _parts;

        public void Initialize(CubismParameter[] parameters, CubismPart[] parts)
        {
            _parameters = parameters;
            _parts = parts;
            _savedParameterValues = new float[parameters.Length];
            _savedPartOpacities = new float[parts.Length];
            SaveParameters();
        }

        public void SaveParameters()
        {
            if (_parameters == null || _savedParameterValues == null)
                return;
            for (int i = 0; i < _parameters.Length; i++)
                _savedParameterValues[i] = _parameters[i].Value;
            if (_parts == null || _savedPartOpacities == null)
                return;
            for (int i = 0; i < _parts.Length; i++)
                _savedPartOpacities[i] = _parts[i].Opacity;
        }

        public void RestoreParameters()
        {
            if (_parameters == null || _savedParameterValues == null)
                return;
            for (int i = 0; i < _parameters.Length; i++)
                _parameters[i].Value = _savedParameterValues[i];
            if (_parts == null || _savedPartOpacities == null)
                return;
            for (int i = 0; i < _parts.Length; i++)
                _parts[i].Opacity = _savedPartOpacities[i];
        }

        public void Refresh()
        {
            if (_savedParameterValues == null || _savedParameterValues.Length != _parameters.Length)
                _savedParameterValues = new float[_parameters.Length];
            if (_savedPartOpacities == null || _savedPartOpacities.Length != _parts.Length)
                _savedPartOpacities = new float[_parts.Length];
        }
    }
}
