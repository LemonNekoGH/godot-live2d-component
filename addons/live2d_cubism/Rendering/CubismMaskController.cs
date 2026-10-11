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
    /// High-precision clipping masks, mirroring Unity CubismRenderer.CalcMaskTransform
    /// and Native CubismClippingManager.
    ///
    /// Each unique mask-index set gets its own SubViewport. Mask drawables are rendered
    /// with their texture alpha into that target. Clipped drawables sample it with
    /// maskUV = (vertex - boundsCenter) / boundsScale + 0.5.
    /// </summary>
    public partial class CubismMaskController : Node2D
    {
        private const int MaskSize = 256;

        private CubismModel _model;
        private CubismDrawableRenderer[] _renderers;
        private Texture2D[] _textures;
        private readonly List<ClippingContext> _contexts = new();

        private sealed class ClippingContext
        {
            public int[] MaskIndices;
            public List<int> ClippedIndices = new();
            public SubViewport Viewport;
            public Camera2D Camera;
            public MeshInstance2D[] MaskMeshes;
            public Vector2 Center;
            public float Scale = 1f;
        }

        public void Initialize(CubismModel model, CubismDrawableRenderer[] renderers, Texture2D[] textures)
        {
            _model = model;
            _renderers = renderers;
            _textures = textures;
            if (model == null) return;

            var groups = new Dictionary<string, ClippingContext>();
            for (int i = 0; i < model.Drawables.Length; i++)
            {
                var d = model.Drawables[i];
                if (d.MaskIndices == null || d.MaskIndices.Length == 0) continue;

                var key = string.Join(",", d.MaskIndices);
                if (!groups.TryGetValue(key, out var ctx))
                {
                    ctx = new ClippingContext { MaskIndices = d.MaskIndices };
                    groups[key] = ctx;
                    _contexts.Add(ctx);
                }
                ctx.ClippedIndices.Add(i);
            }

            foreach (var ctx in _contexts)
                CreateContextViewport(ctx);

            if (_contexts.Count > 0)
                GD.Print($"  [MaskController] {_contexts.Count} clipping context(s), {CountClipped()} clipped drawable(s)");
        }

        private int CountClipped()
        {
            int n = 0;
            foreach (var c in _contexts) n += c.ClippedIndices.Count;
            return n;
        }

        private void CreateContextViewport(ClippingContext ctx)
        {
            var viewport = new SubViewport
            {
                Name = $"MaskVP_{string.Join("_", ctx.MaskIndices)}",
                Size = new Vector2I(MaskSize, MaskSize),
                TransparentBg = true,
                Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Linear,
                RenderTargetClearMode = SubViewport.ClearMode.Always
            };
            AddChild(viewport);
            ctx.Viewport = viewport;

            var camera = new Camera2D
            {
                Name = "MaskCamera",
                Enabled = true,
                IgnoreRotation = true
            };
            viewport.AddChild(camera);
            ctx.Camera = camera;

            ctx.MaskMeshes = new MeshInstance2D[ctx.MaskIndices.Length];
            for (int m = 0; m < ctx.MaskIndices.Length; m++)
            {
                int maskIndex = ctx.MaskIndices[m];
                var mesh = new MeshInstance2D { Name = $"MaskMesh_{maskIndex}" };
                var mat = new ShaderMaterial();
                mat.Shader = CubismDrawableRenderer.GetMaskWriteShader();
                Texture2D tex = null;
                if (maskIndex >= 0 && maskIndex < _model.Drawables.Length)
                {
                    var texIndex = _model.Drawables[maskIndex].TextureIndex;
                    if (_textures != null && texIndex >= 0 && texIndex < _textures.Length)
                        tex = _textures[texIndex];
                }
                if (tex != null)
                    mat.SetShaderParameter("cubism_texture", tex);
                mesh.Material = mat;
                mesh.Texture = tex;
                mesh.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
                viewport.AddChild(mesh);
                ctx.MaskMeshes[m] = mesh;
            }
        }

        public void UpdateMasks()
        {
            if (_model == null) return;

            foreach (var ctx in _contexts)
            {
                if (!ComputeBounds(ctx, out var center, out var scale))
                {
                    foreach (var idx in ctx.ClippedIndices)
                        _renderers[idx]?.SetMaskTexture(null, Vector2.Zero, 0f, false);
                    continue;
                }

                ctx.Center = center;
                ctx.Scale = scale;
                ctx.Camera.Position = center;
                ctx.Camera.Zoom = new Vector2(MaskSize / scale, MaskSize / scale);

                for (int m = 0; m < ctx.MaskIndices.Length; m++)
                    RebuildMaskMesh(ctx.MaskMeshes[m], ctx.MaskIndices[m]);

                var maskTex = ctx.Viewport.GetTexture();
                bool invert = false;
                foreach (var idx in ctx.ClippedIndices)
                {
                    var renderer = _renderers[idx];
                    if (renderer == null) continue;
                    invert = _model.Drawables[idx].IsInvertedMask;
                    renderer.SetMaskTexture(maskTex, center, scale, invert);
                }
            }
        }

        private bool ComputeBounds(ClippingContext ctx, out Vector2 center, out float scale)
        {
            center = Vector2.Zero;
            scale = 1f;

            var ppu = _model.PixelsPerUnit > 0 ? _model.PixelsPerUnit : 1f;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool any = false;

            foreach (var maskIndex in ctx.MaskIndices)
            {
                if (maskIndex < 0 || maskIndex >= _model.Drawables.Length) continue;
                var verts = _model.GetDrawableVertexPositions(maskIndex);
                if (verts == null || verts.Length == 0) continue;
                any = true;
                for (int i = 0; i < verts.Length; i++)
                {
                    var p = verts[i] * ppu;
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Y > maxY) maxY = p.Y;
                }
            }

            if (!any) return false;

            center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            var sizeX = maxX - minX;
            var sizeY = maxY - minY;
            scale = (sizeX > sizeY ? sizeX : sizeY);
            if (scale < 0.0001f) scale = 1f;
            scale *= 1.05f;
            return true;
        }

        private void RebuildMaskMesh(MeshInstance2D mesh, int drawableIndex)
        {
            if (mesh == null || drawableIndex < 0 || drawableIndex >= _renderers.Length) return;
            var src = _renderers[drawableIndex];
            if (src == null) return;

            src.FillMaskArrays(out var pos, out var uvs, out var indices, out var colors);
            if (pos.Length == 0 || indices.Length < 3)
            {
                mesh.Visible = false;
                return;
            }

            var arrayMesh = mesh.Mesh as ArrayMesh ?? new ArrayMesh();
            if (arrayMesh.GetSurfaceCount() > 0)
                arrayMesh.ClearSurfaces();

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = pos;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
            arrays[(int)Mesh.ArrayType.Color] = colors;
            arrays[(int)Mesh.ArrayType.Index] = indices;
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.Mesh = arrayMesh;
            mesh.Visible = true;
        }
    }
}
