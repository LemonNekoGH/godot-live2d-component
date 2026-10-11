/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using Godot;
using Live2D.Cubism.Core;

namespace Live2D.Cubism.Framework.Physics
{
    /// <summary>
    /// Physics rig containing sub-rigs. Mirrors Unity CubismPhysicsRig exactly.
    /// </summary>
    public class CubismPhysicsRig
    {
        public CubismPhysicsSubRig[] SubRigs;
        public Vector2 Gravity = CubismPhysics.Gravity;
        public Vector2 Wind = CubismPhysics.Wind;
        public float Fps = 0.0f;

        private float _currentRemainTime;
        public float[] ParametersCache;
        private float[] _parametersInputCache;

        private CubismParameter[] _parameters;

        public void SetParameters(CubismParameter[] parameters)
        {
            _parameters = parameters;
        }

        public CubismParameter FindParameter(string id)
        {
            if (_parameters == null) return null;
            for (int i = 0; i < _parameters.Length; i++)
                if (_parameters[i].Id == id) return _parameters[i];
            return null;
        }

        public int FindParameterIndex(string id)
        {
            if (_parameters == null) return -1;
            for (int i = 0; i < _parameters.Length; i++)
                if (_parameters[i].Id == id) return i;
            return -1;
        }

        public CubismPhysicsSubRig GetSubRig(string name)
        {
            for (int i = 0; i < SubRigs.Length; i++)
                if (SubRigs[i].Name == name) return SubRigs[i];
            return null;
        }

        public void Initialize()
        {
            _currentRemainTime = 0.0f;
            ParametersCache = new float[_parameters.Length];
            _parametersInputCache = new float[_parameters.Length];

            for (var i = 0; i < SubRigs.Length; ++i)
            {
                SubRigs[i].Rig = this;
                SubRigs[i].Initialize();
            }
        }

        public void Stabilization()
        {
            if (_parameters == null) return;

            if (ParametersCache == null)
                ParametersCache = new float[_parameters.Length];
            if (ParametersCache.Length < _parameters.Length)
                Array.Resize(ref ParametersCache, _parameters.Length);
            if (_parametersInputCache == null)
                _parametersInputCache = new float[_parameters.Length];
            if (_parametersInputCache.Length < _parameters.Length)
                Array.Resize(ref _parametersInputCache, _parameters.Length);

            for (var i = 0; i < _parameters.Length; i++)
            {
                ParametersCache[i] = _parameters[i].Value;
                _parametersInputCache[i] = ParametersCache[i];
            }

            for (var i = 0; i < SubRigs.Length; ++i)
                SubRigs[i].Stabilization();
        }

        /// <summary>
        /// Evaluate physics with interpolation. Mirrors Unity CubismPhysicsRig.Evaluate exactly.
        /// </summary>
        public void Evaluate(float deltaTime)
        {
            if (0.0f >= deltaTime) return;

            _currentRemainTime += deltaTime;
            if (_currentRemainTime > CubismPhysics.MaxDeltaTime)
                _currentRemainTime = 0.0f;

            var physicsDeltaTime = Fps > 0.0f ? (1.0f / Fps) : deltaTime;

            if (ParametersCache == null)
                ParametersCache = new float[_parameters.Length];
            if (ParametersCache.Length < _parameters.Length)
                Array.Resize(ref ParametersCache, _parameters.Length);
            if (_parametersInputCache == null)
                _parametersInputCache = new float[_parameters.Length];
            if (_parametersInputCache.Length < _parameters.Length)
            {
                Array.Resize(ref _parametersInputCache, _parameters.Length);
                for (var i = 0; i < _parametersInputCache.Length; i++)
                    _parametersInputCache[i] = ParametersCache[i];
            }

            while (_currentRemainTime >= physicsDeltaTime)
            {
                var inputWeight = physicsDeltaTime / _currentRemainTime;

                for (var i = 0; i < _parameters.Length; i++)
                {
                    ParametersCache[i] = _parametersInputCache[i] * (1.0f - inputWeight)
                                         + _parameters[i].Value * inputWeight;
                    _parametersInputCache[i] = ParametersCache[i];
                }

                for (var i = 0; i < SubRigs.Length; ++i)
                    SubRigs[i].Evaluate(physicsDeltaTime);

                _currentRemainTime -= physicsDeltaTime;
            }

            float alpha = _currentRemainTime / physicsDeltaTime;
            for (var i = 0; i < SubRigs.Length; ++i)
                SubRigs[i].Interpolate(alpha);
        }
    }
}
