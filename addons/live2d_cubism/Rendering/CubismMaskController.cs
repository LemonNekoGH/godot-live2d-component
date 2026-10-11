/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System.Collections.Generic;
using Godot;
using Live2D.Cubism.Core;

namespace Live2D.Cubism.Rendering
{
    /// <summary>
    /// Placeholder mask controller.
    /// Clipping mask support requires a SubViewport render-to-texture pipeline.
    /// Currently logs masked drawables for debugging.
    /// </summary>
    public partial class CubismMaskController : Node2D
    {
        public void Initialize(CubismModel model, CubismDrawableRenderer[] renderers, Texture2D[] textures)
        {
            if (model == null) return;

            int maskedCount = 0;
            for (int i = 0; i < model.Drawables.Length; i++)
            {
                var d = model.Drawables[i];
                if (d.MaskIndices != null && d.MaskIndices.Length > 0)
                    maskedCount++;
            }

            if (maskedCount > 0)
                GD.Print($"  [MaskController] {maskedCount} drawables use clipping masks (rendering without masks for now)");
        }
    }
}
