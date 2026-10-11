/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System.IO;
using Newtonsoft.Json;

namespace Live2D.Cubism.Framework.Json
{
    /// <summary>
    /// Parses pose3.json files. Mirrors Unity's CubismPose3Json.
    /// </summary>
    public sealed class CubismPose3Json
    {
        [JsonProperty("Type")] public string Type { get; set; }
        [JsonProperty("FadeInTime")] public float FadeInTime { get; set; } = 0.5f;

        [JsonProperty("Groups")] public PoseGroup[][] Groups { get; set; }

        public class PoseGroup
        {
            [JsonProperty("Id")] public string Id { get; set; }
            [JsonProperty("Link")] public string[] Link { get; set; }
        }

        public static CubismPose3Json LoadFrom(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;
            return JsonConvert.DeserializeObject<CubismPose3Json>(jsonText);
        }

        public static CubismPose3Json LoadAtPath(string path)
        {
            if (!File.Exists(path)) return null;
            return LoadFrom(File.ReadAllText(path));
        }
    }
}
