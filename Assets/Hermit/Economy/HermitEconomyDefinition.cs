using System;
using UnityEngine;

namespace Hermit.Economy
{
    /// <summary>
    /// C9.1: every Hermit Coin (HC) reward value in one place — TUNABLE
    /// PROTOTYPE VALUES, not permanent economy commitments (see
    /// Docs/C9_1_HERMIT_COINS_V01.md). Same ScriptableObject pattern as the
    /// game definitions: create an asset via the menu and place it at
    /// Resources/HermitEconomyDefinition to rebalance without code; with no
    /// asset, <see cref="CreateDefault"/> supplies the documented v0.1 values.
    /// Nothing in Clásico reads this — only <see cref="HermitRewardCalculator"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "HermitEconomyDefinition", menuName = "Hermit/Economy/Hermit Economy Definition")]
    public sealed class HermitEconomyDefinition : ScriptableObject
    {
        [Serializable]
        public struct DiminishingTier
        {
            [Tooltip("Inclusive upper bound of the daily valid-session number this tier covers.")]
            public int MaxSessionNumber;

            public float Multiplier;

            public DiminishingTier(int maxSessionNumber, float multiplier)
            {
                MaxSessionNumber = maxSessionNumber;
                Multiplier = multiplier;
            }
        }

        [Header("Session reward (subject to daily diminishing returns)")]
        [SerializeField] private int _baseSessionReward = 12;

        [Tooltip("Accuracy bonus by thirds of the session answered correctly: " +
                 "[< 1/3, >= 1/3, >= 2/3, all correct]. For a 9-round session: 0-2, 3-5, 6-8, 9.")]
        [SerializeField] private int[] _accuracyBonusByThird = { 0, 2, 5, 8 };

        [SerializeField] private int _maxSpeedBonus = 4;

        [Header("Valid session")]
        [Tooltip("A session only pays if the player really interacted with at least " +
                 "this many of every InteractionRequiredDenominator rounds (2 of 3 = 6 of 9).")]
        [SerializeField] private int _interactionRequiredNumerator = 2;
        [SerializeField] private int _interactionRequiredDenominator = 3;

        [Header("Daily diminishing returns (by valid session number today)")]
        [SerializeField] private DiminishingTier[] _diminishingTiers =
        {
            new DiminishingTier(2, 1.00f),
            new DiminishingTier(5, 0.60f),
            new DiminishingTier(10, 0.05f),
            new DiminishingTier(int.MaxValue, 0.02f),
        };

        [Header("Bonuses (never multiplied)")]
        [SerializeField] private int _dailyFirstSessionBonus = 25;

        [SerializeField] private int _dailyVarietyBonus = 15;

        [Tooltip("C9.1 decision: deferred in v0.1. Every valid 9-round Clásico session already " +
                 "covers all four archetypes, so the bonus would always fire on the first session " +
                 "of the day; it becomes meaningful once variety spans several Hermit modes.")]
        [SerializeField] private bool _dailyVarietyBonusEnabled = false;

        [SerializeField] private int _weeklyConsistencyBonus = 225;

        [SerializeField] private int _weeklyActiveDaysRequired = 5;

        public int BaseSessionReward => _baseSessionReward;
        public int MaxSpeedBonus => _maxSpeedBonus;
        public int InteractionRequiredNumerator => _interactionRequiredNumerator;
        public int InteractionRequiredDenominator => _interactionRequiredDenominator;
        public int DailyFirstSessionBonus => _dailyFirstSessionBonus;
        public int DailyVarietyBonus => _dailyVarietyBonus;
        public bool DailyVarietyBonusEnabled => _dailyVarietyBonusEnabled;
        public int WeeklyConsistencyBonus => _weeklyConsistencyBonus;
        public int WeeklyActiveDaysRequired => _weeklyActiveDaysRequired;

        /// <summary>Accuracy bonus for accuracy tier 0..3 (thirds).</summary>
        public int AccuracyBonusForTier(int tier)
        {
            if (_accuracyBonusByThird == null || _accuracyBonusByThird.Length == 0)
            {
                return 0;
            }

            return _accuracyBonusByThird[Mathf.Clamp(tier, 0, _accuracyBonusByThird.Length - 1)];
        }

        /// <summary>Multiplier for the Nth valid session of the day (1-based).</summary>
        public float MultiplierForSessionNumber(int sessionNumber)
        {
            if (_diminishingTiers != null)
            {
                foreach (var tier in _diminishingTiers)
                {
                    if (sessionNumber <= tier.MaxSessionNumber)
                    {
                        return tier.Multiplier;
                    }
                }
            }

            return 0f;
        }

        public static HermitEconomyDefinition CreateDefault() => CreateInstance<HermitEconomyDefinition>();

        /// <summary>Test/rebalancing factory — every argument defaults to the
        /// shipped v0.1 value, same pattern as ClasicoGameDefinition.CreateInMemory.</summary>
        public static HermitEconomyDefinition CreateInMemory(
            int baseSessionReward = 12,
            int[] accuracyBonusByThird = null,
            int maxSpeedBonus = 4,
            int dailyFirstSessionBonus = 25,
            int dailyVarietyBonus = 15,
            bool dailyVarietyBonusEnabled = false,
            int weeklyConsistencyBonus = 225,
            int weeklyActiveDaysRequired = 5,
            DiminishingTier[] diminishingTiers = null)
        {
            var definition = CreateInstance<HermitEconomyDefinition>();
            definition._baseSessionReward = baseSessionReward;
            definition._accuracyBonusByThird = accuracyBonusByThird ?? new[] { 0, 2, 5, 8 };
            definition._maxSpeedBonus = maxSpeedBonus;
            definition._dailyFirstSessionBonus = dailyFirstSessionBonus;
            definition._dailyVarietyBonus = dailyVarietyBonus;
            definition._dailyVarietyBonusEnabled = dailyVarietyBonusEnabled;
            definition._weeklyConsistencyBonus = weeklyConsistencyBonus;
            definition._weeklyActiveDaysRequired = weeklyActiveDaysRequired;
            if (diminishingTiers != null)
            {
                definition._diminishingTiers = diminishingTiers;
            }

            return definition;
        }
    }
}
