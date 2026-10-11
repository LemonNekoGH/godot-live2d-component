/**
 * Demo scene for testing the Cubism SDK for Godot port.
 * Loads a Live2D model and exercises all framework features.
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
        private float _time;
        private bool _testsRun;

        public override void _Ready()
        {
            // Run math verification tests first
            GD.Print("=== Running Golden-Value Math Tests ===");
            CubismMathTests.RunAll();
            GD.Print("=== Math Tests Complete ===\n");
            _testsRun = true;

            // Set up UI
            var canvas = new CanvasLayer();
            AddChild(canvas);

            _infoLabel = new Label();
            _infoLabel.Position = new Vector2(10, 10);
            _infoLabel.AddThemeColorOverride("font_color", Colors.White);
            canvas.AddChild(_infoLabel);

            // Try to load model
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

        private void LoadLive2DModel(string path)
        {
            _modelNode = new CubismModelNode();
            AddChild(_modelNode);

            if (_modelNode.LoadModel(path))
            {
                GD.Print($"Model loaded successfully: {path}");

                // Scale model to fit viewport
                var viewSize = GetViewportRect().Size;
                var canvasH = _modelNode.Model.CanvasSizeInPixels.Y;
                if (canvasH > 0)
                {
                    var fitScale = (viewSize.Y * 0.9f) / canvasH;
                    _modelNode.Scale = new Vector2(fitScale, fitScale);
                }

                // Center model on screen
                _modelNode.Position = new Vector2(viewSize.X / 2f, viewSize.Y / 2f);

                // Start idle motion if available
                _modelNode.PlayMotionFromGroup("Idle", 0, CubismMotionPriority.PriorityIdle, true);
            }
            else
            {
                GD.PrintErr($"Failed to load model: {path}");
            }
        }

        public override void _Process(double delta)
        {
            _time += (float)delta;

            if (_modelNode?.Model != null)
            {
                // Mouse-based look-at
                var mousePos = GetViewport().GetMousePosition();
                var modelPos = _modelNode.GlobalPosition;
                var lookDir = (mousePos - modelPos) / 200f;
                _modelNode.SetLookTarget(new Vector2(
                    Mathf.Clamp(lookDir.X, -1f, 1f),
                    Mathf.Clamp(lookDir.Y, -1f, 1f)));

                _infoLabel.Text = $"Model: {_modelNode.Model.Name}\n" +
                                  $"Parameters: {_modelNode.Model.Parameters.Length}\n" +
                                  $"Parts: {_modelNode.Model.Parts.Length}\n" +
                                  $"Drawables: {_modelNode.Model.Drawables.Length}\n" +
                                  $"FPS: {Engine.GetFramesPerSecond()}\n" +
                                  $"Time: {_time:F1}s";
            }
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
                }
            }
        }
    }
}
