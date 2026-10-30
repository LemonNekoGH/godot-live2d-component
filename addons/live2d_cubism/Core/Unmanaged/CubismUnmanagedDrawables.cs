/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.Runtime.InteropServices;
using Godot;

namespace Live2D.Cubism.Core.Unmanaged
{
    public sealed class CubismUnmanagedDrawables
    {
        private readonly IntPtr _modelPtr;

        public int Count { get; }

        private string[] _ids;
        public string[] Ids => _ids ??= ReadIds();

        public CubismUnmanagedDrawables(IntPtr modelPtr)
        {
            _modelPtr = modelPtr;
            Count = CubismCoreDll.GetDrawableCount(modelPtr);
        }

        private unsafe string[] ReadIds()
        {
            var ids = new string[Count];
            var ptrs = CubismCoreDll.GetDrawableIds(_modelPtr);
            for (int i = 0; i < Count; i++)
            {
                ids[i] = Marshal.PtrToStringAnsi(ptrs[i]);
            }
            return ids;
        }

        public unsafe byte GetConstantFlag(int index) => CubismCoreDll.GetDrawableConstantFlags(_modelPtr)[index];
        public unsafe byte GetDynamicFlag(int index) => CubismCoreDll.GetDrawableDynamicFlags(_modelPtr)[index];
        public unsafe int GetTextureIndex(int index) => CubismCoreDll.GetDrawableTextureIndices(_modelPtr)[index];
        public unsafe int GetDrawOrder(int index) => CubismCoreDll.GetDrawableDrawOrders(_modelPtr)[index];
        public unsafe int GetRenderOrder(int index) => CubismCoreDll.GetRenderOrders(_modelPtr)[index];
        public unsafe float GetOpacity(int index) => CubismCoreDll.GetDrawableOpacities(_modelPtr)[index];
        private unsafe int GetMaskCount(int index) => CubismCoreDll.GetDrawableMaskCounts(_modelPtr)[index];

        public unsafe int[] GetMasks(int index)
        {
            var count = GetMaskCount(index);
            if (count <= 0) return Array.Empty<int>();

            var masks = CubismCoreDll.GetDrawableMasks(_modelPtr);
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = masks[index][i];
            }
            return result;
        }

        public unsafe int GetVertexCount(int index) => CubismCoreDll.GetDrawableVertexCounts(_modelPtr)[index];

        public unsafe int FillVertexPositions(int index, ref Vector2[] destination, float scale = 1f)
        {
            var count = GetVertexCount(index);
            if (count <= 0)
            {
                destination = Array.Empty<Vector2>();
                return 0;
            }

            if (destination == null || destination.Length != count)
                destination = new Vector2[count];

            var positions = CubismCoreDll.GetDrawableVertexPositions(_modelPtr)[index];
            for (int i = 0; i < count; i++)
            {
                // Unity: Y-up. Godot 2D: Y-down.
                // Cubism Core outputs in local model space (Y-up like Unity).
                // We negate Y to convert to Godot's 2D coordinate system.
                destination[i] = new Vector2(positions[i * 2] * scale, -positions[i * 2 + 1] * scale);
            }
            return count;
        }

        public unsafe Vector2[] GetVertexPositions(int index)
        {
            Vector2[] result = null;
            FillVertexPositions(index, ref result, 1f);
            return result;
        }

        public unsafe Vector2[] GetVertexUvs(int index)
        {
            var count = GetVertexCount(index);
            if (count <= 0) return Array.Empty<Vector2>();

            var uvs = CubismCoreDll.GetDrawableVertexUvs(_modelPtr);
            var result = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                // Cubism Core UVs use Unity / D3D convention: V=0 is the bottom of the image.
                // Native OpenGL flips V in VertShaderSrc.vert (`v_texCoord.y = 1.0 - v_texCoord.y`)
                // because glTexImage2D places the first PNG row at V=0 (top). Godot 2D is the same
                // as that OpenGL upload: (0,0) is the top-left of the image. Flip V to match Native.
                result[i] = new Vector2(uvs[index][i * 2], 1f - uvs[index][i * 2 + 1]);
            }
            return result;
        }

        private unsafe int GetIndexCount(int index) => CubismCoreDll.GetDrawableIndexCounts(_modelPtr)[index];

        public unsafe int[] GetIndices(int index)
        {
            var indexCount = GetIndexCount(index);
            if (indexCount <= 0) return Array.Empty<int>();

            var indices = CubismCoreDll.GetDrawableIndices(_modelPtr);
            var result = new int[indexCount];
            // Vertex Y is negated for Godot 2D, which reverses triangle winding.
            // Swap the last two indices of each triangle so front faces stay front-facing
            // (needed if a non-double-sided drawable enables back-face culling).
            for (int i = 0; i + 2 < indexCount; i += 3)
            {
                result[i] = indices[index][i];
                result[i + 1] = indices[index][i + 2];
                result[i + 2] = indices[index][i + 1];
            }
            return result;
        }

        public unsafe Godot.Color GetMultiplyColor(int index)
        {
            var colors = CubismCoreDll.GetDrawableMultiplyColors(_modelPtr);
            return new Godot.Color(
                colors[index * 4 + 0],
                colors[index * 4 + 1],
                colors[index * 4 + 2],
                colors[index * 4 + 3]);
        }

        public unsafe Godot.Color GetScreenColor(int index)
        {
            var colors = CubismCoreDll.GetDrawableScreenColors(_modelPtr);
            return new Godot.Color(
                colors[index * 4 + 0],
                colors[index * 4 + 1],
                colors[index * 4 + 2],
                colors[index * 4 + 3]);
        }

        public unsafe int GetParentPartIndex(int index) => CubismCoreDll.GetDrawableParentPartIndices(_modelPtr)[index];
        public unsafe int GetBlendMode(int index) => CubismCoreDll.GetDrawableBlendModes(_modelPtr)[index];
        public bool IsVisible(int index) => (GetDynamicFlag(index) & CubismCoreDll.IsVisible) != 0;
        public bool IsAdditive(int index) => (GetConstantFlag(index) & CubismCoreDll.BlendAdditive) != 0;
        public bool IsMultiplicative(int index) => (GetConstantFlag(index) & CubismCoreDll.BlendMultiplicative) != 0;
        public bool IsDoubleSided(int index) => (GetConstantFlag(index) & CubismCoreDll.IsDoubleSided) != 0;
        public bool IsInvertedMask(int index) => (GetConstantFlag(index) & CubismCoreDll.IsInvertedMask) != 0;
    }
}
