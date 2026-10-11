// Synthetic pixel reference using the coefficient equations in Cubism Native
// FragShaderSrcAlphaBlend.frag and FragShaderSrcPremultipliedAlphaBlend.frag.
#include <Live2DCubismCore.h>

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <fstream>
#include <string>
#include <vector>

namespace {
constexpr int Width = 96;
constexpr int Height = 96;

std::uint8_t Quantize(float value)
{
    value = std::max(0.0f, std::min(1.0f, value));
    return static_cast<std::uint8_t>(std::floor(value * 255.0f + 0.5f));
}

void Render(const std::string& path, int mode)
{
    std::vector<std::uint8_t> pixels(Width * Height * 4);
    for (int y = 0; y < Height; ++y)
    for (int x = 0; x < Width; ++x)
    {
        const float u = x / static_cast<float>(Width - 1);
        const float v = y / static_cast<float>(Height - 1);
        const float sa = ((x * 17 + y * 13) % Width) / static_cast<float>(Width - 1);
        const float da = ((x * 7 + y * 29 + 11) % Width) / static_cast<float>(Width - 1);

        float p0, p1, p2;
        switch (mode)
        {
        case 1:
            p0 = sa * da; p1 = 0.0f; p2 = da * (1.0f - sa); break;
        case 2:
            p0 = 0.0f; p1 = 0.0f; p2 = da * (1.0f - sa); break;
        case 3:
            p0 = std::min(sa, da); p1 = std::max(sa - da, 0.0f); p2 = std::max(da - sa, 0.0f); break;
        case 4:
            p0 = std::max(sa + da - 1.0f, 0.0f); p1 = std::min(sa, 1.0f - da); p2 = std::min(da, 1.0f - sa); break;
        default:
            p0 = sa * da; p1 = sa * (1.0f - da); p2 = da * (1.0f - sa); break;
        }

        const float blend[3] = { 0.2f + 0.6f * v, 0.1f + 0.7f * u, 0.8f - 0.5f * v };
        const float source[3] = { u, 1.0f - v, 0.25f + 0.5f * v };
        const float destination[3] = { 1.0f - u, v, 0.75f - 0.5f * u };
        const int offset = (y * Width + x) * 4;
        for (int channel = 0; channel < 3; ++channel)
            pixels[offset + channel] = Quantize(blend[channel] * p0 + source[channel] * p1 + destination[channel] * p2);
        pixels[offset + 3] = Quantize(p0 + p1 + p2);
    }

    std::ofstream stream(path, std::ios::binary);
    stream.write(reinterpret_cast<const char*>(pixels.data()), static_cast<std::streamsize>(pixels.size()));
}
}

int main(int argc, char** argv)
{
    if (argc != 2)
    {
        std::fprintf(stderr, "usage: native_alpha_blend_dump OUTPUT_DIR\n");
        return 2;
    }
    const std::string directory(argv[1]);
    const char* names[] = { "over", "atop", "out", "conjoint_over", "disjoint_over" };
    for (int mode = 0; mode < 5; ++mode)
        Render(directory + "/" + names[mode] + ".rgba", mode);
    std::printf("Cubism Core version=%u; wrote five %dx%d RGBA8 references\n", csmGetVersion(), Width, Height);
    return 0;
}
