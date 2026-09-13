using UnityEngine;

namespace Hermit.Games.Clasico
{
    /// <summary>Static config for one Clasico session. An instance ships as
    /// Assets/Hermit/Data/Resources/ClasicoGameDefinition.asset.
    ///
    /// C8.1 note: the C5-C7 single-QuestionSet fields (<c>_questionSet</c>,
    /// <c>_questionCount</c>, <c>_timePerQuestionSeconds</c>) are gone —
    /// Clasico no longer draws one fixed shape of question; it draws a
    /// sequence of heterogeneous microgames (<see cref="ClasicoSessionDirector"/>),
    /// each with its own decision-window length. Content itself moved to
    /// <see cref="Microgames.ClasicoMicrogameLibrary"/> (in-code, not an
    /// Inspector asset — see that file's doc-comment for why). Existing
    /// serialized values for the removed fields in the shipped .asset are
    /// simply ignored by Unity's deserializer; no asset edit was needed.</summary>
    [CreateAssetMenu(fileName = "ClasicoGameDefinition", menuName = "Hermit/Games/Clasico Game Definition")]
    public sealed class ClasicoGameDefinition : GameDefinition
    {
        [Header("Scoring (shared by every microgame)")]
        [SerializeField] private int _pointsPerCorrectAnswer = 100;
        [SerializeField] private int _maxSpeedBonusPoints = 50;

        [Header("C8.1 — microgame session")]
        [SerializeField] private int _microgameCount = 9;
        [SerializeField] private float _decisionWindowSeconds = 3.2f;
        [SerializeField] private float _balanceDecisionWindowSeconds = 6f;
        [SerializeField] private float _commandBeatSeconds = 0.6f;
        [SerializeField] private float _lockSeconds = 0.2f;
        [SerializeField] private float _feedbackDisplaySeconds = 0.8f;

        [Header("C7 — pre-run countdown / combo (kept for compatibility)")]
        [SerializeField] private float _countdownDurationSeconds = 3f;
        [SerializeField] private int _streakBonusThreshold = 3;
        [SerializeField] private int _streakBonusPoints = 30;

        [Header("C8.1d.1 — Western Encounter (one intro, N consecutive rounds)")]
        [SerializeField] private int _westernEncounterRoundCount = 3;
        [SerializeField] private float _encounterIntroSeconds = 7.5f;
        [SerializeField] private float _roundTransitionSeconds = 0.35f;
        [SerializeField] private float _encounterOutroSeconds = 0.7f;

        public int PointsPerCorrectAnswer => _pointsPerCorrectAnswer;
        public int MaxSpeedBonusPoints => _maxSpeedBonusPoints;

        /// <summary>How many microgames make up one session. C8.1 ships a
        /// short 8-10 test run (default 9) — no full 16-18 Design Lock run yet.</summary>
        public int MicrogameCount => _microgameCount;

        /// <summary>Decision-window length for AimSelect/ChooseSide/DetectError.</summary>
        public float DecisionWindowSeconds => _decisionWindowSeconds;

        /// <summary>Balance gets more time than the other three, per the
        /// Design Lock's own "claridad &gt; simulación física" guidance.</summary>
        public float BalanceDecisionWindowSeconds => _balanceDecisionWindowSeconds;

        public float CommandBeatSeconds => _commandBeatSeconds;
        public float LockSeconds => _lockSeconds;
        public float FeedbackDisplaySeconds => _feedbackDisplaySeconds;

        /// <summary>0 (or less) means no pre-run countdown.</summary>
        public float CountdownDurationSeconds => _countdownDurationSeconds;

        /// <summary>0 (either field) disables the streak bonus entirely.</summary>
        public int StreakBonusThreshold => _streakBonusThreshold;
        public int StreakBonusPoints => _streakBonusPoints;

        /// <summary>How many consecutive AimSelect (Western) rounds make up
        /// one Western Encounter — one thematic intro, this many challenges,
        /// one short outro, per Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Western
        /// Encounter Presentation". Values &lt;= 1 fully restore the C8.1
        /// per-microgame rhythm (every AimSelect round gets its own ordinary
        /// Intro, exactly as every other archetype still does) — this is the
        /// default every existing test's <see cref="CreateInMemory"/> call
        /// gets, so none of them observe any behavior change. The shipped
        /// asset (this field's own declared default, 3) is what actually
        /// turns the Encounter model on for real play.</summary>
        public int WesternEncounterRoundCount => _westernEncounterRoundCount;

