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
    internal static unsafe class CubismUnmanagedMemory
    {
        public static IntPtr Allocate(int size, int alignment)
        {
            return (IntPtr)NativeMemory.AlignedAlloc((nuint)size, (nuint)alignment);
        }

        public static void Deallocate(IntPtr alignedPtr)
        {
            NativeMemory.AlignedFree((void*)alignedPtr);
        }
    }
}
