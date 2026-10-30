/**
 * Demo scene for testing the Cubism SDK for Godot port.
 * Loads a Live2D model and exercises all framework features.
 *
 * Environment:
 *   CUBISM_SCREENSHOT=/path/to.png   save a PNG after a few frames, then optionally quit
 *   CUBISM_QUIT_AFTER=3              quit after N seconds (0 = stay open)
 *   CUBISM_DEMO_MODE=static|motion|expression|blink|physics|pose|lipsync|lookat|all
 *   CUBISM_DRAWABLE_ONLY=N           isolate a single drawable index
 *   CUBISM_BG=green|gray|white
 */

using Godot;
using Live2D.Cubism.Rendering;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Tests;

namespace Live2D.Cubism.Demo
{
    public partial class DemoScene : Node2D
    {
        [Export] public string ModelPath { get; set; } = "";

        private CubismModelNode _modelNode;
        private Label _infoLabel;
        private ColorRect _background;
        private float _time;
        private bool _testsRun;
        private string _demoMode = "all";
        private string _lastHitAreas = "-";
        private string _screenshotPath;
        private float _quitAfter = -1f;
        private bool _screenshotTaken;
        private int _isolateDrawable = -1;
        private bool _cycleBlends;
        private bool _cycleAlphaBlends;

        public override async void _Ready()
        {
            GD.Print("=== Running Golden-Value Math Tests ===");
            CubismMathTests.RunAll();
            GD.Print("=== Math Tests Complete ===\n");
            _testsRun = true;

            var alphaDumpDirectory = OS.GetEnvironment("CUBISM_ALPHA_DUMP_DIR");
            if (!string.IsNullOrEmpty(alphaDumpDirectory))
            {
                await CubismAlphaBlendDump.WriteAll(this, alphaDumpDirectory);
                GD.Print($"[AlphaBlendDump] wrote RGBA8 images to {alphaDumpDirectory}");
                GetTree().Quit();
                return;
            }

            _demoMode = OS.GetEnvironment("CUBISM_DEMO_MODE");
            if (string.IsNullOrEmpty(_demoMode))
                _demoMode = "all";
            var envModel = OS.GetEnvironment("CUBISM_MODEL");
            if (!string.IsNullOrEmpty(envModel))
                ModelPath = envModel;
            _screenshotPath = OS.GetEnvironment("CUBISM_SCREENSHOT");
            var quitStr = OS.GetEnvironment("CUBISM_QUIT_AFTER");
            if (!string.IsNullOrEmpty(quitStr) && float.TryParse(quitStr, out var q))
                _quitAfter = q;
            var isolateStr = OS.GetEnvironment("CUBISM_DRAWABLE_ONLY");
            if (!string.IsNullOrEmpty(isolateStr) && int.TryParse(isolateStr, out var iso))
                _isolateDrawable = iso;

            _background = GetNodeOrNull<ColorRect>("Background");
            ApplyBackground(OS.GetEnvironment("CUBISM_BG"));

            var canvas = new CanvasLayer();
            AddChild(canvas);

            _infoLabel = new Label();
            _infoLabel.Position = new Vector2(10, 10);
            _infoLabel.AddThemeColorOverride("font_color", new Color(0.15f, 0.15f, 0.18f));
            var hud = OS.GetEnvironment("CUBISM_HUD");
            _infoLabel.Visible = hud != "0" && string.IsNullOrEmpty(_screenshotPath);
            canvas.AddChild(_infoLabel);

            string foundPath = null;

            if (!string.IsNullOrEmpty(ModelPath))
            {
                var projPath = ProjectSettings.GlobalizePath($"res://{ModelPath}");
                if (System.IO.File.Exists(ModelPath))
                    foundPath = System.IO.Path.GetFullPath(ModelPath);
                else if (System.IO.File.Exists(projPath))
                    foundPath = projPath;
            }

            if (foundPath == null)
            {
                var defaultPaths = new[]
                {
                    "demo/models/Hiyori/Hiyori.model3.json",
                    "demo/models/Senko/senko.model3.json",
                    "demo/models/Haru/Haru.model3.json",
                    "demo/models/Mark/Mark.model3.json",
                };

                foreach (var p in defaultPaths)
                {
                    var projectPath = ProjectSettings.GlobalizePath($"res://{p}");
                    if (System.IO.File.Exists(projectPath))
                    {
                        foundPath = projectPath;
                        break;
                    }

                    if (System.IO.File.Exists(p))
                    {
                        foundPath = System.IO.Path.GetFullPath(p);
                        break;
                    }
                }
            }

            if (foundPath != null)
            {
                LoadLive2DModel(foundPath);
            }
            else
            {
                _infoLabel.Text = "No model loaded.\n" +
                                  "Place a Live2D model in demo/models/ and set the path.\n" +
                                  "See setup_cubism_core.sh for instructions.\n\n" +
                                  "Math tests have been run - check console output.";
            }
        }

