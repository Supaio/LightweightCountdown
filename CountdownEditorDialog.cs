using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LightweightCountdown
{
    internal sealed class CountdownEditorDialog : Form
    {
        private readonly bool creatingNewItem;
        private bool suppressModeChange;

        private TextBox nameTextBox;
        private MinimalSelector modeSelector;
        private DateTimePicker datePicker;
        private DateTimePicker timePicker;
        private NumericUpDown monthlyDayPicker;
        private Label monthlyPrefixLabel;
        private Label monthlySuffixLabel;
        private Label ruleHintLabel;

        public string CountdownName { get; private set; }
        public CountdownMode CountdownMode { get; private set; }
        public DateTime TargetUtc { get; private set; }
        public int MonthlyDay { get; private set; }
        public long LocalTimeOfDayTicks { get; private set; }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int value, int valueSize);

        public CountdownEditorDialog(CountdownItem item, bool creatingNewItem)
        {
            this.creatingNewItem = creatingNewItem;
            BuildWindow(item);
        }

        private void BuildWindow(CountdownItem item)
        {
            Text = creatingNewItem ? "新建倒计日" : "编辑倒计日";
            BackColor = Theme.Background;
            ForeColor = Theme.TextPrimary;
            Font = Theme.Font(9.5f, FontStyle.Regular);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 400);
            AutoScaleMode = AutoScaleMode.Dpi;

            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Theme.Background;
            root.Padding = new Padding(28, 24, 28, 20);
            Controls.Add(root);

            BufferedTableLayoutPanel layout = new BufferedTableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.BackColor = Theme.Background;
            layout.ColumnCount = 1;
            layout.RowCount = 6;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            root.Controls.Add(layout);

            layout.Controls.Add(BuildHeader(), 0, 0);
            layout.Controls.Add(BuildNameRow(item), 0, 1);
            layout.Controls.Add(BuildModeRow(item), 0, 2);
            layout.Controls.Add(BuildScheduleRow(item), 0, 3);

            ruleHintLabel = CreateLabel(string.Empty, Theme.TextSubtle, 9f, FontStyle.Regular);
            ruleHintLabel.Dock = DockStyle.Fill;
            ruleHintLabel.Padding = new Padding(112, 8, 0, 0);
            ruleHintLabel.TextAlign = ContentAlignment.TopLeft;
            layout.Controls.Add(ruleHintLabel, 0, 4);
            layout.Controls.Add(BuildActions(), 0, 5);

            suppressModeChange = true;
            modeSelector.SelectedIndex = item.Mode == CountdownMode.Monthly ? 1 : 0;
            suppressModeChange = false;
            UpdateModeVisibility();
        }

        private Control BuildHeader()
        {
            BufferedTableLayoutPanel header = new BufferedTableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Theme.Background;
            header.ColumnCount = 1;
            header.RowCount = 2;
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label title = CreateLabel(
                creatingNewItem ? "新建一个倒计日" : "编辑当前倒计日",
                Theme.TextPrimary,
                17f,
                FontStyle.Bold);
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;

            Label subtitle = CreateLabel("保存后，桌面浮窗会立即显示这个项目。", Theme.TextSubtle, 9f, FontStyle.Regular);
            subtitle.Dock = DockStyle.Fill;
            subtitle.TextAlign = ContentAlignment.TopLeft;
            header.Controls.Add(title, 0, 0);
            header.Controls.Add(subtitle, 0, 1);
            return header;
        }

        private Control BuildNameRow(CountdownItem item)
        {
            BufferedTableLayoutPanel row = CreateFieldRow("项目名称");
            nameTextBox = new TextBox();
            nameTextBox.Text = item.Name;
            nameTextBox.BackColor = Theme.Surface;
            nameTextBox.ForeColor = Theme.TextPrimary;
            nameTextBox.BorderStyle = BorderStyle.FixedSingle;
            nameTextBox.Font = Theme.Font(10.5f, FontStyle.Regular);
            nameTextBox.Dock = DockStyle.Fill;
            nameTextBox.Margin = new Padding(0, 9, 0, 9);
            nameTextBox.MaxLength = 80;
            row.Controls.Add(nameTextBox, 1, 0);
            return row;
        }

        private Control BuildModeRow(CountdownItem item)
        {
            BufferedTableLayoutPanel row = CreateFieldRow("倒计规则");
            modeSelector = new MinimalSelector();
            modeSelector.Items.Add("指定日期（一次）");
            modeSelector.Items.Add("每月重复");
            modeSelector.Font = Theme.Font(9.5f, FontStyle.Regular);
            modeSelector.Dock = DockStyle.Fill;
            modeSelector.Margin = new Padding(0, 8, 0, 8);
            modeSelector.SelectedIndexChanged += ModeSelector_SelectedIndexChanged;
            row.Controls.Add(modeSelector, 1, 0);
            return row;
        }

        private Control BuildScheduleRow(CountdownItem item)
        {
            BufferedTableLayoutPanel row = CreateFieldRow("日期与时间");
            Panel schedule = new Panel();
            schedule.Dock = DockStyle.Fill;
            schedule.BackColor = Theme.Background;

            DateTime targetLocal = new DateTime(item.TargetUtcTicks, DateTimeKind.Utc).ToLocalTime();
            if (targetLocal < DateTime.Today)
            {
                targetLocal = DateTime.Now.AddDays(1);
            }

            datePicker = new DateTimePicker();
            datePicker.Format = DateTimePickerFormat.Custom;
            datePicker.CustomFormat = "yyyy 年 MM 月 dd 日";
            datePicker.Font = Theme.Font(9.5f, FontStyle.Regular);
            datePicker.MinDate = DateTime.Today;
            datePicker.MaxDate = DateTime.Today.AddYears(50);
            datePicker.Value = targetLocal.Date;
            datePicker.SetBounds(0, 13, 205, 34);

            monthlyPrefixLabel = CreateLabel("每月", Theme.TextSecondary, 9.5f, FontStyle.Regular);
            monthlyPrefixLabel.SetBounds(0, 13, 42, 34);
            monthlyPrefixLabel.TextAlign = ContentAlignment.MiddleLeft;

            monthlyDayPicker = new NumericUpDown();
            monthlyDayPicker.Minimum = 1;
            monthlyDayPicker.Maximum = 31;
            monthlyDayPicker.Value = Math.Max(1, Math.Min(31, item.MonthlyDay));
            monthlyDayPicker.BackColor = Theme.Surface;
            monthlyDayPicker.ForeColor = Theme.TextPrimary;
            monthlyDayPicker.BorderStyle = BorderStyle.FixedSingle;
            monthlyDayPicker.Font = Theme.Font(10f, FontStyle.Regular);
            monthlyDayPicker.TextAlign = HorizontalAlignment.Center;
            monthlyDayPicker.SetBounds(45, 13, 62, 34);

            monthlySuffixLabel = CreateLabel("日", Theme.TextSecondary, 9.5f, FontStyle.Regular);
            monthlySuffixLabel.SetBounds(112, 13, 28, 34);
            monthlySuffixLabel.TextAlign = ContentAlignment.MiddleLeft;

            Label timeLabel = CreateLabel("时间", Theme.TextSecondary, 9.5f, FontStyle.Regular);
            timeLabel.SetBounds(230, 13, 42, 34);
            timeLabel.TextAlign = ContentAlignment.MiddleLeft;

            timePicker = new DateTimePicker();
            timePicker.Format = DateTimePickerFormat.Custom;
            timePicker.CustomFormat = "HH:mm:ss";
            timePicker.ShowUpDown = true;
            timePicker.Font = Theme.Font(9.5f, FontStyle.Regular);
            TimeSpan configuredTime = item.Mode == CountdownMode.Monthly
                ? CountdownSchedule.SafeTimeOfDay(item.LocalTimeOfDayTicks)
                : targetLocal.TimeOfDay;
            timePicker.Value = DateTime.Today.Add(configuredTime);
            timePicker.SetBounds(275, 13, 126, 34);

            schedule.Controls.Add(datePicker);
            schedule.Controls.Add(monthlyPrefixLabel);
            schedule.Controls.Add(monthlyDayPicker);
            schedule.Controls.Add(monthlySuffixLabel);
            schedule.Controls.Add(timeLabel);
            schedule.Controls.Add(timePicker);
            row.Controls.Add(schedule, 1, 0);
            return row;
        }

        private Control BuildActions()
        {
            BufferedFlowLayoutPanel actions = new BufferedFlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.BackColor = Theme.Background;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Padding = new Padding(0, 5, 0, 0);

            RoundedButton save = new RoundedButton();
            save.Text = creatingNewItem ? "创建" : "保存";
            save.Width = 112;
            save.Height = 42;
            save.Font = Theme.Font(10f, FontStyle.Bold);
            save.Margin = new Padding(10, 0, 0, 0);
            save.Click += Save_Click;

            RoundedButton cancel = new RoundedButton();
            cancel.Text = "取消";
            cancel.Width = 92;
            cancel.Height = 42;
            cancel.BaseColor = Theme.SurfaceRaised;
            cancel.HoverColor = Color.FromArgb(42, 50, 70);
            cancel.PressedColor = Theme.Surface;
            cancel.BorderColor = Theme.Border;
            cancel.ForeColor = Theme.TextSecondary;
            cancel.Font = Theme.Font(9.5f, FontStyle.Regular);
            cancel.DialogResult = DialogResult.Cancel;

            actions.Controls.Add(save);
            actions.Controls.Add(cancel);
            CancelButton = cancel;
            return actions;
        }

        private static BufferedTableLayoutPanel CreateFieldRow(string labelText)
        {
            BufferedTableLayoutPanel row = new BufferedTableLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.BackColor = Theme.Background;
            row.ColumnCount = 2;
            row.RowCount = 1;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label label = CreateLabel(labelText, Theme.TextSecondary, 9.5f, FontStyle.Regular);
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            row.Controls.Add(label, 0, 0);
            return row;
        }

        private static Label CreateLabel(string text, Color color, float size, FontStyle style)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Font = Theme.Font(size, style);
            return label;
        }

        private void ModeSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!suppressModeChange)
            {
                UpdateModeVisibility();
            }
        }

        private void UpdateModeVisibility()
        {
            bool monthly = modeSelector.SelectedIndex == 1;
            datePicker.Visible = !monthly;
            monthlyPrefixLabel.Visible = monthly;
            monthlyDayPicker.Visible = monthly;
            monthlySuffixLabel.Visible = monthly;
            ruleHintLabel.Text = monthly
                ? "每月到期后会自动安排下个月；29–31 日在短月份按当月最后一天。"
                : "倒计到指定日期和时间；适合纪念日、考试日和截止日期。";
        }

        private void Save_Click(object sender, EventArgs e)
        {
            string name = nameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this, "请输入项目名称。", "还差一项", MessageBoxButtons.OK, MessageBoxIcon.Information);
                nameTextBox.Focus();
                return;
            }

            DateTime now = DateTime.UtcNow;
            CountdownMode mode = modeSelector.SelectedIndex == 1 ? CountdownMode.Monthly : CountdownMode.Once;
            DateTime target;
            if (mode == CountdownMode.Monthly)
            {
                target = CountdownSchedule.CalculateNextMonthlyOccurrence(
                    (int)monthlyDayPicker.Value,
                    timePicker.Value.TimeOfDay,
                    now);
            }
            else
            {
                DateTime local = datePicker.Value.Date.Add(timePicker.Value.TimeOfDay);
                target = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
                if (target <= now)
                {
                    MessageBox.Show(this, "指定日期和时间需要晚于当前时间。", "无法保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            CountdownName = name;
            CountdownMode = mode;
            TargetUtc = target;
            MonthlyDay = (int)monthlyDayPicker.Value;
            LocalTimeOfDayTicks = timePicker.Value.TimeOfDay.Ticks;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int enabled = 1;
                int result = DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
                if (result != 0)
                {
                    DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }
}
