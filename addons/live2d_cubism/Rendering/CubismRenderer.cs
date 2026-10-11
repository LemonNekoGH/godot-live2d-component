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
        private BackBufferCopy _backBufferCopy;
        public int ColorBlendOverride { get; set; } = -1;
        public int AlphaBlendOverride { get; set; } = -1;

        private static Shader _normalShader;
        private static Shader _additiveShader;
        private static Shader _multiplyShader;
        private static Shader _extendedShader;
        private static Shader _extendedCullShader;
        private static Shader _extendedMaskShader;
        private static Shader _extendedMaskInvertShader;
        private int _appliedBlend = int.MinValue;
        private int _appliedAlphaBlend = int.MinValue;
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
            return GetShader(additive, multiplicative, cullBack, masked, invertMask, CubismCoreDll.ColorBlendType_Normal);
        }

        private static Shader GetShader(bool additive, bool multiplicative, bool cullBack, bool masked, bool invertMask,
            int colorBlend, int alphaBlend = CubismCoreDll.AlphaBlendType_Over)
        {
            if (colorBlend > CubismCoreDll.ColorBlendType_MultiplyCompatible ||
                alphaBlend != CubismCoreDll.AlphaBlendType_Over)
            {
                if (masked && invertMask)
                    return _extendedMaskInvertShader ??= BuildExtendedShader(false, true, true);
                if (masked)
                    return _extendedMaskShader ??= BuildExtendedShader(false, true, false);
                return cullBack
                    ? (_extendedCullShader ??= BuildExtendedShader(true, false, false))
                    : (_extendedShader ??= BuildExtendedShader(false, false, false));
            }

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

        /// <summary>
        /// Cubism 5 extended color blends. Functions match Native FragShaderSrcColorBlend.frag
        /// and Unity ColorBlendVariants.cginc. Destination is hint_screen_texture.
        /// </summary>
        private static Shader BuildExtendedShader(bool cullBack, bool masked, bool invertMask)
        {
            var shader = new Shader();
            var cull = cullBack ? ", cull_back" : "";
            var maskSample = "";
            if (masked)
            {
                maskSample = invertMask
                    ? "    color.a *= 1.0 - texture(cubism_mask_texture, cubism_mask_uv).a;\n"
                    : "    color.a *= texture(cubism_mask_texture, cubism_mask_uv).a;\n";
            }

            var varyings = masked
                ? @"
varying vec2 cubism_mask_uv;
uniform sampler2D cubism_mask_texture : filter_linear, hint_default_white;
uniform vec2 cubism_mask_center = vec2(0.0);
uniform float cubism_mask_inv_scale = 1.0;
void vertex() { cubism_mask_uv = (VERTEX - cubism_mask_center) * cubism_mask_inv_scale + vec2(0.5); }
"
                : "";

            shader.Code = $@"
shader_type canvas_item;
render_mode blend_disabled{cull};

uniform sampler2D cubism_texture : filter_linear_mipmap, repeat_enable;
uniform sampler2D cubism_screen : hint_screen_texture, filter_linear;
uniform float cubism_model_opacity : hint_range(0.0, 1.0) = 1.0;
uniform vec4 cubism_multiply_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 cubism_screen_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform int cubism_color_blend = 0;
uniform int cubism_alpha_blend = 0;
{varyings}
float ColorBurnCh(float cs, float cd) {{
    if (abs(cd - 1.0) < 0.000001) return 1.0;
    if (abs(cs) < 0.000001) return 0.0;
    return 1.0 - min(1.0, (1.0 - cd) / cs);
}}
float ColorDodgeCh(float cs, float cd) {{
    if (cd <= 0.0) return 0.0;
    if (cs == 1.0) return 1.0;
    return min(1.0, cd / (1.0 - cs));
}}
float OverlayCh(float cs, float cd) {{
    return cd < 0.5 ? 2.0 * cs * cd : 1.0 - 2.0 * (1.0 - cs) * (1.0 - cd);
}}
float SoftLightCh(float cs, float cd) {{
    float val1 = cd - (1.0 - 2.0 * cs) * cd * (1.0 - cd);
    float val2 = cd + (2.0 * cs - 1.0) * cd * ((16.0 * cd - 12.0) * cd + 3.0);
    float val3 = cd + (2.0 * cs - 1.0) * (sqrt(cd) - cd);
    if (cs <= 0.5) return val1;
    if (cd <= 0.25) return val2;
    return val3;
}}
float HardLightCh(float cs, float cd) {{
    return cs < 0.5 ? 2.0 * cs * cd : 1.0 - 2.0 * (1.0 - cs) * (1.0 - cd);
}}
float LinearLightCh(float cs, float cd) {{
    return cs < 0.5 ? max(0.0, 2.0 * cs + cd - 1.0) : min(1.0, 2.0 * (cs - 0.5) + cd);
}}
const float rCoeff = 0.30;
const float gCoeff = 0.59;
const float bCoeff = 0.11;
float Luma(vec3 c) {{ return rCoeff * c.r + gCoeff * c.g + bCoeff * c.b; }}
vec3 ClipColor(vec3 rgbC) {{
    float luma = Luma(rgbC);
    float maxv = max(rgbC.r, max(rgbC.g, rgbC.b));
    float minv = min(rgbC.r, min(rgbC.g, rgbC.b));
    vec3 outputColor = rgbC;
    outputColor = minv < 0.0 ? luma + (outputColor - luma) * luma / (luma - minv) : outputColor;
    outputColor = maxv > 1.0 ? luma + (outputColor - luma) * (1.0 - luma) / (maxv - luma) : outputColor;
    return outputColor;
}}
vec3 SetLuma(vec3 rgbC, float luma) {{
    return ClipColor(rgbC + (luma - Luma(rgbC)));
}}
vec3 SetSaturation(vec3 rgbC, float saturation) {{
    float maxv = max(rgbC.r, max(rgbC.g, rgbC.b));
    float minv = min(rgbC.r, min(rgbC.g, rgbC.b));
    float medv = rgbC.r + rgbC.g + rgbC.b - maxv - minv;
    float outputMax = minv < maxv ? saturation : 0.0;
    float outputMed = minv < maxv ? (medv - minv) * saturation / (maxv - minv) : 0.0;
    float outputMin = 0.0;
    if (rgbC.r == maxv)
        return rgbC.b < rgbC.g ? vec3(outputMax, outputMed, outputMin) : vec3(outputMax, outputMin, outputMed);
    if (rgbC.g == maxv)
        return rgbC.r < rgbC.b ? vec3(outputMin, outputMax, outputMed) : vec3(outputMed, outputMax, outputMin);
    return rgbC.g < rgbC.r ? vec3(outputMed, outputMin, outputMax) : vec3(outputMin, outputMed, outputMax);
}}
vec3 ColorBlend(vec3 s, vec3 d, int mode) {{
    if (mode == 3 || mode == 1) return min(s + d, vec3(1.0));
    if (mode == 4) return s + d;
    if (mode == 5) return min(s, d);
    if (mode == 6 || mode == 2) return s * d;
    if (mode == 7) return vec3(ColorBurnCh(s.r, d.r), ColorBurnCh(s.g, d.g), ColorBurnCh(s.b, d.b));
    if (mode == 8) return max(vec3(0.0), s + d - 1.0);
    if (mode == 9) return max(s, d);
    if (mode == 10) return s + d - s * d;
    if (mode == 11) return vec3(ColorDodgeCh(s.r, d.r), ColorDodgeCh(s.g, d.g), ColorDodgeCh(s.b, d.b));
    if (mode == 12) return vec3(OverlayCh(s.r, d.r), OverlayCh(s.g, d.g), OverlayCh(s.b, d.b));
    if (mode == 13) return vec3(SoftLightCh(s.r, d.r), SoftLightCh(s.g, d.g), SoftLightCh(s.b, d.b));
    if (mode == 14) return vec3(HardLightCh(s.r, d.r), HardLightCh(s.g, d.g), HardLightCh(s.b, d.b));
    if (mode == 15) return vec3(LinearLightCh(s.r, d.r), LinearLightCh(s.g, d.g), LinearLightCh(s.b, d.b));
    if (mode == 16) {{
        float sat = max(d.r, max(d.g, d.b)) - min(d.r, min(d.g, d.b));
        return SetLuma(SetSaturation(s, sat), Luma(d));
    }}
    if (mode == 17) return SetLuma(s, Luma(d));
    return s;
}}

vec4 AlphaBlend(vec3 blended, vec4 source, vec4 destination, int mode) {{
    vec3 p;
    if (mode == 1)
        p = vec3(source.a * destination.a, 0.0, destination.a * (1.0 - source.a));
    else if (mode == 2)
        p = vec3(0.0, 0.0, destination.a * (1.0 - source.a));
    else if (mode == 3)
        p = vec3(min(source.a, destination.a), max(source.a - destination.a, 0.0), max(destination.a - source.a, 0.0));
    else if (mode == 4)
        p = vec3(max(source.a + destination.a - 1.0, 0.0), min(source.a, 1.0 - destination.a), min(destination.a, 1.0 - source.a));
    else
        p = vec3(source.a * destination.a, source.a * (1.0 - destination.a), destination.a * (1.0 - source.a));
    return vec4(blended * p.x + source.rgb * p.y + destination.rgb * p.z, p.x + p.y + p.z);
}}

void fragment() {{
    vec4 tex_color = texture(cubism_texture, UV);
    tex_color.rgb = tex_color.rgb * cubism_multiply_color.rgb;
    tex_color.rgb = tex_color.rgb + cubism_screen_color.rgb - (tex_color.rgb * cubism_screen_color.rgb);
    vec4 color = tex_color * COLOR;
{maskSample}
    vec4 destination_pma = texture(cubism_screen, SCREEN_UV);
    vec4 destination = destination_pma.a < 0.00001
        ? vec4(0.0)
        : vec4(destination_pma.rgb / destination_pma.a, destination_pma.a);
    vec4 result = AlphaBlend(ColorBlend(color.rgb, destination.rgb, cubism_color_blend),
        color, destination, cubism_alpha_blend);
    COLOR = clamp(result * cubism_model_opacity, vec4(0.0), vec4(1.0));
}}
";
            return shader;
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

        public void Initialize(CubismModel model, int drawableIndex, Texture2D texture,
            BackBufferCopy backBufferCopy = null)
        {
            _model = model;
            _drawableIndex = drawableIndex;
            _texture = texture;
            _backBufferCopy = backBufferCopy;

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
            var blend = EffectiveColorBlend(drawable);
            var alphaBlend = EffectiveAlphaBlend(drawable);
            var shader = GetShader(drawable.IsAdditive, drawable.IsMultiplicative, cullBack, masked, _invertMask,
                blend, alphaBlend);

            _material = new ShaderMaterial();
            _material.Shader = shader;
            if (_texture != null)
                _material.SetShaderParameter("cubism_texture", _texture);
            _material.SetShaderParameter("cubism_model_opacity", _modelOpacity);
            _material.SetShaderParameter("cubism_color_blend", blend);
            _material.SetShaderParameter("cubism_alpha_blend", alphaBlend);
            Material = _material;
            _useMask = masked;
            _appliedBlend = blend;
            _appliedAlphaBlend = alphaBlend;
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

        private int EffectiveColorBlend(CubismDrawable drawable)
        {
            return ColorBlendOverride >= 0 ? ColorBlendOverride : drawable.ColorBlend;
        }

        private int EffectiveAlphaBlend(CubismDrawable drawable)
        {
            return AlphaBlendOverride >= 0 ? AlphaBlendOverride : drawable.AlphaBlend;
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
            var effectiveColorBlend = EffectiveColorBlend(drawable);
            var effectiveAlphaBlend = EffectiveAlphaBlend(drawable);
            if (_backBufferCopy != null)
                _backBufferCopy.Visible = shouldShow &&
                    (effectiveColorBlend > CubismCoreDll.ColorBlendType_MultiplyCompatible ||
                     effectiveAlphaBlend != CubismCoreDll.AlphaBlendType_Over);
            if (!shouldShow) return;

            ZIndex = drawable.RenderOrder;
            if (_backBufferCopy != null)
            {
                _backBufferCopy.ZIndex = drawable.RenderOrder;
            }

            if (_material != null)
            {
                var blend = effectiveColorBlend;
                var alphaBlend = effectiveAlphaBlend;
                if (blend != _appliedBlend || alphaBlend != _appliedAlphaBlend)
                {
                    _appliedBlend = blend;
                    ApplyMaterial(masked: _useMask);
                }
                _material.SetShaderParameter("cubism_multiply_color",
                    new Color(drawable.MultiplyColor.R, drawable.MultiplyColor.G,
                        drawable.MultiplyColor.B, drawable.MultiplyColor.A));
                _material.SetShaderParameter("cubism_screen_color",
                    new Color(drawable.ScreenColor.R, drawable.ScreenColor.G,
                        drawable.ScreenColor.B, drawable.ScreenColor.A));
                _material.SetShaderParameter("cubism_model_opacity", _modelOpacity);
                _material.SetShaderParameter("cubism_color_blend", blend);
                _material.SetShaderParameter("cubism_alpha_blend", alphaBlend);
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
