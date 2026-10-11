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
    public sealed class CubismUnmanagedParameters
    {
        private readonly IntPtr _modelPtr;

        public int Count { get; }

        private string[] _ids;
        public string[] Ids => _ids ??= ReadIds();

        public CubismUnmanagedParameters(IntPtr modelPtr)
        {
            _modelPtr = modelPtr;
            Count = CubismCoreDll.GetParameterCount(modelPtr);
        }

        private unsafe string[] ReadIds()
        {
            var ids = new string[Count];
            var ptrs = CubismCoreDll.GetParameterIds(_modelPtr);
            for (int i = 0; i < Count; i++)
            {
                ids[i] = Marshal.PtrToStringAnsi(ptrs[i]);
            }
            return ids;
        }

        public unsafe float GetMinimumValue(int index)
        {
            return CubismCoreDll.GetParameterMinimumValues(_modelPtr)[index];
        }

        public unsafe float GetMaximumValue(int index)
        {
            return CubismCoreDll.GetParameterMaximumValues(_modelPtr)[index];
        }

        public unsafe float GetDefaultValue(int index)
        {
            return CubismCoreDll.GetParameterDefaultValues(_modelPtr)[index];
        }

        public unsafe float GetValue(int index)
        {
            return CubismCoreDll.GetParameterValues(_modelPtr)[index];
        }

        public unsafe void SetValue(int index, float value)
        {
            CubismCoreDll.GetParameterValues(_modelPtr)[index] = value;
        }

        public unsafe int GetType(int index)
        {
            return CubismCoreDll.GetParameterTypes(_modelPtr)[index];
        }

        public unsafe int GetRepeat(int index)
        {
            return CubismCoreDll.GetParameterRepeats(_modelPtr)[index];
        }

        public unsafe int GetKeyCount(int index)
        {
            return CubismCoreDll.GetParameterKeyCounts(_modelPtr)[index];
        }
    }
}
