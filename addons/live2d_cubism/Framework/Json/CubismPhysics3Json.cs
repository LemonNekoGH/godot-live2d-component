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
    /// Parses physics3.json files. Mirrors Unity's CubismPhysics3Json structure.
    /// </summary>
    public sealed class CubismPhysics3Json
    {
        [JsonProperty("Version")] public int Version { get; set; }
        [JsonProperty("Meta")] public PhysicsMeta Meta { get; set; }
        [JsonProperty("PhysicsSettings")] public PhysicsSettingData[] PhysicsSettings { get; set; }

        public class PhysicsMeta
        {
            [JsonProperty("PhysicsSettingCount")] public int PhysicsSettingCount { get; set; }
            [JsonProperty("TotalInputCount")] public int TotalInputCount { get; set; }
            [JsonProperty("TotalOutputCount")] public int TotalOutputCount { get; set; }
            [JsonProperty("TotalVertexCount")] public int TotalVertexCount { get; set; }
            [JsonProperty("EffectiveForces")] public EffectiveForce EffectiveForces { get; set; }
        }

        public class EffectiveForce
        {
            [JsonProperty("Gravity")] public Vector2Data Gravity { get; set; }
            [JsonProperty("Wind")] public Vector2Data Wind { get; set; }
        }

        public class Vector2Data
        {
            [JsonProperty("X")] public float X { get; set; }
            [JsonProperty("Y")] public float Y { get; set; }
        }

        public class PhysicsSettingData
        {
            [JsonProperty("Id")] public string Id { get; set; }
            [JsonProperty("Input")] public InputData[] Input { get; set; }
            [JsonProperty("Output")] public OutputData[] Output { get; set; }
            [JsonProperty("Vertices")] public VertexData[] Vertices { get; set; }
            [JsonProperty("Normalization")] public NormalizationData Normalization { get; set; }
        }

        public class InputData
        {
            [JsonProperty("Source")] public SourceData Source { get; set; }
            [JsonProperty("Weight")] public float Weight { get; set; }
            [JsonProperty("Type")] public string Type { get; set; }
            [JsonProperty("Reflect")] public bool Reflect { get; set; }
        }

        public class OutputData
        {
            [JsonProperty("Destination")] public SourceData Destination { get; set; }
            [JsonProperty("VertexIndex")] public int VertexIndex { get; set; }
            [JsonProperty("Scale")] public float Scale { get; set; }
            [JsonProperty("Weight")] public float Weight { get; set; }
            [JsonProperty("Type")] public string Type { get; set; }
            [JsonProperty("Reflect")] public bool Reflect { get; set; }
        }

        public class SourceData
        {
            [JsonProperty("Target")] public string Target { get; set; }
            [JsonProperty("Id")] public string Id { get; set; }
        }

        public class VertexData
        {
            [JsonProperty("Position")] public Vector2Data Position { get; set; }
            [JsonProperty("Mobility")] public float Mobility { get; set; }
            [JsonProperty("Delay")] public float Delay { get; set; }
            [JsonProperty("Acceleration")] public float Acceleration { get; set; }
            [JsonProperty("Radius")] public float Radius { get; set; }
        }

        public class NormalizationData
        {
            [JsonProperty("Position")] public NormalizationValueData Position { get; set; }
            [JsonProperty("Angle")] public NormalizationValueData Angle { get; set; }
        }

        public class NormalizationValueData
        {
            [JsonProperty("Minimum")] public float Minimum { get; set; }
            [JsonProperty("Default")] public float Default { get; set; }
            [JsonProperty("Maximum")] public float Maximum { get; set; }
        }

        public static CubismPhysics3Json LoadFrom(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;
            return JsonConvert.DeserializeObject<CubismPhysics3Json>(jsonText);
        }

        public static CubismPhysics3Json LoadAtPath(string path)
        {
            if (!File.Exists(path)) return null;
            return LoadFrom(File.ReadAllText(path));
        }
    }
}
