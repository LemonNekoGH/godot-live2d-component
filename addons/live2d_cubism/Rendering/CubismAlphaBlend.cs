/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using Live2D.Cubism.Core.Unmanaged;

namespace Live2D.Cubism.Rendering
{
    /// <summary>
    /// CPU reference for Unity AlphaBlendVariants.cginc and Native
    /// FragShaderSrcAlphaBlend.frag. Inputs are straight RGB and alpha; the
    /// returned RGB is premultiplied, exactly like the SDK blend-image pass.
    /// </summary>
    public static class CubismAlphaBlend
    {
        public static void Apply(int mode,
            float cr, float cg, float cb,
            float sr, float sg, float sb, float sourceAlpha,
            float dr, float dg, float db, float destinationAlpha,
            out float rr, out float rg, out float rb, out float ra)
        {
            float overlap;
            float sourceOnly;
            float destinationOnly;

            switch (mode)
            {
                case CubismCoreDll.AlphaBlendType_Atop:
                    overlap = sourceAlpha * destinationAlpha;
                    sourceOnly = 0f;
                    destinationOnly = destinationAlpha * (1f - sourceAlpha);
                    break;
                case CubismCoreDll.AlphaBlendType_Out:
                    overlap = 0f;
                    sourceOnly = 0f;
                    destinationOnly = destinationAlpha * (1f - sourceAlpha);
                    break;
                case CubismCoreDll.AlphaBlendType_ConjointOver:
                    overlap = MathF.Min(sourceAlpha, destinationAlpha);
                    sourceOnly = MathF.Max(sourceAlpha - destinationAlpha, 0f);
                    destinationOnly = MathF.Max(destinationAlpha - sourceAlpha, 0f);
                    break;
                case CubismCoreDll.AlphaBlendType_DisjointOver:
                    overlap = MathF.Max(sourceAlpha + destinationAlpha - 1f, 0f);
                    sourceOnly = MathF.Min(sourceAlpha, 1f - destinationAlpha);
                    destinationOnly = MathF.Min(destinationAlpha, 1f - sourceAlpha);
                    break;
                default:
                    overlap = sourceAlpha * destinationAlpha;
                    sourceOnly = sourceAlpha * (1f - destinationAlpha);
                    destinationOnly = destinationAlpha * (1f - sourceAlpha);
                    break;
            }

            rr = cr * overlap + sr * sourceOnly + dr * destinationOnly;
            rg = cg * overlap + sg * sourceOnly + dg * destinationOnly;
            rb = cb * overlap + sb * sourceOnly + db * destinationOnly;
            ra = overlap + sourceOnly + destinationOnly;
        }
    }
}
