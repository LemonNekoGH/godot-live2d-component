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
    public sealed class CubismUnmanagedModel : IDisposable
    {
        public IntPtr Ptr { get; private set; }
        private IntPtr _allocatedMemory;

        public CubismUnmanagedParameters Parameters { get; private set; }
        public CubismUnmanagedParts Parts { get; private set; }
        public CubismUnmanagedDrawables Drawables { get; private set; }
        public CubismUnmanagedCanvasInformation CanvasInformation { get; private set; }

        public bool IsValid => Ptr != IntPtr.Zero;

        public static CubismUnmanagedModel CreateFrom(CubismUnmanagedMoc moc)
        {
            if (moc == null || !moc.IsValid) return null;

            var modelSize = CubismCoreDll.GetSizeofModel(moc.Ptr);
            if (modelSize == 0) return null;

            var model = new CubismUnmanagedModel();
            model._allocatedMemory = CubismUnmanagedMemory.Allocate(
                (int)modelSize, CubismCoreDll.AlignofModel);

            model.Ptr = CubismCoreDll.InitializeModelInPlace(
                moc.Ptr, model._allocatedMemory, modelSize);

            if (model.Ptr == IntPtr.Zero)
            {
                model.Dispose();
                return null;
            }

            model.Parameters = new CubismUnmanagedParameters(model.Ptr);
            model.Parts = new CubismUnmanagedParts(model.Ptr);
            model.Drawables = new CubismUnmanagedDrawables(model.Ptr);
            model.CanvasInformation = new CubismUnmanagedCanvasInformation(model.Ptr);

            return model;
        }

        public void Update() => CubismCoreDll.UpdateModel(Ptr);
        public void ResetDynamicFlags() => CubismCoreDll.ResetDrawableDynamicFlags(Ptr);

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
