/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Godot;
using Live2D.Cubism.Core;

namespace Live2D.Cubism.Rendering
{
    /// <summary>
    /// Renders a single CubismDrawable as a Godot MeshInstance2D.
    ///
    /// Ported from Unity CubismRenderer + Native CubismShader_OpenGLES2:
    ///
    /// 1. UVs: Core stores V=0 at the image bottom (Unity). Native's VertShaderSrc.vert
    ///    flips V; we flip in CubismUnmanagedDrawables.GetVertexUvs so Godot (V=0 top) matches.
    ///
    /// 2. Color: FragShaderSrc.frag multiply / screen blend, then premultiply
    ///    (CubismCG.cginc COLOR.rgb *= COLOR.a) and apply model opacity.
    ///
    /// 3. Blend: Native uses GL_ONE, GL_ONE_MINUS_SRC_ALPHA (premultiplied).
    ///    Godot render_mode blend_premul_alpha.
    ///
    /// 4. Masks: high-precision path from Unity CubismRenderer.CalcMaskTransform —
    ///    mask UV = (vertex - boundsCenter) / boundsScale + 0.5, sampled from a SubViewport.
    ///
    /// 5. Draw order: Core RenderOrder → ZIndex.
    ///
    /// 6. Culling: hidden when !IsVisible or Opacity≈0. Non-double-sided uses cull_back;
    ///    winding was restored after the Y-negate in GetIndices.
    /// </summary>
    public partial class CubismDrawableRenderer : MeshInstance2D
    {
        private int _drawableIndex;
        private CubismModel _model;
        private ArrayMesh _arrayMesh;
        private ShaderMaterial _material;
        private Texture2D _texture;
        private float _modelOpacity = 1f;
        private bool _useMask;
        private bool _invertMask;
        private bool _isHitArea;

        private static Shader _normalShader;
        private static Shader _additiveShader;
        private static Shader _multiplyShader;
        private static Shader _maskNormalShader;
        private static Shader _maskAdditiveShader;
        private static Shader _maskMultiplyShader;
        private static Shader _maskInvertNormalShader;

        public int DrawableIndex => _drawableIndex;
        public bool IsMasked { get; private set; }
        public bool InvertMask => _invertMask;

        private static string ColorBody(bool masked, bool invertMask)
        {
            var maskSample = "";
            if (masked)
            {
                maskSample = invertMask
                    ? @"
    float cubism_mask_alpha = texture(cubism_mask_texture, cubism_mask_uv).a;
    color *= 1.0 - cubism_mask_alpha;
"
                    : @"
    float cubism_mask_alpha = texture(cubism_mask_texture, cubism_mask_uv).a;
    color *= cubism_mask_alpha;
";
            }

            var varyings = masked
                ? @"
varying vec2 cubism_mask_uv;
uniform sampler2D cubism_mask_texture : filter_linear, hint_default_white;
uniform vec2 cubism_mask_center = vec2(0.0);
uniform float cubism_mask_inv_scale = 1.0;

void vertex() {
    cubism_mask_uv = (VERTEX - cubism_mask_center) * cubism_mask_inv_scale + vec2(0.5);
}
"
                : "";

            return $@"
shader_type canvas_item;
render_mode {{BLEND}}{{CULL}};

uniform sampler2D cubism_texture : filter_linear_mipmap, repeat_enable;
uniform float cubism_model_opacity : hint_range(0.0, 1.0) = 1.0;
uniform vec4 cubism_multiply_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 cubism_screen_color = vec4(0.0, 0.0, 0.0, 1.0);
{varyings}
void fragment() {{
    vec4 tex_color = texture(cubism_texture, UV);
    // Native FragShaderSrc.frag multiply + screen
    tex_color.rgb = tex_color.rgb * cubism_multiply_color.rgb;
    tex_color.rgb = tex_color.rgb + cubism_screen_color.rgb - (tex_color.rgb * cubism_screen_color.rgb);

    vec4 color = tex_color * COLOR;
    color *= cubism_model_opacity;
{maskSample}
    // CubismCG.cginc / FragShaderSrc.frag: premultiply before blend
    COLOR = vec4(color.rgb * color.a, color.a);
}}
";
        }

        private static Shader BuildShader(string blendMode, bool cullBack, bool masked, bool invertMask)
        {
            var shader = new Shader();
            var cull = cullBack ? ", cull_back" : "";
            shader.Code = ColorBody(masked, invertMask)
                .Replace("{BLEND}", blendMode)
                .Replace("{CULL}", cull);
            return shader;
        }

        private static Shader GetShader(bool additive, bool multiplicative, bool cullBack, bool masked, bool invertMask)
        {
            if (masked && invertMask)
                return _maskInvertNormalShader ??= BuildShader("blend_premul_alpha", false, true, true);

            if (masked)
            {
                if (additive)
                    return _maskAdditiveShader ??= BuildShader("blend_add", false, true, false);
                if (multiplicative)
                    return _maskMultiplyShader ??= BuildShader("blend_mul", false, true, false);
                return _maskNormalShader ??= BuildShader("blend_premul_alpha", false, true, false);
            }

            if (additive)
                return _additiveShader ??= BuildShader("blend_add", cullBack, false, false);
            if (multiplicative)
                return _multiplyShader ??= BuildShader("blend_mul", cullBack, false, false);
            return _normalShader ??= BuildShader("blend_premul_alpha", cullBack, false, false);
        }