        /// <summary>Length of the one full cinematic duel intro (establishing
        /// shot, Sheriff/Outlaw/hand close-ups, gunshot, flash-cut) played
        /// before round 1 of a Western Encounter. C8.1d.4 first raised this
        /// from the original C8.1d.1 arcade-style face-off's ~2.2s to 5.0s
        /// for a ~4.11s cinematic (measured buffer ~0.9s, after an initial
        /// 4.5s/~0.4s-buffer attempt proved too tight under real per-frame
        /// while-loop overshoot). C8.1d.5 then re-paced the same cinematic
        /// to a manually-requested ~6.5s (more suspense before the gunshot)
        /// and raised this to 7.5s — the same absolute ~0.9-1.0s buffer
        /// class as C8.1d.4's, since the routine still chains the same
        /// number of per-shot push-in while-loops (each can only overshoot
        /// its own target by a fraction of a frame regardless of how long
        /// that shot itself runs) — see
        /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.5" for the full
        /// timeline and the empirical PlayMode confirmation of this margin.
        /// Irrelevant when <see cref="WesternEncounterRoundCount"/> &lt;=
        /// 1.</summary>
        public float EncounterIntroSeconds => _encounterIntroSeconds;

        /// <summary>Length of the quick reset between rounds 1-2 and 2-3 of
        /// a Western Encounter (no intro replay) — target ~0.25-0.5s.</summary>
        public float RoundTransitionSeconds => _roundTransitionSeconds;

        /// <summary>Extra dwell added onto the final round's Feedback phase
        /// only — a short "outro" beat (Sheriff already idle, outlaws hold
        /// their reveal pose) before the next encounter's transition cut,
        /// rather than a separate phase — target ~0.5-1.0s total with
        /// FeedbackDisplaySeconds already included.</summary>
        public float EncounterOutroSeconds => _encounterOutroSeconds;

        public override IGameEngine CreateEngine() => new ClasicoSessionDirector();

        /// <summary>Builds a definition from code instead of an Inspector
        /// asset — used by tests and by anything that needs a throwaway
        /// configuration.</summary>
        public static ClasicoGameDefinition CreateInMemory(
            string gameId,
            string displayName,
            int microgameCount,
            float decisionWindowSeconds,
            float balanceDecisionWindowSeconds,
            int pointsPerCorrectAnswer = 100,
            int maxSpeedBonusPoints = 0,
            float commandBeatSeconds = 0f,
            float lockSeconds = 0f,
            float feedbackDisplaySeconds = 0.1f,
            float countdownDurationSeconds = 0f,
            int streakBonusThreshold = 0,
            int streakBonusPoints = 0,
            int westernEncounterRoundCount = 1,
            float encounterIntroSeconds = 0f,
            float roundTransitionSeconds = 0f,
            float encounterOutroSeconds = 0f)
        {
            var instance = CreateInstance<ClasicoGameDefinition>();
            instance.SetIdentity(gameId, displayName);
            instance._microgameCount = microgameCount;
            instance._decisionWindowSeconds = decisionWindowSeconds;
            instance._balanceDecisionWindowSeconds = balanceDecisionWindowSeconds;
            instance._pointsPerCorrectAnswer = pointsPerCorrectAnswer;
            instance._maxSpeedBonusPoints = maxSpeedBonusPoints;
            instance._commandBeatSeconds = commandBeatSeconds;
            instance._lockSeconds = lockSeconds;
            instance._feedbackDisplaySeconds = feedbackDisplaySeconds;
            instance._countdownDurationSeconds = countdownDurationSeconds;
            instance._streakBonusThreshold = streakBonusThreshold;
            instance._streakBonusPoints = streakBonusPoints;
            // Defaults to 1 (Encounter grouping off) so every pre-C8.1d.1
            // test using this factory without naming these parameters keeps
            // observing the original one-round-per-archetype rhythm exactly.
            instance._westernEncounterRoundCount = westernEncounterRoundCount;
            instance._encounterIntroSeconds = encounterIntroSeconds;
            instance._roundTransitionSeconds = roundTransitionSeconds;
            instance._encounterOutroSeconds = encounterOutroSeconds;
            return instance;
        }
    }
}
