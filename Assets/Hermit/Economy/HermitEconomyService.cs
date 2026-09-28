using System.Collections.Generic;
using Hermit.Games.Clasico;

namespace Hermit.Economy
{
    /// <summary>What happened when a finished session was offered to the
    /// economy — the reward breakdown plus whether it was newly credited.</summary>
    public sealed class HermitRewardOutcome
    {
        /// <summary>The breakdown; null only when an already-credited
        /// session is offered again after the in-memory breakdown was lost
        /// (e.g. an app restart) — then only "already credited" is shown.</summary>
        public HermitRewardResult Reward { get; }

        /// <summary>True only the first time a valid reward is credited.</summary>
        public bool Credited { get; }

        /// <summary>True when this reward id had already been credited
        /// earlier (re-shown, never re-paid).</summary>
        public bool AlreadyCredited { get; }

        public int BalanceAfter { get; }

        public HermitRewardOutcome(HermitRewardResult reward, bool credited, bool alreadyCredited, int balanceAfter)
        {
            Reward = reward;
            Credited = credited;
            AlreadyCredited = alreadyCredited;
            BalanceAfter = balanceAfter;
        }
    }

    /// <summary>
    /// C9.1: the one entry point Clásico's host uses —
    /// <c>ClasicoSessionResult → HermitRewardCalculator → HermitRewardResult →
    /// HermitWallet → save</c>. Owns the loaded wallet and activity state;
    /// the calculator stays pure and the game mode stays unaware of any of
    /// it. Idempotent per session id: offering the same session again (a
    /// re-raised result, a re-entered result screen, a scene reload) re-shows
    /// the original breakdown and never pays twice — the credited-id list is
    /// persisted, so this holds across app restarts too.
    /// </summary>
    public sealed class HermitEconomyService
    {
        private readonly IHermitEconomyStore _store;
        private readonly IHermitClock _clock;
        private readonly Dictionary<string, HermitRewardResult> _recentRewards = new Dictionary<string, HermitRewardResult>();

        public HermitEconomyDefinition Definition { get; }
        public HermitWallet Wallet { get; private set; }
        public HermitActivityState State { get; private set; }

        public HermitEconomyService(IHermitEconomyStore store, IHermitClock clock, HermitEconomyDefinition definition)
        {
            _store = store;
            _clock = clock;
            Definition = definition ?? HermitEconomyDefinition.CreateDefault();
            Reload();
        }

        /// <summary>The activity state as of right now (rolled over to the
        /// current local day/week) — for the debug view.</summary>
        public HermitActivityState CurrentState => State.RolledOverTo(_clock.LocalNow);

        public HermitRewardOutcome ProcessClasicoSession(ClasicoSessionResult session)
        {
            var localDate = _clock.LocalNow;

            if (session != null && Wallet.HasCredited(session.SessionId))
            {
                // Re-show the original breakdown when it's still in memory;
                // after a restart only "already credited" is known (a
                // recomputed breakdown against today's state would be wrong).
                _recentRewards.TryGetValue(session.SessionId, out var previous);
                return new HermitRewardOutcome(previous, false, true, Wallet.Balance);
            }

            var reward = HermitRewardCalculator.Calculate(session, State, localDate, Definition);
            if (!reward.WasValidSession)
            {
                return new HermitRewardOutcome(reward, false, false, Wallet.Balance);
            }

            if (!Wallet.TryCredit(reward, _clock.UtcNow, out _))
            {
                return new HermitRewardOutcome(reward, false, true, Wallet.Balance);
            }

            State = State.AfterValidSession(localDate, session.ArchetypesPlayed, reward);
            _recentRewards[reward.RewardId] = reward;
            Persist();
            return new HermitRewardOutcome(reward, true, false, Wallet.Balance);
        }

        public void Reload()
        {
            var data = _store.Load();
            Wallet = data?.ToWallet() ?? new HermitWallet();
            State = data?.ToActivityState() ?? HermitActivityState.Empty;
            _recentRewards.Clear();
        }

        /// <summary>Dev-only reset: wipes the local save and starts from zero.</summary>
        public void ResetAll()
        {
            _store.Clear();
            Reload();
        }

        private void Persist()
        {
            _store.Save(HermitEconomySaveData.From(Wallet, State));
        }
    }

    /// <summary>
    /// C9.1: the process-wide economy service used by the runtime — the
    /// shipped local-file store, the system clock, and the definition asset
    /// at Resources/HermitEconomyDefinition (or the v0.1 defaults). Created
    /// lazily on first use. Tests call <see cref="OverrideForTests"/> so they
    /// never touch the player's real save.
    /// </summary>
    public static class HermitEconomy
    {
        private static HermitEconomyService _service;

        public static HermitEconomyService Service => _service ??= CreateDefault();

        public static void OverrideForTests(HermitEconomyService service)
        {
            _service = service;
        }

        public static void ResetOverride()
        {
            _service = null;
        }

        private static HermitEconomyService CreateDefault()
        {
            var definition = UnityEngine.Resources.Load<HermitEconomyDefinition>("HermitEconomyDefinition")
                             ?? HermitEconomyDefinition.CreateDefault();
            return new HermitEconomyService(new LocalFileHermitEconomyStore(), new SystemHermitClock(), definition);
        }
    }
}