        private void ApplyBackground(string name)
        {
            if (_background == null) return;
            _background.Color = name switch
            {
                "green" => new Color(0f, 0.8f, 0f, 1f),
                "white" => new Color(1f, 1f, 1f, 1f),
                "gray" => new Color(0.92f, 0.92f, 0.94f, 1f),
                "black" => new Color(0f, 0f, 0f, 1f),
                _ => new Color(0.93f, 0.94f, 0.96f, 1f)
            };
        }

        private void LoadLive2DModel(string path)
        {
            _modelNode = new CubismModelNode();
            AddChild(_modelNode);

            if (_modelNode.LoadModel(path))
            {
                GD.Print($"Model loaded successfully: {path}");

                var viewSize = GetViewportRect().Size;
                float modelHeight = _modelNode.Model.CanvasSizeInPixels.Y;
                if (modelHeight < 1f) modelHeight = 3778f;
                var fitScale = (viewSize.Y * 0.92f) / modelHeight;
                _modelNode.Scale = new Vector2(fitScale, fitScale);
                _modelNode.Position = new Vector2(viewSize.X / 2f, viewSize.Y * 0.47f);

                float minY = float.MaxValue, maxY = float.MinValue;
                float ppu = _modelNode.Model.PixelsPerUnit > 0f ? _modelNode.Model.PixelsPerUnit : 1f;
                var vbuf = System.Array.Empty<Vector2>();
                for (int di = 0; di < _modelNode.Model.Drawables.Length; di++)
                {
                    var d = _modelNode.Model.Drawables[di];
                    if (!d.IsVisible || d.Opacity <= 0.01f) continue;
                    int vc = _modelNode.Model.FillDrawableVertexPositions(di, ref vbuf, ppu);
                    for (int vi = 0; vi < vc; vi++)
                    {
                        var py = vbuf[vi].Y;
                        if (py < minY) minY = py;
                        if (py > maxY) maxY = py;
                    }
                }
                float meshHeight = (maxY > minY) ? (maxY - minY) : modelHeight;
                if (meshHeight > 100f && meshHeight < modelHeight * 0.75f)
                {
                    fitScale = (viewSize.Y * 0.84f) / meshHeight;
                    var centerY = (minY + maxY) * 0.5f;
                    _modelNode.Scale = new Vector2(fitScale, fitScale);
                    _modelNode.Position = new Vector2(viewSize.X / 2f, viewSize.Y * 0.52f - centerY * fitScale);
                }

                if (_modelNode.ModelJson?.HitAreas != null)
                {
                    foreach (var ha in _modelNode.ModelJson.HitAreas)
                    {
                        for (int di = 0; di < _modelNode.Model.Drawables.Length; di++)
                        {
                            if (_modelNode.Model.Drawables[di].Id != ha.Id) continue;
                            int vc = _modelNode.Model.FillDrawableVertexPositions(di, ref vbuf, ppu);
                            if (vc > 0)
                            {
                                float hx0 = float.MaxValue, hx1 = float.MinValue, hy0 = float.MaxValue, hy1 = float.MinValue;
                                for (int vi = 0; vi < vc; vi++)
                                {
                                    var gp = _modelNode.ToGlobal(vbuf[vi]);
                                    if (gp.X < hx0) hx0 = gp.X;
                                    if (gp.X > hx1) hx1 = gp.X;
                                    if (gp.Y < hy0) hy0 = gp.Y;
                                    if (gp.Y > hy1) hy1 = gp.Y;
                                }
                                GD.Print($"[HitArea] {ha.Name} ({ha.Id}) global rect: x=[{hx0:F0}..{hx1:F0}], y=[{hy0:F0}..{hy1:F0}], center=({(hx0+hx1)*0.5f:F0}, {(hy0+hy1)*0.5f:F0})");
                            }
                            break;
                        }
                    }
                }
                _modelNode.IsolateDrawableIndex = _isolateDrawable;

                EnsureDemoExpressions();
                var dumpDir = OS.GetEnvironment("CUBISM_DUMP_DIR");
                if (!string.IsNullOrEmpty(dumpDir))
                {
                    CubismMotionDump.DumpAllHiyoriMotions(_modelNode, dumpDir);
                    GetTree().Quit();
                    return;
                }
                ApplyDemoMode();
            }
            else
            {
                GD.PrintErr($"Failed to load model: {path}");
            }
        }

