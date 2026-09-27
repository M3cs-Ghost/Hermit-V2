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
        // C8.1f: was 6f under the old continuous nudge/confirm mechanic.
        // The new two-step debit/credit selection is two quick taps against
        // a static list, not iterative tuning — arguably less demanding per
        // se — but it adds one new cost the old mechanic didn't have: a
        // context switch between "which is debited" and "which is
        // credited" while re-reading the same transaction. +1s is the
        // smallest allowance covering that switch without being generous
        // enough to reward trial-and-error against the clock — not a
        // doubling. See Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md, section 14/20.
        [SerializeField] private float _balanceDecisionWindowSeconds = 7f;
        [SerializeField] private float _commandBeatSeconds = 0.6f;
        [SerializeField] private float _lockSeconds = 0.2f;
        [SerializeField] private float _feedbackDisplaySeconds = 0.8f;
        // C8.1f.2: manual acceptance found the shared 0.8s Feedback phase
        // cut Balance's teaching recap off almost as soon as it appeared —
        // the machine-reaction animation alone (~0.5-0.65s worst case)
        // already consumed nearly all of it. A Balance-local override
        // (same pattern as _balanceDecisionWindowSeconds above) rather than
        // extending every archetype's Feedback phase: originally ~0.65s
        // animation + ~1.65s actual readable hold ~= 2.3s total. C8.1f.3
        // raised this to 3.4s for its own longer/richer machine sequence.
        // C8.1f.4 added a dedicated ~1.0s Processing "thinking" beat plus a
        // slightly longer token-travel-in animation between token seating
        // and the outcome resolving (a human Gold review asked the machine
        // to visibly evaluate the decision, not just react instantly), so
        // the worst case (Correct) is now token travel ~0.5s + Processing
        // ~1.0s + oscillate/settle/lock ~0.75s + the verdict hold ~0.6s =
        // ~2.85s, raised here to keep the same ~1.65s readable-hold target
        // for the recap afterward: ~2.85s animation + ~1.65s hold ~= 4.5s
        // total. See Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md, "C8.1f.2",
        // "C8.1f.3", and "C8.1f.4".
        [SerializeField] private float _balanceFeedbackDisplaySeconds = 4.5f;

        // C8.1g.2: same rationale as _balanceFeedbackDisplaySeconds above —
        // the shared 0.8s generic-ding Feedback window is enough for a
        // flash of color, not for reading a real teaching sentence
        // ("Cuentas por pagar es un pasivo; las demás son cuentas de
        // activo."). Detective's new reveal choreography (accusation cue
        // already shown during Lock, then ~0.3-0.5s reject/expose beats,
        // then the recap) plus a genuinely readable ~2.2-2.5s hold for a
        // one-sentence explanation lands comfortably under Balance's own
        // 4.5s — 3.2s chosen to match the archetype's own 3.2s Decision
        // window (brief section 11: decision window itself untouched this
        // phase) rather than an arbitrary new constant.
        // C8.1l user-test pacing: +1.0s of static reading time on the
        // finished teaching recap (3.2 -> 4.2). The reveal choreography is
        // presenter-owned and unchanged, and the recap stays up until the
        // round ends, so the whole extra second lands AFTER the recap is
        // fully visible.
        [SerializeField] private float _detectiveFeedbackDisplaySeconds = 4.2f;

        // C8.1l user-test pacing: new users needed more reading/decision
        // time in Detective and Game Show — each gets its own decision
        // window (+1.0s over the shared 3.2s). Western keeps the shared
        // _decisionWindowSeconds, Balance its own. Intro/reveal timing is
        // untouched.
        [SerializeField] private float _detectiveDecisionWindowSeconds = 4.2f;
        [SerializeField] private float _gameShowDecisionWindowSeconds = 4.2f;

        // C8.1k: Game Show's showmanship pass — a short broadcast preamble
        // (lights -> prize -> statement entrance) that the shared 0.6s
        // command beat cannot hold, and a Feedback phase long enough for
        // the answer-lock suspense beat, the reveal, and a readable
        // FeedbackExplanation hold (~2.8s after the reveal lands). Same
        // Game-Show-local override pattern as Balance/Detective above, so
        // no other archetype's timing moves. The decision window itself is
        // unchanged. See Docs/C8_1K_GAME_SHOW_SHOWMANSHIP.md.
        [SerializeField] private float _gameShowIntroSeconds = 1.5f;
        // C8.1l: 3.4 -> 4.4 — the explanation card stays up until the round
        // ends, so the extra 1.0s is pure reading time after it is fully
        // visible; the suspense beat/reveal/explanation delay are unchanged.
        [SerializeField] private float _gameShowFeedbackDisplaySeconds = 4.4f;

        [Header("C7 — pre-run countdown / combo (kept for compatibility)")]
        [SerializeField] private float _countdownDurationSeconds = 3f;
        [SerializeField] private int _streakBonusThreshold = 3;
        [SerializeField] private int _streakBonusPoints = 30;

        [Header("C8.1d.1 — Western Encounter (one intro, N consecutive rounds)")]
        [SerializeField] private int _westernEncounterRoundCount = 3;
        [SerializeField] private float _encounterIntroSeconds = 6.9f;
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

        /// <summary>Balance-only Feedback-phase length — long enough for
        /// its machine-reaction animation plus a genuinely readable recap
        /// hold, unlike the other three archetypes' brief generic ding.</summary>
        public float BalanceFeedbackDisplaySeconds => _balanceFeedbackDisplaySeconds;

        /// <summary>Detective-only Feedback-phase length — long enough for
        /// its investigative reveal plus a genuinely readable teaching
        /// recap, unlike Western/Game Show's brief generic ding.</summary>
        public float DetectiveFeedbackDisplaySeconds => _detectiveFeedbackDisplaySeconds;

        public float DetectiveDecisionWindowSeconds => _detectiveDecisionWindowSeconds;

        public float GameShowDecisionWindowSeconds => _gameShowDecisionWindowSeconds;

        public float GameShowIntroSeconds => _gameShowIntroSeconds;

        public float GameShowFeedbackDisplaySeconds => _gameShowFeedbackDisplaySeconds;

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
        /// and raised this to 7.5s — a ~0.9-1.0s buffer, the same class as
        /// C8.1d.4's. C8.1d.12 found that generous a buffer was itself the
        /// cause of a manually-reported "~1 second empty Western background"
        /// dead gap: the cinematic's own script-level choreography (see
        /// <c>WesternShootoutPresenter.CinematicIntroRoutine</c>) finishes at
        /// ~6.5s regardless, so the outlaws — whose reveal used to wait
        /// entirely for THIS value to elapse — simply weren't there yet for
        /// the remaining ~1.0s. C8.1d.12 also moved the outlaws' own visual
        /// entrance earlier (overlapping the cinematic's own flash/cut
        /// teardown at ~6.2s, well before this timer even matters), so this
        /// value now only gates when INPUT enables, and was tightened to
        /// 6.9s — a ~0.4s buffer above the routine's own ~6.5s runtime,
        /// still comfortably larger than any realistic single-frame
        /// per-shot overshoot (each push-in while-loop can only overshoot by
        /// a fraction of a frame) but no longer leaving the outlaws idle and
        /// visible-but-uninteractive for a full extra second. See
        /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.5" for the original
        /// timeline/margin history and "C8.1d.12" for this retiming.
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
            float encounterOutroSeconds = 0f,
            float balanceFeedbackDisplaySeconds = -1f,
            float detectiveFeedbackDisplaySeconds = -1f,
            float gameShowIntroSeconds = -1f,
            float gameShowFeedbackDisplaySeconds = -1f,
            float detectiveDecisionWindowSeconds = -1f,
            float gameShowDecisionWindowSeconds = -1f)
        {
            var instance = CreateInstance<ClasicoGameDefinition>();
            instance.SetIdentity(gameId, displayName);
            instance._microgameCount = microgameCount;
            instance._decisionWindowSeconds = decisionWindowSeconds;
            instance._balanceDecisionWindowSeconds = balanceDecisionWindowSeconds;
            // -1 (default): every existing CreateInMemory call site — none
            // of which know or care about this Balance-only field — simply
            // gets the same feedbackDisplaySeconds as every other
            // archetype, exactly the pre-C8.1f.2 behavior.
            instance._balanceFeedbackDisplaySeconds = balanceFeedbackDisplaySeconds >= 0f ? balanceFeedbackDisplaySeconds : feedbackDisplaySeconds;
            // Same -1 sentinel/fallback pattern as Balance's own field
            // above — see C8.1g.2.
            instance._detectiveFeedbackDisplaySeconds = detectiveFeedbackDisplaySeconds >= 0f ? detectiveFeedbackDisplaySeconds : feedbackDisplaySeconds;
            // C8.1k: same -1 sentinel/fallback pattern — Game Show keeps the
            // shared command beat/feedback unless a test opts in.
            instance._gameShowIntroSeconds = gameShowIntroSeconds >= 0f ? gameShowIntroSeconds : commandBeatSeconds;
            instance._gameShowFeedbackDisplaySeconds = gameShowFeedbackDisplaySeconds >= 0f ? gameShowFeedbackDisplaySeconds : feedbackDisplaySeconds;
            // C8.1l: same sentinel — both fall back to the shared window.
            instance._detectiveDecisionWindowSeconds = detectiveDecisionWindowSeconds >= 0f ? detectiveDecisionWindowSeconds : decisionWindowSeconds;
            instance._gameShowDecisionWindowSeconds = gameShowDecisionWindowSeconds >= 0f ? gameShowDecisionWindowSeconds : decisionWindowSeconds;
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
