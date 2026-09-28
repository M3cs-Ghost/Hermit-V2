using System;
using System.Collections.Generic;
using System.Linq;
using Hermit.Games.Clasico.Microgames;

namespace Hermit.Economy
{
    /// <summary>
    /// C9.1: the player's local practice activity — only what the reward
    /// rules need (daily session count and bonus claims, the Monday–Sunday
    /// week's active days and weekly claim). Immutable: every change returns
    /// a new instance, so <see cref="HermitRewardCalculator"/> can reason
    /// about it without side effects.
    ///
    /// Daily fields belong to <see cref="CurrentLocalDate"/>; weekly fields
    /// to <see cref="CurrentWeekId"/>. <see cref="RolledOverTo"/> resets
    /// whichever no longer matches "today" — the only way a new day or week
    /// starts, so an app restart on a later day behaves exactly like staying
    /// open across midnight.
    /// </summary>
    public sealed class HermitActivityState
    {
        public string CurrentLocalDate { get; }
        public int ValidSessionsToday { get; }
        public IReadOnlyCollection<MicrogameArchetype> ArchetypesCompletedToday { get; }
        public bool FirstSessionBonusClaimedToday { get; }
        public bool VarietyBonusClaimedToday { get; }

        public string CurrentWeekId { get; }
        public IReadOnlyCollection<string> ActiveDaysThisWeek { get; }
        public bool WeeklyBonusClaimed { get; }

        public HermitActivityState(
            string currentLocalDate,
            int validSessionsToday,
            IEnumerable<MicrogameArchetype> archetypesCompletedToday,
            bool firstSessionBonusClaimedToday,
            bool varietyBonusClaimedToday,
            string currentWeekId,
            IEnumerable<string> activeDaysThisWeek,
            bool weeklyBonusClaimed)
        {
            CurrentLocalDate = currentLocalDate ?? string.Empty;
            ValidSessionsToday = Math.Max(0, validSessionsToday);
            ArchetypesCompletedToday = (archetypesCompletedToday ?? Enumerable.Empty<MicrogameArchetype>()).Distinct().ToList();
            FirstSessionBonusClaimedToday = firstSessionBonusClaimedToday;
            VarietyBonusClaimedToday = varietyBonusClaimedToday;
            CurrentWeekId = currentWeekId ?? string.Empty;
            ActiveDaysThisWeek = (activeDaysThisWeek ?? Enumerable.Empty<string>()).Distinct().ToList();
            WeeklyBonusClaimed = weeklyBonusClaimed;
        }

        public static HermitActivityState Empty { get; } =
            new HermitActivityState(string.Empty, 0, null, false, false, string.Empty, null, false);

        /// <summary>This state as seen on <paramref name="localDate"/>: daily
        /// fields reset when the day changed, weekly fields reset when the
        /// Monday–Sunday week changed. Lifetime wallet values live elsewhere
        /// and are never touched.</summary>
        public HermitActivityState RolledOverTo(DateTime localDate)
        {
            var dayId = HermitCalendar.DayId(localDate);
            var weekId = HermitCalendar.WeekId(localDate);
            var sameDay = CurrentLocalDate == dayId;
            var sameWeek = CurrentWeekId == weekId;

            if (sameDay && sameWeek)
            {
                return this;
            }

            return new HermitActivityState(
                dayId,
                sameDay ? ValidSessionsToday : 0,
                sameDay ? ArchetypesCompletedToday : null,
                sameDay && FirstSessionBonusClaimedToday,
                sameDay && VarietyBonusClaimedToday,
                weekId,
                sameWeek ? ActiveDaysThisWeek : null,
                sameWeek && WeeklyBonusClaimed);
        }

        /// <summary>The state after a VALID session was credited on
        /// <paramref name="localDate"/> with <paramref name="reward"/>.
        /// Invalid sessions never reach here, so they never consume a
        /// session slot, a bonus, or an active day.</summary>
        public HermitActivityState AfterValidSession(DateTime localDate, IEnumerable<MicrogameArchetype> archetypesPlayed, HermitRewardResult reward)
        {
            var today = RolledOverTo(localDate);
            var dayId = HermitCalendar.DayId(localDate);

            return new HermitActivityState(
                today.CurrentLocalDate,
                today.ValidSessionsToday + 1,
                today.ArchetypesCompletedToday.Concat(archetypesPlayed ?? Enumerable.Empty<MicrogameArchetype>()),
                today.FirstSessionBonusClaimedToday || reward.FirstSessionBonus > 0,
                today.VarietyBonusClaimedToday || reward.VarietyBonus > 0,
                today.CurrentWeekId,
                today.ActiveDaysThisWeek.Append(dayId),
                today.WeeklyBonusClaimed || reward.WeeklyConsistencyBonus > 0);
        }
    }
}
