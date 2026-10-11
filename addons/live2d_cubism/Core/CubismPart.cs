/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core.Unmanaged;

namespace Live2D.Cubism.Core
{
    /// <summary>
    /// Represents a single Cubism model part (visibility group).
    /// Mirrors Unity's CubismPart MonoBehaviour.
    /// </summary>
    public sealed class CubismPart
    {
        public string Id { get; internal set; }
        public int UnmanagedIndex { get; internal set; } = -1;
        public float Opacity = 1f;
        public int ParentPartIndex { get; internal set; } = -1;

        internal void Initialize(CubismUnmanagedParts unmanaged, int index)
        {
            UnmanagedIndex = index;
            Id = unmanaged.Ids[index];
            Opacity = unmanaged.GetOpacity(index);
            ParentPartIndex = unmanaged.GetParentPartIndex(index);
        }
    }
}
