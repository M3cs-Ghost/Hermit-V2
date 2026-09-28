namespace Hermit.Economy
{
    /// <summary>Why a session paid nothing (C9.1 valid-session rule).</summary>
    public enum HermitInvalidSessionReason
    {
        None,
        Aborted,
        IncompleteRounds,
        InsufficientInteraction,
    }

    /// <summary>
    /// C9.1: the complete, immutable breakdown of one session's Hermit Coin
    /// reward — everything the summary UI shows, tests assert, and future
    /// analytics / a server-authoritative recalculation would need. Produced
    /// only by <see cref="HermitRewardCalculator"/>; never mutated after.
    /// </summary>
    public sealed class HermitRewardResult
    {
        /// <summary>Idempotency key — the Clásico session id. A wallet never
        /// credits the same RewardId twice.</summary>
        public string RewardId { get; }

        public bool WasValidSession { get; }
        public HermitInvalidSessionReason InvalidReason { get; }

        public int CorrectAnswers { get; }
        public int TotalRounds { get; }
        public int InteractedRounds { get; }

        /// <summary>0..1 aggregate normalized response speed (see the calculator).</summary>
        public float SpeedScore01 { get; }

        public int BaseReward { get; }
        public int AccuracyBonus { get; }
        public int SpeedBonus { get; }
        public int GameplaySubtotalBeforeMultiplier => BaseReward + AccuracyBonus + SpeedBonus;

        /// <summary>1-based valid-session number today this reward counts as
        /// (0 for an invalid session, which never consumes a slot).</summary>
        public int DailySessionNumber { get; }
        public float DiminishingMultiplier { get; }
        public int GameplayRewardAfterMultiplier { get; }

        public int FirstSessionBonus { get; }
        public int VarietyBonus { get; }
        public int WeeklyConsistencyBonus { get; }

        /// <summary>Active days this Monday–Sunday week once this session is
        /// counted (for the UI/analytics; 0 for an invalid session).</summary>
        public int ActiveDaysThisWeekAfter { get; }

        public int TotalReward => GameplayRewardAfterMultiplier + FirstSessionBonus + VarietyBonus + WeeklyConsistencyBonus;

        public HermitRewardResult(
            string rewardId,
            bool wasValidSession,
            HermitInvalidSessionReason invalidReason,
            int correctAnswers,
            int totalRounds,
            int interactedRounds,
            float speedScore01,
            int baseReward,
            int accuracyBonus,
            int speedBonus,
            int dailySessionNumber,
            float diminishingMultiplier,
            int gameplayRewardAfterMultiplier,
            int firstSessionBonus,
            int varietyBonus,
            int weeklyConsistencyBonus,
            int activeDaysThisWeekAfter)
        {
            RewardId = rewardId ?? string.Empty;
            WasValidSession = wasValidSession;
            InvalidReason = invalidReason;
            CorrectAnswers = correctAnswers;
            TotalRounds = totalRounds;
            InteractedRounds = interactedRounds;
            SpeedScore01 = speedScore01;
            BaseReward = baseReward;
            AccuracyBonus = accuracyBonus;
            SpeedBonus = speedBonus;
            DailySessionNumber = dailySessionNumber;
            DiminishingMultiplier = diminishingMultiplier;
            GameplayRewardAfterMultiplier = gameplayRewardAfterMultiplier;
            FirstSessionBonus = firstSessionBonus;
            VarietyBonus = varietyBonus;
            WeeklyConsistencyBonus = weeklyConsistencyBonus;
            ActiveDaysThisWeekAfter = activeDaysThisWeekAfter;
        }
    }
}
