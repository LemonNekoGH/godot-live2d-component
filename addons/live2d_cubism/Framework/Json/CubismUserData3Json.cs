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
    /// Parses userdata3.json files. Mirrors Unity's CubismUserData3Json.
    /// </summary>
    public sealed class CubismUserData3Json
    {
        [JsonPropertyName("Version")] public int Version { get; set; }
        [JsonPropertyName("Meta")] public UserDataMeta Meta { get; set; }
        [JsonPropertyName("UserData")] public UserDataEntry[] UserData { get; set; }

        public class UserDataMeta
        {
            [JsonPropertyName("UserDataCount")] public int UserDataCount { get; set; }
            [JsonPropertyName("TotalUserDataSize")] public int TotalUserDataSize { get; set; }
        }

        public class UserDataEntry
        {
            [JsonPropertyName("Target")] public string Target { get; set; }
            [JsonPropertyName("Id")] public string Id { get; set; }
            [JsonPropertyName("Value")] public string Value { get; set; }
        }

        public static CubismUserData3Json LoadFrom(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;
            return JsonSerializer.Deserialize(jsonText, CubismJsonContext.Default.CubismUserData3Json);
        }

        public static CubismUserData3Json LoadAtPath(string path)
        {
            var text = CubismAssetIo.ReadAllText(path);
            return text != null ? LoadFrom(text) : null;
        }
    }
}
