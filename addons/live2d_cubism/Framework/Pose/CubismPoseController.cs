/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Json;
using Godot;

namespace Live2D.Cubism.Framework.Pose
{
    /// <summary>
    /// Pose controller for mutually exclusive part visibility.
    /// Mirrors Unity CubismPoseController exactly.
    ///
    /// Part opacity fading:
    /// - Active part fades toward 1.0 over FadeInTime
    /// - Inactive parts fade toward 0.0
    /// - Epsilon threshold of 0.001 prevents fully invisible parts
    /// </summary>
    public class CubismPoseController
    {
        private const float Epsilon = 0.001f;
        private const float DefaultFadeInTime = 0.5f;

        public float FadeInTime { get; set; } = DefaultFadeInTime;

        private CubismPart[] _parts;
        private PoseGroup[][] _groups;

        private class PoseGroup
        {
            public int PartIndex;
            public int[] LinkPartIndices;
        }

        public void Initialize(CubismPart[] parts, CubismPose3Json poseJson)
        {
            _parts = parts;
            if (poseJson?.Groups == null) return;

            FadeInTime = poseJson.FadeInTime > 0 ? poseJson.FadeInTime : DefaultFadeInTime;

            _groups = new PoseGroup[poseJson.Groups.Length][];
            for (int g = 0; g < poseJson.Groups.Length; g++)
            {
                var jsonGroup = poseJson.Groups[g];
                _groups[g] = new PoseGroup[jsonGroup.Length];
                for (int p = 0; p < jsonGroup.Length; p++)
                {
                    var pg = new PoseGroup
                    {
                        PartIndex = FindPartIndex(jsonGroup[p].Id)
                    };

                    if (jsonGroup[p].Link != null)
                    {
                        pg.LinkPartIndices = new int[jsonGroup[p].Link.Length];
                        for (int l = 0; l < jsonGroup[p].Link.Length; l++)
                            pg.LinkPartIndices[l] = FindPartIndex(jsonGroup[p].Link[l]);
                    }
                    else
                    {
                        pg.LinkPartIndices = Array.Empty<int>();
                    }

                    _groups[g][p] = pg;
                }
            }
        }

        public void Update(float deltaTime)
        {
            if (_parts == null || _groups == null) return;

            var fadeStep = deltaTime / FadeInTime;

            for (int g = 0; g < _groups.Length; g++)
            {
                var group = _groups[g];

                int visibleIndex = -1;
                float maxOpacity = float.MinValue;
                for (int p = 0; p < group.Length; p++)
                {
                    if (group[p].PartIndex < 0) continue;
                    var opacity = _parts[group[p].PartIndex].Opacity;
                    if (opacity > maxOpacity)
                    {
                        maxOpacity = opacity;
                        visibleIndex = p;
                    }
                }

                if (visibleIndex < 0) continue;

                for (int p = 0; p < group.Length; p++)
                {
                    if (group[p].PartIndex < 0) continue;

                    ref var part = ref _parts[group[p].PartIndex];

                    if (p == visibleIndex)
                    {
                        part.Opacity = Mathf.Min(part.Opacity + fadeStep, 1.0f);
                    }
                    else
                    {
                        part.Opacity = Mathf.Max(part.Opacity - fadeStep, Epsilon);
                    }

                    foreach (var linkIdx in group[p].LinkPartIndices)
                    {
                        if (linkIdx >= 0 && linkIdx < _parts.Length)
                            _parts[linkIdx].Opacity = part.Opacity;
                    }
                }
            }
        }

        private int FindPartIndex(string id)
        {
            if (_parts == null || id == null) return -1;
            for (int i = 0; i < _parts.Length; i++)
                if (_parts[i].Id == id) return i;
            return -1;
        }
    }
}
