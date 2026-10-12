/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Live2D.Cubism.Core.Unmanaged
{
    public static class CubismCoreDll
    {
        public const string DllName = "Live2DCubismCore";

        static CubismCoreDll()
        {
            NativeLibrary.SetDllImportResolver(typeof(CubismCoreDll).Assembly, ResolveLibrary);
        }

        private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != DllName)
                return IntPtr.Zero;

            string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Live2DCubismCore.dll"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "libLive2DCubismCore.dylib"
                    : "libLive2DCubismCore.so";

            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, fileName),
                Path.Combine(Environment.CurrentDirectory, "native", fileName),
                Path.Combine(AppContext.BaseDirectory, "native", fileName),
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out IntPtr handle))
                    return handle;
            }

            return IntPtr.Zero;
        }

        public const int AlignofMoc = 64;
        public const int AlignofModel = 16;

        public const int MocVersion_Unknown = 0;
        public const int MocVersion_30 = 1;
        public const int MocVersion_33 = 2;
        public const int MocVersion_40 = 3;
        public const int MocVersion_42 = 4;
        public const int MocVersion_50 = 5;
        public const int MocVersion_53 = 6;

        public const int ParameterType_Normal = 0;
        public const int ParameterType_BlendShape = 1;

        public const int ColorBlendType_Normal = 0;
        public const int ColorBlendType_Add = 3;
        public const int ColorBlendType_AddGlow = 4;
        public const int ColorBlendType_Darken = 5;
        public const int ColorBlendType_Multiply = 6;
        public const int ColorBlendType_ColorBurn = 7;
        public const int ColorBlendType_LinearBurn = 8;
        public const int ColorBlendType_Lighten = 9;
        public const int ColorBlendType_Screen = 10;
        public const int ColorBlendType_ColorDodge = 11;
        public const int ColorBlendType_Overlay = 12;
        public const int ColorBlendType_SoftLight = 13;
        public const int ColorBlendType_HardLight = 14;
        public const int ColorBlendType_LinearLight = 15;
        public const int ColorBlendType_Hue = 16;
        public const int ColorBlendType_Color = 17;
        public const int ColorBlendType_AddCompatible = 1;
        public const int ColorBlendType_MultiplyCompatible = 2;

        public const int AlphaBlendType_Over = 0;
        public const int AlphaBlendType_Atop = 1;
        public const int AlphaBlendType_Out = 2;
        public const int AlphaBlendType_ConjointOver = 3;
        public const int AlphaBlendType_DisjointOver = 4;

        public const byte BlendAdditive = 1 << 0;
        public const byte BlendMultiplicative = 1 << 1;
        public const byte IsDoubleSided = 1 << 2;
        public const byte IsInvertedMask = 1 << 3;

        public const byte IsVisible = 1 << 0;
        public const byte VisibilityDidChange = 1 << 1;
        public const byte OpacityDidChange = 1 << 2;
        public const byte DrawOrderDidChange = 1 << 3;
        public const byte RenderOrderDidChange = 1 << 4;
        public const byte VertexPositionsDidChange = 1 << 5;
        public const byte BlendColorDidChange = 1 << 6;

        [DllImport(DllName, EntryPoint = "csmGetVersion")]
        public static extern uint GetVersion();

        [DllImport(DllName, EntryPoint = "csmGetSizeofModel")]
        public static extern uint GetSizeofModel(IntPtr moc);

        [DllImport(DllName, EntryPoint = "csmReviveMocInPlace")]
        public static extern IntPtr ReviveMocInPlace(IntPtr memory, uint mocSize);

        [DllImport(DllName, EntryPoint = "csmInitializeModelInPlace")]
        public static extern IntPtr InitializeModelInPlace(IntPtr moc, IntPtr memory, uint modelSize);

        [DllImport(DllName, EntryPoint = "csmHasMocConsistency")]
        public static extern int HasMocConsistency(IntPtr memory, uint mocSize);

        [DllImport(DllName, EntryPoint = "csmUpdateModel")]
        public static extern void UpdateModel(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmReadCanvasInfo")]
        public static extern void ReadCanvasInfo(IntPtr model, IntPtr outSizeInPixels, IntPtr outOriginInPixels, IntPtr outPixelsPerUnit);

        [DllImport(DllName, EntryPoint = "csmGetRenderOrders")]
        public static extern unsafe int* GetRenderOrders(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetParameterCount")]
        public static extern int GetParameterCount(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetParameterIds")]
        public static extern unsafe IntPtr* GetParameterIds(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetParameterMinimumValues")]
        public static extern unsafe float* GetParameterMinimumValues(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetParameterTypes")]
        public static extern unsafe int* GetParameterTypes(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetParameterMaximumValues")]
        public static extern unsafe float* GetParameterMaximumValues(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetParameterDefaultValues")]
        public static extern unsafe float* GetParameterDefaultValues(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetParameterValues")]
        public static extern unsafe float* GetParameterValues(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetParameterRepeats")]
        public static extern unsafe int* GetParameterRepeats(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetPartCount")]
        public static extern int GetPartCount(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetPartIds")]
        public static extern unsafe IntPtr* GetPartIds(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetPartOpacities")]
        public static extern unsafe float* GetPartOpacities(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetPartParentPartIndices")]
        public static extern unsafe int* GetPartParentPartIndices(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableCount")]
        public static extern int GetDrawableCount(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableIds")]
        public static extern unsafe IntPtr* GetDrawableIds(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableConstantFlags")]
        public static extern unsafe byte* GetDrawableConstantFlags(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableDynamicFlags")]
        public static extern unsafe byte* GetDrawableDynamicFlags(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableTextureIndices")]
        public static extern unsafe int* GetDrawableTextureIndices(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableDrawOrders")]
        public static extern unsafe int* GetDrawableDrawOrders(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableOpacities")]
        public static extern unsafe float* GetDrawableOpacities(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableMaskCounts")]
        public static extern unsafe int* GetDrawableMaskCounts(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableMasks")]
        public static extern unsafe int** GetDrawableMasks(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableVertexCounts")]
        public static extern unsafe int* GetDrawableVertexCounts(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableVertexPositions")]
        public static extern unsafe float** GetDrawableVertexPositions(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableVertexUvs")]
        public static extern unsafe float** GetDrawableVertexUvs(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableIndexCounts")]
        public static extern unsafe int* GetDrawableIndexCounts(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableIndices")]
        public static extern unsafe ushort** GetDrawableIndices(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmResetDrawableDynamicFlags")]
        public static extern void ResetDrawableDynamicFlags(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableMultiplyColors")]
        public static extern unsafe float* GetDrawableMultiplyColors(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableScreenColors")]
        public static extern unsafe float* GetDrawableScreenColors(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableParentPartIndices")]
        public static extern unsafe int* GetDrawableParentPartIndices(IntPtr model);

        [DllImport(DllName, EntryPoint = "csmGetDrawableBlendModes")]
        public static extern unsafe int* GetDrawableBlendModes(IntPtr model);

    }
}
