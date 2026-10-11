using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using Live2D.Cubism.Rendering;

namespace Live2D.Cubism.Tests
{
    /// <summary>Renders deterministic inputs through Godot's production alpha-blend shader.</summary>
    public static class CubismAlphaBlendDump
    {
        public const int Width = 96;
        public const int Height = 96;

        public static async Task WriteAll(Node host, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            string[] names = { "over", "atop", "out", "conjoint_over", "disjoint_over" };
            var source = MakeTexture(true);
            var destination = MakeTexture(false);
            for (int mode = 0; mode < names.Length; mode++)
            {
                var viewport = new SubViewport
                {
                    Size = new Vector2I(Width, Height), TransparentBg = true,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Disable3D = true,
                };
                host.AddChild(viewport);
                viewport.AddChild(MakeRect(destination, null));
                viewport.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
                viewport.AddChild(new ColorRect
                {
                    Color = Colors.White, Position = Vector2.Zero, Size = new Vector2(Width, Height),
                    Material = CubismDrawableRenderer.CreateAlphaBlendTestMaterial(source, mode),
                });
                for (int frame = 0; frame < 3; frame++)
                    await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
                File.WriteAllBytes(Path.Combine(outputDirectory, names[mode] + ".rgba"),
                    viewport.GetTexture().GetImage().GetData());
                viewport.QueueFree();
            }
        }

        private static TextureRect MakeRect(Texture2D texture, Material material) => new()
        {
            Texture = texture, Material = material, Position = Vector2.Zero,
            Size = new Vector2(Width, Height), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };

        private static ImageTexture MakeTexture(bool source)
        {
            var bytes = new byte[Width * Height * 4];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)(Width - 1), v = y / (float)(Height - 1);
                float alpha = source ? ((x * 17 + y * 13) % Width) / (float)(Width - 1)
                    : ((x * 7 + y * 29 + 11) % Width) / (float)(Width - 1);
                int p = (y * Width + x) * 4;
                bytes[p] = Quantize(source ? u : 1f - u);
                bytes[p + 1] = Quantize(source ? 1f - v : v);
                bytes[p + 2] = Quantize(source ? 0.25f + 0.5f * v : 0.75f - 0.5f * u);
                bytes[p + 3] = Quantize(alpha);
            }
            return ImageTexture.CreateFromImage(Image.CreateFromData(Width, Height, false, Image.Format.Rgba8, bytes));
        }

        private static byte Quantize(float value) =>
            (byte)Math.Clamp((int)MathF.Floor(Math.Clamp(value, 0f, 1f) * 255f + 0.5f), 0, 255);
    }
}
