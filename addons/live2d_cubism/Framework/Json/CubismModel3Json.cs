/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Live2D.Cubism.Core;

namespace Live2D.Cubism.Framework.Json
{
    /// <summary>
    /// Parses and exposes model3.json data. Mirrors Unity's CubismModel3Json.
    /// </summary>
    public sealed class CubismModel3Json
    {
        #region JSON Structures

        public class FileReferencesData
        {
            [JsonPropertyName("Moc")] public string Moc { get; set; }
            [JsonPropertyName("Textures")] public string[] Textures { get; set; }
            [JsonPropertyName("Physics")] public string Physics { get; set; }
            [JsonPropertyName("Pose")] public string Pose { get; set; }
            [JsonPropertyName("DisplayInfo")] public string DisplayInfo { get; set; }
            [JsonPropertyName("UserData")] public string UserData { get; set; }
            [JsonPropertyName("Expressions")] public ExpressionReference[] Expressions { get; set; }
            [JsonPropertyName("Motions")] public Dictionary<string, MotionReference[]> Motions { get; set; }
        }

        public class ExpressionReference
        {
            [JsonPropertyName("Name")] public string Name { get; set; }
            [JsonPropertyName("File")] public string File { get; set; }
        }

        public class GroupData
        {
            [JsonPropertyName("Target")] public string Target { get; set; }
            [JsonPropertyName("Name")] public string Name { get; set; }
            [JsonPropertyName("Ids")] public string[] Ids { get; set; }
        }

        public class HitAreaData
        {
            [JsonPropertyName("Id")] public string Id { get; set; }
            [JsonPropertyName("Name")] public string Name { get; set; }
        }

        public class MotionReference
        {
            [JsonPropertyName("File")] public string File { get; set; }
            [JsonPropertyName("Sound")] public string Sound { get; set; }
            [JsonPropertyName("FadeInTime")] public float FadeInTime { get; set; } = -1f;
            [JsonPropertyName("FadeOutTime")] public float FadeOutTime { get; set; } = -1f;
        }

        #endregion

        [JsonPropertyName("Version")] public int Version { get; set; }
        [JsonPropertyName("FileReferences")] public FileReferencesData FileReferences { get; set; }
        [JsonPropertyName("Groups")] public GroupData[] Groups { get; set; }
        [JsonPropertyName("HitAreas")] public HitAreaData[] HitAreas { get; set; }

        /// <summary>
        /// Motion groups parsed from the JSON. Key = group name (e.g. "Idle", "TapBody").
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, MotionReference[]> Motions { get; set; }

        [JsonIgnore]
        public string AssetPath { get; private set; }
        private string _basePath;

        public CubismModel3Json() { }

        /// <summary>
        /// Loads a model3.json file from Godot virtual paths (<c>res://</c>, <c>user://</c>) or OS filesystem.
        /// </summary>
        public static CubismModel3Json LoadAtPath(string path)
        {
            var jsonText = CubismAssetIo.ReadAllText(path);
            if (jsonText == null)
            {
                GD.PrintErr($"[CubismModel3Json] File not found: {path}");
                return null;
            }

            return LoadFromJson(jsonText, path);
        }

        /// <summary>
        /// Loads from a JSON string with a base path for resolving relative references.
        /// </summary>
        public static CubismModel3Json LoadFromJson(string jsonText, string assetPath)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;

            var result = JsonSerializer.Deserialize(jsonText, CubismJsonContext.Default.CubismModel3Json);
            if (result == null) return null;

            result.AssetPath = assetPath;
            result._basePath = CubismAssetIo.GetDirectoryPath(assetPath);
            result.Motions = result.FileReferences?.Motions ?? new Dictionary<string, MotionReference[]>();

            return result;
        }

        /// <summary>
        /// Resolves a relative path from the model3.json to a Godot virtual or OS filesystem path.
        /// </summary>
        public string ResolvePath(string relativePath)
        {
            return CubismAssetIo.CombinePath(_basePath, relativePath);
        }

