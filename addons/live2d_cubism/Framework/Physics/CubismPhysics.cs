/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Godot;

namespace Live2D.Cubism.Framework.Physics
{
    /// <summary>
    /// Global physics constants. Mirrors Unity CubismPhysics exactly.
    /// </summary>
    public static class CubismPhysics
    {
        public static Vector2 Gravity = new Vector2(0, -1);
        public static Vector2 Wind = Vector2.Zero;
        public static float AirResistance = 5.0f;
        public static float MaximumWeight = 100.0f;
        public static bool UseFixedDeltaTime = false;
        public static bool UseAngleCorrection = true;
        public const float MovementThreshold = 0.001f;
        public const float MaxDeltaTime = 5.0f;
    }
}
