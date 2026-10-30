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
    /// Parses physics3.json files. Mirrors Unity's CubismPhysics3Json structure.
    /// </summary>
    public sealed class CubismPhysics3Json
    {
        [JsonPropertyName("Version")] public int Version { get; set; }
        [JsonPropertyName("Meta")] public PhysicsMeta Meta { get; set; }
        [JsonPropertyName("PhysicsSettings")] public PhysicsSettingData[] PhysicsSettings { get; set; }

        public class PhysicsMeta
        {
            [JsonPropertyName("PhysicsSettingCount")] public int PhysicsSettingCount { get; set; }
            [JsonPropertyName("TotalInputCount")] public int TotalInputCount { get; set; }
            [JsonPropertyName("TotalOutputCount")] public int TotalOutputCount { get; set; }
            [JsonPropertyName("TotalVertexCount")] public int TotalVertexCount { get; set; }
            [JsonPropertyName("EffectiveForces")] public EffectiveForce EffectiveForces { get; set; }
        }

        public class EffectiveForce
        {
            [JsonPropertyName("Gravity")] public Vector2Data Gravity { get; set; }
            [JsonPropertyName("Wind")] public Vector2Data Wind { get; set; }
        }

        public class Vector2Data
        {
            [JsonPropertyName("X")] public float X { get; set; }
            [JsonPropertyName("Y")] public float Y { get; set; }
        }

        public class PhysicsSettingData
        {
            [JsonPropertyName("Id")] public string Id { get; set; }
            [JsonPropertyName("Input")] public InputData[] Input { get; set; }
            [JsonPropertyName("Output")] public OutputData[] Output { get; set; }
            [JsonPropertyName("Vertices")] public VertexData[] Vertices { get; set; }
            [JsonPropertyName("Normalization")] public NormalizationData Normalization { get; set; }
        }

        public class InputData
        {
            [JsonPropertyName("Source")] public SourceData Source { get; set; }
            [JsonPropertyName("Weight")] public float Weight { get; set; }
            [JsonPropertyName("Type")] public string Type { get; set; }
            [JsonPropertyName("Reflect")] public bool Reflect { get; set; }
        }

        public class OutputData
        {
            [JsonPropertyName("Destination")] public SourceData Destination { get; set; }
            [JsonPropertyName("VertexIndex")] public int VertexIndex { get; set; }
            [JsonPropertyName("Scale")] public float Scale { get; set; }
            [JsonPropertyName("Weight")] public float Weight { get; set; }
            [JsonPropertyName("Type")] public string Type { get; set; }
            [JsonPropertyName("Reflect")] public bool Reflect { get; set; }
        }

        public class SourceData
        {
            [JsonPropertyName("Target")] public string Target { get; set; }
            [JsonPropertyName("Id")] public string Id { get; set; }
        }

        public class VertexData
        {
            [JsonPropertyName("Position")] public Vector2Data Position { get; set; }
            [JsonPropertyName("Mobility")] public float Mobility { get; set; }
            [JsonPropertyName("Delay")] public float Delay { get; set; }
            [JsonPropertyName("Acceleration")] public float Acceleration { get; set; }
            [JsonPropertyName("Radius")] public float Radius { get; set; }
        }

        public class NormalizationData
        {
            [JsonPropertyName("Position")] public NormalizationValueData Position { get; set; }
            [JsonPropertyName("Angle")] public NormalizationValueData Angle { get; set; }
        }

        public class NormalizationValueData
        {
            [JsonPropertyName("Minimum")] public float Minimum { get; set; }
            [JsonPropertyName("Default")] public float Default { get; set; }
            [JsonPropertyName("Maximum")] public float Maximum { get; set; }
        }

        public static CubismPhysics3Json LoadFrom(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;
            return JsonSerializer.Deserialize(jsonText, CubismJsonContext.Default.CubismPhysics3Json);
        }

        public static CubismPhysics3Json LoadAtPath(string path)
        {
            var text = CubismAssetIo.ReadAllText(path);
            return text != null ? LoadFrom(text) : null;
        }
    }
}
