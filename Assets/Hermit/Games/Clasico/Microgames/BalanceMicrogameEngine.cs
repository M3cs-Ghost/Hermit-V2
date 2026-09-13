using System;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>Nudge a value up/down until it matches a target, then
    /// confirm. Backs Balance Machine — the one C8.1 Gold microgame that does
    /// not reduce to "pick 1 of N" (see <see cref="SelectionMicrogameEngine"/>).</summary>
    internal sealed class BalanceMicrogameEngine : IMicrogameEngine
    {
        private readonly float _correctValue;
        private readonly float _stepSize;
        private readonly float _decisionWindowSeconds;
        private float _elapsed;

        public float CurrentValue { get; private set; }
        public bool IsResolved { get; private set; }
        public bool IsCorrect { get; private set; }

        public float DecisionFraction01
        {
            get
            {
                if (_decisionWindowSeconds <= 0f)
                {
                    return 1f;
                }

                var remaining = _decisionWindowSeconds - _elapsed;
                return (float)Math.Max(0.0, Math.Min(1.0, remaining / _decisionWindowSeconds));
            }
        }

        public BalanceMicrogameEngine(float startValue, float correctValue, float stepSize, float decisionWindowSeconds)
        {
            CurrentValue = startValue;
            _correctValue = correctValue;
            _stepSize = stepSize;
            _decisionWindowSeconds = decisionWindowSeconds;
        }

        public void Tick(float deltaSeconds)
        {
            if (IsResolved)
            {
                return;
            }

            _elapsed += deltaSeconds;
            if (_decisionWindowSeconds > 0f && _elapsed >= _decisionWindowSeconds)
            {
                Confirm();
            }
        }

        /// <summary>direction's sign only matters — positive nudges up one
        /// step, negative nudges down one step.</summary>
        public void Nudge(int direction)
        {
            if (IsResolved || direction == 0)
            {
                return;
            }

            CurrentValue += Math.Sign(direction) * _stepSize;
        }

        public void Confirm()
        {
            if (IsResolved)
            {
                return;
            }

            IsCorrect = Math.Abs(CurrentValue - _correctValue) < 0.001f;
            IsResolved = true;
        }
    }
}
