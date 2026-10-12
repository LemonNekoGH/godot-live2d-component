# Cubism SDK for Godot (C# / .NET)

A faithful port of Live2D's [Cubism SDK for Unity (CubismUnityComponents)](https://github.com/Live2D/CubismUnityComponents) to Godot Engine using C# (.NET).

## Overview

This project ports the open-source components of the Cubism 5 SDK for Unity R5 to Godot 4.7.2 (.NET build), targeting **.NET 10 (`net10.0`)**. The port mirrors the Unity implementation's logic, math, update order, and observable behavior as closely as possible.

> **Godot version:** This project uses **Godot 4.7.2** (.NET / Mono build, released 2026-08-18) with **.NET 10 SDK** (`net10.0` target). The GodotSharp 4.7.2 NuGet package ships with `net8.0` assemblies, but the `LatestMajor` roll-forward policy in Godot's runtime config allows it to run on the .NET 10 runtime.

## Architecture

The project structure mirrors the Unity SDK:

```
addons/live2d_cubism/
├── Core/                  # Core wrapper (P/Invoke to native Cubism Core)
│   └── Unmanaged/        # Low-level native bindings
├── Framework/            # Framework components
│   ├── Json/             # JSON parsers (model3, motion3, exp3, physics3, pose3, userdata3)
│   ├── Physics/          # Physics simulation (pendulum, sub-rigs)
│   ├── Motion/           # Motion playback with fade
│   ├── Expression/       # Expression blending
│   ├── LookAt/           # Look-at targeting
│   ├── Pose/             # Part visibility groups
│   ├── MouthMovement/    # Lip sync
│   ├── HarmonicMotion/   # Sinusoidal animation
│   ├── Raycasting/       # Hit-test on drawables
│   └── UserData/         # User data tags
└── Rendering/            # Godot rendering (MeshInstance2D + shaders)
```

## Coordinate System Mapping

| Aspect | Unity | Godot 2D | Mapping |
|--------|-------|----------|---------|
| Y axis | Up    | Down     | Vertex Y negated in `CubismUnmanagedDrawables.GetVertexPositions` |
| Units  | Model space (PixelsPerUnit) | Pixels | Scaled by Core `PixelsPerUnit` |
| Draw order | SortingOrder | ZIndex | Direct mapping of Core render orders |
| UV origin | (0,0) = bottom-left | (0,0) = top-left | V flipped (`1 - v`), matching Native `VertShaderSrc.vert` |

## Update Order

Matches Unity's `PreLateUpdate` insertion exactly:

1. **Motion/Fade** (order 100) — Native Cardano bezier (Hiyori is unrestricted), sine fade, per-parameter fades, Unity priority
2. **ParameterStore.Save** (order 150) — snapshot after motion, before additive controllers
3. **Pose** (order 200) — mutually exclusive part visibility
4. **Expression** (order 300) — expression blending (Add/Multiply/Override)
5. **Eye Blink** (order 400) — `CubismAutoEyeBlinkInput` (Timescale 10, Idle uses unscaled `deltaTime`) + Multiply `BlendToValue`
6. **Mouth** (order 500) — lip sync input (Unity default Multiply)
7. **Harmonic Motion** (order 600) — sinusoidal parameter animation
8. **Look-at** (order 700) — gaze direction
9. **Physics** (order 800) — pendulum simulation with interpolation
10. **Core Update** — `csmUpdateModel` (parameter → drawable)
11. **ParameterStore.Restore** — after Core read-back, so next frame's Multiply blink/mouth starts from the saved base (not last frame's 0)
12. **Rendering** (order 10000) — mesh rebuild + draw

## Blend Modes

| Unity / Core | Godot |
|--------------|-------|
| Normal | `blend_premul_alpha` (Cubism 4 path) |
| AddCompatible / MultiplyCompatible | `blend_add` / `blend_mul` |
| Add, AddGlow, Darken, Multiply, ColorBurn, LinearBurn, Lighten, Screen, ColorDodge, Overlay, SoftLight, HardLight, LinearLight, Hue, Color | Dest-color shader (`hint_screen_texture`), Core type id in `cubism_color_blend` |
| Alpha Over, Atop, Out, Conjoint Over, Disjoint Over | Destination-alpha shader matching Unity `AlphaBlendVariants.cginc` and Native alpha blend shaders |

