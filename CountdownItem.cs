using System;

namespace LightweightCountdown
{
    internal enum CountdownMode
    {
        Once = 0,
        Monthly = 1
    }

    internal sealed class CountdownItem
    {
        public string Name;
        public CountdownMode Mode;
        public long TargetUtcTicks;
        public int MonthlyDay;
        public long LocalTimeOfDayTicks;
        public bool IsRunning;
        public bool WasStarted;
        public long PausedTicks;
        public long InitialTicks;

        public static CountdownItem CreateDefault(string name)
        {
            DateTime target = DateTime.UtcNow.AddDays(1);
            DateTime localTarget = target.ToLocalTime();
            CountdownItem item = new CountdownItem();
            item.Name = name;
            item.Mode = CountdownMode.Once;
            item.TargetUtcTicks = target.Ticks;
            item.MonthlyDay = localTarget.Day;
            item.LocalTimeOfDayTicks = localTarget.TimeOfDay.Ticks;
            item.IsRunning = false;
            item.WasStarted = false;
            item.PausedTicks = TimeSpan.FromDays(1).Ticks;
            item.InitialTicks = TimeSpan.FromDays(1).Ticks;
            return item;
        }
    }
}
