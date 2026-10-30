/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.IO;
using System.Text.Json.Serialization;
using Godot;

namespace Live2D.Cubism.Framework.Json
{
    /// <summary>
    /// Source-generated System.Text.Json context for all Cubism JSON formats.
    /// Zero external dependencies and AOT-compatible.
    /// </summary>
    [JsonSourceGenerationOptions(
        ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        AllowTrailingCommas = true)]
    [JsonSerializable(typeof(CubismModel3Json))]
    [JsonSerializable(typeof(CubismMotion3Json))]
    [JsonSerializable(typeof(CubismExp3Json))]
    [JsonSerializable(typeof(CubismPhysics3Json))]
    [JsonSerializable(typeof(CubismPose3Json))]
    [JsonSerializable(typeof(CubismUserData3Json))]
    public partial class CubismJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// Unified file and texture I/O helper supporting both Godot virtual paths
    /// (<c>res://</c>, <c>user://</c>, packed <c>.pck</c> archives) and OS filesystem paths.
    /// </summary>
    public static class CubismAssetIo
    {
        public static bool IsGodotVirtualPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.StartsWith("res://", StringComparison.Ordinal) ||
                   path.StartsWith("user://", StringComparison.Ordinal);
        }

        public static bool FileExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                if (Godot.FileAccess.FileExists(path))
                    return true;
            }
            catch
            {
                // Ignore if Godot runtime is not active
            }

            return !IsGodotVirtualPath(path) && File.Exists(path);
        }

        public static string ReadAllText(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                if (Godot.FileAccess.FileExists(path))
                {
                    using var fa = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
                    if (fa != null)
                        return fa.GetAsText();
                }
            }
            catch
            {
                // Fallback to System.IO below
            }

            if (!IsGodotVirtualPath(path) && File.Exists(path))
                return File.ReadAllText(path);

            return null;
        }

        public static byte[] ReadAllBytes(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                if (Godot.FileAccess.FileExists(path))
                {
                    var bytes = Godot.FileAccess.GetFileAsBytes(path);
                    if (Godot.FileAccess.GetOpenError() == Error.Ok)
                        return bytes;
                }
            }
            catch
            {
                // Fallback to System.IO below
            }

            if (!IsGodotVirtualPath(path) && File.Exists(path))
                return File.ReadAllBytes(path);

            return null;
        }

        public static string GetDirectoryPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return string.Empty;

            if (IsGodotVirtualPath(assetPath))
            {
                var normalized = assetPath.Replace('\\', '/');
                var schemeEnd = normalized.IndexOf("://", StringComparison.Ordinal) + 3;
                var lastSlash = normalized.LastIndexOf('/');
                if (lastSlash < schemeEnd)
                    return normalized.Substring(0, schemeEnd);
                return normalized.Substring(0, lastSlash);
            }

            return Path.GetDirectoryName(assetPath) ?? string.Empty;
        }

        public static string CombinePath(string basePath, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (IsGodotVirtualPath(relativePath)) return relativePath.Replace('\\', '/');
            if (string.IsNullOrEmpty(basePath)) return relativePath;

            if (IsGodotVirtualPath(basePath))
            {
                var left = basePath.Replace('\\', '/').TrimEnd('/');
                var right = relativePath.Replace('\\', '/').TrimStart('/');
                return $"{left}/{right}";
            }

            return Path.Combine(basePath, relativePath);
        }

        public static Texture2D LoadTexture2D(string texPath)
        {
            if (string.IsNullOrEmpty(texPath)) return null;

            var bytes = ReadAllBytes(texPath);
            if (bytes != null && bytes.Length > 0)
            {
                var image = new Image();
                var ext = Path.GetExtension(texPath).ToLowerInvariant();
                var err = ext switch
                {
                    ".webp" => image.LoadWebpFromBuffer(bytes),
                    ".jpg" or ".jpeg" => image.LoadJpgFromBuffer(bytes),
                    _ => image.LoadPngFromBuffer(bytes),
                };

                if (err == Error.Ok)
                {
                    // Native LAppTextureManager uploads RGBA and calls glGenerateMipmap.
                    // Keep straight (non-premultiplied) alpha; the shader premultiplies
                    // like CubismCG.cginc / FragShaderSrc.frag.
                    if (image.DetectAlpha() == Image.AlphaMode.None)
                        image.Convert(Image.Format.Rgba8);
                    image.GenerateMipmaps();
                    return ImageTexture.CreateFromImage(image);
                }

                GD.PrintErr($"[CubismAssetIo] Failed to decode image buffer: {texPath} (error: {err})");
            }

            if (IsGodotVirtualPath(texPath) && ResourceLoader.Exists(texPath))
            {
                return ResourceLoader.Load<Texture2D>(texPath);
            }

            return null;
        }
    }
}
