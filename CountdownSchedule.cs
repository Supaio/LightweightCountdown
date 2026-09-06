using System;

namespace LightweightCountdown
{
    internal static class CountdownSchedule
    {
        public static TimeSpan SafeDuration(long ticks)
        {
            if (ticks <= 0L)
            {
                return TimeSpan.Zero;
            }

            long maximum = TimeSpan.FromDays(36500).Ticks;
            return TimeSpan.FromTicks(Math.Min(ticks, maximum));
        }

        public static TimeSpan SafeTimeOfDay(long ticks)
        {
            long safeTicks = Math.Max(0L, Math.Min(TimeSpan.TicksPerDay - 1L, ticks));
            return TimeSpan.FromTicks(safeTicks);
        }

        public static DateTime CalculateNextMonthlyOccurrence(int requestedDay, TimeSpan localTime, DateTime fromUtc)
        {
            DateTime localNow = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc).ToLocalTime();
            int safeDay = Math.Max(1, Math.Min(31, requestedDay));

            for (int monthOffset = 0; monthOffset < 24; monthOffset++)
            {
                DateTime month = new DateTime(localNow.Year, localNow.Month, 1).AddMonths(monthOffset);
                int actualDay = Math.Min(safeDay, DateTime.DaysInMonth(month.Year, month.Month));
                DateTime candidate = new DateTime(month.Year, month.Month, actualDay).Add(localTime);
                candidate = DateTime.SpecifyKind(candidate, DateTimeKind.Local);

                if (TimeZoneInfo.Local.IsInvalidTime(candidate))
                {
                    candidate = candidate.AddHours(1);
                }

                DateTime candidateUtc = candidate.ToUniversalTime();
                if (candidateUtc > fromUtc)
                {
                    return candidateUtc;
                }
            }

            return fromUtc.AddMonths(1);
        }

        public static DateTime CalculatePreviousMonthlyOccurrence(int requestedDay, TimeSpan localTime, DateTime beforeUtc)
        {
            DateTime localBefore = DateTime.SpecifyKind(beforeUtc, DateTimeKind.Utc).ToLocalTime();
            int safeDay = Math.Max(1, Math.Min(31, requestedDay));

            for (int monthOffset = 0; monthOffset < 24; monthOffset++)
            {
                DateTime month = new DateTime(localBefore.Year, localBefore.Month, 1).AddMonths(-monthOffset);
                int actualDay = Math.Min(safeDay, DateTime.DaysInMonth(month.Year, month.Month));
                DateTime candidate = new DateTime(month.Year, month.Month, actualDay).Add(localTime);
                candidate = DateTime.SpecifyKind(candidate, DateTimeKind.Local);

                if (TimeZoneInfo.Local.IsInvalidTime(candidate))
                {
                    candidate = candidate.AddHours(1);
                }

                DateTime candidateUtc = candidate.ToUniversalTime();
                if (candidateUtc < beforeUtc)
                {
                    return candidateUtc;
                }
            }

            return beforeUtc.AddMonths(-1);
        }

        public static TimeSpan CalculateMonthlyCycleDuration(
            int requestedDay,
            TimeSpan localTime,
            DateTime targetUtc)
        {
            DateTime previous = CalculatePreviousMonthlyOccurrence(requestedDay, localTime, targetUtc);
            TimeSpan duration = targetUtc - previous;
            return duration > TimeSpan.Zero ? duration : TimeSpan.FromSeconds(1);
        }
    }
}
