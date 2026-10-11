#!/bin/bash
# ============================================================================
# Cubism Core Native Library Setup Script
# ============================================================================
#
# The Cubism Core native library (Live2DCubismCore) is proprietary and must be
# obtained from Live2D under their license. It is NOT included in this repo.
#
# Steps to set up:
#
# 1. Download the Cubism SDK for Native from:
#    https://www.live2d.com/sdk/download/native/
#
# 2. Accept the Live2D Proprietary Software License Agreement
#
# 3. Extract the archive and locate the native library:
#    - Windows:  Core/dll/windows/x86_64/Live2DCubismCore.dll
#    - macOS:    Core/dll/macos/libLive2DCubismCore.dylib
#    - Linux:    Core/dll/linux/x86_64/libLive2DCubismCore.so
#
# 4. Copy the appropriate library to this project's native/ directory:
#
#    cp /path/to/Live2DCubismCore.{dll,so,dylib} ./native/
#
# 5. For Godot to find the library at runtime, ensure it's in one of:
#    - The project's native/ directory (we set up a symlink below)
#    - A system library path (LD_LIBRARY_PATH on Linux)
#    - The Godot executable's directory
#
# ============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
NATIVE_DIR="$SCRIPT_DIR/native"

mkdir -p "$NATIVE_DIR"

echo "=== Cubism Core Native Library Setup ==="
echo ""
echo "Please place the Cubism Core native library in: $NATIVE_DIR"
echo ""

# Check if library already exists
if [ -f "$NATIVE_DIR/libLive2DCubismCore.so" ] || \
   [ -f "$NATIVE_DIR/Live2DCubismCore.dll" ] || \
   [ -f "$NATIVE_DIR/libLive2DCubismCore.dylib" ]; then
    echo "✓ Cubism Core library found!"

    # Set up LD_LIBRARY_PATH for Linux
    if [ "$(uname)" = "Linux" ]; then
        export LD_LIBRARY_PATH="$NATIVE_DIR:${LD_LIBRARY_PATH:-}"
        echo "  LD_LIBRARY_PATH set to include: $NATIVE_DIR"
    fi
else
    echo "✗ Cubism Core library NOT found."
    echo ""
    echo "Download from: https://www.live2d.com/sdk/download/native/"
    echo "Then copy the library to: $NATIVE_DIR/"
    echo ""
    echo "Expected filenames:"
    echo "  Linux:   libLive2DCubismCore.so"
    echo "  Windows: Live2DCubismCore.dll"
    echo "  macOS:   libLive2DCubismCore.dylib"
    exit 1
fi

echo ""
echo "=== Sample Model Setup ==="
echo ""
echo "To test with Live2D sample models (Hiyori, Haru, Mark, etc.):"
echo ""
echo "1. Download Cubism SDK sample models from:"
echo "   https://www.live2d.com/sdk/download/native/"
echo "   (included in the Cubism SDK for Native download)"
echo ""
echo "2. Copy a model directory to: $SCRIPT_DIR/demo/models/"
echo "   Example: cp -r /path/to/Samples/Hiyori $SCRIPT_DIR/demo/models/"
echo ""
echo "3. The model3.json path will be:"
echo "   $SCRIPT_DIR/demo/models/Hiyori/Hiyori.model3.json"
echo ""
echo "Setup complete!"
