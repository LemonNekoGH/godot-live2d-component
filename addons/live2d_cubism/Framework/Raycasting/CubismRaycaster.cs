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

namespace Live2D.Cubism.Framework.Raycasting
{
    /// <summary>
    /// Result of a raycast hit on a drawable.
    /// </summary>
    public struct CubismRaycastHit
    {
        public CubismDrawable Drawable;
        public string HitAreaName;
        public float Distance;
        public Vector2 LocalPosition;
    }

    /// <summary>
    /// Raycaster for hit-testing drawables. Mirrors Unity CubismRaycaster.
    /// Tests a point against the triangulated mesh of drawables marked as "hit area".
    /// </summary>
    public class CubismRaycaster
    {
        private struct HitTarget
        {
            public int DrawableIndex;
            public string HitAreaName;
            public int[] Indices;
        }

        private CubismModel _model;
        private readonly List<HitTarget> _hitTargets = new();
        private Vector2[] _positionsBuffer = Array.Empty<Vector2>();

        public void Initialize(CubismModel model, string[] hitDrawableIds)
        {
            _model = model;
            _hitTargets.Clear();

            if (model == null || hitDrawableIds == null) return;
            foreach (var id in hitDrawableIds)
            {
                AddTarget(id, id);
            }
        }

        public void Initialize(CubismModel model, CubismModel3Json.HitAreaData[] hitAreas)
        {
            _model = model;
            _hitTargets.Clear();

            if (model == null || hitAreas == null) return;
            foreach (var area in hitAreas)
            {
                if (area == null || string.IsNullOrEmpty(area.Id)) continue;
                var name = !string.IsNullOrEmpty(area.Name) ? area.Name : area.Id;
                AddTarget(area.Id, name);
            }
        }

        private void AddTarget(string drawableId, string hitAreaName)
        {
            for (int i = 0; i < _model.Drawables.Length; i++)
            {
                if (_model.Drawables[i].Id == drawableId)
                {
                    _hitTargets.Add(new HitTarget
                    {
                        DrawableIndex = i,
                        HitAreaName = hitAreaName,
                        Indices = _model.GetDrawableIndices(i) ?? Array.Empty<int>()
                    });
                    break;
                }
            }
        }

        /// <summary>
        /// Tests a point in <see cref="Rendering.CubismModelNode"/> local pixel space
        /// (the same coordinate space as rendered drawable meshes, i.e. <c>node.ToLocal(globalPoint)</c>).
        /// </summary>
        public CubismRaycastHit[] Raycast(Vector2 localPoint, int maxResults = 4)
        {
            if (_model == null || !_model.IsValid) return Array.Empty<CubismRaycastHit>();

            var scale = _model.PixelsPerUnit > 0 ? _model.PixelsPerUnit : 1f;
            var unitPoint = localPoint / scale;
            return RaycastInternal(unitPoint, localPoint, maxResults);
        }

        /// <summary>
        /// Tests a point already in Cubism unit space (Godot Y-down, unscaled by PixelsPerUnit).
        /// </summary>
        public CubismRaycastHit[] RaycastUnit(Vector2 unitPoint, int maxResults = 4)
        {
            if (_model == null || !_model.IsValid) return Array.Empty<CubismRaycastHit>();

            var scale = _model.PixelsPerUnit > 0 ? _model.PixelsPerUnit : 1f;
            return RaycastInternal(unitPoint, unitPoint * scale, maxResults);
        }

        private CubismRaycastHit[] RaycastInternal(Vector2 unitPoint, Vector2 localPoint, int maxResults)
        {
            var results = new List<CubismRaycastHit>();

            foreach (var target in _hitTargets)
            {
                if (results.Count >= maxResults) break;

                var drawable = _model.Drawables[target.DrawableIndex];
                if (!drawable.IsVisible) continue;

                var count = _model.FillDrawableVertexPositions(target.DrawableIndex, ref _positionsBuffer, 1f);
                if (count <= 0 || target.Indices.Length < 3) continue;

                if (PointInMesh(unitPoint, _positionsBuffer, target.Indices))
                {
                    results.Add(new CubismRaycastHit
                    {
                        Drawable = drawable,
                        HitAreaName = target.HitAreaName,
                        Distance = 0f,
                        LocalPosition = localPoint
                    });
                }
            }

            return results.ToArray();
        }

        public static bool PointInMesh(Vector2 point, Vector2[] vertices, int[] indices)
        {
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[i + 1]];
                var c = vertices[indices[i + 2]];

                if (PointInTriangle(point, a, b, c))
                    return true;
            }
            return false;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var d1 = Sign(p, a, b);
            var d2 = Sign(p, b, c);
            var d3 = Sign(p, c, a);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }
    }
}