        public static Shader GetMaskWriteShader()
        {
            if (_maskWriteShader != null) return _maskWriteShader;
            _maskWriteShader = new Shader();
            _maskWriteShader.Code = @"
shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform sampler2D cubism_texture : filter_linear_mipmap, repeat_enable;

void fragment() {
    float a = texture(cubism_texture, UV).a * COLOR.a;
    COLOR = vec4(1.0, 1.0, 1.0, a);
}
";
            return _maskWriteShader;
        }

        private static Shader _maskWriteShader;

        public void Initialize(CubismModel model, int drawableIndex, Texture2D texture)
        {
            _model = model;
            _drawableIndex = drawableIndex;
            _texture = texture;

            var drawable = model.Drawables[drawableIndex];
            IsMasked = drawable.MaskIndices != null && drawable.MaskIndices.Length > 0;
            _invertMask = drawable.IsInvertedMask;
            _isHitArea = drawable.Id != null && drawable.Id.StartsWith("HitArea");

            TextureFilter = TextureFilterEnum.LinearWithMipmaps;
            TextureRepeat = TextureRepeatEnum.Enabled;
            Texture = _texture;

            ApplyMaterial(masked: false);
            RebuildMesh();
        }

        private void ApplyMaterial(bool masked)
        {
            var drawable = _model.Drawables[_drawableIndex];
            var cullBack = !drawable.IsDoubleSided;
            var shader = GetShader(drawable.IsAdditive, drawable.IsMultiplicative, cullBack, masked, _invertMask);

            _material = new ShaderMaterial();
            _material.Shader = shader;
            if (_texture != null)
                _material.SetShaderParameter("cubism_texture", _texture);
            _material.SetShaderParameter("cubism_model_opacity", _modelOpacity);
            Material = _material;
            _useMask = masked;
        }

        public void SetMaskTexture(Texture2D maskTexture, Vector2 maskCenter, float maskScale, bool invertMask)
        {
            _invertMask = invertMask;
            if (maskTexture == null || maskScale <= 0f)
            {
                if (_useMask)
                    ApplyMaterial(masked: false);
                return;
            }

            if (!_useMask)
                ApplyMaterial(masked: true);

            _material.SetShaderParameter("cubism_mask_texture", maskTexture);
            _material.SetShaderParameter("cubism_mask_center", maskCenter);
            _material.SetShaderParameter("cubism_mask_inv_scale", 1f / maskScale);
            if (_texture != null)
                _material.SetShaderParameter("cubism_texture", _texture);
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

            bool shouldShow = !_isHitArea && drawable.IsVisible && drawable.Opacity > 0.001f;
            Visible = shouldShow;
            if (!shouldShow) return;

            ZIndex = drawable.RenderOrder;

            if (_material != null)
            {
                _material.SetShaderParameter("cubism_multiply_color",
                    new Color(drawable.MultiplyColor.R, drawable.MultiplyColor.G,
                        drawable.MultiplyColor.B, drawable.MultiplyColor.A));
                _material.SetShaderParameter("cubism_screen_color",
                    new Color(drawable.ScreenColor.R, drawable.ScreenColor.G,
                        drawable.ScreenColor.B, drawable.ScreenColor.A));
                _material.SetShaderParameter("cubism_model_opacity", _modelOpacity);
            }

            RebuildMesh();
        }

        /// <summary>
        /// Rebuilds the mesh used when this drawable is a clipping-mask source.
        /// Same geometry as the main mesh; the caller assigns the mask-write shader.
        /// </summary>
        public void FillMaskArrays(out Vector2[] positions, out Vector2[] uvs, out int[] indices, out Color[] colors)
        {
            BuildMeshArrays(out positions, out uvs, out indices, out colors);
        }

        private void RebuildMesh()
        {
            if (!BuildMeshArrays(out var packedPos, out var packedUv, out var packedIdx, out var packedCol))
            {
                Visible = false;
                return;
            }

            if (_arrayMesh == null)
                _arrayMesh = new ArrayMesh();
            else
                _arrayMesh.ClearSurfaces();

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = packedPos;
            arrays[(int)Mesh.ArrayType.TexUV] = packedUv;
            arrays[(int)Mesh.ArrayType.Color] = packedCol;
            arrays[(int)Mesh.ArrayType.Index] = packedIdx;

            _arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            Mesh = _arrayMesh;
        }

        private bool BuildMeshArrays(
            out Vector2[] packedPos,
            out Vector2[] packedUv,
            out int[] packedIdx,
            out Color[] packedCol)
        {
            packedPos = System.Array.Empty<Vector2>();
            packedUv = System.Array.Empty<Vector2>();
            packedIdx = System.Array.Empty<int>();
            packedCol = System.Array.Empty<Color>();

            var positions = _model.GetDrawableVertexPositions(_drawableIndex);
            var uvs = _model.GetDrawableVertexUvs(_drawableIndex);
            var indices = _model.GetDrawableIndices(_drawableIndex);

            if (positions == null || positions.Length == 0 ||
                indices == null || indices.Length < 3)
            {
                return false;
            }

            var drawable = _model.Drawables[_drawableIndex];
            var ppu = _model.PixelsPerUnit;
            var scale = ppu > 0 ? ppu : 1f;
            var opacity = drawable.Opacity;

            packedPos = new Vector2[positions.Length];
            packedUv = new Vector2[positions.Length];
            packedCol = new Color[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                packedPos[i] = positions[i] * scale;
                packedUv[i] = i < uvs.Length ? uvs[i] : Vector2.Zero;
                packedCol[i] = new Color(1f, 1f, 1f, opacity);
            }

            packedIdx = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
                packedIdx[i] = indices[i];

            return true;
        }
    }
}
