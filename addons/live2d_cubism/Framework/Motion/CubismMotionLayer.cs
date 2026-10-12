/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System.Collections.Generic;
using Live2D.Cubism.Framework.Json;

namespace Live2D.Cubism.Framework.Motion
{
    /// <summary>
    /// One motion layer. Mirrors Unity CubismMotionLayer (priority + playing list).
    /// </summary>
    public sealed class CubismMotionLayer
    {
        public int LayerIndex { get; }
        public float LayerWeight { get; set; } = 1f;
        public int CurrentPriority { get; set; }
        public bool IsFinished => Playing.Count == 0 || Playing.TrueForAll(m => m.IsFinished);

        public List<CubismPlayingMotion> Playing { get; } = new();

        public CubismMotionLayer(int layerIndex)
        {
            LayerIndex = layerIndex;
        }

        public bool CanPlay(CubismMotionPriority priority)
        {
            if (priority == CubismMotionPriority.PriorityForce)
                return true;
            return (int)priority > CurrentPriority;
        }

        public void BeginFadeOutPrevious(float userTimeSeconds)
        {
            if (Playing.Count == 0)
                return;

            var last = Playing[^1];
            if (last.Motion == null || last.IsFinished)
                return;

            var fadeOut = last.FadeOutTime;
            var newEnd = userTimeSeconds + fadeOut;
            if (last.EndTime < 0f || newEnd < last.EndTime)
                last.EndTime = newEnd;

            last.IsLooping = false;
        }

        public CubismPlayingMotion Play(CubismMotion3Json clip, CubismMotionPriority priority,
            bool loop, float fadeIn, float fadeOut, float userTimeSeconds, float speed = 1f)
        {
            BeginFadeOutPrevious(userTimeSeconds);

            var duration = clip.Meta?.Duration ?? 0f;
            var playing = new CubismPlayingMotion
            {
                Motion = clip,
                CurrentTime = 0f,
                Weight = 0f,
                FadeInTime = fadeIn,
                FadeOutTime = fadeOut,
                IsLooping = loop || (clip.Meta?.Loop ?? false),
                Priority = priority,
                StartTime = userTimeSeconds,
                FadeInStartTime = userTimeSeconds,
                EndTime = loop || duration <= 0f ? -1f : userTimeSeconds + duration / speed,
                Speed = speed
            };

            Playing.Add(playing);
            CurrentPriority = (int)priority;
            return playing;
        }

        public void RemoveFinished()
        {
            Playing.RemoveAll(m => m.IsFinished);
            if (Playing.Count == 0)
                CurrentPriority = 0;
        }

        public void HardClear()
        {
            Playing.Clear();
            CurrentPriority = 0;
        }
    }
}
