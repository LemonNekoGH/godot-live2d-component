/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using Godot;
using Live2D.Cubism.Core;

namespace Live2D.Cubism.Framework.Physics
{
    public enum CubismPhysicsSourceComponent
    {
        X,
        Y,
        Angle
    }

    public struct CubismPhysicsNormalizationTuplet
    {
        public float Maximum;
        public float Minimum;
        public float Default;
    }

    public struct CubismPhysicsNormalization
    {
        public CubismPhysicsNormalizationTuplet Position;
        public CubismPhysicsNormalizationTuplet Angle;
    }

    public struct CubismPhysicsParticle
    {
        public Vector2 InitialPosition;
        public float Mobility;
        public float Delay;
        public float Acceleration;
        public float Radius;
        public Vector2 Position;
        public Vector2 LastPosition;
        public Vector2 LastGravity;
        public Vector2 Force;
        public Vector2 Velocity;
    }

    public struct CubismPhysicsInput
    {
        public string SourceId;
        public Vector2 ScaleOfTranslation;
        public float AngleScale;
        public float Weight;
        public CubismPhysicsSourceComponent SourceComponent;
        public bool IsInverted;
        public CubismParameter Source;
        public int SourceIndex;

        public void GetNormalizedParameterValue(
            ref Vector2 targetTranslation,
            ref float targetAngle,
            CubismParameter parameter,
            ref float parameterValue,
            CubismPhysicsNormalization normalization,
            float weight)
        {
            switch (SourceComponent)
            {
                case CubismPhysicsSourceComponent.X:
                    targetTranslation.X += CubismPhysicsMath.Normalize(
                        parameter, ref parameterValue,
                        normalization.Position.Minimum,
                        normalization.Position.Maximum,
                        normalization.Position.Default,
                        IsInverted) * weight;
                    break;

                case CubismPhysicsSourceComponent.Y:
                    targetTranslation.Y += CubismPhysicsMath.Normalize(
                        parameter, ref parameterValue,
                        normalization.Position.Minimum,
                        normalization.Position.Maximum,
                        normalization.Position.Default,
                        IsInverted) * weight;
                    break;

                case CubismPhysicsSourceComponent.Angle:
                    targetAngle += CubismPhysicsMath.Normalize(
                        parameter, ref parameterValue,
                        normalization.Angle.Minimum,
                        normalization.Angle.Maximum,
                        normalization.Angle.Default,
                        IsInverted) * weight;
                    break;
            }
        }
    }

    public struct CubismPhysicsOutput
    {
        public string DestinationId;
        public int ParticleIndex;
        public Vector2 TranslationScale;
        public float AngleScale;
        public float Weight;
        public CubismPhysicsSourceComponent SourceComponent;
        public bool IsInverted;
        public float ValueBelowMinimum;
        public float ValueExceededMaximum;
        public CubismParameter Destination;
        public int DestinationIndex;

        public float GetValue(
            Vector2 translation,
            CubismPhysicsParticle[] particles,
            int particleIndex,
            Vector2 gravity)
        {
            float outputValue;
            switch (SourceComponent)
            {
                case CubismPhysicsSourceComponent.X:
                    outputValue = translation.X;
                    break;
                case CubismPhysicsSourceComponent.Y:
                    outputValue = translation.Y;
                    break;
                case CubismPhysicsSourceComponent.Angle:
                {
                    Vector2 parentGravity;
                    if (CubismPhysics.UseAngleCorrection)
                    {
                        if (particleIndex < 2)
                        {
                            parentGravity = gravity;
                            parentGravity.Y *= -1.0f;
                        }
                        else
                        {
                            parentGravity = particles[particleIndex - 1].Position -
                                            particles[particleIndex - 2].Position;
                        }
                    }
                    else
                    {
                        parentGravity = gravity;
                        parentGravity.Y *= -1.0f;
                    }
                    outputValue = CubismPhysicsMath.DirectionToRadian(parentGravity, translation);
                    break;
                }
                default:
                    outputValue = 0f;
                    break;
            }

            if (IsInverted)
                outputValue *= -1.0f;

            return outputValue;
        }

        public float GetScale()
        {
            return SourceComponent switch
            {
                CubismPhysicsSourceComponent.X => TranslationScale.X,
                CubismPhysicsSourceComponent.Y => TranslationScale.Y,
                CubismPhysicsSourceComponent.Angle => AngleScale,
                _ => 1.0f,
            };
        }
    }
}
