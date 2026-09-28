using System;
using System.Collections.Generic;
using System.Linq;
using Hermit.Games.Clasico;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Economy
{
    /// <summary>
    /// C9.1: turns one finished Clásico session into a Hermit Coin reward.
    /// Deterministic and side-effect free — no wallet, no persistence, no
    /// clock, no UI. Everything it needs is passed in: the session facts,
    /// the activity state, the local date, and the tunable definition. That
    /// is what lets it move server-side later without touching Clásico.
    ///
    /// Formula (all values TUNABLE PROTOTYPE VALUES — see
    /// Docs/C9_1_HERMIT_COINS_V01.md):
    ///
    ///   gameplay  = Base + Accuracy + Speed
    ///   after     = round_half_away_from_zero(gameplay × multiplier(sessionNumberToday))
    ///   total     = after + FirstSessionBonus + VarietyBonus + WeeklyBonus   (bonuses never multiplied)
    ///
    /// An invalid session (aborted, incomplete, or &lt; 2/3 of rounds with
    /// real interaction) pays 0 and consumes nothing.
    /// </summary>
    public static class HermitRewardCalculator
    {
        public static readonly IReadOnlyCollection<MicrogameArchetype> AllClasicoArchetypes = new[]
        {
            MicrogameArchetype.AimSelect,   // Western
            MicrogameArchetype.ChooseSide,  // Game Show
            MicrogameArchetype.Balance,     // Balance
            MicrogameArchetype.DetectError, // Detective
        };

        public static HermitRewardResult Calculate(
            ClasicoSessionResult session,
            HermitActivityState state,
            DateTime localDate,
            HermitEconomyDefinition definition)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var today = (state ?? HermitActivityState.Empty).RolledOverTo(localDate);
            var totalRounds = session.Rounds.Count;
            var correct = session.CorrectAnswers;
            var interacted = session.InteractedRounds;
            var speedScore = ComputeSpeedScore(session.Rounds);

            var invalidReason = ValidateSession(session, definition);
            if (invalidReason != HermitInvalidSessionReason.None)
            {
                return new HermitRewardResult(
                    session.SessionId, false, invalidReason, correct, totalRounds, interacted, speedScore,
                    0, 0, 0, 0, 0f, 0, 0, 0, 0, 0);
            }

            var baseReward = definition.BaseSessionReward;
            var accuracyBonus = ComputeAccuracyBonus(correct, totalRounds, definition);
            var speedBonus = ComputeSpeedBonus(speedScore, definition);

            var sessionNumber = today.ValidSessionsToday + 1;
            var multiplier = definition.MultiplierForSessionNumber(sessionNumber);
            var gameplayAfter = ApplyMultiplier(baseReward + accuracyBonus + speedBonus, multiplier);

            var firstSessionBonus = today.FirstSessionBonusClaimedToday ? 0 : definition.DailyFirstSessionBonus;

            var varietyBonus = 0;
            if (definition.DailyVarietyBonusEnabled && !today.VarietyBonusClaimedToday)
            {
                var playedToday = new HashSet<MicrogameArchetype>(today.ArchetypesCompletedToday);
                playedToday.UnionWith(session.ArchetypesPlayed);
                if (AllClasicoArchetypes.All(playedToday.Contains))
                {
                    varietyBonus = definition.DailyVarietyBonus;
                }
            }

            var dayId = HermitCalendar.DayId(localDate);
            var activeDaysAfter = today.ActiveDaysThisWeek.Contains(dayId)
                ? today.ActiveDaysThisWeek.Count
                : today.ActiveDaysThisWeek.Count + 1;
            var weeklyBonus = !today.WeeklyBonusClaimed && activeDaysAfter >= definition.WeeklyActiveDaysRequired
                ? definition.WeeklyConsistencyBonus
                : 0;

            return new HermitRewardResult(
                session.SessionId, true, HermitInvalidSessionReason.None, correct, totalRounds, interacted, speedScore,
                baseReward, accuracyBonus, speedBonus,
                sessionNumber, multiplier, gameplayAfter,
                firstSessionBonus, varietyBonus, weeklyBonus, activeDaysAfter);
        }

        /// <summary>Valid only if the session ended naturally, every planned
        /// round resolved, and the player really interacted with at least
        /// Numerator/Denominator of the rounds (2/3 → 6 of 9). Timeouts are
        /// fine; an all-passive session is not.</summary>
        public static HermitInvalidSessionReason ValidateSession(ClasicoSessionResult session, HermitEconomyDefinition definition)
        {
            if (!session.CompletedNaturally)
            {
                return HermitInvalidSessionReason.Aborted;
            }

            var totalRounds = session.Rounds.Count;
            if (totalRounds == 0 || totalRounds < session.PlannedRounds)
            {
                return HermitInvalidSessionReason.IncompleteRounds;
            }

            var numerator = Math.Max(0, definition.InteractionRequiredNumerator);
            var denominator = Math.Max(1, definition.InteractionRequiredDenominator);
            if (session.InteractedRounds * denominator < totalRounds * numerator)
            {
                return HermitInvalidSessionReason.InsufficientInteraction;
            }

            return HermitInvalidSessionReason.None;
        }

        /// <summary>Accuracy by thirds: tier = floor(3 × correct / total),
        /// i.e. 9 rounds → 0-2 = tier 0, 3-5 = 1, 6-8 = 2, 9 = 3 (3 rounds →
        /// 0/1/2/3 correct map straight to tiers 0/1/2/3).</summary>
        public static int ComputeAccuracyBonus(int correct, int totalRounds, HermitEconomyDefinition definition)
        {
            if (totalRounds <= 0)
            {
                return 0;
            }

            var tier = (3 * Math.Max(0, Math.Min(correct, totalRounds))) / totalRounds;
            return definition.AccuracyBonusForTier(tier);
        }

        /// <summary>
        /// Session speed score in [0,1]. Per round:
        ///   s = clamp01(1 − responseSeconds / answerWindowSeconds)
        /// counted only when the round was answered CORRECTLY by a real
        /// interaction; wrong answers and timeouts contribute 0 (no reward
        /// for fast random clicking, none for timeouts). Normalizing by each
        /// archetype's own window (Western 3.2s, Game Show/Detective 4.2s,
        /// Balance 7s) makes rounds comparable. Score = Σ s / total rounds.
        /// </summary>
        public static float ComputeSpeedScore(IReadOnlyList<ClasicoRoundRecord> rounds)
        {
            if (rounds == null || rounds.Count == 0)
            {
                return 0f;
            }

            var sum = 0.0;
            foreach (var round in rounds)
            {
                if (!round.Correct || !round.Interacted || round.AnswerWindowSeconds <= 0f)
                {
                    continue;
                }

                var normalized = 1.0 - round.ResponseSeconds / round.AnswerWindowSeconds;
                sum += Math.Max(0.0, Math.Min(1.0, normalized));
            }

            return (float)(sum / rounds.Count);
        }

        /// <summary>Speed bonus = min(Max, floor(score × (Max + 1))) — with
        /// Max 4: score [0,0.2) → 0, [0.2,0.4) → 1, [0.4,0.6) → 2,
        /// [0.6,0.8) → 3, [0.8,1] → 4.</summary>
        public static int ComputeSpeedBonus(float speedScore01, HermitEconomyDefinition definition)
        {
            var max = Math.Max(0, definition.MaxSpeedBonus);
            var clamped = Math.Max(0.0, Math.Min(1.0, speedScore01));
            var bucket = (int)Math.Floor(clamped * (max + 1) + 1e-6);
            return Math.Min(max, bucket);
        }

        /// <summary>The one rounding rule: round half away from zero
        /// (24 × 0.60 = 14.4 → 14; 15 × 0.60 = 9.0 → 9; 25 × 0.02 = 0.5 → 1).</summary>
        public static int ApplyMultiplier(int subtotal, float multiplier) =>
            (int)Math.Round(subtotal * (decimal)multiplier, MidpointRounding.AwayFromZero);
    }
}
