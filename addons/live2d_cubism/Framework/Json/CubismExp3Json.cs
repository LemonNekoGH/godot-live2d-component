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
    /// Parses exp3.json expression files. Mirrors Unity's CubismExp3Json.
    /// </summary>
    public sealed class CubismExp3Json
    {
        [JsonProperty("Type")] public string Type { get; set; }
        [JsonProperty("FadeInTime")] public float FadeInTime { get; set; } = 1.0f;
        [JsonProperty("FadeOutTime")] public float FadeOutTime { get; set; } = 1.0f;

        [JsonProperty("Parameters")] public SerializableExpressionParameter[] Parameters { get; set; }

        public class SerializableExpressionParameter
        {
            [JsonProperty("Id")] public string Id { get; set; }
            [JsonProperty("Value")] public float Value { get; set; }
            [JsonProperty("Blend")] public string Blend { get; set; }
        }

        /// <summary>
        /// Blend modes matching Unity CubismParameterBlendMode.
        /// </summary>
        public const string BlendModeOverride = "Overwrite";
        public const string BlendModeAdditive = "Add";
        public const string BlendModeMultiply = "Multiply";

        public static CubismExp3Json LoadFrom(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;
            return JsonConvert.DeserializeObject<CubismExp3Json>(jsonText);
        }

        public static CubismExp3Json LoadAtPath(string path)
        {
            if (!File.Exists(path)) return null;
            return LoadFrom(File.ReadAllText(path));
        }
    }
}
