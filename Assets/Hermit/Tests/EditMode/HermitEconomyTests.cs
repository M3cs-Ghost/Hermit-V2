using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Hermit.Economy;
using Hermit.Games.Clasico;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Tests.EditMode
{
    /// <summary>
    /// C9.1 Hermit Coins v0.1 — calculator, activity state, wallet
    /// idempotency and persistence, all without a scene. A Clásico session is
    /// 9 rounds in production (accuracy by thirds, valid with ≥ 6 of 9
    /// interacted rounds); a few cases also use 3-round sessions to pin the
    /// brief's literal 0/3..3/3 mapping.
    /// </summary>
    public class HermitEconomyTests
    {
        // 2026-09-21 is a Monday; the week runs Mon 21 .. Sun 27.
        private static readonly DateTime Monday = new DateTime(2026, 9, 21, 10, 0, 0);

        private static readonly MicrogameArchetype[] NineRoundOrder =
        {
            MicrogameArchetype.ChooseSide, MicrogameArchetype.Balance, MicrogameArchetype.AimSelect,
            MicrogameArchetype.AimSelect, MicrogameArchetype.AimSelect, MicrogameArchetype.DetectError,
            MicrogameArchetype.ChooseSide, MicrogameArchetype.Balance, MicrogameArchetype.DetectError,
        };

        private static int _sessionCounter;

        private static float WindowFor(MicrogameArchetype archetype)
        {
            switch (archetype)
            {
                case MicrogameArchetype.AimSelect: return 3.2f;
                case MicrogameArchetype.Balance: return 7f;
                default: return 4.2f;
            }
        }

        /// <summary>A 9-round session: the first <paramref name="correct"/>
        /// rounds correct, the first <paramref name="interacted"/> rounds
        /// answered (the rest are timeouts), each answered round taking
        /// <paramref name="responseFraction"/> of its own answer window.</summary>
        private static ClasicoSessionResult Session(
            int correct = 6, int interacted = 9, float responseFraction = 1f, bool completed = true,
            MicrogameArchetype[] archetypes = null, string id = null)
        {
            archetypes ??= NineRoundOrder;
            var rounds = new List<ClasicoRoundRecord>();
            for (var i = 0; i < archetypes.Length; i++)
            {
                var answered = i < interacted;
                var window = WindowFor(archetypes[i]);
                rounds.Add(new ClasicoRoundRecord(
                    archetypes[i],
                    answered && i < correct,
                    answered,
                    answered ? window * responseFraction : window,
                    window));
            }

            return new ClasicoSessionResult(id ?? $"session-{++_sessionCounter}", completed, archetypes.Length, rounds);
        }

        private static ClasicoSessionResult ThreeRoundSession(int correct, int interacted = 3) =>
            Session(correct, interacted, archetypes: new[] { MicrogameArchetype.AimSelect, MicrogameArchetype.Balance, MicrogameArchetype.DetectError });

        private static HermitActivityState StateOnMonday(int validSessionsToday = 0, bool firstClaimed = false,
            IEnumerable<MicrogameArchetype> archetypes = null, bool varietyClaimed = false,
            IEnumerable<string> activeDays = null, bool weeklyClaimed = false) =>
            new HermitActivityState(HermitCalendar.DayId(Monday), validSessionsToday, archetypes, firstClaimed, varietyClaimed,
                HermitCalendar.WeekId(Monday), activeDays, weeklyClaimed);

        private static HermitEconomyDefinition Def(bool varietyEnabled = false) =>
            HermitEconomyDefinition.CreateInMemory(dailyVarietyBonusEnabled: varietyEnabled);

        // --- Accuracy ---------------------------------------------------------

        [TestCase(0, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 2)]
        [TestCase(5, 2)]
        [TestCase(6, 5)]
        [TestCase(8, 5)]
        [TestCase(9, 8)]
        public void Accuracy_NineRoundSession_UsesThirds(int correct, int expectedBonus)
        {
            var reward = HermitRewardCalculator.Calculate(Session(correct), HermitActivityState.Empty, Monday, Def());
            Assert.AreEqual(expectedBonus, reward.AccuracyBonus);
            Assert.AreEqual(correct, reward.CorrectAnswers);
            Assert.AreEqual(9, reward.TotalRounds);
        }

        [TestCase(0, 0)]
        [TestCase(1, 2)]
        [TestCase(2, 5)]
        [TestCase(3, 8)]
        public void Accuracy_ThreeRoundSession_MatchesTheBriefsTable(int correct, int expectedBonus)
        {
            var reward = HermitRewardCalculator.Calculate(ThreeRoundSession(correct), HermitActivityState.Empty, Monday, Def());
            Assert.IsTrue(reward.WasValidSession);
            Assert.AreEqual(expectedBonus, reward.AccuracyBonus);
        }

        // --- Speed ------------------------------------------------------------

        [TestCase(0f, 0)]
        [TestCase(0.19f, 0)]
        [TestCase(0.2f, 1)]
        [TestCase(0.4f, 2)]
        [TestCase(0.6f, 3)]
        [TestCase(0.8f, 4)]
        [TestCase(1f, 4)]
        public void SpeedBonus_MapsTheScoreIntoFiveBuckets(float score, int expectedBonus)
        {
            Assert.AreEqual(expectedBonus, HermitRewardCalculator.ComputeSpeedBonus(score, Def()));
        }

        [Test]
        public void SpeedScore_IsNormalizedPerArchetypeWindow_AndOnlyCorrectInteractedRoundsCount()
        {
            var halfWindow = new[]
            {
                new ClasicoRoundRecord(MicrogameArchetype.AimSelect, true, true, 1.6f, 3.2f),  // 0.5
                new ClasicoRoundRecord(MicrogameArchetype.Balance, true, true, 3.5f, 7f),      // 0.5
                new ClasicoRoundRecord(MicrogameArchetype.ChooseSide, true, true, 2.1f, 4.2f), // 0.5
            };
            Assert.AreEqual(0.5f, HermitRewardCalculator.ComputeSpeedScore(halfWindow), 1e-4f,
                "Half of each archetype's own window must score the same across archetypes.");

            var mixed = new[]
            {
                new ClasicoRoundRecord(MicrogameArchetype.AimSelect, true, true, 0.8f, 3.2f),   // 0.75
                new ClasicoRoundRecord(MicrogameArchetype.Balance, false, true, 0.1f, 7f),      // wrong -> 0
                new ClasicoRoundRecord(MicrogameArchetype.DetectError, false, false, 4.2f, 4.2f), // timeout -> 0
            };
            Assert.AreEqual(0.25f, HermitRewardCalculator.ComputeSpeedScore(mixed), 1e-4f,
                "Wrong answers and timeouts must contribute no speed.");
        }

        [TestCase(1f, 0)]
        [TestCase(0.9f, 0)]
        [TestCase(0.5f, 2)]
        [TestCase(0.1f, 4)]
        public void SpeedBonus_EndToEnd_AllCorrectSession(float responseFraction, int expectedBonus)
        {
            var reward = HermitRewardCalculator.Calculate(Session(correct: 9, responseFraction: responseFraction), HermitActivityState.Empty, Monday, Def());
            Assert.AreEqual(expectedBonus, reward.SpeedBonus);
        }

        // --- Daily diminishing returns --------------------------------------

        [TestCase(1, 1.00f)]
        [TestCase(2, 1.00f)]
        [TestCase(3, 0.60f)]
        [TestCase(5, 0.60f)]
        [TestCase(6, 0.05f)]
        [TestCase(10, 0.05f)]
        [TestCase(11, 0.02f)]
        [TestCase(40, 0.02f)]
        public void DiminishingMultiplier_ByDailySessionNumber(int sessionNumber, float expectedMultiplier)
        {
            var state = StateOnMonday(validSessionsToday: sessionNumber - 1, firstClaimed: sessionNumber > 1);
            var reward = HermitRewardCalculator.Calculate(Session(correct: 6), state, Monday, Def());

            Assert.AreEqual(sessionNumber, reward.DailySessionNumber);
            Assert.AreEqual(expectedMultiplier, reward.DiminishingMultiplier, 1e-6f);
            Assert.AreEqual(HermitRewardCalculator.ApplyMultiplier(reward.GameplaySubtotalBeforeMultiplier, expectedMultiplier), reward.GameplayRewardAfterMultiplier);
        }

        [TestCase(17, 1.00f, 17)]
        [TestCase(24, 0.60f, 14)]  // 14.4
        [TestCase(15, 0.60f, 9)]   // 9.0
        [TestCase(20, 0.05f, 1)]   // 1.0
        [TestCase(25, 0.02f, 1)]   // 0.5 -> away from zero
        [TestCase(12, 0.02f, 0)]   // 0.24
        public void ApplyMultiplier_RoundsHalfAwayFromZero(int subtotal, float multiplier, int expected)
        {
            Assert.AreEqual(expected, HermitRewardCalculator.ApplyMultiplier(subtotal, multiplier));
        }

        [Test]
        public void Multiplier_NeverAppliesToBonuses()
        {
            // 3rd session of the day, on the 5th active day of the week: the
            // weekly bonus must arrive whole even though gameplay is x0.60.
            var thisWeek = new[] { "2026-09-21", "2026-09-22", "2026-09-23", "2026-09-24" };
            var friday = Monday.AddDays(4); // Friday 25th = 5th distinct day
            var state = new HermitActivityState(HermitCalendar.DayId(friday), 2, null, true, false,
                HermitCalendar.WeekId(friday), thisWeek, false);

            var reward = HermitRewardCalculator.Calculate(Session(correct: 9), state, friday, Def());

            Assert.AreEqual(0.60f, reward.DiminishingMultiplier, 1e-6f);
            Assert.AreEqual(225, reward.WeeklyConsistencyBonus);
            Assert.AreEqual(reward.GameplayRewardAfterMultiplier + 225, reward.TotalReward);
        }

        // --- First session of the day ----------------------------------------

        [Test]
        public void FirstSessionBonus_OnlyOnTheFirstValidSessionOfTheDay()
        {
            var first = HermitRewardCalculator.Calculate(Session(), HermitActivityState.Empty, Monday, Def());
            Assert.AreEqual(25, first.FirstSessionBonus);

            var second = HermitRewardCalculator.Calculate(Session(), StateOnMonday(1, firstClaimed: true), Monday, Def());
            Assert.AreEqual(0, second.FirstSessionBonus);
        }

        // --- Variety (deferred in v0.1; logic kept and tested when enabled) ---

        [Test]
        public void Variety_IsDeferredInV01_NeverPaysWithTheShippedDefinition()
        {
            Assert.IsFalse(HermitEconomyDefinition.CreateDefault().DailyVarietyBonusEnabled, "v0.1 ships with the variety bonus deferred.");
            var reward = HermitRewardCalculator.Calculate(Session(), HermitActivityState.Empty, Monday, Def(varietyEnabled: false));
            Assert.AreEqual(0, reward.VarietyBonus);
        }

        [Test]
        public void Variety_WhenEnabled_NotCompleteYet_PaysNothing()
        {
            var threeArchetypes = Enumerable.Repeat(MicrogameArchetype.AimSelect, 9).ToArray();
            threeArchetypes[1] = MicrogameArchetype.Balance;
            threeArchetypes[2] = MicrogameArchetype.DetectError;
            var reward = HermitRewardCalculator.Calculate(Session(archetypes: threeArchetypes), HermitActivityState.Empty, Monday, Def(varietyEnabled: true));
            Assert.AreEqual(0, reward.VarietyBonus);
        }

        [Test]
        public void Variety_WhenEnabled_CurrentSessionCompletesTheSet_PaysInThatSession()
        {
            var noGameShow = Enumerable.Repeat(MicrogameArchetype.AimSelect, 9).ToArray();
            noGameShow[1] = MicrogameArchetype.Balance;
            noGameShow[2] = MicrogameArchetype.DetectError;
            var earlier = StateOnMonday(1, firstClaimed: true, archetypes: noGameShow.Distinct());

            var onlyGameShow = Enumerable.Repeat(MicrogameArchetype.ChooseSide, 9).ToArray();
            var reward = HermitRewardCalculator.Calculate(Session(archetypes: onlyGameShow), earlier, Monday, Def(varietyEnabled: true));
            Assert.AreEqual(15, reward.VarietyBonus);
        }

        [Test]
        public void Variety_WhenEnabled_AlreadyClaimedToday_PaysNothing()
        {
            var state = StateOnMonday(1, firstClaimed: true, archetypes: HermitRewardCalculator.AllClasicoArchetypes, varietyClaimed: true);
            var reward = HermitRewardCalculator.Calculate(Session(), state, Monday, Def(varietyEnabled: true));
            Assert.AreEqual(0, reward.VarietyBonus);
        }

        // --- Weekly consistency ----------------------------------------------

        [TestCase(0, 0)]
        [TestCase(3, 0)]
        [TestCase(4, 225)]
        public void Weekly_PaysOnTheFifthDistinctActiveDay(int priorActiveDays, int expectedBonus)
        {
            var day = Monday.AddDays(priorActiveDays);
            var prior = Enumerable.Range(0, priorActiveDays).Select(i => HermitCalendar.DayId(Monday.AddDays(i)));
            var state = new HermitActivityState(HermitCalendar.DayId(day), 0, null, false, false, HermitCalendar.WeekId(day), prior, false);

            var reward = HermitRewardCalculator.Calculate(Session(), state, day, Def());

            Assert.AreEqual(expectedBonus, reward.WeeklyConsistencyBonus);
            Assert.AreEqual(priorActiveDays + 1, reward.ActiveDaysThisWeekAfter);
        }

        [Test]
        public void Weekly_AlreadyClaimed_PaysNothing_AndASecondSessionTheSameDayIsNotANewDay()
        {
            var friday = Monday.AddDays(4);
            var days = Enumerable.Range(0, 5).Select(i => HermitCalendar.DayId(Monday.AddDays(i))).ToList();
            var claimed = new HermitActivityState(HermitCalendar.DayId(friday), 1, null, true, false, HermitCalendar.WeekId(friday), days, true);

            var reward = HermitRewardCalculator.Calculate(Session(), claimed, friday, Def());
            Assert.AreEqual(0, reward.WeeklyConsistencyBonus);
            Assert.AreEqual(5, reward.ActiveDaysThisWeekAfter, "Today was already an active day — it must not count twice.");
        }

        [Test]
        public void Weekly_NewWeek_ResetsActiveDaysAndClaim()
        {
            var sunday = Monday.AddDays(6);
            var days = Enumerable.Range(0, 5).Select(i => HermitCalendar.DayId(Monday.AddDays(i))).ToList();
            var lastWeek = new HermitActivityState(HermitCalendar.DayId(sunday), 1, null, true, false, HermitCalendar.WeekId(sunday), days, true);

            var nextMonday = Monday.AddDays(7);
            var rolled = lastWeek.RolledOverTo(nextMonday);
            Assert.AreEqual("2026-09-28", rolled.CurrentWeekId);
            Assert.IsEmpty(rolled.ActiveDaysThisWeek);
            Assert.IsFalse(rolled.WeeklyBonusClaimed);

            var reward = HermitRewardCalculator.Calculate(Session(), lastWeek, nextMonday, Def());
            Assert.AreEqual(0, reward.WeeklyConsistencyBonus);
            Assert.AreEqual(1, reward.ActiveDaysThisWeekAfter);
        }

        [Test]
        public void Calendar_WeekIsMondayToSunday()
        {
            Assert.AreEqual("2026-09-21", HermitCalendar.WeekId(new DateTime(2026, 9, 21)));
            Assert.AreEqual("2026-09-21", HermitCalendar.WeekId(new DateTime(2026, 9, 27, 23, 59, 0)));
            Assert.AreEqual("2026-09-28", HermitCalendar.WeekId(new DateTime(2026, 9, 28, 0, 0, 1)));
        }

        // --- Valid-session rule ----------------------------------------------

        [Test]
        public void Invalid_AbortedSession_PaysNothing()
        {
            var reward = HermitRewardCalculator.Calculate(Session(completed: false), HermitActivityState.Empty, Monday, Def());
            Assert.IsFalse(reward.WasValidSession);
            Assert.AreEqual(HermitInvalidSessionReason.Aborted, reward.InvalidReason);
            Assert.AreEqual(0, reward.TotalReward);
        }

        [Test]
        public void Invalid_AllPassiveTimeouts_PaysNothing()
        {
            var reward = HermitRewardCalculator.Calculate(Session(correct: 0, interacted: 0), HermitActivityState.Empty, Monday, Def());
            Assert.IsFalse(reward.WasValidSession);
            Assert.AreEqual(HermitInvalidSessionReason.InsufficientInteraction, reward.InvalidReason);
            Assert.AreEqual(0, reward.TotalReward);

            var threePassive = HermitRewardCalculator.Calculate(ThreeRoundSession(0, interacted: 0), HermitActivityState.Empty, Monday, Def());
            Assert.IsFalse(threePassive.WasValidSession, "3 no-input timeouts must not pay.");
        }

        [Test]
        public void Invalid_TooFewInteractedRounds_PaysNothing()
        {
            Assert.IsFalse(HermitRewardCalculator.Calculate(Session(correct: 5, interacted: 5), HermitActivityState.Empty, Monday, Def()).WasValidSession,
                "5 of 9 interacted rounds is below 2/3.");
            Assert.IsFalse(HermitRewardCalculator.Calculate(ThreeRoundSession(1, interacted: 1), HermitActivityState.Empty, Monday, Def()).WasValidSession,
                "Only 1 of 3 interacted rounds must not pay.");
        }

        [Test]
        public void Valid_WithSomeTimeouts_AsLongAsTwoThirdsWereInteracted()
        {
            var nine = HermitRewardCalculator.Calculate(Session(correct: 2, interacted: 6), HermitActivityState.Empty, Monday, Def());
            Assert.IsTrue(nine.WasValidSession, "6 of 9 interacted (3 timeouts) is valid.");
            Assert.AreEqual(12 + 0 + 0 + 25, nine.TotalReward, "Participation pays even with few correct answers.");

            var three = HermitRewardCalculator.Calculate(ThreeRoundSession(0, interacted: 2), HermitActivityState.Empty, Monday, Def());
            Assert.IsTrue(three.WasValidSession, "2 of 3 interacted with one timeout is valid.");
        }

        [Test]
        public void Invalid_IncompleteRounds_PaysNothing()
        {
            var short1 = new ClasicoSessionResult("short", true, 9, Session().Rounds.Take(7));
            var reward = HermitRewardCalculator.Calculate(short1, HermitActivityState.Empty, Monday, Def());
            Assert.AreEqual(HermitInvalidSessionReason.IncompleteRounds, reward.InvalidReason);
        }

        [Test]
        public void FullBreakdown_FirstPerfectFastSession()
        {
            var reward = HermitRewardCalculator.Calculate(Session(correct: 9, responseFraction: 0.1f), HermitActivityState.Empty, Monday, Def());
            Assert.AreEqual(12, reward.BaseReward);
            Assert.AreEqual(8, reward.AccuracyBonus);
            Assert.AreEqual(4, reward.SpeedBonus);
            Assert.AreEqual(24, reward.GameplaySubtotalBeforeMultiplier);
            Assert.AreEqual(1, reward.DailySessionNumber);
            Assert.AreEqual(24, reward.GameplayRewardAfterMultiplier);
            Assert.AreEqual(25, reward.FirstSessionBonus);
            Assert.AreEqual(49, reward.TotalReward);
        }

        // --- Service: wallet, idempotency, persistence, rollover -------------

        private static (HermitEconomyService service, InMemoryHermitEconomyStore store, FixedHermitClock clock) NewService(DateTime? now = null)
        {
            var store = new InMemoryHermitEconomyStore();
            var clock = new FixedHermitClock(now ?? Monday);
            return (new HermitEconomyService(store, clock, Def()), store, clock);
        }

        [Test]
        public void Wallet_CreditsTheRewardTotal_AndLifetimeEarned()
        {
            var (service, _, _) = NewService();
            var outcome = service.ProcessClasicoSession(Session(correct: 9, responseFraction: 0.1f));

            Assert.IsTrue(outcome.Credited);
            Assert.AreEqual(49, service.Wallet.Balance);
            Assert.AreEqual(49, service.Wallet.LifetimeEarned);
            Assert.AreEqual(0, service.Wallet.LifetimeSpent);
            Assert.AreEqual(49, outcome.BalanceAfter);
            Assert.AreEqual(1, service.Wallet.Transactions.Count);
            Assert.AreEqual(outcome.Reward.RewardId, service.Wallet.Transactions[0].Reference);
        }

        [Test]
        public void Wallet_SameSessionTwice_NeverCreditsTwice()
        {
            var (service, store, _) = NewService();
            var session = Session();
            var first = service.ProcessClasicoSession(session);
            var savesAfterFirst = store.SaveCount;

            var again = service.ProcessClasicoSession(session);

            Assert.IsTrue(first.Credited);
            Assert.IsFalse(again.Credited);
            Assert.IsTrue(again.AlreadyCredited);
            Assert.AreSame(first.Reward, again.Reward, "A re-shown result must show the original breakdown.");
            Assert.AreEqual(first.BalanceAfter, service.Wallet.Balance);
            Assert.AreEqual(1, service.State.ValidSessionsToday, "A duplicate must not consume another daily session slot.");
            Assert.AreEqual(savesAfterFirst, store.SaveCount);
        }

        [Test]
        public void Wallet_TryCredit_RejectsInvalidAndDuplicateRewards()
        {
            var wallet = new HermitWallet();
            var valid = HermitRewardCalculator.Calculate(Session(id: "x"), HermitActivityState.Empty, Monday, Def());
            var invalid = HermitRewardCalculator.Calculate(Session(completed: false, id: "y"), HermitActivityState.Empty, Monday, Def());

            Assert.IsFalse(wallet.TryCredit(invalid, DateTime.UtcNow, out _));
            Assert.IsTrue(wallet.TryCredit(valid, DateTime.UtcNow, out var tx));
            Assert.AreEqual(valid.TotalReward, tx.Amount);
            Assert.IsFalse(wallet.TryCredit(valid, DateTime.UtcNow, out _));
            Assert.AreEqual(valid.TotalReward, wallet.Balance);
        }

        [Test]
        public void InvalidSession_ChangesNothing_AndDoesNotConsumeTheFirstSessionBonus()
        {
            var (service, store, _) = NewService();
            var aborted = service.ProcessClasicoSession(Session(completed: false));
            var passive = service.ProcessClasicoSession(Session(correct: 0, interacted: 0));

            Assert.IsFalse(aborted.Credited);
            Assert.IsFalse(passive.Credited);
            Assert.AreEqual(0, service.Wallet.Balance);
            Assert.AreEqual(0, store.SaveCount);

            var valid = service.ProcessClasicoSession(Session());
            Assert.AreEqual(25, valid.Reward.FirstSessionBonus, "An invalid session must not consume the daily first-session bonus.");
            Assert.AreEqual(1, valid.Reward.DailySessionNumber);
        }

        [Test]
        public void SaveReload_PreservesBalanceDailyAndWeeklyState_AndIdempotency()
        {
            var (service, store, clock) = NewService();
            var session = Session();
            var first = service.ProcessClasicoSession(session);
            service.ProcessClasicoSession(Session());

            var reloaded = new HermitEconomyService(store, clock, Def());
            Assert.AreEqual(service.Wallet.Balance, reloaded.Wallet.Balance);
            Assert.AreEqual(service.Wallet.LifetimeEarned, reloaded.Wallet.LifetimeEarned);
            Assert.AreEqual(2, reloaded.State.ValidSessionsToday);
            Assert.IsTrue(reloaded.State.FirstSessionBonusClaimedToday);
            CollectionAssert.AreEquivalent(new[] { "2026-09-21" }, reloaded.State.ActiveDaysThisWeek);
            CollectionAssert.IsSupersetOf(reloaded.State.ArchetypesCompletedToday, HermitRewardCalculator.AllClasicoArchetypes);

            var replay = reloaded.ProcessClasicoSession(session);
            Assert.IsFalse(replay.Credited, "A session credited before the restart must not pay again after it.");
            Assert.IsTrue(replay.AlreadyCredited);
            Assert.IsNull(replay.Reward, "After a restart only 'already credited' is known.");
            Assert.AreEqual(service.Wallet.Balance, reloaded.Wallet.Balance);

            var third = reloaded.ProcessClasicoSession(Session());
            Assert.AreEqual(3, third.Reward.DailySessionNumber);
            Assert.AreEqual(0.60f, third.Reward.DiminishingMultiplier, 1e-6f);
            Assert.AreEqual(0, third.Reward.FirstSessionBonus);
            Assert.IsNotNull(first.Reward);
        }

        [Test]
        public void DayRollover_ResetsDailyFields_KeepsLifetimeAndWeek()
        {
            var (service, store, clock) = NewService();
            service.ProcessClasicoSession(Session());
            service.ProcessClasicoSession(Session());
            var lifetimeBefore = service.Wallet.LifetimeEarned;

            clock.LocalNow = Monday.AddDays(1);
            var reloaded = new HermitEconomyService(store, clock, Def());
            var tuesday = reloaded.ProcessClasicoSession(Session());

            Assert.AreEqual(1, tuesday.Reward.DailySessionNumber);
            Assert.AreEqual(1.00f, tuesday.Reward.DiminishingMultiplier, 1e-6f);
            Assert.AreEqual(25, tuesday.Reward.FirstSessionBonus);
            Assert.AreEqual(2, tuesday.Reward.ActiveDaysThisWeekAfter);
            Assert.AreEqual(lifetimeBefore + tuesday.Reward.TotalReward, reloaded.Wallet.LifetimeEarned, "Lifetime totals never reset.");
        }

        [Test]
        public void WeekRollover_ResetsWeeklyFields_AndTheWeeklyBonusPaysOncePerWeek()
        {
            var (service, _, clock) = NewService();
            var weeklyPaid = 0;
            for (var day = 0; day < 7; day++)
            {
                clock.LocalNow = Monday.AddDays(day);
                weeklyPaid += service.ProcessClasicoSession(Session()).Reward.WeeklyConsistencyBonus;
            }

            Assert.AreEqual(225, weeklyPaid, "Seven active days in one week pay the weekly bonus exactly once (on day 5).");

            clock.LocalNow = Monday.AddDays(7);
            var nextWeek = service.ProcessClasicoSession(Session());
            Assert.AreEqual(0, nextWeek.Reward.WeeklyConsistencyBonus);
            Assert.AreEqual(1, nextWeek.Reward.ActiveDaysThisWeekAfter);
            Assert.AreEqual("2026-09-28", service.State.CurrentWeekId);
        }

        [Test]
        public void ResetAll_ClearsTheSave()
        {
            var (service, store, _) = NewService();
            service.ProcessClasicoSession(Session());
            service.ResetAll();

            Assert.AreEqual(0, service.Wallet.Balance);
            Assert.IsNull(store.Load());
            Assert.AreEqual(25, service.ProcessClasicoSession(Session()).Reward.FirstSessionBonus);
        }

        [Test]
        public void LocalFileStore_RoundTrips_AndSetsACorruptFileAside()
        {
            var directory = Path.Combine(Path.GetTempPath(), "hermit_economy_test_" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "hermit_economy.json");
            try
            {
                var store = new LocalFileHermitEconomyStore(path);
                Assert.IsNull(store.Load());

                var service = new HermitEconomyService(store, new FixedHermitClock(Monday), Def());
                service.ProcessClasicoSession(Session());
                var balance = service.Wallet.Balance;

                var loaded = store.Load();
                Assert.AreEqual(HermitEconomySaveData.CurrentVersion, loaded.version);
                Assert.AreEqual(balance, loaded.balance);

                File.WriteAllText(path, "{ not json");
                Assert.IsNull(store.Load(), "A corrupt save must not crash — start fresh.");
                Assert.IsFalse(File.Exists(path));
                Assert.IsTrue(Directory.GetFiles(directory, "*.bak").Length == 1, "The corrupt save must be set aside, not destroyed.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
