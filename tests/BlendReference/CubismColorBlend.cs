/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core.Unmanaged;

namespace Live2D.Cubism.Rendering
{
    /// <summary>
    /// Test clone of Native FragShaderSrcColorBlend.frag / Unity ColorBlendVariants.cginc.
    /// Core type ids (csmColorBlendType_*) are the runtime values written to
    /// <c>cubism_color_blend</c>. Compatible Add/Multiply stay on the old GL path.
    /// </summary>
    public static class CubismColorBlend
    {
        public static void Apply(int mode, float sr, float sg, float sb, float dr, float dg, float db,
            out float or, out float og, out float ob)
        {
            switch (mode)
            {
                case CubismCoreDll.ColorBlendType_Add:
                case CubismCoreDll.ColorBlendType_AddCompatible:
                    or = Min1(sr + dr); og = Min1(sg + dg); ob = Min1(sb + db);
                    return;
                case CubismCoreDll.ColorBlendType_AddGlow:
                    or = sr + dr; og = sg + dg; ob = sb + db;
                    return;
                case CubismCoreDll.ColorBlendType_Darken:
                    or = Min(sr, dr); og = Min(sg, dg); ob = Min(sb, db);
                    return;
                case CubismCoreDll.ColorBlendType_Multiply:
                case CubismCoreDll.ColorBlendType_MultiplyCompatible:
                    or = sr * dr; og = sg * dg; ob = sb * db;
                    return;
                case CubismCoreDll.ColorBlendType_ColorBurn:
                    or = ColorBurn(sr, dr); og = ColorBurn(sg, dg); ob = ColorBurn(sb, db);
                    return;
                case CubismCoreDll.ColorBlendType_LinearBurn:
                    or = Max(0f, sr + dr - 1f);
                    og = Max(0f, sg + dg - 1f);
                    ob = Max(0f, sb + db - 1f);
                    return;
                case CubismCoreDll.ColorBlendType_Lighten:
                    or = Max(sr, dr); og = Max(sg, dg); ob = Max(sb, db);
                    return;
                case CubismCoreDll.ColorBlendType_Screen:
                    or = sr + dr - sr * dr;
                    og = sg + dg - sg * dg;
                    ob = sb + db - sb * db;
                    return;
                case CubismCoreDll.ColorBlendType_ColorDodge:
                    or = ColorDodge(sr, dr); og = ColorDodge(sg, dg); ob = ColorDodge(sb, db);
                    return;
                case CubismCoreDll.ColorBlendType_Overlay:
                    or = Overlay(sr, dr); og = Overlay(sg, dg); ob = Overlay(sb, db);
                    return;
                case CubismCoreDll.ColorBlendType_SoftLight:
                    or = SoftLight(sr, dr); og = SoftLight(sg, dg); ob = SoftLight(sb, db);
                    return;
                case CubismCoreDll.ColorBlendType_HardLight:
                    or = HardLight(sr, dr); og = HardLight(sg, dg); ob = HardLight(sb, db);
                    return;
                case CubismCoreDll.ColorBlendType_LinearLight:
                    or = LinearLight(sr, dr); og = LinearLight(sg, dg); ob = LinearLight(sb, db);
                    return;
                case CubismCoreDll.ColorBlendType_Hue:
                    Hue(sr, sg, sb, dr, dg, db, out or, out og, out ob);
                    return;
                case CubismCoreDll.ColorBlendType_Color:
                    SetLuma(sr, sg, sb, Luma(dr, dg, db), out or, out og, out ob);
                    return;
                default:
                    or = sr; og = sg; ob = sb;
                    return;
            }
        }

        public static float ColorBurn(float cs, float cd)
        {
            if (Abs(cd - 1f) < 0.000001f) return 1f;
            if (Abs(cs) < 0.000001f) return 0f;
            return 1f - Min(1f, (1f - cd) / cs);
        }

        public static float ColorDodge(float cs, float cd)
        {
            if (cd <= 0f) return 0f;
            if (cs == 1f) return 1f;
            return Min(1f, cd / (1f - cs));
        }

