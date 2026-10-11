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
        private string _demoMode = "static";
        private string _screenshotPath;
        private float _quitAfter = -1f;
        private bool _screenshotTaken;
        private int _isolateDrawable = -1;
        private CubismOffscreenRenderer _offscreen;

        public override void _Ready()
        {
            GD.Print("=== Running Golden-Value Math Tests ===");
            CubismMathTests.RunAll();
            GD.Print("=== Math Tests Complete ===\n");
            _testsRun = true;

            _demoMode = OS.GetEnvironment("CUBISM_DEMO_MODE");
            if (string.IsNullOrEmpty(_demoMode))
                _demoMode = "static";
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

            if (!string.IsNullOrEmpty(ModelPath) && System.IO.File.Exists(ModelPath))
            {
                foundPath = ModelPath;
            }
            else
            {
                var defaultPaths = new[]
                {
                    "demo/models/Hiyori/Hiyori.model3.json",
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
                float modelHeight = 3778f;
                var fitScale = (viewSize.Y * 0.92f) / modelHeight;
                _modelNode.Scale = new Vector2(fitScale, fitScale);
                _modelNode.Position = new Vector2(viewSize.X / 2f, viewSize.Y * 0.47f);
                _modelNode.IsolateDrawableIndex = _isolateDrawable;

                EnsureDemoExpressions();
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
                Type = "Live2D Expression",
                // 2s fade so Movie Maker / 0.25s frame extracts show the
                // CubismExpressionController weight ramp, not a single-step snap.
                FadeInTime = 2.0f,
                FadeOutTime = 0.4f,
                Parameters = new[]
                {
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamMouthForm", Value = 1f, Blend = "Add"
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
                        Id = "ParamBrowLY", Value = 0.4f, Blend = "Add"
                    },
                    new Live2D.Cubism.Framework.Json.CubismExp3Json.SerializableExpressionParameter
                    {
                        Id = "ParamBrowRY", Value = 0.4f, Blend = "Add"
                    },
                }
            };
            _modelNode.ExpressionController.Expressions = new[] { smile };
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
                    break;
                case "offscreen":
                    _modelNode.EnableFrameworkUpdate = true;
                    _modelNode.PlayMotionFromGroup("Idle", 0, CubismMotionPriority.PriorityIdle, true);
                    EnableOffscreenPass();
                    break;
                case "async":
                    _modelNode.EnableFrameworkUpdate = true;
                    _modelNode.EnableAsyncUpdate = true;
                    _modelNode.PlayMotionFromGroup("Idle", 0, CubismMotionPriority.PriorityIdle, true);
                    break;
                case "all":
                    _modelNode.EnableFrameworkUpdate = true;
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

        private void EnableOffscreenPass()
        {
            if (_modelNode == null || _offscreen != null) return;
            _offscreen = new CubismOffscreenRenderer();
            AddChild(_offscreen);
            var size = GetViewportRect().Size;
            _offscreen.Initialize(_modelNode, new Vector2I((int)size.X, (int)size.Y));
            _offscreen.ReparentModel(_modelNode);
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

            if (_modelNode?.Model != null)
            {
                _infoLabel.Text = $"Model: {_modelNode.Model.Name}\n" +
                                  $"Mode: {_demoMode}\n" +
                                  $"Parameters: {_modelNode.Model.Parameters.Length}\n" +
                                  $"Parts: {_modelNode.Model.Parts.Length}\n" +
                                  $"Drawables: {_modelNode.Model.Drawables.Length}\n" +
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

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey keyEvent && keyEvent.Pressed)
            {
                switch (keyEvent.Keycode)
                {
                    case Key.Key1:
                        _modelNode?.StartExpression(0);
                        break;
                    case Key.Key2:
                        _modelNode?.StartExpression(1);
                        break;
                    case Key.Key3:
                        _modelNode?.StartExpression(2);
                        break;
                    case Key.Space:
                        _modelNode?.PlayMotionFromGroup("TapBody", 0, CubismMotionPriority.PriorityNormal);
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
