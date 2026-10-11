/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System.Collections.Generic;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Json;
using Live2D.Cubism.Framework.MotionFade;

namespace Live2D.Cubism.Framework.Motion
{
    public enum CubismMotionPriority
    {
        PriorityNone = 0,
        PriorityIdle = 1,
        PriorityNormal = 2,
        PriorityForce = 3
    }

    /// <summary>
    /// Playing motion instance. Combines Unity CubismFadePlayingMotion + Native queue entry.
    /// </summary>
    public class CubismPlayingMotion
    {
        public CubismMotion3Json Motion { get; set; }
        public CubismMotionState State { get; set; }
        public float CurrentTime { get; set; }
        public float Weight { get; set; } = 1f;
        public float FadeInTime { get; set; } = 1f;
        public float FadeOutTime { get; set; } = 1f;
        public bool IsLooping { get; set; }
        public bool IsFinished { get; set; }
        public bool IsFadingOut { get; set; }
        public CubismMotionPriority Priority { get; set; }
        public float StartTime { get; set; }
        public float FadeInStartTime { get; set; }
        public float EndTime { get; set; } = -1f;
        public float Speed { get; set; } = 1f;
    }

    /// <summary>
    /// Motion playback. Native CubismMotion + CubismMotionQueueManager + Unity
    /// CubismMotionController / CubismFadeController:
    /// sine fade, per-parameter fades, priority reject, Model EyeBlink/LipSync curves,
    /// Cardano bezier when AreBeziersRestricted is false.
    /// </summary>
    public class CubismMotionController
    {
        public int LayerCount { get; }
        public float UserTimeSeconds { get; private set; }

        private readonly CubismMotionLayer[] _layers;
        private CubismParameter[] _parameters;
        private CubismPart[] _parts;
        private string[] _eyeBlinkIds = System.Array.Empty<string>();
        private string[] _lipSyncIds = System.Array.Empty<string>();

        public CubismMotionController(int layerCount = 1)
        {
            LayerCount = layerCount < 1 ? 1 : layerCount;
            _layers = new CubismMotionLayer[LayerCount];
            for (int i = 0; i < LayerCount; i++)
                _layers[i] = new CubismMotionLayer(i);
        }

        public CubismMotionLayer GetLayer(int index) =>
            index >= 0 && index < _layers.Length ? _layers[index] : null;

        public void SetModel(CubismParameter[] parameters, CubismPart[] parts)
        {
            _parameters = parameters;
            _parts = parts;
        }

        public void SetEffectIds(string[] eyeBlinkIds, string[] lipSyncIds)
        {
            _eyeBlinkIds = eyeBlinkIds ?? System.Array.Empty<string>();
            _lipSyncIds = lipSyncIds ?? System.Array.Empty<string>();
        }

        public CubismPlayingMotion PlayMotion(
            CubismMotion3Json motion,
            CubismMotionPriority priority = CubismMotionPriority.PriorityNormal,
            bool isLooping = false,
            float fadeInOverride = -1f,
            float fadeOutOverride = -1f,
            int layerIndex = 0)
        {
            if (motion == null) return null;
            var layer = GetLayer(layerIndex);
            if (layer == null) return null;
            if (!layer.CanPlay(priority))
                return null;

            var fadeIn = fadeInOverride >= 0f ? fadeInOverride
                : (motion.Meta?.FadeInTime >= 0f ? motion.Meta.FadeInTime : 1f);
            var fadeOut = fadeOutOverride >= 0f ? fadeOutOverride
                : (motion.Meta?.FadeOutTime >= 0f ? motion.Meta.FadeOutTime : 1f);

            return layer.Play(motion, priority, isLooping, fadeIn, fadeOut, UserTimeSeconds);
        }

        public void StopAll()
        {
            foreach (var layer in _layers)
            {
                foreach (var pm in layer.Playing)
                {
                    if (pm.EndTime < 0f || UserTimeSeconds + pm.FadeOutTime < pm.EndTime)
                        pm.EndTime = UserTimeSeconds + pm.FadeOutTime;
                    pm.IsLooping = false;
                }
            }
        }

