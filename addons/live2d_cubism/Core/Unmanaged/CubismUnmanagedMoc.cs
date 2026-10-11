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
    public sealed class CubismUnmanagedMoc : IDisposable
    {
        public IntPtr Ptr { get; private set; }
        private IntPtr _allocatedMemory;
        private uint _mocSize;

        public bool IsValid => Ptr != IntPtr.Zero;

        public static CubismUnmanagedMoc CreateFrom(byte[] mocBytes)
        {
            if (mocBytes == null || mocBytes.Length == 0)
                return null;

            var moc = new CubismUnmanagedMoc();
            moc._mocSize = (uint)mocBytes.Length;
            moc._allocatedMemory = CubismUnmanagedMemory.Allocate(
                mocBytes.Length, CubismCoreDll.AlignofMoc);

            Marshal.Copy(mocBytes, 0, moc._allocatedMemory, mocBytes.Length);

            if (CubismCoreDll.HasMocConsistency(moc._allocatedMemory, moc._mocSize) == 0)
            {
                moc.Dispose();
                return null;
            }

            moc.Ptr = CubismCoreDll.ReviveMocInPlace(moc._allocatedMemory, moc._mocSize);

            if (moc.Ptr == IntPtr.Zero)
            {
                moc.Dispose();
                return null;
            }

            return moc;
        }

        public void Dispose()
        {
            if (_allocatedMemory != IntPtr.Zero)
            {
                CubismUnmanagedMemory.Deallocate(_allocatedMemory);
                _allocatedMemory = IntPtr.Zero;
            }
            Ptr = IntPtr.Zero;
        }
    }
}
