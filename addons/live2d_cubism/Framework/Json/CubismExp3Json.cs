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
    /// Parses exp3.json expression files. Mirrors Unity's CubismExp3Json.
    /// </summary>
    public sealed class CubismExp3Json
    {
        [JsonIgnore] public string Name { get; set; }
        [JsonPropertyName("Type")] public string Type { get; set; }
        [JsonPropertyName("FadeInTime")] public float FadeInTime { get; set; } = 1.0f;
        [JsonPropertyName("FadeOutTime")] public float FadeOutTime { get; set; } = 1.0f;

        [JsonPropertyName("Parameters")] public SerializableExpressionParameter[] Parameters { get; set; }

        public class SerializableExpressionParameter
        {
            [JsonPropertyName("Id")] public string Id { get; set; }
            [JsonPropertyName("Value")] public float Value { get; set; }
            [JsonPropertyName("Blend")] public string Blend { get; set; }
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
            return JsonSerializer.Deserialize(jsonText, CubismJsonContext.Default.CubismExp3Json);
        }

        public static CubismExp3Json LoadAtPath(string path)
        {
            var text = CubismAssetIo.ReadAllText(path);
            return text != null ? LoadFrom(text) : null;
        }
    }
}
