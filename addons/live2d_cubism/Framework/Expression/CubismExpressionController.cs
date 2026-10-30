/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.Collections.Generic;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Json;
using Godot;

namespace Live2D.Cubism.Framework.Expression
{
    /// <summary>
    /// Controls expression playback. Mirrors Unity CubismExpressionController.
    ///
    /// Expression blending follows the Unity SDK:
    /// - Additive: parameter.Value += expressionValue * weight
    /// - Multiply: parameter.Value *= 1.0 + (expressionValue - 1.0) * weight
    /// - Override: parameter.Value = parameter.Value * (1.0 - weight) + expressionValue * weight
    /// </summary>
    public class CubismExpressionController
    {
        public CubismExp3Json[] Expressions { get; set; }

        private readonly List<PlayingExpression> _playingExpressions = new();
        private CubismParameter[] _parameters;

        public float FadeTime { get; set; } = 0.5f;
        public int CurrentExpressionIndex { get; private set; } = -1;

        public void SetParameters(CubismParameter[] parameters)
        {
            _parameters = parameters;
        }

        /// <summary>
        /// Start playing an expression by index. Marks any previously active expressions
        /// as fading out so they cross-fade cleanly instead of stacking indefinitely.
        /// Passing a negative index fades out all active expressions.
        /// </summary>
        public void StartExpression(int index)
        {
            if (index < 0)
            {
                StopAllExpressions();
                return;
            }

            if (Expressions == null || index >= Expressions.Length) return;

            var exp = Expressions[index];
            if (exp == null) return;

            foreach (var pe in _playingExpressions)
                pe.IsFadingOut = true;

            CurrentExpressionIndex = index;
            _playingExpressions.Add(new PlayingExpression
            {
                Expression = exp,
                Weight = 0f,
                FadeInTime = exp.FadeInTime >= 0f ? exp.FadeInTime : FadeTime,
                FadeOutTime = exp.FadeOutTime >= 0f ? exp.FadeOutTime : FadeTime,
                ElapsedTime = 0f,
                IsFadingOut = false
            });
        }

        /// <summary>
        /// Start playing an expression by its name (from model3.json FileReferences.Expressions).
        /// </summary>
        public bool StartExpression(string expressionName)
        {
            if (Expressions == null || string.IsNullOrEmpty(expressionName)) return false;

            for (int i = 0; i < Expressions.Length; i++)
            {
                if (Expressions[i] != null &&
                    string.Equals(Expressions[i].Name, expressionName, StringComparison.Ordinal))
                {
                    StartExpression(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Stop all expressions with fade out.
        /// </summary>
        public void StopAllExpressions()
        {
            CurrentExpressionIndex = -1;
            foreach (var pe in _playingExpressions)
                pe.IsFadingOut = true;
        }

        /// <summary>
        /// Update expression weights and apply to parameters in FIFO order so newer
        /// expressions blend over older fading-out expressions.
        /// Mirrors Unity CubismExpressionController.OnLateUpdate.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (_parameters == null || Expressions == null) return;

            for (int i = 0; i < _playingExpressions.Count; i++)
            {
                var pe = _playingExpressions[i];
                pe.ElapsedTime += deltaTime;

                if (pe.IsFadingOut)
                {
                    if (pe.FadeOutTime <= 0f)
                    {
                        _playingExpressions.RemoveAt(i--);
                        continue;
                    }

                    pe.Weight -= deltaTime / pe.FadeOutTime;
                    if (pe.Weight <= 0f)
                    {
                        _playingExpressions.RemoveAt(i--);
                        continue;
                    }
                }
                else
                {
                    if (pe.Weight < 1f)
                    {
                        if (pe.FadeInTime <= 0f)
                            pe.Weight = 1f;
                        else
                        {
                            pe.Weight += deltaTime / pe.FadeInTime;
                            if (pe.Weight > 1f) pe.Weight = 1f;
                        }
                    }
                }

                ApplyExpression(pe);
            }
        }

        private void ApplyExpression(PlayingExpression pe)
        {
            if (pe.Expression.Parameters == null) return;

            foreach (var ep in pe.Expression.Parameters)
            {
                var param = FindParameter(ep.Id);
                if (param == null) continue;

                var weight = Mathf.Clamp(pe.Weight, 0f, 1f);

                switch (ep.Blend)
                {
                    case CubismExp3Json.BlendModeAdditive:
                        param.Value += ep.Value * weight;
                        break;
                    case CubismExp3Json.BlendModeMultiply:
                        param.Value *= 1.0f + (ep.Value - 1.0f) * weight;
                        break;
                    case CubismExp3Json.BlendModeOverride:
                    default:
                        param.Value = param.Value * (1.0f - weight) + ep.Value * weight;
                        break;
                }
            }
        }

        private CubismParameter FindParameter(string id)
        {
            if (_parameters == null) return null;
            for (int i = 0; i < _parameters.Length; i++)
                if (_parameters[i].Id == id) return _parameters[i];
            return null;
        }

        private class PlayingExpression
        {
            public CubismExp3Json Expression;
            public float Weight;
            public float FadeInTime;
            public float FadeOutTime;
            public float ElapsedTime;
            public bool IsFadingOut;
        }
    }
}
