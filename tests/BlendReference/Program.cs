using System;
using Live2D.Cubism.Core.Unmanaged;
using Live2D.Cubism.Rendering;

const float epsilon = 0.000001f;
var passed = true;

void Close(float actual, float expected, string name)
{
    if (MathF.Abs(actual - expected) <= epsilon)
        return;

    Console.Error.WriteLine($"FAIL {name}: expected {expected}, got {actual}");
    passed = false;
}

void Color(int mode, float expectedR, float expectedG, float expectedB, string name)
{
    CubismColorBlend.Apply(mode, 0.7f, 0.2f, 0.4f, 0.3f, 0.8f, 0.1f,
        out var r, out var g, out var b);
    Close(r, expectedR, name + ".r");
    Close(g, expectedG, name + ".g");
    Close(b, expectedB, name + ".b");
}

Color(CubismCoreDll.ColorBlendType_Add, 1f, 1f, 0.5f, "Add");
Color(CubismCoreDll.ColorBlendType_Darken, 0.3f, 0.2f, 0.1f, "Darken");
Color(CubismCoreDll.ColorBlendType_Multiply, 0.21f, 0.16f, 0.04f, "Multiply");
Color(CubismCoreDll.ColorBlendType_LinearBurn, 0f, 0f, 0f, "LinearBurn");
Color(CubismCoreDll.ColorBlendType_Screen, 0.79f, 0.84f, 0.46f, "Screen");
Close(CubismColorBlend.ColorBurn(0.4f, 1f), 1f, "ColorBurn dest=1");
Close(CubismColorBlend.ColorBurn(0f, 0.5f), 0f, "ColorBurn src=0");
Close(CubismColorBlend.ColorDodge(1f, 0.4f), 1f, "ColorDodge src=1");
Close(CubismColorBlend.ColorDodge(0.5f, 0f), 0f, "ColorDodge dest=0");

void Alpha(int mode, float overlap, float sourceOnly, float destinationOnly, string name)
{
    CubismAlphaBlend.Apply(mode, 0.8f, 0.3f, 0.6f, 0.7f, 0.2f, 0.4f, 0.6f,
        0.3f, 0.8f, 0.1f, 0.25f, out var r, out var g, out var b, out var a);
    Close(r, 0.8f * overlap + 0.7f * sourceOnly + 0.3f * destinationOnly, name + ".r");
    Close(g, 0.3f * overlap + 0.2f * sourceOnly + 0.8f * destinationOnly, name + ".g");
    Close(b, 0.6f * overlap + 0.4f * sourceOnly + 0.1f * destinationOnly, name + ".b");
    Close(a, overlap + sourceOnly + destinationOnly, name + ".a");
}

Alpha(CubismCoreDll.AlphaBlendType_Over, 0.15f, 0.45f, 0.1f, "Over");
Alpha(CubismCoreDll.AlphaBlendType_Atop, 0.15f, 0f, 0.1f, "Atop");
Alpha(CubismCoreDll.AlphaBlendType_Out, 0f, 0f, 0.1f, "Out");
Alpha(CubismCoreDll.AlphaBlendType_ConjointOver, 0.25f, 0.35f, 0f, "ConjointOver");
Alpha(CubismCoreDll.AlphaBlendType_DisjointOver, 0f, 0.6f, 0.25f, "DisjointOver");

Console.WriteLine(passed ? "PASS blend reference tests" : "FAIL blend reference tests");
return passed ? 0 : 1;
