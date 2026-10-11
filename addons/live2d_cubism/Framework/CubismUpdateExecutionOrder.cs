/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

namespace Live2D.Cubism.Framework
{
    /// <summary>
    /// Defines execution order for updatable components.
    /// Mirrors Unity's CubismUpdateExecutionOrder exactly.
    /// Order values determine the sequence in which controllers are updated
    /// during the late-update phase, matching Unity's PreLateUpdate behavior.
    /// </summary>
    public static class CubismUpdateExecutionOrder
    {
        public const int CubismFadeController = 100;
        public const int CubismPoseController = 200;
        public const int CubismExpressionController = 300;
        public const int CubismEyeBlinkController = 400;
        public const int CubismMouthController = 500;
        public const int CubismHarmonicMotionController = 600;
        public const int CubismLookController = 700;
        public const int CubismPhysicsController = 800;
        public const int CubismRenderController = 10000;
    }
}
