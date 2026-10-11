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
    /// Parses userdata3.json files. Mirrors Unity's CubismUserData3Json.
    /// </summary>
    public sealed class CubismUserData3Json
    {
        [JsonProperty("Version")] public int Version { get; set; }
        [JsonProperty("Meta")] public UserDataMeta Meta { get; set; }
        [JsonProperty("UserData")] public UserDataEntry[] UserData { get; set; }

        public class UserDataMeta
        {
            [JsonProperty("UserDataCount")] public int UserDataCount { get; set; }
            [JsonProperty("TotalUserDataSize")] public int TotalUserDataSize { get; set; }
        }

        public class UserDataEntry
        {
            [JsonProperty("Target")] public string Target { get; set; }
            [JsonProperty("Id")] public string Id { get; set; }
            [JsonProperty("Value")] public string Value { get; set; }
        }

        public static CubismUserData3Json LoadFrom(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;
            return JsonConvert.DeserializeObject<CubismUserData3Json>(jsonText);
        }

        public static CubismUserData3Json LoadAtPath(string path)
        {
            if (!File.Exists(path)) return null;
            return LoadFrom(File.ReadAllText(path));
        }
    }
}