        /// <summary>Drop every playing motion and rewind the user clock (dump / tests).</summary>
        public void HardReset()
        {
            foreach (var layer in _layers)
                layer.HardClear();
            UserTimeSeconds = 0f;
        }

        public void Update(float deltaTime)
        {
            if (_parameters == null) return;

            UserTimeSeconds += deltaTime;

            foreach (var layer in _layers)
            {
                foreach (var pm in layer.Playing)
                    ApplyMotion(pm, layer.LayerWeight);

                layer.RemoveFinished();
            }
        }

        private void ApplyMotion(CubismPlayingMotion pm, float layerWeight)
        {
            if (pm.Motion?.Curves == null || pm.IsFinished)
                return;

            var userTime = UserTimeSeconds;
            var fadeWeight = UpdateFadeWeight(pm, userTime) * layerWeight;
            pm.Weight = fadeWeight;

            var timeOffset = userTime - pm.StartTime;
            if (timeOffset < 0f) timeOffset = 0f;

            var duration = pm.Motion.Meta?.Duration ?? 0f;
            var fps = pm.Motion.Meta?.Fps > 0f ? pm.Motion.Meta.Fps : 30f;
            var areRestricted = pm.Motion.Meta?.AreBeziersRestricted ?? false;
            var isLoop = pm.IsLooping;
            var evalDuration = duration;
            if (isLoop)
                evalDuration += 1.0f / fps;

            var time = timeOffset;
            if (isLoop && evalDuration > 0f)
            {
                while (time > evalDuration)
                    time -= evalDuration;
            }

            pm.CurrentTime = time;

            float eyeBlinkValue = float.MaxValue;
            float lipSyncValue = float.MaxValue;
            ulong eyeBlinkFlags = 0;
            ulong lipSyncFlags = 0;

            foreach (var curve in pm.Motion.Curves)
            {
                if (curve.Target != "Model")
                    continue;

                var value = CubismMotion3Json.EvaluateCurve(curve.Segments, time, areRestricted);
                if (curve.Id == "EyeBlink")
                    eyeBlinkValue = value;
                else if (curve.Id == "LipSync")
                    lipSyncValue = value;
            }

            foreach (var curve in pm.Motion.Curves)
            {
                if (curve.Target != "Parameter")
                    continue;

                var param = FindParameter(curve.Id);
                if (param == null)
                    continue;

                var source = param.Value;
                var value = CubismMotion3Json.EvaluateCurve(curve.Segments, time, areRestricted);

                if (eyeBlinkValue != float.MaxValue)
                {
                    for (int i = 0; i < _eyeBlinkIds.Length && i < 64; i++)
                    {
                        if (_eyeBlinkIds[i] == curve.Id)
                        {
                            value *= eyeBlinkValue;
                            eyeBlinkFlags |= 1UL << i;
                            break;
                        }
                    }
                }

                if (lipSyncValue != float.MaxValue)
                {
                    for (int i = 0; i < _lipSyncIds.Length && i < 64; i++)
                    {
                        if (_lipSyncIds[i] == curve.Id)
                        {
                            value += lipSyncValue;
                            lipSyncFlags |= 1UL << i;
                            break;
                        }
                    }
                }

                if (param.IsRepeat())
                    value = param.GetParameterRepeatValue(value);

                param.Value = BlendParameter(param, curve, source, value, fadeWeight, userTime, pm);
            }

            if (eyeBlinkValue != float.MaxValue)
            {
                for (int i = 0; i < _eyeBlinkIds.Length && i < 64; i++)
                {
                    if (((eyeBlinkFlags >> i) & 1UL) != 0)
                        continue;
                    var param = FindParameter(_eyeBlinkIds[i]);
                    if (param == null) continue;
                    param.Value = param.Value + (eyeBlinkValue - param.Value) * fadeWeight;
                }
            }

            if (lipSyncValue != float.MaxValue)
            {
                for (int i = 0; i < _lipSyncIds.Length && i < 64; i++)
                {
                    if (((lipSyncFlags >> i) & 1UL) != 0)
                        continue;
                    var param = FindParameter(_lipSyncIds[i]);
                    if (param == null) continue;
                    param.Value = param.Value + (lipSyncValue - param.Value) * fadeWeight;
                }
            }

            foreach (var curve in pm.Motion.Curves)
            {
                if (curve.Target != "PartOpacity")
                    continue;
                var part = FindPart(curve.Id);
                if (part == null) continue;
                part.Opacity = CubismMotion3Json.EvaluateCurve(curve.Segments, time, areRestricted);
            }

            if (!isLoop && duration > 0f && timeOffset >= duration)
                pm.IsFinished = true;
        }

