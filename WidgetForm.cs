using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LightweightCountdown
{
    internal sealed class WidgetForm : Form
    {
        private readonly AppSettings settings;
        private readonly List<CountdownItem> countdownItems;
        private int selectedItemIndex;
        private bool allowExit;
        private bool suppressStartupChange;
        private bool trayHintShown;
        private bool pointerGesturePending;
        private bool itemReorderActive;
        private bool itemOrderChanged;
        private int draggedItemIndex = -1;
        private Point pointerDownLocation;
        private float pointerOffsetFromItemCenter;

        private Timer countdownTimer;
        private ToolTip widgetToolTip;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem showHideMenuItem;
        private ToolStripMenuItem projectsMenuItem;
        private ToolStripMenuItem toggleMenuItem;
        private ToolStripMenuItem resetMenuItem;
        private ToolStripMenuItem editMenuItem;
        private ToolStripMenuItem deleteMenuItem;
        private ToolStripMenuItem topMostMenuItem;
        private ToolStripMenuItem startupMenuItem;

        private const int WmNcHitTest = 0x0084;
        private const int WmNcLButtonDown = 0x00A1;
        private const int WmSizing = 0x0214;
        private const int WmMoving = 0x0216;
        private const int HtCaption = 2;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;
        private const int CompactItemMinimumHeight = 50;
        private const int CompactItemGap = 6;
        private const int DragActivationDistance = 6;
        private const int EdgeSnapDistance = 10;
        private const int WsExLayered = 0x00080000;
        private const int UlwAlpha = 0x00000002;
        private const byte AcSrcOver = 0x00;
        private const byte AcSrcAlpha = 0x01;
        private static readonly string CountdownFontName = ResolveCountdownFontName();

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wordParameter, IntPtr longParameter);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr iconHandle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr windowHandle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr windowHandle, IntPtr deviceContext);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr deviceContext);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr graphicsObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr windowHandle,
            IntPtr destinationDeviceContext,
            ref NativePoint destinationPoint,
            ref NativeSize size,
            IntPtr sourceDeviceContext,
            ref NativePoint sourcePoint,
            int colorKey,
            ref BlendFunction blend,
            int flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;

            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public int Width;
            public int Height;

            public NativeSize(int width, int height)
            {
                Width = width;
                Height = height;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOperation;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        public WidgetForm(bool startedWithWindows)
        {
            settings = AppSettings.Load();
            countdownItems = settings.Items;
            selectedItemIndex = Math.Max(0, Math.Min(countdownItems.Count - 1, settings.SelectedIndex));

            BuildWindow();
            BuildTrayMenu();

            countdownTimer = new Timer();
            countdownTimer.Interval = 1000;
            countdownTimer.Tick += CountdownTimer_Tick;

            ProcessDueItems(true);
            UpdateTimerState();
            UpdatePresentation();

            FormClosing += WidgetForm_FormClosing;
            FormClosed += WidgetForm_FormClosed;
            ResizeEnd += delegate
            {
                ConstrainCurrentBoundsToWorkingArea();
                PersistSettings();
            };

            widgetToolTip = new ToolTip();
            widgetToolTip.AutoPopDelay = 4500;
            widgetToolTip.InitialDelay = 700;
            widgetToolTip.ReshowDelay = 200;
            widgetToolTip.SetToolTip(this, "上下拖动项目可排序，横向拖动可移动浮窗；右键管理。 ");
        }

        private void BuildWindow()
        {
            SuspendLayout();
            Text = "轻量倒计日";
            BackColor = Color.Black;
            AllowTransparency = false;
            ForeColor = Theme.TextPrimary;
            Font = Theme.Font(9.5f, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            int minimumHeight = CalculateMinimumWidgetHeight();
            int maximumWidth = Math.Max(320, Screen.PrimaryScreen.WorkingArea.Width - 20);
            int maximumHeight = Math.Max(minimumHeight, Screen.PrimaryScreen.WorkingArea.Height - 20);
            MinimumSize = new Size(320, minimumHeight);
            Size = new Size(
                Math.Max(320, Math.Min(maximumWidth, settings.WindowWidth)),
                Math.Max(minimumHeight, Math.Min(maximumHeight, settings.WindowHeight)));
            TopMost = settings.AlwaysOnTop;
            DoubleBuffered = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            RestoreSavedPosition();
            ResumeLayout(false);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExLayered;
                return parameters;
            }
        }

        private void RestoreSavedPosition()
        {
            if (settings.WindowX == int.MinValue || settings.WindowY == int.MinValue)
            {
                return;
            }

            Rectangle proposed = new Rectangle(
                settings.WindowX,
                settings.WindowY,
                Width,
                Height);

            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                Rectangle visible = Rectangle.Intersect(proposed, screens[i].WorkingArea);
                if (visible.Width >= 140 && visible.Height >= 40)
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = ConstrainRectangleToWorkingArea(proposed, screens[i].WorkingArea, false);
                    return;
                }
            }
        }

        private void BuildTrayMenu()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.BackColor = Theme.SurfaceRaised;
            trayMenu.ForeColor = Theme.TextPrimary;
            trayMenu.Font = Theme.Font(9.2f, FontStyle.Regular);
            trayMenu.ShowImageMargin = false;
            trayMenu.ShowCheckMargin = true;
            trayMenu.Padding = new Padding(4);
            trayMenu.Opening += TrayMenu_Opening;

            showHideMenuItem = new ToolStripMenuItem("隐藏桌面倒计日");
            showHideMenuItem.Click += delegate { ToggleWidgetVisibility(); };

            projectsMenuItem = new ToolStripMenuItem("选择操作项目");

            toggleMenuItem = new ToolStripMenuItem("暂停当前项目");
            toggleMenuItem.Click += delegate { ToggleCurrentCountdown(); };

            resetMenuItem = new ToolStripMenuItem("重新计算当前项目");
            resetMenuItem.Click += delegate { ResetCurrentCountdown(); };

            ToolStripMenuItem createMenuItem = new ToolStripMenuItem("新建倒计日...");
            createMenuItem.Click += delegate { CreateCountdownItem(); };

            editMenuItem = new ToolStripMenuItem("编辑当前倒计日...");
            editMenuItem.Click += delegate { EditCurrentCountdown(); };

            deleteMenuItem = new ToolStripMenuItem("删除当前倒计日");
            deleteMenuItem.Click += delegate { DeleteCurrentCountdown(); };

            topMostMenuItem = new ToolStripMenuItem("始终置顶");
            topMostMenuItem.Click += delegate { ToggleAlwaysOnTop(); };

            startupMenuItem = new ToolStripMenuItem("开机自启");
            startupMenuItem.Click += delegate { ToggleStartup(); };

            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("退出");
            exitMenuItem.Click += delegate { ExitApplication(); };

            trayMenu.Items.Add(showHideMenuItem);
            trayMenu.Items.Add(projectsMenuItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(toggleMenuItem);
            trayMenu.Items.Add(resetMenuItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(createMenuItem);
            trayMenu.Items.Add(editMenuItem);
            trayMenu.Items.Add(deleteMenuItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(topMostMenuItem);
            trayMenu.Items.Add(startupMenuItem);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(exitMenuItem);

            trayIcon = new NotifyIcon();
            trayIcon.Icon = CreateApplicationIcon();
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { ToggleWidgetVisibility(); };
            Icon = trayIcon.Icon;

            suppressStartupChange = true;
            startupMenuItem.Checked = StartupManager.IsEnabled();
            suppressStartupChange = false;
            UpdateTrayTooltip();
        }

        private void TrayMenu_Opening(object sender, CancelEventArgs e)
        {
            showHideMenuItem.Text = Visible ? "隐藏桌面倒计日" : "显示桌面倒计日";
            topMostMenuItem.Checked = TopMost;
            startupMenuItem.Checked = StartupManager.IsEnabled();
            deleteMenuItem.Enabled = countdownItems.Count > 1;

            CountdownItem current = GetCurrentItem();
            if (current.IsRunning)
            {
                toggleMenuItem.Text = current.Mode == CountdownMode.Monthly ? "暂停每月提醒" : "暂停当前倒计日";
            }
            else
            {
                toggleMenuItem.Text = current.Mode == CountdownMode.Monthly ? "恢复每月提醒" : "启动当前倒计日";
            }

            projectsMenuItem.DropDownItems.Clear();
            for (int i = 0; i < countdownItems.Count; i++)
            {
                CountdownItem item = countdownItems[i];
                string prefix = item.IsRunning ? "●  " : string.Empty;
                ToolStripMenuItem project = new ToolStripMenuItem(prefix + item.Name);
                project.Checked = i == selectedItemIndex;
                project.Tag = i;
                project.Click += ProjectMenuItem_Click;
                projectsMenuItem.DropDownItems.Add(project);
            }
        }

        private void ProjectMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            if (menuItem == null || !(menuItem.Tag is int))
            {
                return;
            }

            int index = (int)menuItem.Tag;
            if (index < 0 || index >= countdownItems.Count)
            {
                return;
            }

            selectedItemIndex = index;
            UpdatePresentation();
            PersistSettings();
        }

        private CountdownItem GetCurrentItem()
        {
            return countdownItems[selectedItemIndex];
        }

        private TimeSpan GetRemaining(CountdownItem item)
        {
            TimeSpan remaining = item.IsRunning
                ? new DateTime(item.TargetUtcTicks, DateTimeKind.Utc) - DateTime.UtcNow
                : CountdownSchedule.SafeDuration(item.PausedTicks);
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        private static long CalculateInitialTicks(
            CountdownItem item,
            DateTime targetUtc,
            DateTime fromUtc,
            bool preserveOneTimeBaseline = false)
        {
            TimeSpan duration = item.Mode == CountdownMode.Monthly
                ? CountdownSchedule.CalculateMonthlyCycleDuration(
                    item.MonthlyDay,
                    CountdownSchedule.SafeTimeOfDay(item.LocalTimeOfDayTicks),
                    targetUtc)
                : targetUtc - fromUtc;
            long durationTicks = Math.Max(TimeSpan.FromSeconds(1).Ticks, duration.Ticks);
            return preserveOneTimeBaseline && item.Mode == CountdownMode.Once
                ? Math.Max(item.InitialTicks, durationTicks)
                : durationTicks;
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            if (!ProcessDueItems(true))
            {
                UpdatePresentation();
            }
        }

        private bool ProcessDueItems(bool notify)
        {
            DateTime now = DateTime.UtcNow;
            List<string> dueNames = new List<string>();
            bool changed = false;
            bool monthlyAdvanced = false;

            for (int i = 0; i < countdownItems.Count; i++)
            {
                CountdownItem item = countdownItems[i];
                if (!item.IsRunning)
                {
                    continue;
                }

                DateTime target = new DateTime(item.TargetUtcTicks, DateTimeKind.Utc);
                if (target > now)
                {
                    continue;
                }

                dueNames.Add(string.IsNullOrWhiteSpace(item.Name) ? "未命名目标" : item.Name);
                changed = true;
                if (item.Mode == CountdownMode.Monthly)
                {
                    DateTime next = CountdownSchedule.CalculateNextMonthlyOccurrence(
                        item.MonthlyDay,
                        CountdownSchedule.SafeTimeOfDay(item.LocalTimeOfDayTicks),
                        now);
                    item.TargetUtcTicks = next.Ticks;
                    item.InitialTicks = CalculateInitialTicks(item, next, now);
                    item.PausedTicks = 0L;
                    item.IsRunning = true;
                    item.WasStarted = true;
                    monthlyAdvanced = true;
                }
                else
                {
                    item.IsRunning = false;
                    item.WasStarted = true;
                    item.PausedTicks = 0L;
                }
            }

            if (!changed)
            {
                return false;
            }

            UpdateTimerState();
            UpdatePresentation();
            PersistSettings();

            if (notify && trayIcon != null)
            {
                string names = string.Join("、", dueNames.ToArray());
                if (names.Length > 120)
                {
                    names = names.Substring(0, 117) + "...";
                }
                string message = names + " 已到时间。";
                if (monthlyAdvanced)
                {
                    message += " 每月项目已自动安排到下个月。";
                }
                SystemSounds.Asterisk.Play();
                trayIcon.ShowBalloonTip(6000, "倒计日提醒", message, ToolTipIcon.Info);
            }

            return true;
        }

        private void ToggleCurrentCountdown()
        {
            CountdownItem item = GetCurrentItem();
            DateTime now = DateTime.UtcNow;
            if (item.IsRunning)
            {
                TimeSpan remaining = new DateTime(item.TargetUtcTicks, DateTimeKind.Utc) - now;
                if (remaining <= TimeSpan.Zero)
                {
                    ProcessDueItems(true);
                    return;
                }
                item.IsRunning = false;
                item.WasStarted = true;
                item.PausedTicks = remaining.Ticks;
                if (item.Mode == CountdownMode.Monthly)
                {
                    item.InitialTicks = CalculateInitialTicks(
                        item,
                        new DateTime(item.TargetUtcTicks, DateTimeKind.Utc),
                        now);
                }
            }
            else
            {
                DateTime target;
                if (item.Mode == CountdownMode.Monthly)
                {
                    target = CountdownSchedule.CalculateNextMonthlyOccurrence(
                        item.MonthlyDay,
                        CountdownSchedule.SafeTimeOfDay(item.LocalTimeOfDayTicks),
                        now);
                }
                else
                {
                    target = new DateTime(item.TargetUtcTicks, DateTimeKind.Utc);
                    if (target <= now)
                    {
                        MessageBox.Show(this, "这个指定日期已经过去，请先从托盘右键菜单编辑日期。", "日期已过", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }

                item.TargetUtcTicks = target.Ticks;
                item.InitialTicks = CalculateInitialTicks(item, target, now, true);
                item.PausedTicks = 0L;
                item.IsRunning = true;
                item.WasStarted = true;
            }

            UpdateTimerState();
            UpdatePresentation();
            PersistSettings();
        }

        private void ResetCurrentCountdown()
        {
            CountdownItem item = GetCurrentItem();
            DateTime now = DateTime.UtcNow;
            DateTime target;
            if (item.Mode == CountdownMode.Monthly)
            {
                target = CountdownSchedule.CalculateNextMonthlyOccurrence(
                    item.MonthlyDay,
                    CountdownSchedule.SafeTimeOfDay(item.LocalTimeOfDayTicks),
                    now);
            }
            else
            {
                target = new DateTime(item.TargetUtcTicks, DateTimeKind.Utc);
                if (target <= now)
                {
                    MessageBox.Show(this, "这个指定日期已经过去，请先编辑目标日期。", "日期已过", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            item.TargetUtcTicks = target.Ticks;
            item.InitialTicks = CalculateInitialTicks(item, target, now, true);
            item.PausedTicks = 0L;
            item.IsRunning = true;
            item.WasStarted = true;
            UpdateTimerState();
            UpdatePresentation();
            PersistSettings();
        }

        private void CreateCountdownItem()
        {
            CountdownItem draft = CountdownItem.CreateDefault("新的倒计日 " + (countdownItems.Count + 1).ToString(CultureInfo.InvariantCulture));
            using (CountdownEditorDialog dialog = new CountdownEditorDialog(draft, true))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                ApplyEditorResult(draft, dialog, true);
                countdownItems.Add(draft);
                selectedItemIndex = countdownItems.Count - 1;
            }

            UpdateMinimumWidgetSize(false);
            ShowWidget();
            UpdateTimerState();
            UpdatePresentation();
            PersistSettings();
        }

        private void EditCurrentCountdown()
        {
            CountdownItem item = GetCurrentItem();
            bool shouldRunAfterSave = item.IsRunning || !item.WasStarted;
            using (CountdownEditorDialog dialog = new CountdownEditorDialog(item, false))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                ApplyEditorResult(item, dialog, shouldRunAfterSave);
            }

            UpdateTimerState();
            UpdatePresentation();
            PersistSettings();
        }

        private static void ApplyEditorResult(CountdownItem item, CountdownEditorDialog dialog, bool runAfterSave)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan duration = dialog.TargetUtc - now;
            item.Name = dialog.CountdownName;
            item.Mode = dialog.CountdownMode;
            item.TargetUtcTicks = dialog.TargetUtc.Ticks;
            item.MonthlyDay = dialog.MonthlyDay;
            item.LocalTimeOfDayTicks = dialog.LocalTimeOfDayTicks;
            item.InitialTicks = CalculateInitialTicks(item, dialog.TargetUtc, now);
            item.IsRunning = runAfterSave;
            item.WasStarted = runAfterSave;
            item.PausedTicks = runAfterSave ? 0L : Math.Max(0L, duration.Ticks);
        }

        private void DeleteCurrentCountdown()
        {
            if (countdownItems.Count <= 1)
            {
                return;
            }

            CountdownItem item = GetCurrentItem();
            DialogResult answer = MessageBox.Show(
                this,
                "确定删除倒计日「" + item.Name + "」吗？",
                "删除倒计日",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            countdownItems.RemoveAt(selectedItemIndex);
            selectedItemIndex = Math.Min(selectedItemIndex, countdownItems.Count - 1);
            UpdateMinimumWidgetSize(true);
            UpdateTimerState();
            UpdatePresentation();
            PersistSettings();
        }

        private int CalculateMinimumWidgetHeight()
        {
            int count = Math.Max(1, countdownItems.Count);
            long requested = 6L + (long)count * CompactItemMinimumHeight + (long)(count - 1) * CompactItemGap;
            int screenLimit = Math.Max(56, Screen.PrimaryScreen.WorkingArea.Height - 20);
            return (int)Math.Max(56L, Math.Min(requested, screenLimit));
        }

        private void UpdateMinimumWidgetSize(bool shrinkToFit)
        {
            int minimumHeight = CalculateMinimumWidgetHeight();
            MinimumSize = new Size(320, minimumHeight);
            if (Height < minimumHeight || shrinkToFit)
            {
                Height = minimumHeight;
            }
            ConstrainCurrentBoundsToWorkingArea();
        }

        private void UpdateTimerState()
        {
            if (countdownTimer == null)
            {
                return;
            }

            bool anyRunning = false;
            for (int i = 0; i < countdownItems.Count; i++)
            {
                if (countdownItems[i].IsRunning)
                {
                    anyRunning = true;
                    break;
                }
            }
            countdownTimer.Enabled = anyRunning;
        }

        private void UpdatePresentation()
        {
            if (IsHandleCreated && Visible && !IsDisposed)
            {
                RenderLayeredWindow();
            }
            else
            {
                Invalidate();
            }
            UpdateTrayTooltip();
        }

        private void UpdateTrayTooltip()
        {
            if (trayIcon == null)
            {
                return;
            }

            CountdownItem item = GetCurrentItem();
            TimeSpan remaining = GetRemaining(item);
            long totalSeconds = (long)Math.Ceiling(remaining.TotalSeconds);
            long days = totalSeconds / 86400L;
            int hours = (int)((totalSeconds % 86400L) / 3600L);
            int minutes = (int)((totalSeconds % 3600L) / 60L);
            int seconds = (int)(totalSeconds % 60L);
            string tooltip = item.Name + " · " + days.ToString(CultureInfo.InvariantCulture) + "日 " +
                             hours.ToString("00", CultureInfo.InvariantCulture) + ":" +
                             minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                             seconds.ToString("00", CultureInfo.InvariantCulture);
            if (tooltip.Length > 63)
            {
                tooltip = tooltip.Substring(0, 60) + "...";
            }
            trayIcon.Text = tooltip;
        }

        private void ToggleWidgetVisibility()
        {
            if (Visible)
            {
                HideToTray(true);
            }
            else
            {
                ShowWidget();
            }
        }

        private void HideToTray(bool showHint)
        {
            Hide();
            if (showHint && !trayHintShown)
            {
                trayIcon.ShowBalloonTip(2500, "倒计日已隐藏", "双击托盘图标可重新显示；右键托盘可管理项目。", ToolTipIcon.Info);
                trayHintShown = true;
            }
        }

        private void ShowWidget()
        {
            Show();
            WindowState = FormWindowState.Normal;
            TopMost = settings.AlwaysOnTop;
            ConstrainCurrentBoundsToWorkingArea();
            RenderLayeredWindow();
            Activate();
            BringToFront();
        }

        private void ToggleAlwaysOnTop()
        {
            settings.AlwaysOnTop = !TopMost;
            TopMost = settings.AlwaysOnTop;
            topMostMenuItem.Checked = TopMost;
            PersistSettings();
        }

        private void ToggleStartup()
        {
            if (suppressStartupChange)
            {
                return;
            }

            bool desired = !StartupManager.IsEnabled();
            string error;
            if (!StartupManager.TrySetEnabled(desired, out error))
            {
                MessageBox.Show(this, error, "开机自启", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            startupMenuItem.Checked = desired;
        }

        private void ExitApplication()
        {
            allowExit = true;
            PersistSettings();
            trayIcon.Visible = false;
            Application.Exit();
        }

        private void WidgetForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !allowExit)
            {
                e.Cancel = true;
                HideToTray(true);
                return;
            }
            PersistSettings();
        }

        private void WidgetForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (countdownTimer != null)
            {
                countdownTimer.Dispose();
            }
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            if (widgetToolTip != null)
            {
                widgetToolTip.Dispose();
            }
        }

        private void PersistSettings()
        {
            settings.SelectedIndex = selectedItemIndex;
            settings.AlwaysOnTop = TopMost;
            settings.Save(Bounds);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.Transparent);
            DrawWidget(e.Graphics);
        }

        private void DrawWidget(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.GammaCorrected;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.TextContrast = 0;

            int count = Math.Max(1, countdownItems.Count);
            float outer;
            float gap;
            float itemHeight;
            GetItemLayout(out outer, out gap, out itemHeight);
            float y = outer;

            for (int i = 0; i < count; i++)
            {
                CountdownItem item = countdownItems[Math.Min(i, countdownItems.Count - 1)];
                RectangleF itemBounds = new RectangleF(
                    outer,
                    y,
                    Math.Max(1f, Width - outer * 2f),
                    itemHeight);
                DrawCompactItem(graphics, item, i == selectedItemIndex, itemBounds);
                y += itemHeight + gap;
            }
        }

        private void GetItemLayout(out float outer, out float gap, out float itemHeight)
        {
            int count = Math.Max(1, countdownItems.Count);
            outer = 3f;
            gap = count > 1 ? CompactItemGap : 0f;
            float availableHeight = Math.Max(1f, Height - outer * 2f - gap * (count - 1));
            itemHeight = availableHeight / count;
        }

        private Bitmap CreateWidgetBitmap(bool keepTransparentAreaInteractive)
        {
            Bitmap bitmap = new Bitmap(
                Math.Max(1, Width),
                Math.Max(1, Height),
                PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(keepTransparentAreaInteractive
                    ? Color.FromArgb(1, 0, 0, 0)
                    : Color.Transparent);
                DrawWidget(graphics);
            }
            return bitmap;
        }

        private bool RenderLayeredWindow()
        {
            if (!IsHandleCreated || !Visible || IsDisposed || Width <= 0 || Height <= 0)
            {
                return false;
            }

            using (Bitmap bitmap = CreateWidgetBitmap(true))
            {
                return ApplyLayeredBitmap(bitmap);
            }
        }

        private bool ApplyLayeredBitmap(Bitmap bitmap)
        {
            IntPtr screenDeviceContext = IntPtr.Zero;
            IntPtr memoryDeviceContext = IntPtr.Zero;
            IntPtr bitmapHandle = IntPtr.Zero;
            IntPtr previousObject = IntPtr.Zero;

            try
            {
                screenDeviceContext = GetDC(IntPtr.Zero);
                if (screenDeviceContext == IntPtr.Zero)
                {
                    return false;
                }

                memoryDeviceContext = CreateCompatibleDC(screenDeviceContext);
                if (memoryDeviceContext == IntPtr.Zero)
                {
                    return false;
                }

                bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0, 0, 0, 0));
                previousObject = SelectObject(memoryDeviceContext, bitmapHandle);

                NativePoint destination = new NativePoint(Left, Top);
                NativePoint source = new NativePoint(0, 0);
                NativeSize size = new NativeSize(bitmap.Width, bitmap.Height);
                BlendFunction blend = new BlendFunction();
                blend.BlendOperation = AcSrcOver;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = AcSrcAlpha;

                return UpdateLayeredWindow(
                    Handle,
                    screenDeviceContext,
                    ref destination,
                    ref size,
                    memoryDeviceContext,
                    ref source,
                    0,
                    ref blend,
                    UlwAlpha);
            }
            finally
            {
                if (previousObject != IntPtr.Zero && memoryDeviceContext != IntPtr.Zero)
                {
                    SelectObject(memoryDeviceContext, previousObject);
                }
                if (bitmapHandle != IntPtr.Zero)
                {
                    DeleteObject(bitmapHandle);
                }
                if (memoryDeviceContext != IntPtr.Zero)
                {
                    DeleteDC(memoryDeviceContext);
                }
                if (screenDeviceContext != IntPtr.Zero)
                {
                    ReleaseDC(IntPtr.Zero, screenDeviceContext);
                }
            }
        }

        private void DrawCompactItem(Graphics graphics, CountdownItem item, bool selected, RectangleF bounds)
        {
            TimeSpan remaining = GetRemaining(item);
            long totalSeconds = (long)Math.Ceiling(remaining.TotalSeconds);
            long days = totalSeconds / 86400L;
            int hours = (int)((totalSeconds % 86400L) / 3600L);
            int minutes = (int)((totalSeconds % 3600L) / 60L);
            int seconds = (int)(totalSeconds % 60L);

            bool completed = !item.IsRunning && item.WasStarted && item.PausedTicks <= 0L;
            Color statusColor = item.IsRunning
                ? Theme.Success
                : (completed ? Theme.Danger : (item.WasStarted ? Theme.Warning : Theme.Accent));

            float barHeight = 3f;
            float barTop = bounds.Bottom - barHeight - 1f;
            float contentHeight = Math.Max(1f, barTop - bounds.Y - 3f);
            float projectWidth = Math.Max(116f, Math.Min(136f, bounds.Width * 0.40f));
            float contentGap = 4f;
            RectangleF projectBounds = new RectangleF(bounds.X, bounds.Y, projectWidth, contentHeight);
            RectangleF countdownBounds = new RectangleF(
                projectBounds.Right + contentGap,
                bounds.Y,
                Math.Max(1f, bounds.Right - projectBounds.Right - contentGap),
                contentHeight);

            DrawProjectLabel(graphics, item.Name, statusColor, selected, projectBounds);
            DrawCountdownText(graphics, days, hours, minutes, seconds, countdownBounds);
            DrawCountdownBar(
                graphics,
                item,
                remaining,
                new RectangleF(bounds.X, barTop, Math.Max(1f, bounds.Width), barHeight));
        }

        private static void DrawProjectLabel(
            Graphics graphics,
            string name,
            Color statusColor,
            bool selected,
            RectangleF bounds)
        {
            float dotSize = 5.5f;
            float dotX = bounds.X + 1f;
            float dotY = bounds.Y + (bounds.Height - dotSize) / 2f;
            Color haloColor = selected ? Theme.Accent : statusColor;
            using (SolidBrush haloBrush = new SolidBrush(Color.FromArgb(selected ? 92 : 48, haloColor)))
            using (SolidBrush dotBrush = new SolidBrush(statusColor))
            {
                graphics.FillEllipse(haloBrush, dotX - 2f, dotY - 2f, dotSize + 4f, dotSize + 4f);
                graphics.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
            }

            float fontSize = 17f;
            RectangleF textBounds = new RectangleF(
                dotX + dotSize + 6f,
                bounds.Y,
                Math.Max(1f, bounds.Right - dotX - dotSize - 6f),
                Math.Max(1f, bounds.Height));
            using (Font nameFont = new Font("Microsoft YaHei UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                string label = string.IsNullOrWhiteSpace(name) ? "未命名项目" : name;
                DrawSmoothText(
                    graphics,
                    label,
                    nameFont,
                    textBounds,
                    format,
                    selected ? Color.White : Color.FromArgb(232, 235, 241));
            }
        }

        private static void DrawCountdownText(
            Graphics graphics,
            long days,
            int hours,
            int minutes,
            int seconds,
            RectangleF bounds)
        {
            string dayText = Math.Max(0L, days).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
            string[] values =
            {
                dayText,
                hours.ToString("00", CultureInfo.InvariantCulture),
                minutes.ToString("00", CultureInfo.InvariantCulture),
                seconds.ToString("00", CultureInfo.InvariantCulture)
            };
            string[] labels = { "日", "时", "分", "秒" };
            float maximumDigitSize = Math.Max(32f, Math.Min(36f, bounds.Height * 0.82f));
            float maximumUnitSize = 14f;
            float groupGap = 2f;
            float totalWidth;
            float digitSize = maximumDigitSize;
            float unitSize = maximumUnitSize;

            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            using (Font maximumDigitFont = CreateCountdownFont(maximumDigitSize))
            using (Font maximumUnitFont = new Font("Microsoft YaHei UI", maximumUnitSize, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags |= StringFormatFlags.NoWrap;
                totalWidth = MeasureCountdownWidth(
                    graphics,
                    values,
                    labels,
                    maximumDigitFont,
                    maximumUnitFont,
                    format,
                    groupGap);
            }

            if (totalWidth > bounds.Width && totalWidth > 0f)
            {
                float scale = Math.Max(0.72f, bounds.Width / totalWidth);
                digitSize = maximumDigitSize * scale;
                unitSize = maximumUnitSize * scale;
                groupGap *= scale;
            }

            digitSize = (float)Math.Round(digitSize * 2f) / 2f;
            unitSize = (float)Math.Round(unitSize * 2f) / 2f;

            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            using (Font digitFont = CreateCountdownFont(digitSize))
            using (Font unitFont = new Font("Microsoft YaHei UI", Math.Max(8.5f, unitSize), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                format.FormatFlags |= StringFormatFlags.NoWrap;
                totalWidth = MeasureCountdownWidth(
                    graphics,
                    values,
                    labels,
                    digitFont,
                    unitFont,
                    format,
                    groupGap);
                float x = (float)Math.Round(bounds.X + Math.Max(0f, (bounds.Width - totalWidth) / 2f));

                for (int group = 0; group < values.Length; group++)
                {
                    float valueWidth = MeasureTextWidth(graphics, values[group], digitFont, format);
                    RectangleF valueBounds = new RectangleF(x, bounds.Y - 0.5f, valueWidth + 1f, bounds.Height + 1f);
                    DrawSmoothText(
                        graphics,
                        values[group],
                        digitFont,
                        valueBounds,
                        format,
                        Color.FromArgb(248, 249, 252));
                    x += valueWidth;

                    float labelWidth = MeasureTextWidth(graphics, labels[group], unitFont, format);
                    RectangleF labelBounds = new RectangleF(x, bounds.Y + 1f, labelWidth + 1f, bounds.Height);
                    DrawSmoothText(
                        graphics,
                        labels[group],
                        unitFont,
                        labelBounds,
                        format,
                        Color.FromArgb(218, 223, 232));
                    x += labelWidth;

                    if (group < values.Length - 1)
                    {
                        x += groupGap;
                    }
                }
            }
        }

        private static float MeasureCountdownWidth(
            Graphics graphics,
            string[] values,
            string[] labels,
            Font digitFont,
            Font unitFont,
            StringFormat format,
            float groupGap)
        {
            float width = groupGap * (values.Length - 1);
            for (int i = 0; i < values.Length; i++)
            {
                width += MeasureTextWidth(graphics, values[i], digitFont, format);
                width += MeasureTextWidth(graphics, labels[i], unitFont, format);
            }
            return width;
        }

        private static float MeasureTextWidth(Graphics graphics, string text, Font font, StringFormat format)
        {
            return graphics.MeasureString(text, font, 1000, format).Width;
        }

        private static void DrawSmoothText(
            Graphics graphics,
            string text,
            Font font,
            RectangleF bounds,
            StringFormat format,
            Color textColor)
        {
            RectangleF snappedBounds = new RectangleF(
                (float)Math.Round(bounds.X),
                (float)Math.Round(bounds.Y),
                (float)Math.Ceiling(bounds.Width),
                (float)Math.Ceiling(bounds.Height));
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                graphics.DrawString(text, font, textBrush, snappedBounds, format);
            }
        }

        private static Font CreateCountdownFont(float size)
        {
            try
            {
                return new Font(CountdownFontName, size, FontStyle.Bold, GraphicsUnit.Pixel);
            }
            catch (ArgumentException)
            {
                return new Font(CountdownFontName, size, FontStyle.Regular, GraphicsUnit.Pixel);
            }
        }

        private static string ResolveCountdownFontName()
        {
            string[] candidates =
            {
                "Frutiger LT Std 45 Light",
                "Frutiger LT 45 Light",
                "Frutiger 45 Light",
                "Frutiger Next LT Light",
                "Frutiger",
                "Segoe UI Variable Display",
                "Segoe UI"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                try
                {
                    using (Font candidate = new Font(candidates[i], 12f, FontStyle.Regular, GraphicsUnit.Pixel))
                    {
                        if (string.Equals(candidate.Name, candidates[i], StringComparison.OrdinalIgnoreCase) ||
                            (candidates[i].StartsWith("Frutiger", StringComparison.OrdinalIgnoreCase) &&
                             candidate.Name.StartsWith("Frutiger", StringComparison.OrdinalIgnoreCase)))
                        {
                            return candidates[i];
                        }
                    }
                }
                catch (ArgumentException)
                {
                }
            }

            return "Segoe UI";
        }

        private static void DrawCountdownBar(
            Graphics graphics,
            CountdownItem item,
            TimeSpan remaining,
            RectangleF barBounds)
        {
            double elapsedRatio = CalculateElapsedRatio(item, remaining);

            using (GraphicsPath barPath = RoundedGeometry.Create(barBounds, barBounds.Height / 2f))
            using (SolidBrush remainingBrush = new SolidBrush(Color.FromArgb(194, Theme.Success)))
            {
                graphics.FillPath(remainingBrush, barPath);

                if (elapsedRatio > 0d)
                {
                    GraphicsState saved = graphics.Save();
                    RectangleF elapsedClip = new RectangleF(barBounds.X, barBounds.Y, (float)(barBounds.Width * elapsedRatio), barBounds.Height);
                    graphics.SetClip(elapsedClip);
                    using (LinearGradientBrush elapsedBrush = new LinearGradientBrush(
                        barBounds,
                        Color.FromArgb(226, Theme.Danger),
                        Color.FromArgb(206, Theme.Danger),
                        0f))
                    {
                        graphics.FillPath(elapsedBrush, barPath);
                    }
                    graphics.Restore(saved);
                }

            }
        }

        private static double CalculateElapsedRatio(CountdownItem item, TimeSpan remaining)
        {
            TimeSpan initialDuration = item.Mode == CountdownMode.Monthly
                ? CountdownSchedule.CalculateMonthlyCycleDuration(
                    item.MonthlyDay,
                    CountdownSchedule.SafeTimeOfDay(item.LocalTimeOfDayTicks),
                    new DateTime(item.TargetUtcTicks, DateTimeKind.Utc))
                : CountdownSchedule.SafeDuration(item.InitialTicks);
            double initialMilliseconds = initialDuration.TotalMilliseconds;
            bool completed = !item.IsRunning && item.WasStarted && item.PausedTicks <= 0L;
            if (completed)
            {
                return 1d;
            }
            if (initialMilliseconds <= 0d)
            {
                return 0d;
            }
            double elapsedRatio = 1d - (remaining.TotalMilliseconds / initialMilliseconds);
            return Math.Max(0d, Math.Min(1d, elapsedRatio));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int itemIndex = GetItemIndexAtY(e.Y);
                if (countdownItems.Count > 1 && itemIndex >= 0)
                {
                    pointerGesturePending = true;
                    itemReorderActive = false;
                    itemOrderChanged = false;
                    draggedItemIndex = itemIndex;
                    pointerDownLocation = e.Location;
                    pointerOffsetFromItemCenter = e.Y - GetItemCenterY(itemIndex);
                    Capture = true;

                    if (selectedItemIndex != itemIndex)
                    {
                        selectedItemIndex = itemIndex;
                        UpdatePresentation();
                    }
                }
                else
                {
                    BeginWindowMove();
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                int itemIndex = GetItemIndexAtY(e.Y);
                if (itemIndex >= 0 && selectedItemIndex != itemIndex)
                {
                    selectedItemIndex = itemIndex;
                    UpdatePresentation();
                    PersistSettings();
                }
                trayMenu.Show(Cursor.Position);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!pointerGesturePending || draggedItemIndex < 0)
            {
                return;
            }

            if ((Control.MouseButtons & MouseButtons.Left) == MouseButtons.None)
            {
                CompletePointerGesture();
                return;
            }

            int horizontalDistance = Math.Abs(e.X - pointerDownLocation.X);
            int verticalDistance = Math.Abs(e.Y - pointerDownLocation.Y);
            int activationDistance = Math.Max(
                DragActivationDistance,
                (int)Math.Round(DragActivationDistance * DeviceDpi / 96f));

            if (!itemReorderActive)
            {
                if (horizontalDistance < activationDistance && verticalDistance < activationDistance)
                {
                    return;
                }

                if (verticalDistance > horizontalDistance)
                {
                    itemReorderActive = true;
                    Cursor = Cursors.SizeNS;
                }
                else
                {
                    BeginWindowMove();
                    return;
                }
            }

            int targetIndex = GetReorderTargetIndex(e.Y);
            MoveDraggedItemTo(targetIndex);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && (pointerGesturePending || itemReorderActive))
            {
                CompletePointerGesture();
            }
            base.OnMouseUp(e);
        }

        private int GetItemIndexAtY(int y)
        {
            float outer;
            float gap;
            float itemHeight;
            GetItemLayout(out outer, out gap, out itemHeight);
            float stride = itemHeight + gap;

            for (int i = 0; i < countdownItems.Count; i++)
            {
                float top = outer + i * stride;
                if (y >= top && y <= top + itemHeight)
                {
                    return i;
                }
            }
            return -1;
        }

        private float GetItemCenterY(int index)
        {
            float outer;
            float gap;
            float itemHeight;
            GetItemLayout(out outer, out gap, out itemHeight);
            return outer + index * (itemHeight + gap) + itemHeight / 2f;
        }

        private int GetReorderTargetIndex(int pointerY)
        {
            float outer;
            float gap;
            float itemHeight;
            GetItemLayout(out outer, out gap, out itemHeight);
            float stride = itemHeight + gap;
            float draggedCenter = pointerY - pointerOffsetFromItemCenter;
            float relativeIndex = (draggedCenter - (outer + itemHeight / 2f)) / stride;
            int targetIndex = (int)Math.Floor(relativeIndex + 0.5f);
            return Math.Max(0, Math.Min(countdownItems.Count - 1, targetIndex));
        }

        private void MoveDraggedItemTo(int targetIndex)
        {
            if (draggedItemIndex < 0 || draggedItemIndex >= countdownItems.Count)
            {
                return;
            }

            targetIndex = Math.Max(0, Math.Min(countdownItems.Count - 1, targetIndex));
            if (targetIndex == draggedItemIndex)
            {
                return;
            }

            CountdownItem draggedItem = countdownItems[draggedItemIndex];
            countdownItems.RemoveAt(draggedItemIndex);
            countdownItems.Insert(targetIndex, draggedItem);
            draggedItemIndex = targetIndex;
            selectedItemIndex = targetIndex;
            itemOrderChanged = true;
            UpdatePresentation();
        }

        private void CompletePointerGesture()
        {
            bool shouldPersist = pointerGesturePending || itemReorderActive || itemOrderChanged;
            pointerGesturePending = false;
            itemReorderActive = false;
            itemOrderChanged = false;
            draggedItemIndex = -1;
            Cursor = Cursors.Default;
            if (Capture)
            {
                Capture = false;
            }

            if (shouldPersist)
            {
                UpdatePresentation();
                PersistSettings();
            }
        }

        private void BeginWindowMove()
        {
            pointerGesturePending = false;
            itemReorderActive = false;
            itemOrderChanged = false;
            draggedItemIndex = -1;
            Cursor = Cursors.Default;
            Capture = false;
            ReleaseCapture();
            SendMessage(Handle, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RenderLayeredWindow();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated && Visible && !IsDisposed)
            {
                RenderLayeredWindow();
            }
        }

        private void ConstrainCurrentBoundsToWorkingArea()
        {
            if (WindowState != FormWindowState.Normal || Width <= 0 || Height <= 0)
            {
                return;
            }

            Rectangle current = Bounds;
            Rectangle workingArea = Screen.FromRectangle(current).WorkingArea;
            Rectangle constrained = ConstrainRectangleToWorkingArea(current, workingArea, false);
            if (constrained != current)
            {
                Bounds = constrained;
            }
        }

        private static Rectangle ConstrainRectangleToWorkingArea(
            Rectangle proposed,
            Rectangle workingArea,
            bool snapToEdges)
        {
            int width = Math.Min(Math.Max(1, proposed.Width), workingArea.Width);
            int height = Math.Min(Math.Max(1, proposed.Height), workingArea.Height);
            int left = proposed.Left;
            int top = proposed.Top;

            if (snapToEdges)
            {
                if (Math.Abs(left - workingArea.Left) <= EdgeSnapDistance)
                {
                    left = workingArea.Left;
                }
                else if (Math.Abs((left + width) - workingArea.Right) <= EdgeSnapDistance)
                {
                    left = workingArea.Right - width;
                }

                if (Math.Abs(top - workingArea.Top) <= EdgeSnapDistance)
                {
                    top = workingArea.Top;
                }
                else if (Math.Abs((top + height) - workingArea.Bottom) <= EdgeSnapDistance)
                {
                    top = workingArea.Bottom - height;
                }
            }

            left = Math.Max(workingArea.Left, Math.Min(workingArea.Right - width, left));
            top = Math.Max(workingArea.Top, Math.Min(workingArea.Bottom - height, top));
            return new Rectangle(left, top, width, height);
        }

        private static NativeRectangle ToNativeRectangle(Rectangle rectangle)
        {
            NativeRectangle native = new NativeRectangle();
            native.Left = rectangle.Left;
            native.Top = rectangle.Top;
            native.Right = rectangle.Right;
            native.Bottom = rectangle.Bottom;
            return native;
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmMoving && message.LParam != IntPtr.Zero)
            {
                NativeRectangle native = (NativeRectangle)Marshal.PtrToStructure(
                    message.LParam,
                    typeof(NativeRectangle));
                Rectangle proposed = Rectangle.FromLTRB(native.Left, native.Top, native.Right, native.Bottom);
                Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
                Rectangle constrained = ConstrainRectangleToWorkingArea(proposed, workingArea, true);
                NativeRectangle result = ToNativeRectangle(constrained);
                Marshal.StructureToPtr(result, message.LParam, false);
                message.Result = new IntPtr(1);
                return;
            }

            if (message.Msg == WmSizing && message.LParam != IntPtr.Zero)
            {
                NativeRectangle native = (NativeRectangle)Marshal.PtrToStructure(
                    message.LParam,
                    typeof(NativeRectangle));
                Rectangle workingArea = Screen.FromHandle(Handle).WorkingArea;

                if (Math.Abs(native.Left - workingArea.Left) <= EdgeSnapDistance || native.Left < workingArea.Left)
                {
                    native.Left = workingArea.Left;
                }
                if (Math.Abs(native.Right - workingArea.Right) <= EdgeSnapDistance || native.Right > workingArea.Right)
                {
                    native.Right = workingArea.Right;
                }
                if (Math.Abs(native.Top - workingArea.Top) <= EdgeSnapDistance || native.Top < workingArea.Top)
                {
                    native.Top = workingArea.Top;
                }
                if (Math.Abs(native.Bottom - workingArea.Bottom) <= EdgeSnapDistance || native.Bottom > workingArea.Bottom)
                {
                    native.Bottom = workingArea.Bottom;
                }

                Marshal.StructureToPtr(native, message.LParam, false);
                message.Result = new IntPtr(1);
                return;
            }

            if (message.Msg == WmNcHitTest)
            {
                base.WndProc(ref message);
                if ((int)message.Result == 1)
                {
                    Point point = PointToClient(Cursor.Position);
                    int grip = Math.Max(7, (int)(8f * DeviceDpi / 96f));
                    bool left = point.X <= grip;
                    bool right = point.X >= ClientSize.Width - grip;
                    bool top = point.Y <= grip;
                    bool bottom = point.Y >= ClientSize.Height - grip;

                    if (left && top) message.Result = new IntPtr(HtTopLeft);
                    else if (right && top) message.Result = new IntPtr(HtTopRight);
                    else if (left && bottom) message.Result = new IntPtr(HtBottomLeft);
                    else if (right && bottom) message.Result = new IntPtr(HtBottomRight);
                    else if (left) message.Result = new IntPtr(HtLeft);
                    else if (right) message.Result = new IntPtr(HtRight);
                    else if (top) message.Result = new IntPtr(HtTop);
                    else if (bottom) message.Result = new IntPtr(HtBottom);
                }
                return;
            }
            base.WndProc(ref message);
        }

        private static Icon CreateApplicationIcon()
        {
            using (Bitmap bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                RectangleF circle = new RectangleF(2f, 2f, 28f, 28f);
                using (LinearGradientBrush fill = new LinearGradientBrush(circle, Theme.AccentHover, Theme.Accent, 90f))
                {
                    graphics.FillEllipse(fill, circle);
                }
                using (Pen clockPen = new Pen(Color.White, 2.1f))
                {
                    clockPen.StartCap = LineCap.Round;
                    clockPen.EndCap = LineCap.Round;
                    graphics.DrawEllipse(clockPen, 8f, 8f, 16f, 16f);
                    graphics.DrawLine(clockPen, 16f, 11.5f, 16f, 16f);
                    graphics.DrawLine(clockPen, 16f, 16f, 20f, 18.5f);
                }

                IntPtr handle = bitmap.GetHicon();
                Icon icon = (Icon)Icon.FromHandle(handle).Clone();
                DestroyIcon(handle);
                return icon;
            }
        }
    }
}
