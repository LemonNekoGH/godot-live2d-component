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
    public sealed class CubismUnmanagedParts
    {
        private readonly IntPtr _modelPtr;

        public int Count { get; }

        private string[] _ids;
        public string[] Ids => _ids ??= ReadIds();

        public CubismUnmanagedParts(IntPtr modelPtr)
        {
            _modelPtr = modelPtr;
            Count = CubismCoreDll.GetPartCount(modelPtr);
        }

        private unsafe string[] ReadIds()
        {
            var ids = new string[Count];
            var ptrs = CubismCoreDll.GetPartIds(_modelPtr);
            for (int i = 0; i < Count; i++)
            {
                ids[i] = Marshal.PtrToStringAnsi(ptrs[i]);
            }
            return ids;
        }

        public unsafe float GetOpacity(int index)
        {
            return CubismCoreDll.GetPartOpacities(_modelPtr)[index];
        }

        public unsafe void SetOpacity(int index, float value)
        {
            CubismCoreDll.GetPartOpacities(_modelPtr)[index] = value;
        }

        public unsafe int GetParentPartIndex(int index)
        {
            return CubismCoreDll.GetPartParentPartIndices(_modelPtr)[index];
        }
    }
}
