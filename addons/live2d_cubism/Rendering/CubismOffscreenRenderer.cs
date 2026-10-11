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
    /// target then composite. This hook always allocates a SubViewport and reparents
    /// the model into it (Hiyori OffscreenCount=0; Ren=24). Per-owner RTs are not
    /// split yet — the whole model is drawn into one target, then shown through
    /// SubViewportContainer so Movie Maker sees unique frames.
    /// </summary>
    public partial class CubismOffscreenRenderer : Node2D
    {
        public int OffscreenCount { get; private set; }
        public SubViewport Target { get; private set; }
        public SubViewportContainer Container { get; private set; }
        public bool IsActive => Target != null;

        public void Initialize(CubismModelNode model, Vector2I size)
        {
            OffscreenCount = model?.Model?.OffscreenCount ?? 0;

            Container = new SubViewportContainer
            {
                Stretch = true,
                Size = size,
                Position = Vector2.Zero,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(Container);

            Target = new SubViewport
            {
                Size = size,
                TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                HandleInputLocally = false,
                Disable3D = true
            };
            Container.AddChild(Target);

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

        public override void _Process(double delta)
        {
            if (Target == null) return;
            Target.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            Container?.QueueRedraw();
        }
    }
}
