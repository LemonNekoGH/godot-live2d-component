/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;
using Godot;
using Live2D.Cubism.Core;

namespace Live2D.Cubism.Framework.Physics
{
    /// <summary>
    /// Sub-rig of a physics rig. Mirrors Unity CubismPhysicsSubRig exactly.
    /// </summary>
    public class CubismPhysicsSubRig
    {
        public string Name;
        public CubismPhysicsInput[] Input;
        public CubismPhysicsInput[] OriginalInput;
        public CubismPhysicsOutput[] Output;
        public CubismPhysicsOutput[] OriginalOutput;
        public CubismPhysicsParticle[] Particles;
        public CubismPhysicsNormalization Normalization;
        public CubismPhysicsRig Rig;

        private float[] _currentRigOutput;
        private float[] _previousRigOutput;

        public void Interpolate(float weight)
        {
            for (int i = 0; i < Output.Length; ++i)
            {
                if (Output[i].Destination == null)
                {
                    Output[i].Destination = Rig.FindParameter(Output[i].DestinationId);
                    if (Output[i].Destination == null) continue;
                }

                UpdateOutputParameterValue(
                    Output[i].Destination,
                    ref Output[i].Destination.Value,
                    _previousRigOutput[i] * (1 - weight) + _currentRigOutput[i] * weight,
                    ref Output[i]);
            }
        }

        private void UpdateOutputParameterValue(
            CubismParameter parameter,
            ref float parameterValue,
            float translation,
            ref CubismPhysicsOutput output)
        {
            var outputScale = output.GetScale();
            var value = translation * outputScale;

            if (value < parameter.MinimumValue)
            {
                if (value < output.ValueBelowMinimum)
                    output.ValueBelowMinimum = value;
                value = parameter.MinimumValue;
            }
            else if (value > parameter.MaximumValue)
            {
                if (value > output.ValueExceededMaximum)
                    output.ValueExceededMaximum = value;
                value = parameter.MaximumValue;
            }

            var weight = output.Weight / CubismPhysics.MaximumWeight;
            if (weight >= 1.0f)
            {
                parameterValue = value;
            }
            else
            {
                value = (parameterValue * (1.0f - weight)) + (value * weight);
                parameterValue = value;
            }
        }

        private void UpdateParticles(
            CubismPhysicsParticle[] strand,
            Vector2 totalTranslation,
            float totalAngle,
            Vector2 wind,
            float thresholdValue,
            float deltaTime)
        {
            strand[0].Position = totalTranslation;

            var totalRadian = CubismPhysicsMath.DegreesToRadian(totalAngle);
            var currentGravity = CubismPhysicsMath.RadianToDirection(totalRadian);
            currentGravity = currentGravity.Normalized();

            for (var i = 1; i < strand.Length; ++i)
            {
                strand[i].Force = (currentGravity * strand[i].Acceleration) + wind;
                strand[i].LastPosition = strand[i].Position;

                var delay = strand[i].Delay * deltaTime * 30.0f;

                var direction = strand[i].Position - strand[i - 1].Position;
                var radian = CubismPhysicsMath.DirectionToRadian(strand[i].LastGravity, currentGravity)
                             / CubismPhysics.AirResistance;

                direction.X = (Mathf.Cos(radian) * direction.X) - (direction.Y * Mathf.Sin(radian));
                direction.Y = (Mathf.Sin(radian) * direction.X) + (direction.Y * Mathf.Cos(radian));

                strand[i].Position = strand[i - 1].Position + direction;

                var velocity = strand[i].Velocity * delay;
                var force = strand[i].Force * delay * delay;
                strand[i].Position += velocity + force;

                var newDirection = (strand[i].Position - strand[i - 1].Position).Normalized();
                strand[i].Position = strand[i - 1].Position + newDirection * strand[i].Radius;

                if (Mathf.Abs(strand[i].Position.X) < thresholdValue)
                    strand[i].Position = new Vector2(0.0f, strand[i].Position.Y);

                if (delay != 0.0f)
                {
                    strand[i].Velocity =
                        ((strand[i].Position - strand[i].LastPosition) / delay) * strand[i].Mobility;
                }

                strand[i].Force = Vector2.Zero;
                strand[i].LastGravity = currentGravity;
            }
        }

        private void UpdateParticlesForStabilization(
            CubismPhysicsParticle[] strand,
            Vector2 totalTranslation,
            float totalAngle,
            Vector2 wind,
            float thresholdValue)
        {
            strand[0].Position = totalTranslation;

            var totalRadian = CubismPhysicsMath.DegreesToRadian(totalAngle);
            var currentGravity = CubismPhysicsMath.RadianToDirection(totalRadian);
            currentGravity = currentGravity.Normalized();

            for (var i = 1; i < strand.Length; ++i)
            {
                strand[i].Force = (currentGravity * strand[i].Acceleration) + wind;
                strand[i].LastPosition = strand[i].Position;
                strand[i].Velocity = Vector2.Zero;
                var force = strand[i].Force.Normalized();
                strand[i].Position = strand[i - 1].Position + force * strand[i].Radius;

                if (Mathf.Abs(strand[i].Position.X) < thresholdValue)
                    strand[i].Position = new Vector2(0.0f, strand[i].Position.Y);

                strand[i].Force = Vector2.Zero;
                strand[i].LastGravity = currentGravity;
            }
        }

