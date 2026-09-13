using System;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>
    /// One correct index among N options, under a timer. Backs Western
    /// Shootout (AimSelect), TV Game Show (ChooseSide), and Detective Lineup
    /// (DetectError) — three different fantasies and inputs at the
    /// presentation layer, but the same resolution shape underneath: a
    /// player either picks the right index in time, or doesn't.
    /// </summary>
    internal sealed class SelectionMicrogameEngine : IMicrogameEngine
    {
        private readonly int _correctIndex;
        private readonly float _decisionWindowSeconds;
        private float _elapsed;

        public bool IsResolved { get; private set; }
        public bool IsCorrect { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public float ElapsedSeconds => _elapsed;

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

        public SelectionMicrogameEngine(int correctIndex, float decisionWindowSeconds)
        {
            _correctIndex = correctIndex;
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
                Submit(-1);
            }
        }

        public void Submit(int index)
        {
            if (IsResolved)
            {
                return;
            }

            SelectedIndex = index;
            IsCorrect = index == _correctIndex;
            IsResolved = true;
        }
    }
}
