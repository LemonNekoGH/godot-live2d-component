/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.Runtime.InteropServices;
using Godot;

namespace Live2D.Cubism.Core.Unmanaged
{
    public sealed class CubismUnmanagedCanvasInformation
    {
        public Vector2 SizeInPixels { get; }
        public Vector2 OriginInPixels { get; }
        public float PixelsPerUnit { get; }

        public CubismUnmanagedCanvasInformation(IntPtr modelPtr)
        {
            var sizePtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector2>());
            var originPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector2>());
            var ppuPtr = Marshal.AllocHGlobal(sizeof(float));

            try
            {
                CubismCoreDll.ReadCanvasInfo(modelPtr, sizePtr, originPtr, ppuPtr);

                unsafe
                {
                    var sizeFloats = (float*)sizePtr;
                    SizeInPixels = new Vector2(sizeFloats[0], sizeFloats[1]);

                    var originFloats = (float*)originPtr;
                    OriginInPixels = new Vector2(originFloats[0], originFloats[1]);

                    PixelsPerUnit = *(float*)ppuPtr;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(sizePtr);
                Marshal.FreeHGlobal(originPtr);
                Marshal.FreeHGlobal(ppuPtr);
            }
        }
    }
}
