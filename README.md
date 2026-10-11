# Cubism SDK for Godot (C# / .NET)

A faithful port of Live2D's [Cubism SDK for Unity (CubismUnityComponents)](https://github.com/Live2D/CubismUnityComponents) to Godot Engine using C# (.NET).

## Overview

This project ports the open-source components of the Cubism 5 SDK for Unity R5 to Godot 4.4.1 (.NET build), targeting **.NET 10 (`net10.0`)**. The port mirrors the Unity implementation's logic, math, update order, and observable behavior as closely as possible.

> **Note on Godot version:** Godot 4.7.2 is not yet released. This project uses Godot 4.4.1 (latest stable 4.x .NET build). The GodotSharp 4.4.1 NuGet package ships with `net8.0` assemblies, but the `LatestMajor` roll-forward policy in Godot's runtime config allows it to run on the .NET 10 runtime.

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
| Units  | Model space (PixelsPerUnit) | Pixels | Scaled by `100 / PixelsPerUnit` |
| Draw order | SortingOrder | ZIndex | Direct mapping of Core render orders |
| UV origin | (0,0) = bottom-left | (0,0) = top-left | Passed through (Core UVs are already top-left) |

## Update Order

Matches Unity's `PreLateUpdate` insertion exactly:

1. **Motion/Fade** (order 100) — curve evaluation, fade in/out
2. **Pose** (order 200) — mutually exclusive part visibility
3. **Expression** (order 300) — expression blending (Add/Multiply/Override)
4. **Eye Blink** (order 400) — automatic blink cycle
5. **Mouth** (order 500) — lip sync input
6. **Harmonic Motion** (order 600) — sinusoidal parameter animation
7. **Look-at** (order 700) — gaze direction
8. **Physics** (order 800) — pendulum simulation with interpolation
9. **Core Update** — `csmUpdateModel` (parameter → drawable)
10. **Rendering** (order 10000) — mesh rebuild + draw

## Blend Modes

| Unity Blend | Godot Equivalent |
|-------------|-----------------|
| Normal      | Default shader   |
| Additive    | `CanvasItemMaterial.BlendMode.Add` |
| Multiplicative | `CanvasItemMaterial.BlendMode.Mul` |
| Masking     | Custom shader with mask texture |

## Requirements

- **Godot 4.4.1** (.NET / Mono build) — [download](https://godotengine.org/download)
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

Open the project in Godot 4.4.1 (.NET) and run the demo scene, or:

```bash
export LD_LIBRARY_PATH="$(pwd)/native:$LD_LIBRARY_PATH"
godot --path . demo/scenes/demo.tscn
```

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

## License

The ported open-source components retain the [Live2D Open Software License](https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html) from the original CubismUnityComponents.

The Cubism Core native library is subject to the [Live2D Proprietary Software License](https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html).

Live2D sample models are subject to the [Free Material License Agreement](https://www.live2d.com/eula/live2d-free-material-license-agreement_en.html).
