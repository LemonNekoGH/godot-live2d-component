/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.Runtime.InteropServices;

namespace Live2D.Cubism.Core.Unmanaged
{
    internal static class CubismUnmanagedMemory
    {
        public static IntPtr Allocate(int size, int alignment)
        {
            var rawPtr = Marshal.AllocHGlobal(size + alignment);
            var aligned = new IntPtr(((long)rawPtr + alignment - 1) & ~((long)alignment - 1));

            unsafe
            {
                ((IntPtr*)aligned)[-1] = rawPtr;
            }

            return aligned;
        }

        public static void Deallocate(IntPtr alignedPtr)
        {
            if (alignedPtr == IntPtr.Zero) return;

            unsafe
            {
                var rawPtr = ((IntPtr*)alignedPtr)[-1];
                Marshal.FreeHGlobal(rawPtr);
            }
        }
    }
}
