using System;
using System.Globalization;

namespace Hermit.Economy
{
    /// <summary>C9.1: the economy's only source of "now", so daily/weekly
    /// rules are testable (a fake clock) and never scattered
    /// <c>DateTime.Now</c> calls.</summary>
    public interface IHermitClock
    {
        /// <summary>Local wall-clock time — decides the calendar day/week.</summary>
        DateTime LocalNow { get; }

        /// <summary>UTC time — stamps wallet transactions.</summary>
        DateTime UtcNow { get; }
    }

    public sealed class SystemHermitClock : IHermitClock
    {
        public DateTime LocalNow => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Settable clock for tests and dev tooling.</summary>
    public sealed class FixedHermitClock : IHermitClock
    {
        public DateTime LocalNow { get; set; }
        public DateTime UtcNow => LocalNow.ToUniversalTime();

        public FixedHermitClock(DateTime localNow)
        {
            LocalNow = localNow;
        }
    }

    /// <summary>C9.1 calendar rules. A day is the LOCAL calendar date. A
    /// week is Monday through Sunday (local), identified by its Monday's
    /// date. Both stored as invariant "yyyy-MM-dd" strings.</summary>
    public static class HermitCalendar
    {
        public const string DateFormat = "yyyy-MM-dd";

        public static string DayId(DateTime localDate) =>
            localDate.Date.ToString(DateFormat, CultureInfo.InvariantCulture);

        public static DateTime MondayOf(DateTime localDate)
        {
            var date = localDate.Date;
            var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7; // Monday=0 ... Sunday=6
            return date.AddDays(-daysSinceMonday);
        }

        public static string WeekId(DateTime localDate) => DayId(MondayOf(localDate));
    }
}
