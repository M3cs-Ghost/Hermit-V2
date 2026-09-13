using System;
using System.Collections.Generic;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Games.Clasico
{
    /// <summary>
    /// C8.1's replacement for the C5-C7 <c>ClasicoGameEngine</c> (deleted this
    /// phase — see Docs/C8_1_GOLD_MICROGAME_SLICE.md, "Migration strategy").
    /// Where the old engine drew N questions and showed one at a time, this
    /// director draws N *microgames* — possibly of different mechanical
    /// archetypes — and hosts one at a time, handing control to a small
    /// per-archetype <see cref="IMicrogameEngine"/> for the actual decision
    /// logic. From <see cref="GameFlowController"/>'s point of view nothing
    /// changed: this is still just one <see cref="IGameEngine"/>, exactly the
    /// architectural rule the C8.0 Design Lock requires (GameFlowController/
    /// GameRegistry never learn a microgame exists).
    ///
    /// Per-microgame rhythm (C8.1, single tier, no speed escalation yet):
    /// Intro (command beat, non-interactive) -&gt; Decision (the archetype
    /// engine is live) -&gt; Lock (tiny freeze, no new visuals) -&gt; Feedback
    /// (reveal) -&gt; next microgame's Intro. An optional session-level
    /// Countdown (reusing the C7 field/mechanic) can precede microgame 0.
    /// </summary>
    public sealed class ClasicoSessionDirector : IGameEngine
    {
        private enum Phase
        {
            Countdown,
            Intro,
            Decision,
            Lock,
            Feedback
        }

        private ClasicoGameDefinition _definition;
        private GameContext _context;
        private GameSession _session;

        private MicrogameArchetype[] _sequence;
        // Parallel to _sequence: 0 for a round that opens its Encounter
        // (an ordinary single-round archetype is always 0), 1/2/... for a
        // continuation round inside a multi-round Encounter (Western only,
        // this phase) — see Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Western
        // Encounter Presentation". Built once in Begin(), never mutated.
        private int[] _roundWithinEncounter;
        private int _currentRoundWithinEncounter;
        private int _index = -1;
        private bool _finished;
        private Phase _phase;
        private float _phaseTimer;
        private float _decisionElapsed;

        private IMicrogameEngine _activeEngine;
        private int _classificationCursor;
        private int _trueFalseCursor;
        private int _equationCursor;
        private int _errorDetectionCursor;
        private IReadOnlyList<ClassificationChallenge> _classificationPool;
        private IReadOnlyList<TrueFalseChallenge> _trueFalsePool;
        private IReadOnlyList<EquationChallenge> _equationPool;
        private IReadOnlyList<ErrorDetectionChallenge> _errorDetectionPool;

        private int _lastStreakBonus;

        public bool IsFinished => _finished;
        public MicrogameArchetype CurrentArchetype { get; private set; }
        public ClassificationChallenge CurrentClassification { get; private set; }
        public TrueFalseChallenge CurrentTrueFalse { get; private set; }
        public EquationChallenge CurrentEquation { get; private set; }
        public ErrorDetectionChallenge CurrentErrorDetection { get; private set; }

        public int CurrentMicrogameIndex => _index;
        public int TotalMicrogames => _sequence?.Length ?? 0;

        /// <summary>0 for a round that opens its Encounter, 1/2/... for a
        /// continuation round — see <see cref="ClasicoEncounterPlan"/>.</summary>
        public int CurrentRoundWithinEncounter => _currentRoundWithinEncounter;

        public bool IsCountingDown => !_finished && _phase == Phase.Countdown;
        public bool IsIntroPhase => !_finished && _phase == Phase.Intro;
        public bool IsDecisionPhase => !_finished && _phase == Phase.Decision;
        public bool IsFeedbackPhase => !_finished && _phase == Phase.Feedback;

        public int CountdownSecondsRemaining
        {
            get
            {
                if (!IsCountingDown)
                {
                    return 0;
                }

                var remaining = _definition.CountdownDurationSeconds - _phaseTimer;
                return (int)Math.Ceiling(Math.Max(0.0, (double)remaining));
            }
        }

        /// <summary>1 at the start of the decision window, decaying to 0 —
        /// only meaningful while <see cref="IsDecisionPhase"/> is true.</summary>
        public float DecisionFraction01 => _activeEngine?.DecisionFraction01 ?? 1f;

        public bool LastAnswerCorrect { get; private set; }
        public int LastSelectedIndex { get; private set; } = -1;
        public float LastBalanceValue { get; private set; }
        public int LastPointsAwarded { get; private set; }
        public int LastStreakBonus => _lastStreakBonus;

        /// <summary>Live value while a Balance microgame's Decision phase is
        /// active — 0 for every other archetype.</summary>
        public float CurrentBalanceValue => (_activeEngine as BalanceMicrogameEngine)?.CurrentValue ?? 0f;

        public void Begin(GameContext context, GameDefinition definition, GameSession session)
        {
            _context = context;
            _definition = (ClasicoGameDefinition)definition;
            _session = session;

            _classificationPool = Shuffled(ClasicoMicrogameLibrary.ClassificationPool, context.Rng);
            _trueFalsePool = Shuffled(ClasicoMicrogameLibrary.TrueFalsePool, context.Rng);
            _equationPool = Shuffled(ClasicoMicrogameLibrary.EquationPool, context.Rng);
            _errorDetectionPool = Shuffled(ClasicoMicrogameLibrary.ErrorDetectionPool, context.Rng);

            var westernRounds = _definition.WesternEncounterRoundCount;
            if (westernRounds >= 2 && _definition.MicrogameCount >= westernRounds)
            {
                var plans = BuildEncounterPlans(_definition.MicrogameCount, westernRounds, context.Rng);
                Flatten(plans, out _sequence, out _roundWithinEncounter);
            }
            else
            {
                // Encounter grouping off (either not configured, or the
                // session is too short to fit even one Western Encounter) —
                // this is exactly the original C8.1 per-round rhythm, byte
                // for byte, so every existing test observes no change.
                _sequence = ClasicoMicrogameLibrary.BuildSequence(_definition.MicrogameCount, context.Rng);
                _roundWithinEncounter = new int[_sequence.Length];
            }

            _index = -1;
            _finished = _sequence.Length == 0;

            if (_finished)
            {
                return;
            }

            if (_definition.CountdownDurationSeconds > 0f)
            {
                _phase = Phase.Countdown;
                _phaseTimer = 0f;
            }
            else
            {
                AdvanceToNextMicrogame();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_finished)
            {
                return;
            }

            switch (_phase)
            {
                case Phase.Countdown:
                    _phaseTimer += deltaSeconds;
                    if (_phaseTimer >= _definition.CountdownDurationSeconds)
                    {
                        AdvanceToNextMicrogame();
                    }

                    break;

                case Phase.Intro:
                    _phaseTimer += deltaSeconds;
                    if (_phaseTimer >= GetIntroDurationSeconds())
                    {
                        _phase = Phase.Decision;
                        _decisionElapsed = 0f;
                    }

                    break;

                case Phase.Decision:
                    _decisionElapsed += deltaSeconds;
                    _activeEngine.Tick(deltaSeconds);
                    if (_activeEngine.IsResolved)
                    {
                        ResolveCurrentMicrogame();
                    }

                    break;

                case Phase.Lock:
                    _phaseTimer += deltaSeconds;
                    if (_phaseTimer >= _definition.LockSeconds)
                    {
                        _phase = Phase.Feedback;
                        _phaseTimer = 0f;
                    }

                    break;

                default: // Feedback
                    _phaseTimer += deltaSeconds;
                    if (_phaseTimer >= GetFeedbackDurationSeconds())
                    {
                        AdvanceToNextMicrogame();
                    }

                    break;
            }
        }

        /// <summary>Called by the presenter host on a click/confirm for
        /// AimSelect/ChooseSide/DetectError. A no-op outside Decision or for
        /// a Balance microgame — mirrors the old engine's
        /// "ignore input outside AwaitingAnswer" guard.</summary>
        public void SubmitSelection(int index)
        {
            if (_phase != Phase.Decision || !(_activeEngine is SelectionMicrogameEngine selection))
            {
                return;
            }

            selection.Submit(index);
            ResolveCurrentMicrogame();
        }

        public void NudgeBalance(int direction)
        {
            if (_phase != Phase.Decision || !(_activeEngine is BalanceMicrogameEngine balance))
            {
                return;
            }

            balance.Nudge(direction);
        }

        public void ConfirmBalance()
        {
            if (_phase != Phase.Decision || !(_activeEngine is BalanceMicrogameEngine balance))
            {
                return;
            }

            balance.Confirm();
            ResolveCurrentMicrogame();
        }

        public GameResult BuildResult(bool completed)
        {
            const string contentSetId = "clasico_microgames_v1";
            const int contentSchemaVersion = 1;
            return new GameResult(
                _definition.GameId,
                _session.SessionId,
                _session.Score,
                _session.Correct,
                _session.Incorrect,
                _session.ElapsedSeconds,
                completed,
                contentSetId,
                contentSchemaVersion,
                _session.BestStreak);
        }

        public void Cleanup()
        {
            _activeEngine = null;
            CurrentClassification = null;
            CurrentTrueFalse = null;
            CurrentEquation = null;
            CurrentErrorDetection = null;
            _sequence = null;
        }

        private void ResolveCurrentMicrogame()
        {
            var correct = _activeEngine.IsCorrect;

            if (_activeEngine is SelectionMicrogameEngine selection)
            {
                LastSelectedIndex = selection.SelectedIndex;
            }
            else if (_activeEngine is BalanceMicrogameEngine balance)
            {
                LastBalanceValue = balance.CurrentValue;
            }

            var decisionWindow = GetDecisionWindowSeconds(CurrentArchetype, _currentRoundWithinEncounter);
            var gained = ClasicoScoring.ComputeQuestionScore(
                correct, _decisionElapsed, decisionWindow, _definition.PointsPerCorrectAnswer, _definition.MaxSpeedBonusPoints);

            if (correct)
            {
                _session.Correct++;
                _session.Streak++;
                if (_session.Streak > _session.BestStreak)
                {
                    _session.BestStreak = _session.Streak;
                }
            }
            else
            {
                _session.Incorrect++;
                _session.Streak = 0;
            }

            _lastStreakBonus = ClasicoScoring.ComputeStreakBonus(_session.Streak, _definition.StreakBonusThreshold, _definition.StreakBonusPoints);
            LastPointsAwarded = gained + _lastStreakBonus;
            LastAnswerCorrect = correct;
            _session.Score += LastPointsAwarded;

            _context.Analytics.AnswerSubmitted(_definition.GameId, _session.SessionId, CurrentChallengeId(), correct, _decisionElapsed, CurrentContentVersion());

            _phase = Phase.Lock;
            _phaseTimer = 0f;
        }

        private void AdvanceToNextMicrogame()
        {
            _index++;
            if (_index >= _sequence.Length)
            {
                _finished = true;
                _activeEngine = null;
                CurrentClassification = null;
                CurrentTrueFalse = null;
                CurrentEquation = null;
                CurrentErrorDetection = null;
                return;
            }

            _session.Round = _index + 1;
            CurrentArchetype = _sequence[_index];
            _currentRoundWithinEncounter = _roundWithinEncounter[_index];
            CurrentClassification = null;
            CurrentTrueFalse = null;
            CurrentEquation = null;
            CurrentErrorDetection = null;
            LastSelectedIndex = -1;
            LastBalanceValue = 0f;

            switch (CurrentArchetype)
            {
                case MicrogameArchetype.AimSelect:
                    CurrentClassification = DrawNext(_classificationPool, ref _classificationCursor);
                    var correctIndex = Array.IndexOf(CurrentClassification.CategoryOptions, CurrentClassification.CorrectCategory);
                    _activeEngine = new SelectionMicrogameEngine(correctIndex, _definition.DecisionWindowSeconds);
                    break;

                case MicrogameArchetype.ChooseSide:
                    CurrentTrueFalse = DrawNext(_trueFalsePool, ref _trueFalseCursor);
                    _activeEngine = new SelectionMicrogameEngine(CurrentTrueFalse.IsTrue ? 0 : 1, _definition.DecisionWindowSeconds);
                    break;

                case MicrogameArchetype.Balance:
                    CurrentEquation = DrawNext(_equationPool, ref _equationCursor);
                    _activeEngine = new BalanceMicrogameEngine(CurrentEquation.StartValue, CurrentEquation.CorrectValue, CurrentEquation.StepSize, _definition.BalanceDecisionWindowSeconds);
                    break;

                default: // DetectError
                    CurrentErrorDetection = DrawNext(_errorDetectionPool, ref _errorDetectionCursor);
                    _activeEngine = new SelectionMicrogameEngine(CurrentErrorDetection.AnomalyIndex, _definition.DecisionWindowSeconds);
                    break;
            }

            _phase = Phase.Intro;
            _phaseTimer = 0f;
            _context.Analytics.QuestionPresented(_definition.GameId, _session.SessionId, CurrentChallengeId(), _index, CurrentContentVersion());
        }

        /// <summary><paramref name="roundWithinEncounter"/> is threaded
        /// through but currently unused — a documented hook (C8.1d.1 brief,
        /// section 14) for a future light escalation (e.g. Round 2/3 of a
        /// Western Encounter getting a slightly shorter window). Left as a
        /// no-op this phase: doing it safely needs a tuned floor (never below
        /// ~1.2-1.5s) validated against real play, and the brief explicitly
        /// allows leaving every round at the same duration rather than
        /// guessing at numbers. The parameter's presence is the hook itself —
        /// wiring escalation in later is a one-line change here, not a new
        /// call site.</summary>
        private float GetDecisionWindowSeconds(MicrogameArchetype archetype, int roundWithinEncounter) =>
            archetype == MicrogameArchetype.Balance ? _definition.BalanceDecisionWindowSeconds : _definition.DecisionWindowSeconds;

        /// <summary>Which archetypes currently own the Encounter treatment
        /// (a themed intro once, a quick reset between rounds, an extended
        /// final-round outro) rather than the plain per-round Intro every
        /// other archetype still uses. Western-only this phase — see the
        /// C8.1d.1 brief, section 6 ("prove Western first"). Centralized here
        /// so a future world's encounter support is a one-line addition
        /// instead of new branching scattered through Tick/Advance/Resolve.</summary>
        private int GetEncounterRoundCount(MicrogameArchetype archetype) =>
            archetype == MicrogameArchetype.AimSelect ? Math.Max(1, _definition.WesternEncounterRoundCount) : 1;

        private bool UsesEncounterPresentation(MicrogameArchetype archetype) => GetEncounterRoundCount(archetype) >= 2;

        private float GetIntroDurationSeconds()
        {
            if (!UsesEncounterPresentation(CurrentArchetype))
            {
                return _definition.CommandBeatSeconds;
            }

            return _currentRoundWithinEncounter == 0 ? _definition.EncounterIntroSeconds : _definition.RoundTransitionSeconds;
        }

        private float GetFeedbackDurationSeconds()
        {
            if (!UsesEncounterPresentation(CurrentArchetype))
            {
                return _definition.FeedbackDisplaySeconds;
            }

            var isFinalRoundOfEncounter = _currentRoundWithinEncounter == GetEncounterRoundCount(CurrentArchetype) - 1;
            return isFinalRoundOfEncounter
                ? _definition.FeedbackDisplaySeconds + _definition.EncounterOutroSeconds
                : _definition.FeedbackDisplaySeconds;
        }

        /// <summary>Builds one session's worth of Encounters: exactly one
        /// Western block of <paramref name="westernRounds"/> consecutive
        /// AimSelect rounds, inserted at a random position among single-round
        /// blocks of the other three archetypes (round-robin, then reshuffled
        /// until no two adjacent single-round blocks share an archetype —
        /// reusing <see cref="ClasicoMicrogameLibrary"/>'s own helpers rather
        /// than re-deriving the rule). Total flattened round count always
        /// equals <paramref name="totalRounds"/>.</summary>
        private static List<ClasicoEncounterPlan> BuildEncounterPlans(int totalRounds, int westernRounds, Random rng)
        {
            var others = new[] { MicrogameArchetype.ChooseSide, MicrogameArchetype.Balance, MicrogameArchetype.DetectError };
            var remaining = Math.Max(0, totalRounds - westernRounds);
            var otherSequence = new List<MicrogameArchetype>(remaining);
            for (var i = 0; i < remaining; i++)
            {
                otherSequence.Add(others[i % others.Length]);
            }

            const int maxAttempts = 500;
            for (var attempt = 0; attempt < maxAttempts && otherSequence.Count > 1; attempt++)
            {
                ClasicoMicrogameLibrary.Shuffle(otherSequence, rng);
                if (!ClasicoMicrogameLibrary.HasAdjacentDuplicate(otherSequence))
                {
                    break;
                }
            }

            var insertAt = rng.Next(otherSequence.Count + 1);
            var plans = new List<ClasicoEncounterPlan>(otherSequence.Count + 1);
            for (var i = 0; i < insertAt; i++)
            {
                plans.Add(new ClasicoEncounterPlan(otherSequence[i], 1));
            }

            plans.Add(new ClasicoEncounterPlan(MicrogameArchetype.AimSelect, westernRounds));

            for (var i = insertAt; i < otherSequence.Count; i++)
            {
                plans.Add(new ClasicoEncounterPlan(otherSequence[i], 1));
            }

            return plans;
        }

        private static void Flatten(List<ClasicoEncounterPlan> plans, out MicrogameArchetype[] sequence, out int[] roundWithinEncounter)
        {
            var total = 0;
            foreach (var plan in plans)
            {
                total += plan.RoundCount;
            }

            sequence = new MicrogameArchetype[total];
            roundWithinEncounter = new int[total];
            var index = 0;
            foreach (var plan in plans)
            {
                for (var round = 0; round < plan.RoundCount; round++)
                {
                    sequence[index] = plan.Archetype;
                    roundWithinEncounter[index] = round;
                    index++;
                }
            }
        }

        private string CurrentChallengeId()
        {
            switch (CurrentArchetype)
            {
                case MicrogameArchetype.AimSelect: return CurrentClassification?.Id ?? string.Empty;
                case MicrogameArchetype.ChooseSide: return CurrentTrueFalse?.Id ?? string.Empty;
                case MicrogameArchetype.Balance: return CurrentEquation?.Id ?? string.Empty;
                default: return CurrentErrorDetection?.Id ?? string.Empty;
            }
        }

        private int CurrentContentVersion()
        {
            switch (CurrentArchetype)
            {
                case MicrogameArchetype.AimSelect: return CurrentClassification?.ContentVersion ?? 0;
                case MicrogameArchetype.ChooseSide: return CurrentTrueFalse?.ContentVersion ?? 0;
                case MicrogameArchetype.Balance: return CurrentEquation?.ContentVersion ?? 0;
                default: return CurrentErrorDetection?.ContentVersion ?? 0;
            }
        }

        private static T DrawNext<T>(IReadOnlyList<T> pool, ref int cursor)
        {
            var item = pool[cursor % pool.Count];
            cursor++;
            return item;
        }

        private static IReadOnlyList<T> Shuffled<T>(IReadOnlyList<T> source, Random rng)
        {
            var copy = new List<T>(source);
            for (var i = copy.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            return copy;
        }
    }
}
