/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System;

namespace Live2D.Cubism.Framework
{
    /// <summary>
    /// Automatic eye-blink timing. Mirrors Unity CubismAutoEyeBlinkInput exactly:
    ///
    /// - UserTimeSeconds accumulates <c>deltaTime * Timescale</c> (default Timescale = 10).
    /// - Closing / Closed / Opening phases measure t against that scaled clock.
    ///   Default SetBlinkingSettings(1.0, 0.5, 1.5) therefore lasts 0.1 / 0.05 / 0.15 s
    ///   of wall / Movie Maker time.
    /// - Idling counts down <see cref="NextBlinkingTime"/> with unscaled deltaTime
    ///   (Unity: <c>NextBlinkingTime -= Time.deltaTime</c>).
    /// </summary>
    public sealed class CubismAutoEyeBlinkInput
    {
        public float Mean = 2.5f;
        public float MaximumDeviation = 2f;
        public float Timescale = 10f;

        public Phase CurrentPhase { get; private set; } = Phase.Idling;
        public float LastValue { get; private set; }
        public float UserTimeSeconds { get; private set; }
        public float StateStartTimeSeconds { get; private set; }
        public float ClosingSeconds { get; private set; } = 1.0f;
        public float ClosedSeconds { get; private set; } = 0.5f;
        public float OpeningSeconds { get; private set; } = 1.5f;
        public float NextBlinkingTime { get; set; }

        private readonly Random _rng;

        public CubismAutoEyeBlinkInput()
            : this(null)
        {
        }

        public CubismAutoEyeBlinkInput(Random rng)
        {
            _rng = rng ?? new Random();
            SetBlinkingSettings(1.0f, 0.5f, 1.5f);
            Reset();
        }

        public void Reset()
        {
            CurrentPhase = Phase.Idling;
            NextBlinkingTime = Mean + NextDeviation();
            UserTimeSeconds = 0f;
            StateStartTimeSeconds = 0f;
            LastValue = 1f;
        }

        public void SetBlinkingSettings(float closing, float closed, float opening)
        {
            ClosingSeconds = closing;
            ClosedSeconds = closed;
            OpeningSeconds = opening;
        }

        /// <summary>
        /// Advance one frame. <paramref name="deltaTime"/> is the same value Unity
        /// would pass as Time.deltaTime (Movie Maker: 1/fixed-fps).
        /// </summary>
        public float Evaluate(float deltaTime)
        {
            UserTimeSeconds += (deltaTime * Timescale);
            var value = 0.0f;

            switch (CurrentPhase)
            {
                case Phase.ClosingEyes:
                    value = UpdateEyeBlinkClosing();
                    break;
                case Phase.ClosedEyes:
                    value = UpdateEyeBlinkClosed();
                    break;
                case Phase.OpeningEyes:
                    value = UpdateEyeBlinkOpening();
                    break;
                case Phase.Idling:
                    value = UpdateEyeBlinkIdling(deltaTime);
                    break;
            }

            LastValue = value;
            return value;
        }

        private float UpdateEyeBlinkClosing()
        {
            var t = (UserTimeSeconds - StateStartTimeSeconds) / ClosingSeconds;

            if (t >= 1.0f)
            {
                CurrentPhase = Phase.ClosedEyes;
                StateStartTimeSeconds = UserTimeSeconds;
            }

            return 1.0f - t;
        }

        private float UpdateEyeBlinkClosed()
        {
            var t = (UserTimeSeconds - StateStartTimeSeconds) / ClosedSeconds;

            if (t >= 1.0f)
            {
                CurrentPhase = Phase.OpeningEyes;
                StateStartTimeSeconds = UserTimeSeconds;
            }

            return 0.0f;
        }

        private float UpdateEyeBlinkOpening()
        {
            var t = (UserTimeSeconds - StateStartTimeSeconds) / OpeningSeconds;

            if (t >= 1.0f)
            {
                t = 1.0f;
                CurrentPhase = Phase.Idling;
                NextBlinkingTime = Mean + NextDeviation();
            }

            return t;
        }

        private float UpdateEyeBlinkIdling(float deltaTime)
        {
            NextBlinkingTime -= deltaTime;

            if (NextBlinkingTime < 0.0f)
            {
                CurrentPhase = Phase.ClosingEyes;
                StateStartTimeSeconds = UserTimeSeconds;
            }

            return 1.0f;
        }

        private float NextDeviation()
        {
            return (float)((_rng.NextDouble() * 2.0 - 1.0) * MaximumDeviation);
        }

        public enum Phase
        {
            Idling,
            ClosingEyes,
            ClosedEyes,
            OpeningEyes
        }
    }
}
