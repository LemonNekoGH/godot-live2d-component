/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using Live2D.Cubism.Core.Unmanaged;
using Godot;

namespace Live2D.Cubism.Core
{
    /// <summary>
    /// Runtime Cubism model. Central node analogous to Unity's CubismModel MonoBehaviour.
    ///
    /// Coordinate mapping (Unity → Godot):
    ///   Unity is Y-up; Godot 2D is Y-down. Vertex Y is negated in CubismUnmanagedDrawables.
    ///   Unity's PixelsPerUnit maps model space → screen; in Godot we scale by (1/PixelsPerUnit)*100
    ///   so that a 1-unit model-space length ≈ 100 Godot pixels (adjustable via ModelScale).
    ///
    /// Update order (mirrors Unity PreLateUpdate insertion):
    ///   1. Framework controllers write parameter/part values
    ///   2. CubismModel.UpdateModel() syncs values to Core, calls csmUpdateModel, reads back drawables
    ///   3. Renderer reads updated drawable data and rebuilds meshes
    /// </summary>
    public sealed class CubismModel : IDisposable
    {
        public string Name { get; set; }

        public CubismMoc Moc { get; private set; }

        internal CubismUnmanagedModel UnmanagedModel { get; private set; }

        public CubismParameter[] Parameters { get; private set; }
        public CubismPart[] Parts { get; private set; }
        public CubismDrawable[] Drawables { get; private set; }

        public Vector2 CanvasSizeInPixels { get; private set; }
        public Vector2 CanvasOriginInPixels { get; private set; }
        public float PixelsPerUnit { get; private set; } = 1f;

        public bool IsValid => UnmanagedModel != null && UnmanagedModel.IsValid;

        private bool _isOverriddenParameterRepeat = true;
        private bool _ownsMoc;

        public bool GetOverrideFlagForModelParameterRepeat() => _isOverriddenParameterRepeat;
        public void SetOverrideFlagForModelParameterRepeat(bool v) => _isOverriddenParameterRepeat = v;

        public static CubismModel InstantiateFrom(CubismMoc moc, bool ownsMoc = true)
        {
            if (moc == null || !moc.IsValid) return null;

            var model = new CubismModel();
            model.Moc = moc;
            model._ownsMoc = ownsMoc;
            model.Name = moc.Name ?? "CubismModel";

            var unmanaged = moc.AcquireUnmanagedModel();
            if (unmanaged == null) return null;

            model.UnmanagedModel = unmanaged;
            model.InitializeFromUnmanaged();
            return model;
        }

        private void InitializeFromUnmanaged()
        {
            var um = UnmanagedModel;

            // Canvas
            var ci = um.CanvasInformation;
            CanvasSizeInPixels = ci.SizeInPixels;
            CanvasOriginInPixels = ci.OriginInPixels;
            PixelsPerUnit = ci.PixelsPerUnit;

            // Parameters
            var pCount = um.Parameters.Count;
            Parameters = new CubismParameter[pCount];
            for (int i = 0; i < pCount; i++)
            {
                Parameters[i] = new CubismParameter();
                Parameters[i].Initialize(um.Parameters, i);
            }

            // Parts
            var partCount = um.Parts.Count;
            Parts = new CubismPart[partCount];
            for (int i = 0; i < partCount; i++)
            {
                Parts[i] = new CubismPart();
                Parts[i].Initialize(um.Parts, i);
            }

            // Drawables
            var dCount = um.Drawables.Count;
            Drawables = new CubismDrawable[dCount];
            for (int i = 0; i < dCount; i++)
            {
                Drawables[i] = new CubismDrawable();
                Drawables[i].Initialize(um.Drawables, i);
            }
        }

        /// <summary>
        /// Writes parameter and part values from managed proxies to unmanaged Core,
        /// calls csmUpdateModel, then reads back drawable dynamic data.
        /// Mirrors Unity CubismModel.OnModelUpdate flow.
        /// </summary>
        public void UpdateModel()
        {
            if (!IsValid) return;

            var um = UnmanagedModel;

            // Write parameters
            for (int i = 0; i < Parameters.Length; i++)
            {
                var p = Parameters[i];
                if (p.UnmanagedIndex >= 0)
                    um.Parameters.SetValue(p.UnmanagedIndex, p.Value);
            }

            // Write part opacities
            for (int i = 0; i < Parts.Length; i++)
            {
                var p = Parts[i];
                if (p.UnmanagedIndex >= 0)
                    um.Parts.SetOpacity(p.UnmanagedIndex, p.Opacity);
            }

            // Core update
            um.Update();

            // Read back parameters (Core may clamp)
            for (int i = 0; i < Parameters.Length; i++)
            {
                var p = Parameters[i];
                if (p.UnmanagedIndex >= 0)
                    p.Value = um.Parameters.GetValue(p.UnmanagedIndex);
            }

            // Read back part opacities
            for (int i = 0; i < Parts.Length; i++)
            {
                var p = Parts[i];
                if (p.UnmanagedIndex >= 0)
                    p.Opacity = um.Parts.GetOpacity(p.UnmanagedIndex);
            }

            // Read back drawable dynamic data
            for (int i = 0; i < Drawables.Length; i++)
            {
                Drawables[i].UpdateDynamic(um.Drawables);
            }

            // Reset dynamic flags for next frame
            um.ResetDynamicFlags();
        }

        /// <summary>
        /// Gets vertex positions for a drawable, already converted to Godot Y-down coordinates.
        /// </summary>
        public Vector2[] GetDrawableVertexPositions(int drawableIndex)
        {
            return UnmanagedModel.Drawables.GetVertexPositions(drawableIndex);
        }

        /// <summary>
        /// Gets UV coordinates for a drawable.
        /// </summary>
        public Vector2[] GetDrawableVertexUvs(int drawableIndex)
        {
            return UnmanagedModel.Drawables.GetVertexUvs(drawableIndex);
        }

        /// <summary>
        /// Gets triangle indices for a drawable.
        /// </summary>
        public int[] GetDrawableIndices(int drawableIndex)
        {
            return UnmanagedModel.Drawables.GetIndices(drawableIndex);
        }

        /// <summary>
        /// Finds a parameter by its ID string.
        /// </summary>
        public CubismParameter FindParameter(string id)
        {
            for (int i = 0; i < Parameters.Length; i++)
                if (Parameters[i].Id == id) return Parameters[i];
            return null;
        }

        /// <summary>
        /// Finds a part by its ID string.
        /// </summary>
        public CubismPart FindPart(string id)
        {
            for (int i = 0; i < Parts.Length; i++)
                if (Parts[i].Id == id) return Parts[i];
            return null;
        }

        public void Dispose()
        {
            UnmanagedModel?.Dispose();
            UnmanagedModel = null;

            if (_ownsMoc)
            {
                Moc?.Dispose();
            }
            Moc = null;
        }
    }
}