        private static float UpdateFadeWeight(CubismPlayingMotion pm, float userTimeSeconds)
        {
            var fadeIn = pm.FadeInTime == 0.0f
                ? 1.0f
                : CubismFadeMath.GetEasingSine((userTimeSeconds - pm.FadeInStartTime) / pm.FadeInTime);

            var fadeOut = (pm.FadeOutTime == 0.0f || pm.EndTime < 0.0f)
                ? 1.0f
                : CubismFadeMath.GetEasingSine((pm.EndTime - userTimeSeconds) / pm.FadeOutTime);

            var w = fadeIn * fadeOut;
            if (w < 0f) w = 0f;
            if (w > 1f) w = 1f;
            if (pm.EndTime >= 0f && userTimeSeconds >= pm.EndTime && fadeOut <= 0f)
                pm.IsFinished = true;
            return w;
        }

        private static float BlendParameter(CubismParameter param, CubismMotion3Json.SerializableCurve curve,
            float source, float value, float fadeWeight, float userTime, CubismPlayingMotion pm)
        {
            float v;
            if (curve.FadeInTime < 0.0f && curve.FadeOutTime < 0.0f)
            {
                v = source + (value - source) * fadeWeight;
            }
            else
            {
                float fin = curve.FadeInTime < 0.0f
                    ? CubismFadeMath.GetEasingSine((userTime - pm.FadeInStartTime) / pm.FadeInTime)
                    : (curve.FadeInTime == 0.0f
                        ? 1.0f
                        : CubismFadeMath.GetEasingSine((userTime - pm.FadeInStartTime) / curve.FadeInTime));

                float fout;
                if (curve.FadeOutTime < 0.0f)
                {
                    fout = (pm.FadeOutTime == 0.0f || pm.EndTime < 0.0f)
                        ? 1.0f
                        : CubismFadeMath.GetEasingSine((pm.EndTime - userTime) / pm.FadeOutTime);
                }
                else
                {
                    fout = (curve.FadeOutTime == 0.0f || pm.EndTime < 0.0f)
                        ? 1.0f
                        : CubismFadeMath.GetEasingSine((pm.EndTime - userTime) / curve.FadeOutTime);
                }

                v = source + (value - source) * (fin * fout);
            }

            return param.GetParameterClampValue(v);
        }

        private CubismParameter FindParameter(string id)
        {
            if (_parameters == null || id == null) return null;
            for (int i = 0; i < _parameters.Length; i++)
                if (_parameters[i].Id == id) return _parameters[i];
            return null;
        }

        private CubismPart FindPart(string id)
        {
            if (_parts == null || id == null) return null;
            for (int i = 0; i < _parts.Length; i++)
                if (_parts[i].Id == id) return _parts[i];
            return null;
        }

        public bool IsPlaying
        {
            get
            {
                foreach (var layer in _layers)
                    if (!layer.IsFinished) return true;
                return false;
            }
        }

        /// <summary>
        /// Evaluate every Parameter curve at a fixed time with fadeWeight=1.
        /// Used by golden tests (Native DoUpdateParameters after fade-in).
        /// </summary>
        public static Dictionary<string, float> EvaluateAllParameterCurves(CubismMotion3Json motion, float time)
        {
            var result = new Dictionary<string, float>();
            if (motion?.Curves == null) return result;
            var restricted = motion.Meta?.AreBeziersRestricted ?? false;
            foreach (var curve in motion.Curves)
            {
                if (curve.Target != "Parameter" || curve.Id == null)
                    continue;
                result[curve.Id] = CubismMotion3Json.EvaluateCurve(curve.Segments, time, restricted);
            }
            return result;
        }
    }
}
