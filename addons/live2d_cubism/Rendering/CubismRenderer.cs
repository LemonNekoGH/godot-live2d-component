/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Godot;
using Live2D.Cubism.Core;
using Live2D.Cubism.Core.Unmanaged;

namespace Live2D.Cubism.Rendering
{
    /// <summary>
    /// Renders a single CubismDrawable as a Godot MeshInstance2D.
    ///
    /// Coordinate mapping (Unity → Godot):
    ///   - Vertex Y is already negated in CubismUnmanagedDrawables.GetVertexPositions
    ///   - Model-space units are scaled by (CanvasSize / PixelsPerUnit) to Godot pixels
    ///   - UV coordinates are passed through unchanged (both engines use 0,0=top-left)
    ///
    /// Draw order mapping:
    ///   - Unity uses SortingOrder on MeshRenderer
    ///   - Godot uses ZIndex on CanvasItem
    ///   - Render orders from Core are directly mapped to ZIndex
    ///
    /// Blend mode mapping:
    ///   - Normal → default CanvasItemMaterial blend
    ///   - Additive → CanvasItemMaterial.BlendMode = Add
    ///   - Multiplicative → CanvasItemMaterial.BlendMode = Mul
    ///   - For masking, a SubViewport + shader approach is used
    ///
    /// Culling:
    ///   - Unity CubismRenderer skips update when !IsVisible
    ///   - We set Visible = false on the MeshInstance2D node when drawable.IsVisible is false
    /// </summary>
    public partial class CubismDrawableRenderer : MeshInstance2D
    {
        private int _drawableIndex;
        private CubismModel _model;
        private ArrayMesh _arrayMesh;
        private ShaderMaterial _material;

        private static Shader _normalShader;
        private static Shader _maskedShader;

        public int DrawableIndex => _drawableIndex;

        public static Shader GetNormalShader()
        {
            if (_normalShader != null) return _normalShader;

            _normalShader = new Shader();
            _normalShader.Code = @"
shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D cubism_texture : filter_linear_mipmap;
uniform vec4 multiply_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 screen_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);
uniform float drawable_opacity : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    vec4 tex_color = texture(cubism_texture, UV);
    vec4 result = tex_color * multiply_color;
    result.rgb = result.rgb + screen_color.rgb - (result.rgb * screen_color.rgb);
    result.a *= drawable_opacity;
    COLOR = result;
}
";
            return _normalShader;
        }

        public static Shader GetMaskedShader()
        {
            if (_maskedShader != null) return _maskedShader;

            _maskedShader = new Shader();
            _maskedShader.Code = @"
shader_type canvas_item;

uniform sampler2D cubism_texture : filter_linear_mipmap;
uniform sampler2D mask_texture : hint_default_white;
uniform vec4 multiply_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 screen_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);
uniform float drawable_opacity : hint_range(0.0, 1.0) = 1.0;
uniform bool invert_mask = false;

void fragment() {
    vec4 tex_color = texture(cubism_texture, UV);
    vec4 result = tex_color * multiply_color;
    result.rgb = result.rgb + screen_color.rgb - (result.rgb * screen_color.rgb);

    float mask_alpha = texture(mask_texture, SCREEN_UV).a;
    if (invert_mask) {
        mask_alpha = 1.0 - mask_alpha;
    }
    result.a *= drawable_opacity * mask_alpha;
    COLOR = result;
}
";
            return _maskedShader;
        }

        public void Initialize(CubismModel model, int drawableIndex, Texture2D texture)
        {
            _model = model;
            _drawableIndex = drawableIndex;

            _material = new ShaderMaterial();
            _material.Shader = GetNormalShader();
            if (texture != null)
                _material.SetShaderParameter("cubism_texture", texture);

            Material = _material;

            UpdateMesh();
            UpdateBlendMode();
        }

        public void SetMaskTexture(Texture2D maskTexture, bool invertMask)
        {
            _material.Shader = GetMaskedShader();
            _material.SetShaderParameter("mask_texture", maskTexture);
            _material.SetShaderParameter("invert_mask", invertMask);
        }

        public void UpdateRender()
        {
            if (_model == null || !_model.IsValid) return;

            var drawable = _model.Drawables[_drawableIndex];

            // Culling: hide invisible drawables
            Visible = drawable.IsVisible && drawable.Opacity > 0.001f;
            if (!Visible) return;

            // Draw order → ZIndex
            ZIndex = drawable.RenderOrder;

            // Update opacity
            _material?.SetShaderParameter("drawable_opacity", drawable.Opacity);

            // Update multiply/screen colors
            _material?.SetShaderParameter("multiply_color",
                new Godot.Color(drawable.MultiplyColor.R, drawable.MultiplyColor.G,
                    drawable.MultiplyColor.B, drawable.MultiplyColor.A));
            _material?.SetShaderParameter("screen_color",
                new Godot.Color(drawable.ScreenColor.R, drawable.ScreenColor.G,
                    drawable.ScreenColor.B, drawable.ScreenColor.A));

            // Rebuild mesh if vertex positions changed
            UpdateMesh();
        }

        private void UpdateMesh()
        {
            var positions = _model.GetDrawableVertexPositions(_drawableIndex);
            var uvs = _model.GetDrawableVertexUvs(_drawableIndex);
            var indices = _model.GetDrawableIndices(_drawableIndex);

            if (positions == null || positions.Length == 0 ||
                indices == null || indices.Length < 3)
            {
                Visible = false;
                return;
            }

            // Cubism Core positions are in model space where 1 unit = PixelsPerUnit pixels.
            // In Unity, the camera orthographic size handles the scaling.
            // In Godot 2D, we multiply by PixelsPerUnit to get pixel coordinates,
            // then optionally divide by a display scale for fitting on screen.
            // A typical Cubism model uses PPU matching canvas width, so model coords
            // span roughly [-0.5, 0.5]. Multiplying by PPU gives pixel coords.
            var ppu = _model.PixelsPerUnit;
            var scale = ppu > 0 ? ppu : 1f;

            var scaledPositions = new Vector2[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                scaledPositions[i] = positions[i] * scale;
            }

            var colors = new Color[positions.Length];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Colors.White;

            if (_arrayMesh == null)
                _arrayMesh = new ArrayMesh();
            else
                _arrayMesh.ClearSurfaces();

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = scaledPositions;
            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
            arrays[(int)Mesh.ArrayType.Color] = colors;
            arrays[(int)Mesh.ArrayType.Index] = indices;

            _arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            Mesh = _arrayMesh;
        }

        private void UpdateBlendMode()
        {
            if (_model == null) return;

            var drawable = _model.Drawables[_drawableIndex];

            // Set blend mode via CanvasItem blend_mode property
            // Shader material remains as the rendering material
            if (drawable.IsAdditive)
            {
                _material.SetShaderParameter("blend_mode", 1);
            }
            else if (drawable.IsMultiplicative)
            {
                _material.SetShaderParameter("blend_mode", 2);
            }
        }
    }
}
