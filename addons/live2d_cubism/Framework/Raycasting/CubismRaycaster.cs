/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System.Collections.Generic;
using Live2D.Cubism.Core;
using Godot;

namespace Live2D.Cubism.Framework.Raycasting
{
    /// <summary>
    /// Result of a raycast hit on a drawable.
    /// </summary>
    public struct CubismRaycastHit
    {
        public CubismDrawable Drawable;
        public float Distance;
        public Vector2 LocalPosition;
    }

    /// <summary>
    /// Raycaster for hit-testing drawables. Mirrors Unity CubismRaycaster.
    /// Tests a point against the triangulated mesh of drawables marked as "hit area".
    /// </summary>
    public class CubismRaycaster
    {
        private CubismModel _model;
        private HashSet<int> _hitDrawableIndices = new();

        public void Initialize(CubismModel model, string[] hitDrawableIds)
        {
            _model = model;
            _hitDrawableIndices.Clear();

            if (hitDrawableIds == null) return;
            foreach (var id in hitDrawableIds)
            {
                for (int i = 0; i < model.Drawables.Length; i++)
                {
                    if (model.Drawables[i].Id == id)
                    {
                        _hitDrawableIndices.Add(i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Tests a point against all hit-area drawables.
        /// Point should be in model-local space.
        /// </summary>
        public CubismRaycastHit[] Raycast(Vector2 point, int maxResults = 4)
        {
            if (_model == null) return System.Array.Empty<CubismRaycastHit>();

            var results = new List<CubismRaycastHit>();

            foreach (var idx in _hitDrawableIndices)
            {
                if (results.Count >= maxResults) break;

                var drawable = _model.Drawables[idx];
                if (!drawable.IsVisible) continue;

                var positions = _model.GetDrawableVertexPositions(idx);
                var indices = _model.GetDrawableIndices(idx);

                if (PointInMesh(point, positions, indices))
                {
                    results.Add(new CubismRaycastHit
                    {
                        Drawable = drawable,
                        Distance = 0f,
                        LocalPosition = point
                    });
                }
            }

            return results.ToArray();
        }

        private static bool PointInMesh(Vector2 point, Vector2[] vertices, int[] indices)
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
