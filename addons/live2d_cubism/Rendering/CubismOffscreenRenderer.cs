/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Godot;

namespace Live2D.Cubism.Rendering
{
    /// <summary>
    /// Cubism 5 offscreen pass. Native/Unity render owner parts into an offscreen
    /// target then composite. When Core reports no offscreens (typical for Hiyori),
    /// this still allocates a SubViewport and blits the model — the same hook the
    /// Core offscreen list would drive.
    /// </summary>
    public partial class CubismOffscreenRenderer : Node2D
    {
        public int OffscreenCount { get; private set; }
        public SubViewport Target { get; private set; }
        public Sprite2D Composite { get; private set; }
        public bool IsActive => Target != null;

        public void Initialize(CubismModelNode model, Vector2I size)
        {
            OffscreenCount = model?.Model?.OffscreenCount ?? 0;

            Target = new SubViewport
            {
                Size = size,
                TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                HandleInputLocally = false
            };
            AddChild(Target);

            Composite = new Sprite2D
            {
                Centered = false,
                Texture = Target.GetTexture()
            };
            AddChild(Composite);

            GD.Print($"[Offscreen] Core offscreens={OffscreenCount}, target={size.X}x{size.Y}");
        }

        public void ReparentModel(CubismModelNode model)
        {
            if (Target == null || model == null) return;
            if (model.GetParent() != Target)
            {
                model.GetParent()?.RemoveChild(model);
                Target.AddChild(model);
            }
        }
    }
}
