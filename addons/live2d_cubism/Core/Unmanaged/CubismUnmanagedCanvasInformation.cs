/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using Godot;

namespace Live2D.Cubism.Core.Unmanaged
{
    public sealed class CubismUnmanagedCanvasInformation
    {
        public Vector2 SizeInPixels { get; }
        public Vector2 OriginInPixels { get; }
        public float PixelsPerUnit { get; }

        public unsafe CubismUnmanagedCanvasInformation(IntPtr modelPtr)
        {
            float* size = stackalloc float[2];
            float* origin = stackalloc float[2];
            float pixelsPerUnit;

            CubismCoreDll.ReadCanvasInfo(
                modelPtr, (IntPtr)size, (IntPtr)origin, (IntPtr)(&pixelsPerUnit));

            SizeInPixels = new Vector2(size[0], size[1]);
            OriginInPixels = new Vector2(origin[0], origin[1]);
            PixelsPerUnit = pixelsPerUnit;
        }
    }
}
