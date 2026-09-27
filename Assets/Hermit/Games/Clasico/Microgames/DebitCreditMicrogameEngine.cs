using System;

namespace Hermit.Games.Clasico.Microgames
{
    /// <summary>Which prompt the player is currently answering.</summary>
    internal enum DebitCreditStep
    {
        Debit,
        Credit
    }

    /// <summary>The four presentation states a resolved
    /// <see cref="DebitCreditMicrogameEngine"/> can end in — the presenter's
    /// only source of truth for which machine reaction to play. This is a
    /// classification for teaching/animation only; <see cref="DebitCreditMicrogameEngine.IsCorrect"/>
    /// (true only for <see cref="Correct"/>) is still the sole input to
    /// scoring, unchanged from every other archetype — see
    /// Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md, section 10. Public (unlike
    /// the engine itself, and unlike every other archetype's engine): it
    /// crosses the Hermit.Games -&gt; Hermit.Runtime assembly boundary via
    /// <see cref="ClasicoSessionDirector.LastDebitCreditOutcome"/>, the same
    /// way the public Challenge data classes already do.</summary>
    public enum DebitCreditOutcome
    {
        Correct,
        Partial,
        Incorrect,
        Timeout
    }

    /// <summary>
    /// Two sequential "pick 1 of N" selections (debit, then credit) instead
    /// of <see cref="BalanceMicrogameEngine"/>'s continuous nudge/confirm —
    /// see Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md. No machine reaction is
    /// possible to infer from this engine's state before both selections
    /// are committed: <see cref="IsResolved"/> stays false, and nothing here
    /// exposes partial correctness until then (the anti-cheat rule, section
    /// 15 of the redesign doc, enforced at the data layer, not just by the
    /// presenter choosing not to render it).
    /// </summary>
    internal sealed class DebitCreditMicrogameEngine : IMicrogameEngine
    {
        private readonly int _correctDebitIndex;
        private readonly int _correctCreditIndex;
        private readonly float _decisionWindowSeconds;
        private float _elapsed;

        public DebitCreditStep CurrentStep { get; private set; } = DebitCreditStep.Debit;
        public int SelectedDebitIndex { get; private set; } = -1;
        public int SelectedCreditIndex { get; private set; } = -1;
        public bool IsDebitCorrect { get; private set; }
        public bool IsCreditCorrect { get; private set; }
        public DebitCreditOutcome Outcome { get; private set; }

        public bool IsResolved { get; private set; }

        /// <summary>True only when BOTH accounts are correct — the sole
        /// signal <see cref="ClasicoSessionDirector"/>'s scoring reads, same
        /// contract every other <see cref="IMicrogameEngine"/> honors.</summary>
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

        public DebitCreditMicrogameEngine(int correctDebitIndex, int correctCreditIndex, float decisionWindowSeconds)
        {
            _correctDebitIndex = correctDebitIndex;
            _correctCreditIndex = correctCreditIndex;
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
                ResolveAsTimeout();
            }
        }

        /// <summary>Locks the debit answer and advances to the credit step.
        /// A no-op if already resolved or already past this step — prevents
        /// cross-step input leakage (redesign doc, section 8).</summary>
        public void SubmitDebit(int index)
        {
            if (IsResolved || CurrentStep != DebitCreditStep.Debit)
            {
                return;
            }

            SelectedDebitIndex = index;
            IsDebitCorrect = index == _correctDebitIndex;
            CurrentStep = DebitCreditStep.Credit;
        }

        /// <summary>Locks the credit answer and resolves the challenge — the
        /// only place this engine ever becomes <see cref="IsResolved"/> from
        /// player input. A no-op if already resolved or the debit step
        /// hasn't happened yet.</summary>
        public void SubmitCredit(int index)
        {
            if (IsResolved || CurrentStep != DebitCreditStep.Credit)
            {
                return;
            }

            SelectedCreditIndex = index;
            IsCreditCorrect = index == _correctCreditIndex;
            IsCorrect = IsDebitCorrect && IsCreditCorrect;
            Outcome = IsCorrect
                ? DebitCreditOutcome.Correct
                : (IsDebitCorrect || IsCreditCorrect) ? DebitCreditOutcome.Partial : DebitCreditOutcome.Incorrect;
            IsResolved = true;
        }

        /// <summary>Fewer than two selections were committed in time —
        /// always Timeout, even if the debit step already locked (never
        /// synthesizes a fake credit answer just to produce a Partial —
        /// redesign doc, section 11/16).</summary>
        private void ResolveAsTimeout()
        {
            IsCorrect = false;
            Outcome = DebitCreditOutcome.Timeout;
            IsResolved = true;
        }
    }
}