## Requirements

- **Godot 4.7.2** (.NET / Mono build) — [download](https://github.com/godotengine/godot/releases/tag/4.7.2-stable)
- **.NET 10 SDK** — [download](https://dotnet.microsoft.com/download)
- **Cubism Core native library** (proprietary, see below)

## Setup

### 1. Install .NET 10 SDK

```bash
# Using the official installer script
wget -q https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
```

### 2. Get the Cubism Core Native Library

The Cubism Core (`Live2DCubismCore`) is a proprietary native library from Live2D. It is **not included** in this repository.

```bash
# Run the setup script for instructions
./setup_cubism_core.sh
```

1. Download from [Live2D SDK Download](https://www.live2d.com/sdk/download/native/)
2. Accept the license agreement
3. Copy the native library to `native/`:
   - Linux: `libLive2DCubismCore.so`
   - Windows: `Live2DCubismCore.dll`
   - macOS: `libLive2DCubismCore.dylib`

### 3. Build

```bash
dotnet build
```

### 4. Add a Live2D Model

Copy a model directory (e.g., Hiyori from the Cubism SDK samples) to `demo/models/`:

```bash
cp -r /path/to/Hiyori demo/models/
```

### 5. Run

Open the project in Godot 4.7.2 (.NET) and run the demo scene, or:

```bash
export LD_LIBRARY_PATH="$(pwd)/native:$LD_LIBRARY_PATH"
godot --path . demo/scenes/demo.tscn
```

Demo modes (optional environment variables):

| Variable | Purpose |
|----------|---------|
| `CUBISM_DEMO_MODE` | `static` (default), `motion`, `expression`, `blink`, `physics`, `pose`, `lipsync`, `lookat`, `all` |
| `CUBISM_SCREENSHOT` | Write a PNG after the first rendered frames |
| `CUBISM_QUIT_AFTER` | Quit after N seconds |
| `CUBISM_BG` | `green`, `white`, or `gray` |
| `CUBISM_HUD` | Set to `0` to hide the overlay |

Keys in the demo: `1`/`2`/`3` start expressions, `Space` plays TapBody, `F` toggles framework updates, `S` saves a screenshot.

### 6. Record a feature (Movie Maker)

Do **not** use `ffmpeg` `x11grab` for feature videos. Godot's Movie Maker advances the engine on a fixed clock, so the file is 60 fps even when the software renderer is slow.

```bash
# One mode → H.264 mp4 (default 8s @ 60 fps, 480 frames)
./tools/record_feature.sh motion

# Explicit output path
./tools/record_feature.sh static /tmp/feature-static-render.mp4

# Every CUBISM_DEMO_MODE
./tools/record_feature.sh all /opt/cursor/artifacts

# All Hiyori Idle + TapBody motion3 files (10s each)
./tools/record_feature.sh motions

# Cubism 5 remaining features
./tools/record_feature.sh blend
./tools/record_feature.sh alpha
```

The script runs:

```bash
godot --path . --write-movie /tmp/mode.avi --fixed-fps 60 --quit-after 480 --disable-vsync demo/scenes/demo.tscn
```

`--quit-after` is a **frame count** (8 × 60 = 480). It then transcodes the AVI to H.264 (`libx264`, yuv420p, no audio) and keeps the mp4 under 15 MB.

| Variable | Purpose |
|----------|---------|
| `CUBISM_RECORD_SECONDS` | Simulated duration (default `8`) |
| `CUBISM_RECORD_FPS` | Movie fps (default `60`) |
| `CUBISM_RECORD_BG` | Background (`green` by default) |
| `GODOT` | Godot binary (default `godot`) |

Mode → artifact name: `static` → `feature-static-render.mp4`, `motion` → `feature-motion-idle.mp4`, `expression` → `feature-expression-smile.mp4`, `blink` → `feature-eye-blink.mp4`, `physics` → `feature-physics.mp4`, `pose` → `feature-pose-tapbody.mp4`, `lipsync` → `feature-lipsync.mp4`, `lookat` → `feature-lookat.mp4`.

## Usage

### Loading a Model

```csharp
var modelNode = new CubismModelNode();
AddChild(modelNode);
modelNode.LoadModel("path/to/model.model3.json");
```

### Playing Motions

```csharp
modelNode.PlayMotionFromGroup("Idle", 0, CubismMotionPriority.PriorityIdle, loop: true);
modelNode.PlayMotion("path/to/motion.motion3.json");
```

### Expressions

```csharp
modelNode.StartExpression(0); // Start first expression
```

### Look-at

```csharp
modelNode.SetLookTarget(new Vector2(0.5f, -0.3f)); // Normalized direction
```

### Direct Parameter Access

```csharp
var param = modelNode.Model.FindParameter("ParamAngleX");
param.Value = 15f;
```

## Verification

Math functions are verified via golden-value tests that compare output against the Unity C# source code with identical inputs. Tests run automatically when the demo scene starts and cover:

- `CubismMath.ModF` / `ClampF`
- `CubismPhysicsMath` (all functions: DegreesToRadian, RadianToDegrees, DirectionToRadian, etc.)
- `CubismPhysicsMath.Normalize`
- `CubismParameter.GetParameterRepeatValue` / `GetParameterClampValue`
- `CubismMotion3Json.EvaluateCurve` (linear, stepped, inverse-stepped, bezier segments)
- Unity-style motion loop wrap (`time >= Duration`) and PartOpacity writes

### Frame-by-frame motion vs Native SDK

`tools/native_motion_dump` is a headless Cubism Native harness (Framework + Core, no renderer). It loads Hiyori, plays each Idle / TapBody motion3 with the same fade / priority / loop as the Godot demo, steps `dt=1/60` for 10 s, and writes parameter + part-opacity CSVs.

Godot dump mode: `CUBISM_DUMP_DIR=/tmp/out godot --path . --headless demo/scenes/demo.tscn` (blink / breath / physics / pose off).

```bash
# Generate dumps at test time (CSVs are not committed) and diff them
./tools/dump_motions.sh /tmp/cubism-motion-dumps
# or
python3 tests/test_motion_golden.py
```

Pass/fail uses parameter columns for `t < Duration` at `2e-3` (Cardano matches Native on identical coefficients; leftover ulps are CubismJson vs Newtonsoft on near-degenerate unrestricted beziers). Native V2 loop wrap (`Duration+1/fps` + CorrectEndPoint) is a documented Native≠Unity difference and is excluded; this port follows Unity. Official Native `DoUpdateParameters` looks PartOpacity ids up as parameters (usually a no-op); the harness patches that write to parts so the CSV matches Unity / this port.

Set `CUBISM_NATIVE_SDK` if the SDK is not at `/tmp/cubism_native/CubismSdkForNative-5-r.5`.

### Isolated Alpha Blend pixel comparison

The synthetic GPU comparison excludes offscreen groups and renders the same 96×96 RGBA8 input grid in Native OpenGL and a Godot `SubViewport` for Over, Atop, Out, Conjoint Over, and Disjoint Over. Native compiles the official SDK `FragShaderSrcAlphaBlend.frag`; Godot uses the production extended shader with `BackBufferCopy`. Both sides read back RGBA8 framebuffers. The test writes per-mode PSNR, maximum/mean channel error, reference images, and a 16× difference image.

```bash
CUBISM_NATIVE_SDK=/path/to/CubismSdkForNative-5-r.5 \
GODOT=/path/to/godot \
./tools/test_alpha_blend_pixels.sh /tmp/alpha-blend-artifacts
```

## License

The ported open-source components retain the [Live2D Open Software License](https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html) from the original CubismUnityComponents.

The Cubism Core native library is subject to the [Live2D Proprietary Software License](https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html).

Live2D sample models are subject to the [Free Material License Agreement](https://www.live2d.com/eula/live2d-free-material-license-agreement_en.html).
