/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using System.IO;
using Godot;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.Expression;
using Live2D.Cubism.Framework.HarmonicMotion;
using Live2D.Cubism.Framework.Json;
using Live2D.Cubism.Framework.LookAt;
using Live2D.Cubism.Framework.Motion;
using Live2D.Cubism.Framework.MouthMovement;
using Live2D.Cubism.Framework.Physics;
using Live2D.Cubism.Framework.Pose;
using Live2D.Cubism.Framework.Raycasting;
using Live2D.Cubism.Framework.UserData;

namespace Live2D.Cubism.Rendering
{
    /// <summary>
    /// Main Godot node for a Live2D Cubism model.
    /// Integrates Core, Framework, and Rendering into Godot's scene tree.
    ///
    /// Update order (mirrors Unity CubismUpdateController execution order):
    ///   1. CubismFadeController (motion fade)           - order 100
    ///   2. CubismParameterStore.SaveParameters()        - order 150
    ///   3. CubismPoseController                         - order 200
    ///   4. CubismExpressionController                   - order 300
    ///   5. CubismEyeBlinkController                     - order 400
    ///   6. CubismMouthController                        - order 500
    ///   7. CubismHarmonicMotionController               - order 600
    ///   8. CubismLookController                         - order 700
    ///   9. CubismPhysicsController                      - order 800
    ///  10. CubismModel.UpdateModel() (Core update)
    ///  11. CubismParameterStore.RestoreParameters()     - after Core read-back
    ///  12. CubismRenderController (mesh rebuild)        - order 10000
    ///
    /// Restore after Core is required so Multiply blink/mouth apply to the saved
    /// base (typically 1 / motion) rather than last frame's closed 0.
    /// </summary>
    public partial class CubismModelNode : Node2D
    {
        [Export] public string ModelJsonPath { get; set; }

        public CubismModel Model { get; private set; }
        public CubismModel3Json ModelJson { get; private set; }
        public CubismMotionController MotionController { get; private set; }
        public CubismExpressionController ExpressionController { get; private set; }
        public CubismEyeBlinkController EyeBlinkController { get; private set; }
        public CubismMouthController MouthController { get; private set; }
        public CubismLookController LookController { get; private set; }
        public CubismPoseController PoseController { get; private set; }
        public CubismPhysicsRig PhysicsRig { get; private set; }
        public CubismHarmonicMotionController HarmonicMotionController { get; private set; }
        public CubismRaycaster Raycaster { get; private set; }
        public CubismUserDataController UserDataController { get; private set; }

        public CubismParameterStore ParameterStore { get; private set; }

        private CubismDrawableRenderer[] _drawableRenderers;
        private Texture2D[] _textures;
        private CubismMaskController _maskController;
        private bool _initialized;
        public int IsolateDrawableIndex { get; set; } = -1;

        public override void _Ready()
        {
            if (!string.IsNullOrEmpty(ModelJsonPath))
            {
                LoadModel(ModelJsonPath);
            }
        }

