/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using Live2D.Cubism.Core.Unmanaged;
using Godot;

namespace Live2D.Cubism.Core
{
    /// <summary>
    /// Managed wrapper around a Cubism moc (model source).
    /// Mirrors the Unity CubismMoc ScriptableObject.
    /// </summary>
    public sealed class CubismMoc : IDisposable
    {
        public string Name { get; set; }

        private CubismUnmanagedMoc _unmanagedMoc;
        private int _referenceCount;

        public bool IsValid => _unmanagedMoc != null && _unmanagedMoc.IsValid;

        public static CubismMoc CreateFrom(byte[] mocBytes)
        {
            if (mocBytes == null || mocBytes.Length == 0) return null;

            var moc = new CubismMoc();
            moc._unmanagedMoc = CubismUnmanagedMoc.CreateFrom(mocBytes);

            if (moc._unmanagedMoc == null)
            {
                GD.PrintErr("[CubismMoc] Failed to revive moc data.");
                return null;
            }

            return moc;
        }

        internal CubismUnmanagedModel AcquireUnmanagedModel()
        {
            if (!IsValid) return null;

            var model = CubismUnmanagedModel.CreateFrom(_unmanagedMoc);
            if (model != null)
            {
                _referenceCount++;
            }
            return model;
        }

        internal void ReleaseUnmanagedModel()
        {
            _referenceCount--;
        }

        public void Dispose()
        {
            _unmanagedMoc?.Dispose();
            _unmanagedMoc = null;
        }
    }
}
