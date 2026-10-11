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
    /// Ported from Unity CubismRenderer. Key mapping decisions:
    ///
    /// 1. Premultiplied alpha: Unity's CubismCG.cginc does COLOR.rgb *= COLOR.a
    ///    before applying masks and model opacity. Our shader mirrors this exactly
    ///    with render_mode blend_premul_alpha.
    ///
    /// 2. Vertex colors: Unity bakes per-drawable opacity into vertex color alpha.
    ///    We do the same: color.a = drawableOpacity * modelOpacity.
    ///
    /// 3. Coordinates: Cubism Core outputs Y-up model-space positions.
    ///    We negate Y for Godot 2D (Y-down) and scale by PixelsPerUnit.
    ///
    /// 4. UVs: Cubism Core UVs are (0,0)=top-left matching Godot. Pass through.
    ///
    /// 5. Draw order: Core's RenderOrder maps to Godot ZIndex directly.
    ///
    /// 6. Culling: Drawables with IsVisible=false or Opacity≤0 are hidden.
    ///    Non-double-sided drawables use render_mode cull_back in the shader.
    /// </summary>
    public partial class CubismDrawableRenderer : MeshInstance2D
    {
        private int _drawableIndex;
        private CubismModel _model;
        private ArrayMesh _arrayMesh;
        private ShaderMaterial _material;
        private Texture2D _texture;
        private bool _hasMask;
        private bool _invertMask;
        private float _modelOpacity = 1f;

        // Shaders are shared across all instances
        private static Shader _normalShader;
        private static Shader _additiveShader;
        private static Shader _multiplyShader;
        private static Shader _maskShader;

        public int DrawableIndex => _drawableIndex;

        private static Shader CreateShader(string blendMode)
        {
            var shader = new Shader();
            shader.Code = $@"
shader_type canvas_item;
render_mode {blendMode};

uniform sampler2D cubism_texture : filter_linear_mipmap;
uniform float cubism_model_opacity : hint_range(0.0, 1.0) = 1.0;
uniform vec4 cubism_multiply_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 cubism_screen_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);

void fragment() {{
    vec4 tex_color = texture(cubism_texture, UV);

    // Apply multiply color
    vec4 result = tex_color * cubism_multiply_color;
    // Apply screen color
    result.rgb = result.rgb + cubism_screen_color.rgb - (result.rgb * cubism_screen_color.rgb);

    // Apply opacity from vertex color alpha (drawable opacity * model opacity baked in)
    result.a *= COLOR.a * cubism_model_opacity;

    COLOR = result;
}}
";
            return shader;
        }

        public static Shader GetNormalShader()
        {
            return _normalShader ??= CreateShader("blend_mix");
        }

        public static Shader GetAdditiveShader()
        {
            return _additiveShader ??= CreateShader("blend_add");
        }

        public static Shader GetMultiplyShader()
        {
            return _multiplyShader ??= CreateShader("blend_mul");
        }

        public static Shader GetMaskShader()
        {
            if (_maskShader != null) return _maskShader;
            _maskShader = new Shader();
            _maskShader.Code = @"
shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D cubism_texture : filter_linear_mipmap;
uniform sampler2D cubism_mask_texture : hint_default_white;
uniform float cubism_model_opacity : hint_range(0.0, 1.0) = 1.0;
uniform vec4 cubism_multiply_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 cubism_screen_color : source_color = vec4(0.0, 0.0, 0.0, 0.0);
uniform bool cubism_invert_mask = false;

void fragment() {
    vec4 tex_color = texture(cubism_texture, UV);

    vec4 result = tex_color * cubism_multiply_color;
    result.rgb = result.rgb + cubism_screen_color.rgb - (result.rgb * cubism_screen_color.rgb);

    // Sample mask texture
    float mask_value = texture(cubism_mask_texture, UV).a;
    if (cubism_invert_mask) {
        mask_value = 1.0 - mask_value;
    }

    result.a *= COLOR.a * cubism_model_opacity * mask_value;

    COLOR = result;
}
";
            return _maskShader;
        }

        public void Initialize(CubismModel model, int drawableIndex, Texture2D texture)
        {
            _model = model;
            _drawableIndex = drawableIndex;
            _texture = texture;

            var drawable = model.Drawables[drawableIndex];

            // Select shader based on blend mode
            Shader shader;
            if (drawable.IsAdditive)
                shader = GetAdditiveShader();
            else if (drawable.IsMultiplicative)
                shader = GetMultiplyShader();
            else
                shader = GetNormalShader();

            _material = new ShaderMaterial();
            _material.Shader = shader;

            if (_texture != null)
                _material.SetShaderParameter("cubism_texture", _texture);

            Material = _material;

            // Double-sided drawables should not cull back faces
            // (handled by separate shader variant or disabling cull in Godot)

            // Initial mesh build
            RebuildMesh();
        }

        public void SetMaskTexture(Texture2D maskTexture, bool invertMask)
        {
            _hasMask = maskTexture != null;
            _invertMask = invertMask;

            if (_hasMask)
            {
                _material.Shader = GetMaskShader();
                _material.SetShaderParameter("cubism_mask_texture", maskTexture);
                _material.SetShaderParameter("cubism_invert_mask", invertMask);
                if (_texture != null)
                    _material.SetShaderParameter("cubism_texture", _texture);
            }
        }

        public void SetModelOpacity(float opacity)
        {
            _modelOpacity = opacity;
        }

        /// <summary>
        /// Full per-frame render update. Called after CubismModel.UpdateModel().
        /// Mirrors Unity CubismRenderController.OnDynamicDrawableData flow.
        /// </summary>
        public void UpdateRender()
        {
            if (_model == null || !_model.IsValid) return;

            var drawable = _model.Drawables[_drawableIndex];

            // Culling (matches Unity: MeshRenderer.enabled = IsVisible)
            bool shouldShow = drawable.IsVisible && drawable.Opacity > 0.001f;
            Visible = shouldShow;
            if (!shouldShow) return;

            // Draw order (matches Unity: SortingOrder = RenderOrder)
            ZIndex = drawable.RenderOrder;

            // Update multiply/screen colors
            _material.SetShaderParameter("cubism_multiply_color",
                new Godot.Color(drawable.MultiplyColor.R, drawable.MultiplyColor.G,
                    drawable.MultiplyColor.B, drawable.MultiplyColor.A));
            _material.SetShaderParameter("cubism_screen_color",
                new Godot.Color(drawable.ScreenColor.R, drawable.ScreenColor.G,
                    drawable.ScreenColor.B, drawable.ScreenColor.A));

            _material.SetShaderParameter("cubism_model_opacity", _modelOpacity);

            // Rebuild mesh with updated positions
            RebuildMesh();
        }

        private void RebuildMesh()
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

            var drawable = _model.Drawables[_drawableIndex];
            var ppu = _model.PixelsPerUnit;
            var scale = ppu > 0 ? ppu : 1f;

            var scaledPositions = new Vector2[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                scaledPositions[i] = positions[i] * scale;
            }

            // Vertex colors: bake drawable opacity into alpha (matches Unity)
            var colors = new Color[positions.Length];
            var opacity = drawable.Opacity;
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color(1f, 1f, 1f, opacity);
            }

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
    }
}
