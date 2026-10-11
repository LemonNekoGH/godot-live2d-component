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
            int extra = alignment + IntPtr.Size;
            var rawPtr = Marshal.AllocHGlobal(size + extra);
            long raw = (long)rawPtr;
            long aligned = (raw + extra) & ~((long)alignment - 1);
            var alignedPtr = new IntPtr(aligned);

            Marshal.WriteIntPtr(alignedPtr - IntPtr.Size, rawPtr);

            return alignedPtr;
        }

        public static void Deallocate(IntPtr alignedPtr)
        {
            if (alignedPtr == IntPtr.Zero) return;

            var rawPtr = Marshal.ReadIntPtr(alignedPtr - IntPtr.Size);
            Marshal.FreeHGlobal(rawPtr);
        }
    }
}