        /// <summary>
        /// Loads a model from a model3.json path.
        /// </summary>
        public bool LoadModel(string modelJsonPath)
        {
            try
            {
                ModelJson = CubismModel3Json.LoadAtPath(modelJsonPath);
                if (ModelJson == null)
                {
                    GD.PrintErr($"[CubismModelNode] Failed to load model3.json: {modelJsonPath}");
                    return false;
                }

                Model = ModelJson.ToModel();
                if (Model == null)
                {
                    GD.PrintErr("[CubismModelNode] Failed to create model from moc.");
                    return false;
                }

                Name = Model.Name;

                _textures = ModelJson.LoadTextures();
                InitializeRenderers();
                InitializeFramework();
                _initialized = true;

                // Force initial Core update so drawables have geometry
                Model.UpdateModel();
                _maskController?.UpdateMasks();
                UpdateRenderers();

                LogBlendHistogram();

                GD.Print($"[CubismModelNode] Loaded model: {Model.Name} " +
                         $"({Model.Parameters.Length} params, {Model.Parts.Length} parts, " +
                         $"{Model.Drawables.Length} drawables, " +
                         $"canvas={Model.CanvasSizeInPixels}, PPU={Model.PixelsPerUnit}, " +
                         $"offscreens={Model.OffscreenCount})");

                return true;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[CubismModelNode] Error loading model: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        private void InitializeRenderers()
        {
            // Clear existing children
            foreach (var child in GetChildren())
            {
                child.QueueFree();
            }

            _drawableRenderers = new CubismDrawableRenderer[Model.Drawables.Length];

            for (int i = 0; i < Model.Drawables.Length; i++)
            {
                var drawable = Model.Drawables[i];
                var renderer = new CubismDrawableRenderer();
                renderer.Name = $"Drawable_{drawable.Id}";

                Texture2D texture = null;
                if (_textures != null && drawable.TextureIndex >= 0 && drawable.TextureIndex < _textures.Length)
                    texture = _textures[drawable.TextureIndex];

                AddChild(renderer);
                renderer.Initialize(Model, i, texture);
                _drawableRenderers[i] = renderer;
            }

            _maskController = new CubismMaskController();
            _maskController.Name = "MaskController";
            AddChild(_maskController);
            _maskController.Initialize(Model, _drawableRenderers, _textures);
        }

        private void InitializeFramework()
        {
            // Motion controller
            MotionController = new CubismMotionController();
            MotionController.SetModel(Model.Parameters, Model.Parts);

            // Expression controller
            ExpressionController = new CubismExpressionController();
            ExpressionController.SetParameters(Model.Parameters);
            if (ModelJson.FileReferences?.Expressions != null)
            {
                var expJsons = new CubismExp3Json[ModelJson.FileReferences.Expressions.Length];
                for (int i = 0; i < ModelJson.FileReferences.Expressions.Length; i++)
                {
                    var expPath = ModelJson.ResolvePath(ModelJson.FileReferences.Expressions[i].File);
                    if (expPath != null && File.Exists(expPath))
                        expJsons[i] = CubismExp3Json.LoadAtPath(expPath);
                }
                ExpressionController.Expressions = expJsons;
            }

            // Eye blink
            EyeBlinkController = new CubismEyeBlinkController();
            var blinkIds = ModelJson.GetGroupIds("EyeBlink");
            EyeBlinkController.EyeBlinkParameterIds = blinkIds;
            EyeBlinkController.SetParameters(Model.Parameters);

            // Mouth
            MouthController = new CubismMouthController();
            var lipSyncIds = ModelJson.GetGroupIds("LipSync");
            MouthController.MouthParameterIds = lipSyncIds;
            MouthController.SetParameters(Model.Parameters);

            MotionController.SetEffectIds(blinkIds, lipSyncIds);

            // Look-at — default bindings match Unity CubismLookParameter on official models
            LookController = new CubismLookController();
            LookController.SetParameters(Model.Parameters);
            LookController.LookParameters = new[]
            {
                new CubismLookParameter { ParameterId = "ParamAngleX", Axis = CubismLookAxis.X, Factor = 30f },
                new CubismLookParameter { ParameterId = "ParamAngleY", Axis = CubismLookAxis.Y, Factor = 30f },
                new CubismLookParameter { ParameterId = "ParamAngleZ", Axis = CubismLookAxis.Z, Factor = 30f },
                new CubismLookParameter { ParameterId = "ParamEyeBallX", Axis = CubismLookAxis.X, Factor = 1f },
                new CubismLookParameter { ParameterId = "ParamEyeBallY", Axis = CubismLookAxis.Y, Factor = 1f },
                new CubismLookParameter { ParameterId = "ParamBodyAngleX", Axis = CubismLookAxis.X, Factor = 10f },
            };
            LookController.BlendMode = CubismParameterBlendMode.Override;

            // Pose
            PoseController = new CubismPoseController();
            var poseJsonStr = ModelJson.LoadPose3Json();
            if (poseJsonStr != null)
            {
                var poseJson = CubismPose3Json.LoadFrom(poseJsonStr);
                PoseController.Initialize(Model.Parts, poseJson);
            }

            // Physics
            var physicsJsonStr = ModelJson.LoadPhysics3Json();
            if (physicsJsonStr != null)
            {
                var physics3 = CubismPhysics3Json.LoadFrom(physicsJsonStr);
                if (physics3 != null)
                    InitializePhysics(physics3);
            }

            // Harmonic motion
            HarmonicMotionController = new CubismHarmonicMotionController();
            HarmonicMotionController.SetParameters(Model.Parameters);

            // User data
            UserDataController = new CubismUserDataController();
            var userDataStr = ModelJson.LoadUserData3Json();
            if (userDataStr != null)
            {
                var userData3 = CubismUserData3Json.LoadFrom(userDataStr);
                UserDataController.Initialize(userData3);
            }

            // Raycaster
            Raycaster = new CubismRaycaster();
            if (ModelJson.HitAreas != null)
            {
                var hitIds = new string[ModelJson.HitAreas.Length];
                for (int i = 0; i < ModelJson.HitAreas.Length; i++)
                    hitIds[i] = ModelJson.HitAreas[i].Id;
                Raycaster.Initialize(Model, hitIds);
            }

            // Parameter store: save the moc defaults so Multiply blink/mouth
            // and Additive expressions apply to a stable base each frame.
            ParameterStore = new CubismParameterStore();
            ParameterStore.Initialize(Model.Parameters, Model.Parts);
        }

        private void InitializePhysics(CubismPhysics3Json physics3)
        {
            if (physics3.PhysicsSettings == null) return;

            PhysicsRig = new CubismPhysicsRig();
            PhysicsRig.SubRigs = new CubismPhysicsSubRig[physics3.PhysicsSettings.Length];

            if (physics3.Meta?.EffectiveForces != null)
            {
                if (physics3.Meta.EffectiveForces.Gravity != null)
                    PhysicsRig.Gravity = new Vector2(
                        physics3.Meta.EffectiveForces.Gravity.X,
                        physics3.Meta.EffectiveForces.Gravity.Y);
                if (physics3.Meta.EffectiveForces.Wind != null)
                    PhysicsRig.Wind = new Vector2(
                        physics3.Meta.EffectiveForces.Wind.X,
                        physics3.Meta.EffectiveForces.Wind.Y);
            }

            for (int s = 0; s < physics3.PhysicsSettings.Length; s++)
            {
                var setting = physics3.PhysicsSettings[s];
                var subRig = new CubismPhysicsSubRig();
                subRig.Name = setting.Id;

                // Inputs
                subRig.Input = new CubismPhysicsInput[setting.Input.Length];
                for (int i = 0; i < setting.Input.Length; i++)
                {
                    subRig.Input[i] = new CubismPhysicsInput
                    {
                        SourceId = setting.Input[i].Source.Id,
                        Weight = setting.Input[i].Weight,
                        IsInverted = setting.Input[i].Reflect,
                        SourceComponent = ParseSourceComponent(setting.Input[i].Type)
                    };
                }

                // Outputs
                subRig.Output = new CubismPhysicsOutput[setting.Output.Length];
                for (int i = 0; i < setting.Output.Length; i++)
                {
                    subRig.Output[i] = new CubismPhysicsOutput
                    {
                        DestinationId = setting.Output[i].Destination.Id,
                        ParticleIndex = setting.Output[i].VertexIndex,
                        AngleScale = setting.Output[i].Scale,
                        Weight = setting.Output[i].Weight,
                        IsInverted = setting.Output[i].Reflect,
                        SourceComponent = ParseSourceComponent(setting.Output[i].Type),
                        TranslationScale = new Vector2(setting.Output[i].Scale, setting.Output[i].Scale)
                    };
                }

                // Particles (vertices)
                subRig.Particles = new CubismPhysicsParticle[setting.Vertices.Length];
                for (int i = 0; i < setting.Vertices.Length; i++)
                {
                    subRig.Particles[i] = new CubismPhysicsParticle
                    {
                        Mobility = setting.Vertices[i].Mobility,
                        Delay = setting.Vertices[i].Delay,
                        Acceleration = setting.Vertices[i].Acceleration,
                        Radius = setting.Vertices[i].Radius,
                        Position = new Vector2(
                            setting.Vertices[i].Position?.X ?? 0,
                            setting.Vertices[i].Position?.Y ?? 0)
                    };
                }

                // Normalization
                if (setting.Normalization != null)
                {
                    subRig.Normalization = new CubismPhysicsNormalization
                    {
                        Position = new CubismPhysicsNormalizationTuplet
                        {
                            Minimum = setting.Normalization.Position?.Minimum ?? -10f,
                            Default = setting.Normalization.Position?.Default ?? 0f,
                            Maximum = setting.Normalization.Position?.Maximum ?? 10f
                        },
                        Angle = new CubismPhysicsNormalizationTuplet
                        {
                            Minimum = setting.Normalization.Angle?.Minimum ?? -10f,
                            Default = setting.Normalization.Angle?.Default ?? 0f,
                            Maximum = setting.Normalization.Angle?.Maximum ?? 10f
                        }
                    };
                }

                PhysicsRig.SubRigs[s] = subRig;
            }

            PhysicsRig.SetParameters(Model.Parameters);
            PhysicsRig.Initialize();
            PhysicsRig.Stabilization();
        }

        private static CubismPhysicsSourceComponent ParseSourceComponent(string type)
        {
            return type switch
            {
                "X" => CubismPhysicsSourceComponent.X,
                "Y" => CubismPhysicsSourceComponent.Y,
                "Angle" => CubismPhysicsSourceComponent.Angle,
                _ => CubismPhysicsSourceComponent.X
            };
        }

        public bool EnableFrameworkUpdate { get; set; } = false;
        public bool EnableAsyncUpdate { get; set; } = false;
        /// <summary>When &gt;= 0, every drawable uses this Core color-blend id (demo / tests).</summary>
        public int ForceColorBlend { get; set; } = -1;

        public override void _Process(double delta)
        {
            if (!_initialized || Model == null) return;

            var dt = (float)delta;

            // Framework update in execution order (mirrors Unity CubismUpdateController)
            if (EnableFrameworkUpdate)
            {
                // Start from last saved base (Unity restores after Core on the
                // previous frame, so LateUpdate already sees restored values).
                ParameterStore?.RestoreParameters();

                MotionController?.Update(dt);
                ParameterStore?.SaveParameters();
                PoseController?.Update(dt);
                ExpressionController?.Update(dt);
                EyeBlinkController?.Update(dt);
                MouthController?.Update(dt);
                HarmonicMotionController?.Update(dt);
                LookController?.Update(dt);
                PhysicsRig?.Evaluate(dt);
            }

            // Core update (writes params/parts → Core → reads back drawables)
            if (EnableAsyncUpdate && Model.Taskable != null)
            {
                Model.Taskable.ExecuteAsync = true;
                Model.Taskable.StartUpdate();
                Model.Taskable.WaitForCompletion();
            }
            else
            {
                Model.UpdateModel();
            }

            // Unity CubismModel.OnModelUpdate: after TryReadParameters, restore
            // so next frame's Multiply/Add start from the saved (pre-blink) base.
            // Drawables already captured the blinked/mouthed pose for this frame.
            if (EnableFrameworkUpdate)
                ParameterStore?.RestoreParameters();

            // Masks then renderers (order 10000)
            _maskController?.UpdateMasks();
            UpdateRenderers();
        }

        private void LogBlendHistogram()
        {
            var counts = new System.Collections.Generic.Dictionary<int, int>();
            foreach (var d in Model.Drawables)
            {
                counts.TryGetValue(d.ColorBlend, out var n);
                counts[d.ColorBlend] = n + 1;
            }
            var parts = new System.Text.StringBuilder("[BlendModes]");
            foreach (var kv in counts)
                parts.Append($" {kv.Key}={kv.Value}");
            GD.Print(parts.ToString());
        }

        private void UpdateRenderers()
        {
            if (_drawableRenderers == null) return;

            for (int i = 0; i < _drawableRenderers.Length; i++)
            {
                if (IsolateDrawableIndex >= 0 && i != IsolateDrawableIndex)
                {
                    if (_drawableRenderers[i] != null)
                        _drawableRenderers[i].Visible = false;
                    continue;
                }

                if (_drawableRenderers[i] != null)
                    _drawableRenderers[i].ColorBlendOverride = ForceColorBlend;
                _drawableRenderers[i]?.UpdateRender();
            }
        }

        /// <summary>
        /// Plays a motion from a file path.
        /// </summary>
        public CubismPlayingMotion PlayMotion(string motionFilePath,
            CubismMotionPriority priority = CubismMotionPriority.PriorityNormal,
            bool loop = false,
            float fadeInOverride = -1f,
            float fadeOutOverride = -1f)
        {
            var motion = CubismMotion3Json.LoadAtPath(motionFilePath);
            return MotionController?.PlayMotion(motion, priority, loop, fadeInOverride, fadeOutOverride);
        }

        /// <summary>
        /// Plays a motion from the model's motion groups, using the group FadeIn/Out
        /// times from model3.json (Native / Unity importer overrides).
        /// </summary>
        public CubismPlayingMotion PlayMotionFromGroup(string groupName, int index,
            CubismMotionPriority priority = CubismMotionPriority.PriorityNormal,
            bool loop = false)
        {
            if (ModelJson?.Motions == null || !ModelJson.Motions.ContainsKey(groupName))
                return null;

            var motionRefs = ModelJson.Motions[groupName];
            if (index < 0 || index >= motionRefs.Length) return null;

            var motionPath = ModelJson.ResolvePath(motionRefs[index].File);
            if (motionPath == null) return null;

            return PlayMotion(motionPath, priority, loop,
                motionRefs[index].FadeInTime, motionRefs[index].FadeOutTime);
        }

        /// <summary>
        /// Starts an expression by index.
        /// </summary>
        public void StartExpression(int index)
        {
            ExpressionController?.StartExpression(index);
        }

        /// <summary>
        /// Sets the look-at target position.
        /// </summary>
        public void SetLookTarget(Vector2 position)
        {
            if (LookController != null)
                LookController.TargetPosition = new Vector3(position.X, position.Y, 0);
        }

        /// <summary>
        /// Sets the mouth opening value (0–1).
        /// </summary>
        public void SetMouthOpening(float value)
        {
            if (MouthController != null)
                MouthController.MouthOpening = value;
        }

        public override void _ExitTree()
        {
            Model?.Dispose();
            Model = null;
        }
    }
}
