/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Live2D.Cubism.Framework.Json
{
    /// <summary>
    /// Parses pose3.json files. Mirrors Unity's CubismPose3Json.
    /// </summary>
    public sealed class CubismPose3Json
    {
        [JsonPropertyName("Type")] public string Type { get; set; }
        [JsonPropertyName("FadeInTime")] public float FadeInTime { get; set; } = 0.5f;

        [JsonPropertyName("Groups")] public PoseGroup[][] Groups { get; set; }

        public class PoseGroup
        {
            [JsonPropertyName("Id")] public string Id { get; set; }
            [JsonPropertyName("Link")] public string[] Link { get; set; }
        }

        public static CubismPose3Json LoadFrom(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;
            return JsonSerializer.Deserialize(jsonText, CubismJsonContext.Default.CubismPose3Json);
        }

        public static CubismPose3Json LoadAtPath(string path)
        {
            var text = CubismAssetIo.ReadAllText(path);
            return text != null ? LoadFrom(text) : null;
        }
    }
}