        private void EnsureDemoExpressions()
        {
            if (_modelNode?.ExpressionController == null) return;
            if (_modelNode.ExpressionController.Expressions != null &&
                _modelNode.ExpressionController.Expressions.Length > 0)
                return;

            // Hiyori sample ships without exp3.json. Install a smile constructed
            // the same way CubismExp3Json would, so the expression controller can be demoed.
            var smile = new Live2D.Cubism.Framework.Json.CubismExp3Json
            {
                Name = "Smile",
                Type = "Live2D Expression",
                // 2s fade in expression mode so Movie Maker / 0.25s frame extracts show the
                // CubismExpressionController weight ramp; 0.35f in interactive mode.
                FadeInTime = _demoMode == "expression" ? 2.0f : 0.35f,
                FadeOutTime = 0.35f,
                Parameters = new[]
                {
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamMouthForm", Value = 1f, Blend = "Overwrite"},
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamMouthOpenY", Value = 0f, Blend = "Overwrite"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEyeLSmile", Value = 1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEyeRSmile", Value = 1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamCheek", Value = 1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamFang", Value = 1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEarLeft", Value = 1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEarRight", Value = 1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamBrowLY", Value = 0.4f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamBrowRY", Value = 0.4f, Blend = "Add"
                    },
                }
            };
            var surprised = new Live2D.Cubism.Framework.Json.CubismExp3Json
            {
                Name = "Surprised",
                Type = "Live2D Expression",
                FadeInTime = 0.35f,
                FadeOutTime = 0.35f,
                Parameters = new[]
                {
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEyeLOpen", Value = 1.3f, Blend = "Multiply"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEyeROpen", Value = 1.3f, Blend = "Multiply"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEyeLSmile", Value = 0f, Blend = "Overwrite"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEyeRSmile", Value = 0f, Blend = "Overwrite"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamCatEye", Value = 1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamShock", Value = 1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamSweatVisible", Value = 1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEarLeft", Value = -1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamEarRight", Value = -1f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamMouthForm", Value = -0.8f, Blend = "Overwrite"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamMouthOpenY", Value = 0.75f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamBrowLY", Value = 0.8f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamBrowRY", Value = 0.8f, Blend = "Add"
                    },
                }
            };
            _modelNode.ExpressionController.Expressions = new[] { smile, surprised };
        }

        private void ApplyDemoMode()
        {
            if (_modelNode == null) return;

            switch (_demoMode)
            {
                case "static":
                    _modelNode.EnableFrameworkUpdate = false;
                    break;
                case "motion":
                    _modelNode.EnableFrameworkUpdate = true;
                    PlayConfiguredMotion("Idle", 0, CubismMotionPriority.PriorityIdle, true);
                    break;
                case "expression":
                    _modelNode.EnableFrameworkUpdate = true;
                    if (_modelNode.EyeBlinkController != null)
                        _modelNode.EyeBlinkController.EnableAutoInput = false;
                    _modelNode.StartExpression(0);
                    break;
                case "blink":
                    _modelNode.EnableFrameworkUpdate = true;
                    if (_modelNode.EyeBlinkController?.AutoInput != null)
                    {
                        // Several full close/open cycles in an 8s Movie Maker clip.
                        // Interval is unscaled (Unity Idle: NextBlinkingTime -= deltaTime).
                        var auto = _modelNode.EyeBlinkController.AutoInput;
                        auto.Mean = 0.9f;
                        auto.MaximumDeviation = 0.3f;
                        auto.Reset();
                    }
                    break;
                case "physics":
                    _modelNode.EnableFrameworkUpdate = true;
                    _modelNode.PlayMotionFromGroup("Idle", 0, CubismMotionPriority.PriorityIdle, true);
                    break;
                case "pose":
                    _modelNode.EnableFrameworkUpdate = true;
                    _modelNode.PlayMotionFromGroup("TapBody", 0, CubismMotionPriority.PriorityNormal);
                    break;
                case "lipsync":
                    _modelNode.EnableFrameworkUpdate = true;
                    if (_modelNode.EyeBlinkController != null)
                        _modelNode.EyeBlinkController.EnableAutoInput = false;
                    // ParamMouthOpenY default is 0; Unity Multiply would stay 0.
                    // Override so the varying input is the mouth value itself.
                    if (_modelNode.MouthController != null)
                        _modelNode.MouthController.BlendMode = Live2D.Cubism.Framework.CubismParameterBlendMode.Override;
                    break;
                case "lookat":
                    _modelNode.EnableFrameworkUpdate = true;
                    break;
                case "blend":
                    _modelNode.EnableFrameworkUpdate = true;
                    _modelNode.PlayMotionFromGroup("Idle", 0, CubismMotionPriority.PriorityIdle, true);
                    ApplyForcedBlendFromEnv();
                    // Only cycle dest-blends when the moc has no Cubism 5 types
                    // (Hiyori is all Normal). Ren keeps HardLight / Color / Multiply.
                    if (string.IsNullOrEmpty(OS.GetEnvironment("CUBISM_FORCE_BLEND")))
                    {
                        _cycleBlends = true;
                        foreach (var d in _modelNode.Model.Drawables)
                        {
                            if (d.ColorBlend > 2)
                            {
                                _cycleBlends = false;
                                break;
                            }
                        }
                    }
                    break;
                case "alpha":
                    _modelNode.EnableFrameworkUpdate = true;
                    _modelNode.PlayMotionFromGroup("Idle", 0, CubismMotionPriority.PriorityIdle, true);
                    _cycleAlphaBlends = true;
                    break;
                case "all":
                    _modelNode.EnableFrameworkUpdate = true;
                    if (_modelNode.Model?.Name == "Hiyori")
                        _modelNode.PlayMotionFromGroup("Idle", 0, CubismMotionPriority.PriorityIdle, true);
                    break;
                default:
                    _modelNode.EnableFrameworkUpdate = false;
                    break;
            }

            if (_demoMode.StartsWith("idle") || _demoMode.StartsWith("tapbody") || _demoMode.StartsWith("motion-"))
                PlayIndexedMotionMode();
        }

        private void PlayIndexedMotionMode()
        {
            _modelNode.EnableFrameworkUpdate = true;
            if (_modelNode.EyeBlinkController != null)
                _modelNode.EyeBlinkController.EnableAutoInput = false;

            string group = "Idle";
            int index = 0;
            bool loop = false;

            if (_demoMode.StartsWith("idle"))
            {
                int.TryParse(_demoMode.Substring(4), out index);
                group = "Idle";
                loop = true;
            }
            else if (_demoMode.StartsWith("tapbody"))
            {
                int.TryParse(_demoMode.Substring(7), out index);
                group = "TapBody";
            }

            PlayConfiguredMotion(group, index, CubismMotionPriority.PriorityNormal, loop);
        }

        private void PlayConfiguredMotion(string defaultGroup, int defaultIndex,
            CubismMotionPriority priority, bool defaultLoop)
        {
            var group = OS.GetEnvironment("CUBISM_MOTION_GROUP");
            if (string.IsNullOrEmpty(group))
                group = defaultGroup;
            var index = defaultIndex;
            var indexStr = OS.GetEnvironment("CUBISM_MOTION_INDEX");
            if (!string.IsNullOrEmpty(indexStr))
                int.TryParse(indexStr, out index);
            var loop = defaultLoop;
            var loopStr = OS.GetEnvironment("CUBISM_MOTION_LOOP");
            if (loopStr == "0") loop = false;
            if (loopStr == "1") loop = true;

            if (_modelNode.EyeBlinkController != null &&
                (_demoMode.StartsWith("idle") || _demoMode.StartsWith("tapbody")))
                _modelNode.EyeBlinkController.EnableAutoInput = false;

            _modelNode.PlayMotionFromGroup(group, index, priority, loop);
            GD.Print($"Play motion group={group} index={index} loop={loop}");
        }

        private void ApplyForcedBlendFromEnv()
        {
            var forced = OS.GetEnvironment("CUBISM_FORCE_BLEND");
            if (!string.IsNullOrEmpty(forced) && int.TryParse(forced, out var id))
                _modelNode.ForceColorBlend = id;
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;

            if (_demoMode == "lookat" && _modelNode != null)
            {
                var t = _time;
                _modelNode.SetLookTarget(new Vector2(Mathf.Sin(t) * 0.7f, Mathf.Cos(t * 0.7f) * 0.4f));
            }

            if (_demoMode == "lipsync" && _modelNode != null)
            {
                _modelNode.SetMouthOpening((Mathf.Sin(_time * 8f) + 1f) * 0.5f);
            }

            if (_cycleBlends && _modelNode != null)
            {
                // 2s per mode: ColorBurn, LinearBurn, Screen, Overlay
                int[] cycle = { 7, 8, 10, 12 };
                _modelNode.ForceColorBlend = cycle[Mathf.Clamp((int)(_time / 2f), 0, cycle.Length - 1)];
            }

            if (_cycleAlphaBlends && _modelNode != null)
            {
                // 1.6s per Core mode: Over, Atop, Out, Conjoint Over, Disjoint Over.
                _modelNode.ForceAlphaBlend = Mathf.Clamp((int)(_time / 1.6f), 0, 4);
            }

            if (_demoMode == "all" && _modelNode != null)
            {
                var viewRect = GetViewportRect().Size;
                if (viewRect.X > 1f && viewRect.Y > 1f)
                {
                    var mouse = GetViewport().GetMousePosition();
                    var center = new Vector2(viewRect.X * 0.5f, viewRect.Y * 0.38f);
                    var radius = viewRect.Y * 0.35f;
                    var normX = Mathf.Clamp((mouse.X - center.X) / radius, -1f, 1f);
                    var normY = Mathf.Clamp((center.Y - mouse.Y) / radius, -1f, 1f);
                    _modelNode.SetLookTarget(new Vector2(normX, normY));
                }
            }

            if (_modelNode?.Model != null)
            {
                _infoLabel.Text = $"Model: {_modelNode.Model.Name}\n" +
                                  $"Mode: {_demoMode}\n" +
                                  $"Parameters: {_modelNode.Model.Parameters.Length}\n" +
                                  $"Parts: {_modelNode.Model.Parts.Length}\n" +
                                  $"Drawables: {_modelNode.Model.Drawables.Length}\n" +
                                  $"Hit: {_lastHitAreas}\n" +
                                  $"FPS: {Engine.GetFramesPerSecond()}\n" +
                                  $"Time: {_time:F1}s";
            }

            if (!_screenshotTaken && !string.IsNullOrEmpty(_screenshotPath) && _time >= 0.6f)
            {
                _screenshotTaken = true;
                CaptureScreenshot(_screenshotPath);
            }

            if (_quitAfter >= 0f && _time >= _quitAfter)
            {
                GetTree().Quit();
            }
        }

        private void CaptureScreenshot(string path)
        {
            var img = GetViewport().GetTexture().GetImage();
            if (img == null)
            {
                GD.PrintErr($"Screenshot failed: no viewport image");
                return;
            }
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);
            var err = img.SavePng(path);
            GD.Print($"Screenshot saved: {path} ({err})");
        }

        private void PlayTapMotion()
        {
            if (_modelNode?.ModelJson?.Motions == null) return;
            foreach (var group in new[] { "TapBody", "Taphead", "Tap" })
            {
                if (_modelNode.ModelJson.Motions.TryGetValue(group, out var list) && list != null && list.Length > 0)
                {
                    if (_modelNode.LookController != null)
                        _modelNode.LookController.BlendMode = Live2D.Cubism.Framework.CubismParameterBlendMode.Additive;
                    _modelNode.PlayMotionFromGroup(group, 0, CubismMotionPriority.PriorityNormal);
                    return;
                }
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton &&
                mouseButton.Pressed &&
                mouseButton.ButtonIndex == MouseButton.Left &&
                _modelNode != null)
            {
                var hits = _modelNode.RaycastGlobal(mouseButton.Position);
                if (hits != null && hits.Length > 0)
                {
                    var names = new string[hits.Length];
                    for (int i = 0; i < hits.Length; i++)
                        names[i] = !string.IsNullOrEmpty(hits[i].HitAreaName) ? hits[i].HitAreaName : hits[i].Drawable.Id;
                    _lastHitAreas = string.Join(", ", names);
                    GD.Print($"[Raycast] Hit at {mouseButton.Position}: {_lastHitAreas}");
                    PlayTapMotion();
                }
                else
                {
                    _lastHitAreas = "None";
                    GD.Print($"[Raycast] Miss at {mouseButton.Position}");
                }
            }

            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                switch (keyEvent.Keycode)
                {
                    case Key.Key1:
                        _modelNode?.MotionController?.StopAll();
                        if (_modelNode?.LookController != null)
                            _modelNode.LookController.BlendMode = Live2D.Cubism.Framework.CubismParameterBlendMode.Override;
                        _modelNode?.StartExpression(0);
                        break;
                    case Key.Key2:
                        _modelNode?.MotionController?.StopAll();
                        if (_modelNode?.LookController != null)
                            _modelNode.LookController.BlendMode = Live2D.Cubism.Framework.CubismParameterBlendMode.Override;
                        _modelNode?.StartExpression(1);
                        break;
                    case Key.Key3:
                        _modelNode?.StartExpression(-1);
                        break;
                    case Key.Space:
                        PlayTapMotion();
                        break;
                    case Key.F:
                        if (_modelNode != null)
                            _modelNode.EnableFrameworkUpdate = !_modelNode.EnableFrameworkUpdate;
                        break;
                    case Key.S:
                        CaptureScreenshot("/opt/cursor/artifacts/manual-screenshot.png");
                        break;
                }
            }
        }
    }
}
