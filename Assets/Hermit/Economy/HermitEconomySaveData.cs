using System;
using System.Collections.Generic;
using System.Linq;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Economy
{
    /// <summary>
    /// C9.1: the ONE versioned, structured local save for the Hermit economy
    /// (wallet + activity/bonus state) — JsonUtility-friendly public fields,
    /// no scattered PlayerPrefs keys. <see cref="Version"/> gates future
    /// migrations.
    /// </summary>
    [Serializable]
    public sealed class HermitEconomySaveData
    {
        public const int CurrentVersion = 1;

        [Serializable]
        public sealed class TransactionData
        {
            public int amount;
            public string type;
            public string timestampUtc;
            public string reference;
            public string source;
        }

        public int version = CurrentVersion;

        // Wallet
        public int balance;
        public int lifetimeEarned;
        public int lifetimeSpent;
        public List<TransactionData> transactions = new List<TransactionData>();
        public List<string> creditedRewardIds = new List<string>();

        // Activity — daily
        public string currentLocalDate = string.Empty;
        public int validSessionsToday;
        public List<string> archetypesCompletedToday = new List<string>();
        public bool firstSessionBonusClaimedToday;
        public bool varietyBonusClaimedToday;

        // Activity — weekly (Monday–Sunday, id = Monday's date)
        public string currentWeekId = string.Empty;
        public List<string> activeDaysThisWeek = new List<string>();
        public bool weeklyBonusClaimed;

        public static HermitEconomySaveData From(HermitWallet wallet, HermitActivityState state)
        {
            return new HermitEconomySaveData
            {
                version = CurrentVersion,
                balance = wallet.Balance,
                lifetimeEarned = wallet.LifetimeEarned,
                lifetimeSpent = wallet.LifetimeSpent,
                transactions = wallet.Transactions.Select(t => new TransactionData
                {
                    amount = t.Amount,
                    type = t.Type.ToString(),
                    timestampUtc = HermitWallet.FormatTimestamp(t.TimestampUtc),
                    reference = t.Reference,
                    source = t.Source,
                }).ToList(),
                creditedRewardIds = wallet.CreditedRewardIds.ToList(),
                currentLocalDate = state.CurrentLocalDate,
                validSessionsToday = state.ValidSessionsToday,
                archetypesCompletedToday = state.ArchetypesCompletedToday.Select(a => a.ToString()).ToList(),
                firstSessionBonusClaimedToday = state.FirstSessionBonusClaimedToday,
                varietyBonusClaimedToday = state.VarietyBonusClaimedToday,
                currentWeekId = state.CurrentWeekId,
                activeDaysThisWeek = state.ActiveDaysThisWeek.ToList(),
                weeklyBonusClaimed = state.WeeklyBonusClaimed,
            };
        }

        public HermitWallet ToWallet()
        {
            var restored = (transactions ?? new List<TransactionData>()).Select(t => new HermitWalletTransaction(
                t.amount,
                Enum.TryParse<HermitWalletTransactionType>(t.type, out var type) ? type : HermitWalletTransactionType.RewardCredit,
                HermitWallet.ParseTimestamp(t.timestampUtc),
                t.reference,
                t.source));
            return new HermitWallet(balance, lifetimeEarned, lifetimeSpent, restored, creditedRewardIds);
        }

        public HermitActivityState ToActivityState()
        {
            var archetypes = (archetypesCompletedToday ?? new List<string>())
                .Select(a => Enum.TryParse<MicrogameArchetype>(a, out var parsed) ? (MicrogameArchetype?)parsed : null)
                .Where(a => a.HasValue)
                .Select(a => a.Value);
            return new HermitActivityState(
                currentLocalDate, validSessionsToday, archetypes, firstSessionBonusClaimedToday, varietyBonusClaimedToday,
                currentWeekId, activeDaysThisWeek, weeklyBonusClaimed);
        }
    }
}