        public static float Overlay(float cs, float cd)
        {
            float mul = 2f * cs * cd;
            float scr = 1f - 2f * (1f - cs) * (1f - cd);
            return cd < 0.5f ? mul : scr;
        }

        public static float SoftLight(float cs, float cd)
        {
            float val1 = cd - (1f - 2f * cs) * cd * (1f - cd);
            float val2 = cd + (2f * cs - 1f) * cd * ((16f * cd - 12f) * cd + 3f);
            float val3 = cd + (2f * cs - 1f) * (Sqrt(cd) - cd);
            if (cs <= 0.5f) return val1;
            if (cd <= 0.25f) return val2;
            return val3;
        }

        public static float HardLight(float cs, float cd)
        {
            float mul = 2f * cs * cd;
            float scr = 1f - 2f * (1f - cs) * (1f - cd);
            return cs < 0.5f ? mul : scr;
        }

        public static float LinearLight(float cs, float cd)
        {
            return cs < 0.5f
                ? Max(0f, 2f * cs + cd - 1f)
                : Min(1f, 2f * (cs - 0.5f) + cd);
        }

        const float RCoeff = 0.30f;
        const float GCoeff = 0.59f;
        const float BCoeff = 0.11f;

        public static float Luma(float r, float g, float b) => RCoeff * r + GCoeff * g + BCoeff * b;

        static void Hue(float sr, float sg, float sb, float dr, float dg, float db,
            out float or, out float og, out float ob)
        {
            SetSaturation(sr, sg, sb, Saturation(dr, dg, db), out or, out og, out ob);
            SetLuma(or, og, ob, Luma(dr, dg, db), out or, out og, out ob);
        }

        static float Saturation(float r, float g, float b) => Max(r, Max(g, b)) - Min(r, Min(g, b));

        static void SetLuma(float r, float g, float b, float luma, out float or, out float og, out float ob)
        {
            float d = luma - Luma(r, g, b);
            ClipColor(r + d, g + d, b + d, out or, out og, out ob);
        }

        static void SetSaturation(float r, float g, float b, float saturation,
            out float or, out float og, out float ob)
        {
            float maxv = Max(r, Max(g, b));
            float minv = Min(r, Min(g, b));
            float medv = r + g + b - maxv - minv;
            float outputMax = minv < maxv ? saturation : 0f;
            float outputMed = minv < maxv ? (medv - minv) * saturation / (maxv - minv) : 0f;
            float outputMin = 0f;

            if (r == maxv)
            {
                if (b < g) { or = outputMax; og = outputMed; ob = outputMin; }
                else { or = outputMax; og = outputMin; ob = outputMed; }
            }
            else if (g == maxv)
            {
                if (r < b) { or = outputMin; og = outputMax; ob = outputMed; }
                else { or = outputMed; og = outputMax; ob = outputMin; }
            }
            else
            {
                if (g < r) { or = outputMed; og = outputMin; ob = outputMax; }
                else { or = outputMin; og = outputMed; ob = outputMax; }
            }
        }

        static void ClipColor(float r, float g, float b, out float or, out float og, out float ob)
        {
            float luma = Luma(r, g, b);
            float maxv = Max(r, Max(g, b));
            float minv = Min(r, Min(g, b));
            or = r; og = g; ob = b;
            if (minv < 0f)
            {
                float s = luma / (luma - minv);
                or = luma + (or - luma) * s;
                og = luma + (og - luma) * s;
                ob = luma + (ob - luma) * s;
            }
            if (maxv > 1f)
            {
                float s = (1f - luma) / (maxv - luma);
                or = luma + (or - luma) * s;
                og = luma + (og - luma) * s;
                ob = luma + (ob - luma) * s;
            }
        }

        static float Abs(float v) => v < 0f ? -v : v;
        static float Min(float a, float b) => a < b ? a : b;
        static float Max(float a, float b) => a > b ? a : b;
        static float Min1(float v) => v > 1f ? 1f : v;
        static float Sqrt(float v) => System.MathF.Sqrt(v);
    }
}
