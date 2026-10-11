using System;
using System.IO;
using Live2D.Cubism.Rendering;

namespace Live2D.Cubism.Tests
{
    /// <summary>Produces deterministic RGBA8 images for Native/Godot pixel comparison.</summary>
    public static class CubismAlphaBlendDump
    {
        public const int Width = 96;
        public const int Height = 96;

        public static void WriteAll(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            string[] names = { "over", "atop", "out", "conjoint_over", "disjoint_over" };
            for (int mode = 0; mode < names.Length; mode++)
                File.WriteAllBytes(Path.Combine(outputDirectory, names[mode] + ".rgba"), Render(mode));
        }

        private static byte[] Render(int mode)
        {
            var pixels = new byte[Width * Height * 4];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)(Width - 1);
                float v = y / (float)(Height - 1);
                float sourceAlpha = ((x * 17 + y * 13) % Width) / (float)(Width - 1);
                float destinationAlpha = ((x * 7 + y * 29 + 11) % Width) / (float)(Width - 1);

                CubismAlphaBlend.Apply(mode,
                    0.2f + 0.6f * v, 0.1f + 0.7f * u, 0.8f - 0.5f * v,
                    u, 1f - v, 0.25f + 0.5f * v, sourceAlpha,
                    1f - u, v, 0.75f - 0.5f * u, destinationAlpha,
                    out float r, out float g, out float b, out float a);

                int p = (y * Width + x) * 4;
                pixels[p] = Quantize(r);
                pixels[p + 1] = Quantize(g);
                pixels[p + 2] = Quantize(b);
                pixels[p + 3] = Quantize(a);
            }
            return pixels;
        }

        private static byte Quantize(float value)
        {
            return (byte)Math.Clamp((int)MathF.Floor(Math.Clamp(value, 0f, 1f) * 255f + 0.5f), 0, 255);
        }
    }
}
