/**
 * Headless motion dump for the Godot port.
 * Same clock, fades, loop, and extras-off policy as tools/native_motion_dump.
 */

using System.Globalization;
using System.IO;
using System.Text;
using Godot;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Rendering;

namespace Live2D.Cubism.Tests
{
    public static class CubismMotionDump
    {
        public const float DefaultSeconds = 10f;
        public const float DefaultFps = 60f;

        public static void DumpAllHiyoriMotions(CubismModelNode node, string outDir,
            float seconds = DefaultSeconds, float fps = DefaultFps)
        {
            if (node?.Model == null || node.MotionController == null || node.ModelJson == null)
            {
                GD.PrintErr("[MotionDump] model not ready");
                return;
            }

            Directory.CreateDirectory(outDir);
            var jobs = new (string Group, int Index, bool Loop)[]
            {
                ("Idle", 0, true), ("Idle", 1, true), ("Idle", 2, true), ("Idle", 3, true),
                ("Idle", 4, true), ("Idle", 5, true), ("Idle", 6, true), ("Idle", 7, true),
                ("Idle", 8, true), ("TapBody", 0, false),
            };

            foreach (var job in jobs)
            {
                var path = Path.Combine(outDir, $"{job.Group}_{job.Index}.csv");
                DumpOne(node, job.Group, job.Index, job.Loop, path, seconds, fps);
            }
        }

        public static void DumpOne(CubismModelNode node, string group, int index, bool loop,
            string csvPath, float seconds, float fps)
        {
            ResetToDefaults(node.Model);
            node.ParameterStore?.SaveParameters();
            node.MotionController.HardReset();

            var playing = node.PlayMotionFromGroup(group, index, CubismMotionPriority.PriorityNormal, loop);
            if (playing == null)
            {
                GD.PrintErr($"[MotionDump] failed to play {group}[{index}]");
                return;
            }

            var frames = Mathf.RoundToInt(seconds * fps);
            var dt = 1f / fps;
            using var writer = new StreamWriter(csvPath, false, new UTF8Encoding(false));
            WriteHeader(writer, node.Model);

            for (int f = 0; f < frames; f++)
            {
                node.ParameterStore?.RestoreParameters();
                node.MotionController.Update(dt);
                node.ParameterStore?.SaveParameters();
                WriteRow(writer, f, (f + 1) * dt, node.Model);
            }

            GD.Print($"[MotionDump] wrote {csvPath} frames={frames} loop={loop}");
        }

        public static void ResetToDefaults(CubismModel model)
        {
            if (model?.Parameters == null) return;
            foreach (var p in model.Parameters)
                p.Value = p.DefaultValue;
            if (model.Parts == null) return;
            foreach (var part in model.Parts)
                part.Opacity = part.DefaultOpacity;
        }

        static void WriteHeader(StreamWriter writer, CubismModel model)
        {
            var sb = new StringBuilder();
            sb.Append("frame,time");
            foreach (var p in model.Parameters)
                sb.Append(",P:").Append(p.Id);
            foreach (var part in model.Parts)
                sb.Append(",O:").Append(part.Id);
            writer.WriteLine(sb.ToString());
        }

        static void WriteRow(StreamWriter writer, int frame, float time, CubismModel model)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(frame).Append(',').Append(time.ToString("G9", inv));
            foreach (var p in model.Parameters)
                sb.Append(',').Append(p.Value.ToString("G9", inv));
            foreach (var part in model.Parts)
                sb.Append(',').Append(part.Opacity.ToString("G9", inv));
            writer.WriteLine(sb.ToString());
        }
    }
}
