/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Live2D.Cubism.Core.Unmanaged;
using Godot;

namespace Live2D.Cubism.Core
{
    /// <summary>
    /// Represents a single Cubism drawable (mesh primitive).
    /// Mirrors Unity's CubismDrawable MonoBehaviour.
    /// </summary>
    public sealed class CubismDrawable
    {
        public string Id { get; internal set; }
        public int UnmanagedIndex { get; internal set; } = -1;
        public int TextureIndex { get; internal set; }
        public int DrawOrder { get; internal set; }
        public int RenderOrder { get; internal set; }
        public float Opacity { get; internal set; } = 1f;

        public bool IsVisible { get; internal set; } = true;
        public bool IsDoubleSided { get; internal set; }
        public bool IsInvertedMask { get; internal set; }
        public bool IsAdditive { get; internal set; }
        public bool IsMultiplicative { get; internal set; }

        public int[] MaskIndices { get; internal set; }
        public int ParentPartIndex { get; internal set; } = -1;
        public int BlendMode { get; internal set; }

        public Color MultiplyColor { get; internal set; } = Colors.White;
        public Color ScreenColor { get; internal set; } = new Color(0, 0, 0, 0);

        /// <summary>
        /// Color blend type from the Cubism Core for extended blend modes.
        /// Maps to CubismCoreDll.ColorBlendType_* constants.
        /// </summary>
        public int ColorBlend { get; internal set; }

        /// <summary>
        /// Alpha blend type from the Cubism Core.
        /// Maps to CubismCoreDll.AlphaBlendType_* constants.
        /// </summary>
        public int AlphaBlend { get; internal set; }

        public byte ConstantFlags { get; internal set; }
        public byte DynamicFlags { get; internal set; }

        internal void Initialize(CubismUnmanagedDrawables unmanaged, int index)
        {
            UnmanagedIndex = index;
            Id = unmanaged.Ids[index];
            TextureIndex = unmanaged.GetTextureIndex(index);
            ConstantFlags = unmanaged.GetConstantFlag(index);
            IsDoubleSided = unmanaged.IsDoubleSided(index);
            IsInvertedMask = unmanaged.IsInvertedMask(index);
            IsAdditive = unmanaged.IsAdditive(index);
            IsMultiplicative = unmanaged.IsMultiplicative(index);
            MaskIndices = unmanaged.GetMasks(index);
            ParentPartIndex = unmanaged.GetParentPartIndex(index);
            BlendMode = unmanaged.GetBlendMode(index);
            ColorBlend = BlendMode & 0xFF;
            AlphaBlend = (BlendMode >> 8) & 0xFF;
            if (ColorBlend == CubismCoreDll.ColorBlendType_Normal)
            {
                if (IsAdditive)
                    ColorBlend = CubismCoreDll.ColorBlendType_AddCompatible;
                else if (IsMultiplicative)
                    ColorBlend = CubismCoreDll.ColorBlendType_MultiplyCompatible;
            }

            UpdateDynamic(unmanaged);
        }

        internal void UpdateDynamic(CubismUnmanagedDrawables unmanaged)
        {
            DynamicFlags = unmanaged.GetDynamicFlag(UnmanagedIndex);
            DrawOrder = unmanaged.GetDrawOrder(UnmanagedIndex);
            RenderOrder = unmanaged.GetRenderOrder(UnmanagedIndex);
            Opacity = unmanaged.GetOpacity(UnmanagedIndex);
            IsVisible = unmanaged.IsVisible(UnmanagedIndex);
            MultiplyColor = unmanaged.GetMultiplyColor(UnmanagedIndex);
            ScreenColor = unmanaged.GetScreenColor(UnmanagedIndex);
        }
    }
}
