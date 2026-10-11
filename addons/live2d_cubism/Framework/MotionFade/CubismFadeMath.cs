/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core;

namespace Live2D.Cubism.Framework.MotionFade
{
    /// <summary>Unity CubismFadeMath / Native CubismMath.GetEasingSine.</summary>
    public static class CubismFadeMath
    {
        public static float GetEasingSine(float value) => CubismMath.GetEasingSine(value);
    }
}
