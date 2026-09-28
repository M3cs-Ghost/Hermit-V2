using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Hermit.Economy
{
    public enum HermitWalletTransactionType
    {
        RewardCredit,
    }

    /// <summary>C9.1: one wallet movement. v0.1 only ever records
    /// <see cref="HermitWalletTransactionType.RewardCredit"/> (no store yet).
    /// <see cref="Reference"/> is the reward id — a later signed/verified
    /// transaction can carry the same fields.</summary>
    public sealed class HermitWalletTransaction
    {
        public int Amount { get; }
        public HermitWalletTransactionType Type { get; }
        public DateTime TimestampUtc { get; }
        public string Reference { get; }
        public string Source { get; }

        public HermitWalletTransaction(int amount, HermitWalletTransactionType type, DateTime timestampUtc, string reference, string source)
        {
            Amount = amount;
            Type = type;
            TimestampUtc = timestampUtc;
            Reference = reference ?? string.Empty;
            Source = source ?? string.Empty;
        }
    }

    /// <summary>
    /// C9.1: the local, OFF-CHAIN Hermit Coin wallet. Balance can only change
    /// through a transaction-like operation (<see cref="TryCredit"/>) — there
    /// is no public setter — and each reward id can be credited at most once
    /// (idempotency survives save/reload via <see cref="CreditedRewardIds"/>).
    /// Deliberately small: a future account- or chain-backed wallet can
    /// replace it behind the same service without Clásico noticing.
    /// </summary>
    public sealed class HermitWallet
    {
        public const string ClasicoSessionSource = "clasico_session";
        private const int MaxTransactionHistory = 100;
        private const int MaxRememberedRewardIds = 1000;

        private readonly List<HermitWalletTransaction> _transactions;
        private readonly List<string> _creditedRewardIds;

        public int Balance { get; private set; }
        public int LifetimeEarned { get; private set; }
        public int LifetimeSpent { get; private set; }

        /// <summary>Newest last; capped at the most recent 100.</summary>
        public IReadOnlyList<HermitWalletTransaction> Transactions => _transactions;

        public IReadOnlyList<string> CreditedRewardIds => _creditedRewardIds;

        public HermitWallet()
            : this(0, 0, 0, null, null)
        {
        }

        public HermitWallet(int balance, int lifetimeEarned, int lifetimeSpent,
            IEnumerable<HermitWalletTransaction> transactions, IEnumerable<string> creditedRewardIds)
        {
            Balance = Math.Max(0, balance);
            LifetimeEarned = Math.Max(0, lifetimeEarned);
            LifetimeSpent = Math.Max(0, lifetimeSpent);
            _transactions = (transactions ?? Enumerable.Empty<HermitWalletTransaction>()).ToList();
            _creditedRewardIds = (creditedRewardIds ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        }

        public bool HasCredited(string rewardId) =>
            !string.IsNullOrEmpty(rewardId) && _creditedRewardIds.Contains(rewardId);

        /// <summary>Credits a VALID reward exactly once. Returns false (and
        /// changes nothing) for an invalid session, an empty id, or an id
        /// that was already credited. A valid 0-HC reward is still recorded
        /// as claimed, so it can never be re-processed.</summary>
        public bool TryCredit(HermitRewardResult reward, DateTime timestampUtc, out HermitWalletTransaction transaction)
        {
            transaction = null;
            if (reward == null || !reward.WasValidSession || string.IsNullOrEmpty(reward.RewardId) || HasCredited(reward.RewardId))
            {
                return false;
            }

            var amount = Math.Max(0, reward.TotalReward);
            transaction = new HermitWalletTransaction(amount, HermitWalletTransactionType.RewardCredit, timestampUtc, reward.RewardId, ClasicoSessionSource);

            Balance += amount;
            LifetimeEarned += amount;

            _transactions.Add(transaction);
            if (_transactions.Count > MaxTransactionHistory)
            {
                _transactions.RemoveRange(0, _transactions.Count - MaxTransactionHistory);
            }

            _creditedRewardIds.Add(reward.RewardId);
            if (_creditedRewardIds.Count > MaxRememberedRewardIds)
            {
                _creditedRewardIds.RemoveRange(0, _creditedRewardIds.Count - MaxRememberedRewardIds);
            }

            return true;
        }

        internal static string FormatTimestamp(DateTime utc) =>
            utc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

        internal static DateTime ParseTimestamp(string value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.MinValue;
    }
}
