/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System.Collections.Generic;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Json;
using Godot;

namespace Live2D.Cubism.Framework.Motion
{
    /// <summary>
    /// Motion priority levels. Mirrors Unity CubismMotionPriority.
    /// </summary>
    public enum CubismMotionPriority
    {
        PriorityNone = 0,
        PriorityIdle = 1,
        PriorityNormal = 2,
        PriorityForce = 3
    }

    /// <summary>
    /// Represents a playing motion instance.
    /// </summary>
    public class CubismPlayingMotion
    {
        public CubismMotion3Json Motion { get; set; }
        public float CurrentTime { get; set; }
        public float Weight { get; set; } = 1f;
        public float FadeInTime { get; set; } = 1f;
        public float FadeOutTime { get; set; } = 1f;
        public bool IsLooping { get; set; }
        public bool IsFinished { get; set; }
        public bool IsFadingOut { get; set; }
        public CubismMotionPriority Priority { get; set; }
    }

    /// <summary>
    /// Motion playback controller. Mirrors Unity CubismMotionController / CubismFadeController.
    ///
    /// Motion evaluation:
    /// - Curves are evaluated using CubismMotion3Json.EvaluateCurve (same segment math as Unity)
    /// - Target types: "Parameter", "PartOpacity", "Model"
    /// - Fade in/out uses linear weight ramp
    /// - Multiple motions can play simultaneously with priority-based layering
    /// </summary>
    public class CubismMotionController
    {
        private readonly List<CubismPlayingMotion> _playingMotions = new();

        private CubismParameter[] _parameters;
        private CubismPart[] _parts;

        public void SetModel(CubismParameter[] parameters, CubismPart[] parts)
        {
            _parameters = parameters;
            _parts = parts;
        }

        /// <summary>
        /// Starts playing a motion. Returns the playing instance.
        /// </summary>
        public CubismPlayingMotion PlayMotion(
            CubismMotion3Json motion,
            CubismMotionPriority priority = CubismMotionPriority.PriorityNormal,
            bool isLooping = false,
            float fadeInOverride = -1f,
            float fadeOutOverride = -1f)
        {
            if (motion == null) return null;

            // Fade out lower-priority motions
            foreach (var pm in _playingMotions)
            {
                if (pm.Priority <= priority && !pm.IsFadingOut)
                    pm.IsFadingOut = true;
            }

            var playing = new CubismPlayingMotion
            {
                Motion = motion,
                CurrentTime = 0f,
                Weight = 0f,
                FadeInTime = fadeInOverride >= 0 ? fadeInOverride :
                    (motion.Meta?.FadeInTime >= 0 ? motion.Meta.FadeInTime : 1f),
                FadeOutTime = fadeOutOverride >= 0 ? fadeOutOverride :
                    (motion.Meta?.FadeOutTime >= 0 ? motion.Meta.FadeOutTime : 1f),
                IsLooping = isLooping || (motion.Meta?.Loop ?? false),
                Priority = priority
            };

            _playingMotions.Add(playing);
            return playing;
        }

        /// <summary>
        /// Stops all motions.
        /// </summary>
        public void StopAll()
        {
            foreach (var pm in _playingMotions)
                pm.IsFadingOut = true;
        }

        /// <summary>
        /// Updates all playing motions and applies them to parameters.
        /// Mirrors Unity motion evaluation exactly.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (_parameters == null) return;

            for (int i = _playingMotions.Count - 1; i >= 0; i--)
            {
                var pm = _playingMotions[i];
                pm.CurrentTime += deltaTime;

                var duration = pm.Motion.Meta?.Duration ?? 0f;

                // Handle looping
                if (pm.IsLooping && duration > 0f && pm.CurrentTime > duration)
                {
                    pm.CurrentTime %= duration;
                }

                // Fade in
                if (!pm.IsFadingOut)
                {
                    if (pm.FadeInTime > 0f && pm.Weight < 1f)
                    {
                        pm.Weight += deltaTime / pm.FadeInTime;
                        if (pm.Weight > 1f) pm.Weight = 1f;
                    }
                    else if (pm.FadeInTime <= 0f)
                    {
                        pm.Weight = 1f;
                    }
                }

                // Check if finished (non-looping)
                if (!pm.IsLooping && duration > 0f && pm.CurrentTime >= duration)
                {
                    pm.IsFadingOut = true;
                }

                // Fade out
                if (pm.IsFadingOut)
                {
                    if (pm.FadeOutTime > 0f)
                    {
                        pm.Weight -= deltaTime / pm.FadeOutTime;
                        if (pm.Weight <= 0f)
                        {
                            pm.Weight = 0f;
                            pm.IsFinished = true;
                        }
                    }
                    else
                    {
                        pm.Weight = 0f;
                        pm.IsFinished = true;
                    }
                }

                if (pm.IsFinished)
                {
                    _playingMotions.RemoveAt(i);
                    continue;
                }

                // Apply curves
                ApplyMotion(pm);
            }
        }

        private void ApplyMotion(CubismPlayingMotion pm)
        {
            if (pm.Motion.Curves == null) return;

            var weight = Mathf.Clamp(pm.Weight, 0f, 1f);

            foreach (var curve in pm.Motion.Curves)
            {
                var value = CubismMotion3Json.EvaluateCurve(curve.Segments, pm.CurrentTime);

                switch (curve.Target)
                {
                    case "Parameter":
                    {
                        var param = FindParameter(curve.Id);
                        if (param != null)
                            param.Value = param.Value * (1f - weight) + value * weight;
                        break;
                    }
                    case "PartOpacity":
                    {
                        var part = FindPart(curve.Id);
                        if (part != null)
                            part.Opacity = part.Opacity * (1f - weight) + value * weight;
                        break;
                    }
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

        private CubismPart FindPart(string id)
        {
            if (_parts == null) return null;
            for (int i = 0; i < _parts.Length; i++)
                if (_parts[i].Id == id) return _parts[i];
            return null;
        }

        public bool IsPlaying => _playingMotions.Count > 0;
    }
}