        /// <summary>
        /// Loads the .moc3 bytes referenced by this model3.json.
        /// </summary>
        public byte[] LoadMoc3()
        {
            var mocPath = ResolvePath(FileReferences?.Moc);
            var bytes = CubismAssetIo.ReadAllBytes(mocPath);
            if (bytes == null)
            {
                GD.PrintErr($"[CubismModel3Json] Moc file not found: {mocPath}");
                return null;
            }
            return bytes;
        }

        /// <summary>
        /// Loads textures referenced by this model3.json as Godot Texture2D (from PNG files).
        /// </summary>
        public Texture2D[] LoadTextures()
        {
            if (FileReferences?.Textures == null) return Array.Empty<Texture2D>();

            var textures = new Texture2D[FileReferences.Textures.Length];
            for (int i = 0; i < FileReferences.Textures.Length; i++)
            {
                var texPath = ResolvePath(FileReferences.Textures[i]);
                if (texPath != null && CubismAssetIo.FileExists(texPath))
                {
                    textures[i] = CubismAssetIo.LoadTexture2D(texPath);
                    if (textures[i] == null)
                        GD.PrintErr($"[CubismModel3Json] Failed to load texture: {texPath}");
                }
                else
                {
                    GD.PrintErr($"[CubismModel3Json] Texture not found: {texPath}");
                }
            }
            return textures;
        }

        /// <summary>
        /// Loads and parses the referenced physics3.json.
        /// </summary>
        public CubismPhysics3Json LoadPhysics3Json()
        {
            var path = ResolvePath(FileReferences?.Physics);
            if (path == null || !CubismAssetIo.FileExists(path)) return null;
            return CubismPhysics3Json.LoadAtPath(path);
        }

        /// <summary>
        /// Loads and parses the referenced pose3.json.
        /// </summary>
        public CubismPose3Json LoadPose3Json()
        {
            var path = ResolvePath(FileReferences?.Pose);
            if (path == null || !CubismAssetIo.FileExists(path)) return null;
            return CubismPose3Json.LoadAtPath(path);
        }

        /// <summary>
        /// Loads and parses the referenced userdata3.json.
        /// </summary>
        public CubismUserData3Json LoadUserData3Json()
        {
            var path = ResolvePath(FileReferences?.UserData);
            if (path == null || !CubismAssetIo.FileExists(path)) return null;
            return CubismUserData3Json.LoadAtPath(path);
        }

        /// <summary>
        /// Checks whether a parameter ID belongs to a named group.
        /// </summary>
        public bool IsParameterInGroup(string parameterId, string groupName)
        {
            if (Groups == null) return false;
            foreach (var group in Groups)
            {
                if (group.Name != groupName || group.Target != "Parameter") continue;
                if (group.Ids == null) continue;
                foreach (var id in group.Ids)
                {
                    if (id == parameterId) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the IDs for a named group.
        /// </summary>
        public string[] GetGroupIds(string groupName)
        {
            if (Groups == null) return Array.Empty<string>();
            foreach (var group in Groups)
            {
                if (group.Name == groupName)
                    return group.Ids ?? Array.Empty<string>();
            }
            return Array.Empty<string>();
        }

        /// <summary>
        /// Creates a CubismModel from this model3.json data.
        /// </summary>
        public CubismModel ToModel()
        {
            var mocBytes = LoadMoc3();
            if (mocBytes == null) return null;

            var moc = CubismMoc.CreateFrom(mocBytes);
            if (moc == null) return null;

            moc.Name = Path.GetFileNameWithoutExtension(FileReferences?.Moc ?? "Model");
            var model = CubismModel.InstantiateFrom(moc, ownsMoc: true);
            if (model == null)
            {
                moc.Dispose();
                return null;
            }

            model.Name = moc.Name;
            return model;
        }
    }
}