        public void Initialize()
        {
            var strand = Particles;
            strand[0].InitialPosition = Vector2.Zero;
            strand[0].LastPosition = strand[0].InitialPosition;
            strand[0].LastGravity = Rig.Gravity;
            strand[0].LastGravity = new Vector2(strand[0].LastGravity.X, strand[0].LastGravity.Y * -1.0f);

            for (var i = 1; i < strand.Length; ++i)
            {
                var radius = new Vector2(0, strand[i].Radius);
                strand[i].InitialPosition = strand[i - 1].InitialPosition + radius;
                strand[i].Position = strand[i].InitialPosition;
                strand[i].LastPosition = strand[i].InitialPosition;
                strand[i].LastGravity = Rig.Gravity;
                strand[i].LastGravity = new Vector2(strand[i].LastGravity.X, strand[i].LastGravity.Y * -1.0f);
            }

            OriginalInput = new CubismPhysicsInput[Input.Length];
            Array.Copy(Input, OriginalInput, Input.Length);

            _previousRigOutput = new float[Output.Length];
            _currentRigOutput = new float[Output.Length];

            OriginalOutput = new CubismPhysicsOutput[Output.Length];
            Array.Copy(Output, OriginalOutput, Output.Length);
        }

        public void Evaluate(float deltaTime)
        {
            var totalAngle = 0.0f;
            var totalTranslation = Vector2.Zero;

            for (var i = 0; i < Input.Length; ++i)
            {
                var weight = Input[i].Weight / CubismPhysics.MaximumWeight;

                if (Input[i].Source == null)
                {
                    Input[i].Source = Rig.FindParameter(Input[i].SourceId);
                    Input[i].SourceIndex = Rig.FindParameterIndex(Input[i].SourceId);
                }

                var parameter = Input[i].Source;
                Input[i].GetNormalizedParameterValue(
                    ref totalTranslation, ref totalAngle,
                    parameter, ref Rig.ParametersCache[Input[i].SourceIndex],
                    Normalization, weight);
            }

            var radAngle = CubismPhysicsMath.DegreesToRadian(-totalAngle);
            totalTranslation.X = (totalTranslation.X * Mathf.Cos(radAngle) - totalTranslation.Y * Mathf.Sin(radAngle));
            totalTranslation.Y = (totalTranslation.X * Mathf.Sin(radAngle) + totalTranslation.Y * Mathf.Cos(radAngle));

            UpdateParticles(Particles, totalTranslation, totalAngle, Rig.Wind,
                CubismPhysics.MovementThreshold * Normalization.Position.Maximum, deltaTime);

            for (var i = 0; i < Output.Length; ++i)
            {
                _previousRigOutput[i] = _currentRigOutput[i];

                if (Output[i].Destination == null)
                {
                    Output[i].Destination = Rig.FindParameter(Output[i].DestinationId);
                    if (Output[i].Destination == null) continue;
                }

                var particleIndex = Output[i].ParticleIndex;
                if (particleIndex < 1 || particleIndex >= Particles.Length) continue;

                Output[i].DestinationIndex = Rig.FindParameterIndex(Output[i].DestinationId);

                var translation = Particles[particleIndex].Position - Particles[particleIndex - 1].Position;
                var outputValue = Output[i].GetValue(translation, Particles, particleIndex, Rig.Gravity);
                _currentRigOutput[i] = outputValue;

                UpdateOutputParameterValue(
                    Output[i].Destination,
                    ref Rig.ParametersCache[Output[i].DestinationIndex],
                    outputValue, ref Output[i]);
            }
        }

        public void Stabilization()
        {
            var totalAngle = 0.0f;
            var totalTranslation = Vector2.Zero;

            for (var i = 0; i < Input.Length; ++i)
            {
                var weight = Input[i].Weight / CubismPhysics.MaximumWeight;
                if (Input[i].Source == null)
                    Input[i].Source = Rig.FindParameter(Input[i].SourceId);

                var index = Rig.FindParameterIndex(Input[i].SourceId);
                var parameter = Input[i].Source;
                Input[i].GetNormalizedParameterValue(
                    ref totalTranslation, ref totalAngle,
                    parameter, ref Input[i].Source.Value,
                    Normalization, weight);
                Rig.ParametersCache[index] = Input[i].Source.Value;
            }

            var radAngle = CubismPhysicsMath.DegreesToRadian(-totalAngle);
            totalTranslation.X = (totalTranslation.X * Mathf.Cos(radAngle) - totalTranslation.Y * Mathf.Sin(radAngle));
            totalTranslation.Y = (totalTranslation.X * Mathf.Sin(radAngle) + totalTranslation.Y * Mathf.Cos(radAngle));

            UpdateParticlesForStabilization(Particles, totalTranslation, totalAngle, Rig.Wind,
                CubismPhysics.MovementThreshold * Normalization.Position.Maximum);

            for (var i = 0; i < Output.Length; ++i)
            {
                _previousRigOutput[i] = _currentRigOutput[i];
                if (Output[i].Destination == null)
                {
                    Output[i].Destination = Rig.FindParameter(Output[i].DestinationId);
                    if (Output[i].Destination == null) continue;
                }

                var particleIndex = Output[i].ParticleIndex;
                if (particleIndex < 1 || particleIndex >= Particles.Length) continue;

                var index = Rig.FindParameterIndex(Output[i].DestinationId);
                var translation = Particles[particleIndex].Position - Particles[particleIndex - 1].Position;
                var outputValue = Output[i].GetValue(translation, Particles, particleIndex, Rig.Gravity);
                _currentRigOutput[i] = outputValue;
                _previousRigOutput[i] = outputValue;

                UpdateOutputParameterValue(Output[i].Destination, ref Output[i].Destination.Value,
                    outputValue, ref Output[i]);
                Rig.ParametersCache[index] = Output[i].Destination.Value;
            }
        }
    }
}
