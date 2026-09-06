using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace LightweightCountdown
{
    internal sealed class AppSettings
    {
        private const string AppFolderName = "LightweightCountdown";
        private const string SettingsFileName = "settings.ini";

        public string CountdownName = "重要日程";
        public long TargetUtcTicks = DateTime.UtcNow.AddDays(1).Ticks;
        public bool IsRunning;
        public bool WasStarted;
        public long PausedTicks = TimeSpan.FromDays(1).Ticks;
        public long InitialTicks = TimeSpan.FromDays(1).Ticks;
        public int WindowX = int.MinValue;
        public int WindowY = int.MinValue;
        public int WindowWidth = 340;
        public int WindowHeight = 56;
        public bool AlwaysOnTop = true;
        public List<CountdownItem> Items = new List<CountdownItem>();
        public int SelectedIndex;

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            string path = GetSettingsPath();
            if (!File.Exists(path))
            {
                settings.Items.Add(CountdownItem.CreateDefault(settings.CountdownName));
                return settings;
            }

            try
            {
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf('=');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    string key = lines[i].Substring(0, separator).Trim();
                    string value = lines[i].Substring(separator + 1).Trim();
                    values[key] = value;
                }

                string encodedName;
                if (values.TryGetValue("Name", out encodedName))
                {
                    try
                    {
                        settings.CountdownName = Encoding.UTF8.GetString(Convert.FromBase64String(encodedName));
                    }
                    catch (FormatException)
                    {
                        settings.CountdownName = "重要日程";
                    }
                }

                settings.TargetUtcTicks = ReadLong(values, "TargetUtcTicks", settings.TargetUtcTicks);
                settings.IsRunning = ReadBool(values, "IsRunning", false);
                settings.WasStarted = ReadBool(values, "WasStarted", false);
                settings.PausedTicks = ReadLong(values, "PausedTicks", settings.PausedTicks);
                settings.InitialTicks = ReadLong(values, "InitialTicks", settings.InitialTicks);
                settings.WindowX = ReadInt(values, "WindowX", int.MinValue);
                settings.WindowY = ReadInt(values, "WindowY", int.MinValue);
                int layoutVersion = ReadInt(values, "LayoutVersion", 0);
                settings.WindowWidth = layoutVersion >= 8
                    ? Math.Max(320, ReadInt(values, "WindowWidth", 340))
                    : 340;
                settings.WindowHeight = layoutVersion >= 8
                    ? Math.Max(52, ReadInt(values, "WindowHeight", 56))
                    : 56;
                settings.AlwaysOnTop = ReadBool(values, "AlwaysOnTop", true);

                if (settings.TargetUtcTicks < DateTime.MinValue.Ticks || settings.TargetUtcTicks > DateTime.MaxValue.Ticks)
                {
                    settings.TargetUtcTicks = DateTime.UtcNow.AddDays(1).Ticks;
                    settings.IsRunning = false;
                }

                settings.PausedTicks = Math.Max(0L, settings.PausedTicks);
                settings.InitialTicks = Math.Max(TimeSpan.FromSeconds(1).Ticks, settings.InitialTicks);
                if (string.IsNullOrWhiteSpace(settings.CountdownName))
                {
                    settings.CountdownName = "重要日程";
                }

                int itemCount = Math.Max(0, Math.Min(100, ReadInt(values, "ItemCount", 0)));
                for (int i = 0; i < itemCount; i++)
                {
                    string prefix = "Item" + i.ToString(CultureInfo.InvariantCulture) + ".";
                    CountdownItem item = CountdownItem.CreateDefault("倒计日 " + (i + 1).ToString(CultureInfo.InvariantCulture));
                    string itemName;
                    if (values.TryGetValue(prefix + "Name", out itemName))
                    {
                        item.Name = DecodeName(itemName, item.Name);
                    }

                    int mode = ReadInt(values, prefix + "Mode", 0);
                    item.Mode = mode == 1 ? CountdownMode.Monthly : CountdownMode.Once;
                    item.TargetUtcTicks = ReadLong(values, prefix + "TargetUtcTicks", item.TargetUtcTicks);
                    item.MonthlyDay = Math.Max(1, Math.Min(31, ReadInt(values, prefix + "MonthlyDay", item.MonthlyDay)));
                    item.LocalTimeOfDayTicks = ReadLong(values, prefix + "LocalTimeOfDayTicks", item.LocalTimeOfDayTicks);
                    item.IsRunning = ReadBool(values, prefix + "IsRunning", false);
                    item.WasStarted = ReadBool(values, prefix + "WasStarted", false);
                    item.PausedTicks = ReadLong(values, prefix + "PausedTicks", item.PausedTicks);
                    item.InitialTicks = ReadLong(values, prefix + "InitialTicks", item.InitialTicks);
                    ValidateItem(item);
                    settings.Items.Add(item);
                }

                settings.SelectedIndex = ReadInt(values, "SelectedIndex", 0);
            }
            catch (IOException)
            {
                AppSettings fallback = new AppSettings();
                fallback.Items.Add(CountdownItem.CreateDefault(fallback.CountdownName));
                return fallback;
            }
            catch (UnauthorizedAccessException)
            {
                AppSettings fallback = new AppSettings();
                fallback.Items.Add(CountdownItem.CreateDefault(fallback.CountdownName));
                return fallback;
            }

            if (settings.Items.Count == 0)
            {
                CountdownItem migrated = CountdownItem.CreateDefault(settings.CountdownName);
                migrated.TargetUtcTicks = settings.TargetUtcTicks;
                migrated.IsRunning = settings.IsRunning;
                migrated.WasStarted = settings.WasStarted;
                migrated.PausedTicks = settings.PausedTicks;
                migrated.InitialTicks = settings.InitialTicks;
                DateTime localTarget = new DateTime(migrated.TargetUtcTicks, DateTimeKind.Utc).ToLocalTime();
                migrated.MonthlyDay = localTarget.Day;
                migrated.LocalTimeOfDayTicks = localTarget.TimeOfDay.Ticks;
                ValidateItem(migrated);
                settings.Items.Add(migrated);
            }

            settings.SelectedIndex = Math.Max(0, Math.Min(settings.Items.Count - 1, settings.SelectedIndex));
            return settings;
        }

        public void Save(Rectangle windowBounds)
        {
            WindowX = windowBounds.X;
            WindowY = windowBounds.Y;
            WindowWidth = Math.Max(320, windowBounds.Width);
            WindowHeight = Math.Max(52, windowBounds.Height);

            string path = GetSettingsPath();
            string directory = Path.GetDirectoryName(path);
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (Items == null)
                {
                    Items = new List<CountdownItem>();
                }
                if (Items.Count == 0)
                {
                    Items.Add(CountdownItem.CreateDefault("重要日程"));
                }

                SelectedIndex = Math.Max(0, Math.Min(Items.Count - 1, SelectedIndex));
                CountdownItem selected = Items[SelectedIndex];
                CountdownName = selected.Name;
                TargetUtcTicks = selected.TargetUtcTicks;
                IsRunning = selected.IsRunning;
                WasStarted = selected.WasStarted;
                PausedTicks = selected.PausedTicks;
                InitialTicks = selected.InitialTicks;

                List<string> lines = new List<string>();
                lines.Add("Version=8");
                lines.Add("LayoutVersion=8");
                lines.Add("Name=" + EncodeName(CountdownName));
                lines.Add("TargetUtcTicks=" + TargetUtcTicks.ToString(CultureInfo.InvariantCulture));
                lines.Add("IsRunning=" + IsRunning.ToString(CultureInfo.InvariantCulture));
                lines.Add("WasStarted=" + WasStarted.ToString(CultureInfo.InvariantCulture));
                lines.Add("PausedTicks=" + PausedTicks.ToString(CultureInfo.InvariantCulture));
                lines.Add("InitialTicks=" + InitialTicks.ToString(CultureInfo.InvariantCulture));
                lines.Add("WindowX=" + WindowX.ToString(CultureInfo.InvariantCulture));
                lines.Add("WindowY=" + WindowY.ToString(CultureInfo.InvariantCulture));
                lines.Add("WindowWidth=" + WindowWidth.ToString(CultureInfo.InvariantCulture));
                lines.Add("WindowHeight=" + WindowHeight.ToString(CultureInfo.InvariantCulture));
                lines.Add("AlwaysOnTop=" + AlwaysOnTop.ToString(CultureInfo.InvariantCulture));
                lines.Add("SelectedIndex=" + SelectedIndex.ToString(CultureInfo.InvariantCulture));
                lines.Add("ItemCount=" + Items.Count.ToString(CultureInfo.InvariantCulture));

                for (int i = 0; i < Items.Count; i++)
                {
                    CountdownItem item = Items[i];
                    ValidateItem(item);
                    string prefix = "Item" + i.ToString(CultureInfo.InvariantCulture) + ".";
                    lines.Add(prefix + "Name=" + EncodeName(item.Name));
                    lines.Add(prefix + "Mode=" + ((int)item.Mode).ToString(CultureInfo.InvariantCulture));
                    lines.Add(prefix + "TargetUtcTicks=" + item.TargetUtcTicks.ToString(CultureInfo.InvariantCulture));
                    lines.Add(prefix + "MonthlyDay=" + item.MonthlyDay.ToString(CultureInfo.InvariantCulture));
                    lines.Add(prefix + "LocalTimeOfDayTicks=" + item.LocalTimeOfDayTicks.ToString(CultureInfo.InvariantCulture));
                    lines.Add(prefix + "IsRunning=" + item.IsRunning.ToString(CultureInfo.InvariantCulture));
                    lines.Add(prefix + "WasStarted=" + item.WasStarted.ToString(CultureInfo.InvariantCulture));
                    lines.Add(prefix + "PausedTicks=" + item.PausedTicks.ToString(CultureInfo.InvariantCulture));
                    lines.Add(prefix + "InitialTicks=" + item.InitialTicks.ToString(CultureInfo.InvariantCulture));
                }

                File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (IOException)
            {
                // Settings are a convenience; the countdown keeps working if saving fails.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the app usable in restricted Windows profiles.
            }
        }

        private static string GetSettingsPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName,
                SettingsFileName);
        }

        private static string EncodeName(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeName(string encoded, string fallback)
        {
            try
            {
                string value = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return string.IsNullOrWhiteSpace(value) ? fallback : value;
            }
            catch (FormatException)
            {
                return fallback;
            }
        }

        private static void ValidateItem(CountdownItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                item.Name = "未命名目标";
            }
            if (item.TargetUtcTicks < DateTime.MinValue.Ticks || item.TargetUtcTicks > DateTime.MaxValue.Ticks)
            {
                item.TargetUtcTicks = DateTime.UtcNow.AddDays(1).Ticks;
                item.IsRunning = false;
            }
            item.MonthlyDay = Math.Max(1, Math.Min(31, item.MonthlyDay));
            item.LocalTimeOfDayTicks = Math.Max(0L, Math.Min(TimeSpan.TicksPerDay - 1L, item.LocalTimeOfDayTicks));
            item.PausedTicks = Math.Max(0L, item.PausedTicks);
            item.InitialTicks = Math.Max(TimeSpan.FromSeconds(1).Ticks, item.InitialTicks);
        }

        private static long ReadLong(Dictionary<string, string> values, string key, long fallback)
        {
            string value;
            long parsed;
            return values.TryGetValue(key, out value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static int ReadInt(Dictionary<string, string> values, string key, int fallback)
        {
            string value;
            int parsed;
            return values.TryGetValue(key, out value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static bool ReadBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string value;
            bool parsed;
            return values.TryGetValue(key, out value) && bool.TryParse(value, out parsed)
                ? parsed
                : fallback;
        }
    }
}
