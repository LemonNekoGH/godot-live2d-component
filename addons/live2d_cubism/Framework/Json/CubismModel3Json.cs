/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.IO;
using Godot;
using Live2D.Cubism.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            [JsonProperty("Moc")] public string Moc { get; set; }
            [JsonProperty("Textures")] public string[] Textures { get; set; }
            [JsonProperty("Physics")] public string Physics { get; set; }
            [JsonProperty("Pose")] public string Pose { get; set; }
            [JsonProperty("DisplayInfo")] public string DisplayInfo { get; set; }
            [JsonProperty("UserData")] public string UserData { get; set; }
            [JsonProperty("Expressions")] public ExpressionReference[] Expressions { get; set; }
        }

        public class ExpressionReference
        {
            [JsonProperty("Name")] public string Name { get; set; }
            [JsonProperty("File")] public string File { get; set; }
        }

        public class GroupData
        {
            [JsonProperty("Target")] public string Target { get; set; }
            [JsonProperty("Name")] public string Name { get; set; }
            [JsonProperty("Ids")] public string[] Ids { get; set; }
        }

        public class HitAreaData
        {
            [JsonProperty("Id")] public string Id { get; set; }
            [JsonProperty("Name")] public string Name { get; set; }
        }

        public class MotionReference
        {
            [JsonProperty("File")] public string File { get; set; }
            [JsonProperty("Sound")] public string Sound { get; set; }
            [JsonProperty("FadeInTime")] public float FadeInTime { get; set; } = -1f;
            [JsonProperty("FadeOutTime")] public float FadeOutTime { get; set; } = -1f;
        }

        #endregion

        public int Version { get; set; }
        public FileReferencesData FileReferences { get; set; }
        public GroupData[] Groups { get; set; }
        public HitAreaData[] HitAreas { get; set; }

        /// <summary>
        /// Motion groups parsed from the JSON. Key = group name (e.g. "Idle", "TapBody").
        /// </summary>
        public System.Collections.Generic.Dictionary<string, MotionReference[]> Motions { get; set; }

        public string AssetPath { get; private set; }
        private string _basePath;

        private CubismModel3Json() { }

        /// <summary>
        /// Loads a model3.json file from the filesystem.
        /// </summary>
        public static CubismModel3Json LoadAtPath(string path)
        {
            if (!File.Exists(path))
            {
                GD.PrintErr($"[CubismModel3Json] File not found: {path}");
                return null;
            }

            var jsonText = File.ReadAllText(path);
            return LoadFromJson(jsonText, path);
        }

        /// <summary>
        /// Loads from a JSON string with a base path for resolving relative references.
        /// </summary>
        public static CubismModel3Json LoadFromJson(string jsonText, string assetPath)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;

            var result = new CubismModel3Json();
            result.AssetPath = assetPath;
            result._basePath = Path.GetDirectoryName(assetPath);

            var root = JObject.Parse(jsonText);

            result.Version = root["Version"]?.Value<int>() ?? 0;

            // FileReferences
            result.FileReferences = root["FileReferences"]?.ToObject<FileReferencesData>();

            // Groups
            result.Groups = root["Groups"]?.ToObject<GroupData[]>();

            // HitAreas
            result.HitAreas = root["HitAreas"]?.ToObject<HitAreaData[]>();

            // Motions (special nested structure)
            result.Motions = new System.Collections.Generic.Dictionary<string, MotionReference[]>();
            var motionsToken = root["FileReferences"]?["Motions"];
            if (motionsToken is JObject motionsObj)
            {
                foreach (var prop in motionsObj.Properties())
                {
                    var motionArray = prop.Value?.ToObject<MotionReference[]>();
                    if (motionArray != null)
                        result.Motions[prop.Name] = motionArray;
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a relative path from the model3.json to an absolute filesystem path.
        /// </summary>
        public string ResolvePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            return Path.Combine(_basePath, relativePath);
        }

        /// <summary>
        /// Loads the .moc3 bytes referenced by this model3.json.
        /// </summary>
        public byte[] LoadMoc3()
        {
            var mocPath = ResolvePath(FileReferences?.Moc);
            if (mocPath == null || !File.Exists(mocPath))
            {
                GD.PrintErr($"[CubismModel3Json] Moc file not found: {mocPath}");
                return null;
            }
            return File.ReadAllBytes(mocPath);
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
                if (texPath != null && File.Exists(texPath))
                {
                    var image = new Image();
                    var err = image.Load(texPath);
                    if (err == Error.Ok)
                    {
                        textures[i] = ImageTexture.CreateFromImage(image);
                    }
                    else
                    {
                        GD.PrintErr($"[CubismModel3Json] Failed to load texture: {texPath} (error: {err})");
                    }
                }
                else
                {
                    GD.PrintErr($"[CubismModel3Json] Texture not found: {texPath}");
                }
            }
            return textures;
        }

        /// <summary>
        /// Loads the physics3.json as a string.
        /// </summary>
        public string LoadPhysics3Json()
        {
            var path = ResolvePath(FileReferences?.Physics);
            if (path == null || !File.Exists(path)) return null;
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Loads the pose3.json as a string.
        /// </summary>
        public string LoadPose3Json()
        {
            var path = ResolvePath(FileReferences?.Pose);
            if (path == null || !File.Exists(path)) return null;
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Loads the userdata3.json as a string.
        /// </summary>
        public string LoadUserData3Json()
        {
            var path = ResolvePath(FileReferences?.UserData);
            if (path == null || !File.Exists(path)) return null;
            return File.ReadAllText(path);
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
            var model = CubismModel.InstantiateFrom(moc);
            if (model != null)
            {
                model.Name = moc.Name;
            }
            return model;
        }
    }
}
