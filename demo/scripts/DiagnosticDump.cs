using System;
using System.IO;
using System.Text;
using Godot;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.Json;

namespace Live2D.Cubism.Demo
{
    public partial class DiagnosticDump : Node2D
    {
        public override void _Ready()
        {
            var modelPath = "/workspace/demo/models/Hiyori/Hiyori.model3.json";
            var json = CubismModel3Json.LoadAtPath(modelPath);
            if (json == null) { GD.PrintErr("Failed to load model JSON"); GetTree().Quit(); return; }

            var model = json.ToModel();
            if (model == null) { GD.PrintErr("Failed to create model"); GetTree().Quit(); return; }

            // Force core update to populate drawables
            model.UpdateModel();

            var sb = new StringBuilder();
            sb.AppendLine($"Model: {model.Name}");
            sb.AppendLine($"Canvas: {model.CanvasSizeInPixels}, Origin: {model.CanvasOriginInPixels}, PPU: {model.PixelsPerUnit}");
            sb.AppendLine($"Parameters: {model.Parameters.Length}, Parts: {model.Parts.Length}, Drawables: {model.Drawables.Length}");
            sb.AppendLine();

            var ppu = model.PixelsPerUnit;

            int visibleCount = 0, maskedCount = 0, emptyMeshCount = 0;
            float globalMinX = float.MaxValue, globalMaxX = float.MinValue;
            float globalMinY = float.MaxValue, globalMaxY = float.MinValue;

            for (int i = 0; i < model.Drawables.Length; i++)
            {
                var d = model.Drawables[i];
                var verts = model.GetDrawableVertexPositions(i);
                var uvs = model.GetDrawableVertexUvs(i);
                var indices = model.GetDrawableIndices(i);

                bool hasMask = d.MaskIndices != null && d.MaskIndices.Length > 0;
                if (hasMask) maskedCount++;
                if (!d.IsVisible) { sb.AppendLine($"[{i:D3}] {d.Id,-30} INVISIBLE opacity={d.Opacity:F3}"); continue; }
                if (verts == null || verts.Length == 0 || indices == null || indices.Length < 3)
                {
                    emptyMeshCount++;
                    sb.AppendLine($"[{i:D3}] {d.Id,-30} EMPTY_MESH verts={verts?.Length ?? 0} idx={indices?.Length ?? 0}");
                    continue;
                }

                visibleCount++;

                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                float minU = float.MaxValue, maxU = float.MinValue;
                float minV = float.MaxValue, maxV = float.MinValue;

                for (int v = 0; v < verts.Length; v++)
                {
                    if (verts[v].X < minX) minX = verts[v].X;
                    if (verts[v].X > maxX) maxX = verts[v].X;
                    if (verts[v].Y < minY) minY = verts[v].Y;
                    if (verts[v].Y > maxY) maxY = verts[v].Y;
                }
                for (int v = 0; v < uvs.Length; v++)
                {
                    if (uvs[v].X < minU) minU = uvs[v].X;
                    if (uvs[v].X > maxU) maxU = uvs[v].X;
                    if (uvs[v].Y < minV) minV = uvs[v].Y;
                    if (uvs[v].Y > maxV) maxV = uvs[v].Y;
                }

                if (minX * ppu < globalMinX) globalMinX = minX * ppu;
                if (maxX * ppu > globalMaxX) globalMaxX = maxX * ppu;
                if (minY * ppu < globalMinY) globalMinY = minY * ppu;
                if (maxY * ppu > globalMaxY) globalMaxY = maxY * ppu;

                string maskInfo = hasMask ? $" MASK=[{string.Join(",", d.MaskIndices)}]" : "";
                string blendInfo = d.IsAdditive ? " ADD" : (d.IsMultiplicative ? " MUL" : "");
                string flags = "";
                if (d.IsDoubleSided) flags += " DBL";
                if (d.IsInvertedMask) flags += " INVMASK";

                sb.AppendLine($"[{i:D3}] {d.Id,-30} tex={d.TextureIndex} ro={d.RenderOrder,4} do={d.DrawOrder,4} " +
                              $"op={d.Opacity:F3} verts={verts.Length,3} idx={indices.Length,4} " +
                              $"pos=({minX:F4},{minY:F4})-({maxX:F4},{maxY:F4}) " +
                              $"uv=({minU:F3},{minV:F3})-({maxU:F3},{maxV:F3})" +
                              $"{blendInfo}{flags}{maskInfo}");
            }

            sb.AppendLine();
            sb.AppendLine($"Summary: visible={visibleCount}, masked={maskedCount}, emptyMesh={emptyMeshCount}");
            sb.AppendLine($"Global bounds (pixels): ({globalMinX:F1},{globalMinY:F1})-({globalMaxX:F1},{globalMaxY:F1})");
            sb.AppendLine($"Global size: {globalMaxX - globalMinX:F1} x {globalMaxY - globalMinY:F1}");

            // Check for suspicious patterns
            sb.AppendLine("\n=== Diagnostic checks ===");

            // Check UV ranges
            int badUvCount = 0;
            for (int i = 0; i < model.Drawables.Length; i++)
            {
                var uvs = model.GetDrawableVertexUvs(i);
                if (uvs == null) continue;
                foreach (var uv in uvs)
                {
                    if (uv.X < -0.01f || uv.X > 1.01f || uv.Y < -0.01f || uv.Y > 1.01f)
                    {
                        badUvCount++;
                        break;
                    }
                }
            }
            sb.AppendLine($"Drawables with UVs outside [0,1]: {badUvCount}");

            // Check index validity
            int badIndexCount = 0;
            for (int i = 0; i < model.Drawables.Length; i++)
            {
                var verts = model.GetDrawableVertexPositions(i);
                var indices = model.GetDrawableIndices(i);
                if (verts == null || indices == null) continue;
                foreach (var idx in indices)
                {
                    if (idx < 0 || idx >= verts.Length)
                    {
                        badIndexCount++;
                        sb.AppendLine($"  BAD INDEX: drawable[{i}] index={idx} vertCount={verts.Length}");
                        break;
                    }
                }
            }
            sb.AppendLine($"Drawables with invalid indices: {badIndexCount}");

            // Print parts and their opacities
            sb.AppendLine("\n=== Parts ===");
            for (int i = 0; i < model.Parts.Length; i++)
            {
                var p = model.Parts[i];
                sb.AppendLine($"  [{i:D2}] {p.Id,-30} opacity={p.Opacity:F3} parent={p.ParentPartIndex}");
            }

            var output = sb.ToString();
            GD.Print(output);

            File.WriteAllText("/opt/cursor/artifacts/drawable-diagnostic.txt", output);
            GD.Print("\nDiagnostic written to /opt/cursor/artifacts/drawable-diagnostic.txt");

            model.Dispose();
            GetTree().Quit();
        }
    }
}
