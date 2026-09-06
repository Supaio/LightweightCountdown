using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LightweightCountdown
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(14, 18, 28);
        public static readonly Color Surface = Color.FromArgb(22, 28, 42);
        public static readonly Color SurfaceRaised = Color.FromArgb(28, 35, 51);
        public static readonly Color CardTop = Color.FromArgb(34, 42, 61);
        public static readonly Color CardBottom = Color.FromArgb(24, 30, 45);
        public static readonly Color Border = Color.FromArgb(53, 63, 84);
        public static readonly Color TextPrimary = Color.FromArgb(242, 245, 250);
        public static readonly Color TextSecondary = Color.FromArgb(158, 169, 190);
        public static readonly Color TextSubtle = Color.FromArgb(111, 123, 145);
        public static readonly Color Accent = Color.FromArgb(124, 140, 248);
        public static readonly Color AccentHover = Color.FromArgb(143, 157, 255);
        public static readonly Color AccentPressed = Color.FromArgb(104, 120, 224);
        public static readonly Color AccentSoft = Color.FromArgb(39, 47, 76);
        public static readonly Color Success = Color.FromArgb(88, 214, 166);
        public static readonly Color Warning = Color.FromArgb(246, 188, 92);
        public static readonly Color Danger = Color.FromArgb(245, 113, 130);

        public static Font Font(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        }
    }

    internal static class RoundedGeometry
    {
        public static GraphicsPath Create(RectangleF rectangle, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float safeRadius = Math.Max(1f, Math.Min(radius, Math.Min(rectangle.Width, rectangle.Height) / 2f));
            float diameter = safeRadius * 2f;
            RectangleF arc = new RectangleF(rectangle.X, rectangle.Y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
    {
        public BufferedTableLayoutPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    internal sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public BufferedFlowLayoutPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    internal sealed class MinimalSelector : Control
    {
        private readonly List<string> items;
        private int selectedIndex;
        private ContextMenuStrip dropDownMenu;

        public event EventHandler SelectedIndexChanged;
        public event EventHandler SelectionChangeCommitted;

        public List<string> Items
        {
            get { return items; }
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                int next = value;
                if (next < -1 || next >= items.Count)
                {
                    next = -1;
                }

                bool changed = selectedIndex != next;
                selectedIndex = next;
                Invalidate();
                if (changed && SelectedIndexChanged != null)
                {
                    SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
        }

        public MinimalSelector()
        {
            items = new List<string>();
            selectedIndex = -1;
            BackColor = Theme.SurfaceRaised;
            ForeColor = Theme.TextPrimary;
            Height = 34;
            MinimumSize = new Size(70, 30);
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.Selectable, true);
        }

        protected override void OnClick(EventArgs e)
        {
            ShowDropDown();
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                ShowDropDown();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down && selectedIndex < items.Count - 1)
            {
                CommitSelection(selectedIndex + 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up && selectedIndex > 0)
            {
                CommitSelection(selectedIndex - 1);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF bounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = RoundedGeometry.Create(bounds, 7f))
            using (SolidBrush background = new SolidBrush(Theme.SurfaceRaised))
            using (Pen border = new Pen(Focused ? Theme.Accent : Theme.Border))
            {
                e.Graphics.FillPath(background, path);
                e.Graphics.DrawPath(border, path);
            }

            string text = selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : string.Empty;
            Rectangle textBounds = new Rectangle(10, 0, Math.Max(1, Width - 34), Height);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                Font,
                textBounds,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            Point center = new Point(Width - 15, Height / 2 + 1);
            using (Pen arrow = new Pen(Theme.TextSecondary, 1.6f))
            {
                arrow.StartCap = LineCap.Round;
                arrow.EndCap = LineCap.Round;
                e.Graphics.DrawLine(arrow, center.X - 3, center.Y - 2, center.X, center.Y + 1);
                e.Graphics.DrawLine(arrow, center.X, center.Y + 1, center.X + 3, center.Y - 2);
            }
        }

        private void ShowDropDown()
        {
            if (items.Count == 0)
            {
                return;
            }

            if (dropDownMenu != null)
            {
                dropDownMenu.Dispose();
            }

            dropDownMenu = new ContextMenuStrip();
            dropDownMenu.BackColor = Theme.SurfaceRaised;
            dropDownMenu.ForeColor = Theme.TextPrimary;
            dropDownMenu.Font = Font;
            dropDownMenu.ShowImageMargin = false;
            dropDownMenu.Padding = new Padding(3);

            for (int i = 0; i < items.Count; i++)
            {
                ToolStripMenuItem menuItem = new ToolStripMenuItem(items[i]);
                menuItem.Tag = i;
                menuItem.Checked = i == selectedIndex;
                menuItem.Click += DropDownItem_Click;
                dropDownMenu.Items.Add(menuItem);
            }

            dropDownMenu.Show(this, new Point(0, Height));
        }

        private void DropDownItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = sender as ToolStripMenuItem;
            if (menuItem == null || !(menuItem.Tag is int))
            {
                return;
            }

            CommitSelection((int)menuItem.Tag);
        }

        private void CommitSelection(int index)
        {
            SelectedIndex = index;
            if (SelectionChangeCommitted != null)
            {
                SelectionChangeCommitted(this, EventArgs.Empty);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && dropDownMenu != null)
            {
                dropDownMenu.Dispose();
                dropDownMenu = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class SurfacePanel : Panel
    {
        public int CornerRadius { get; set; }

        public SurfacePanel()
        {
            CornerRadius = 16;
            BackColor = Theme.Surface;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color outside = Parent == null ? Theme.Background : Parent.BackColor;
            e.Graphics.Clear(outside);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF bounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = RoundedGeometry.Create(bounds, CornerRadius))
            using (SolidBrush brush = new SolidBrush(BackColor))
            using (Pen border = new Pen(Theme.Border))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }

    internal sealed class TimeCard : Control
    {
        private string displayValue;
        private string unitText;

        public string DisplayValue
        {
            get { return displayValue; }
            set
            {
                string next = string.IsNullOrEmpty(value) ? "00" : value;
                if (displayValue != next)
                {
                    displayValue = next;
                    Invalidate();
                }
            }
        }

        public string UnitText
        {
            get { return unitText; }
            set
            {
                unitText = value ?? string.Empty;
                Invalidate();
            }
        }

        public TimeCard()
        {
            displayValue = "00";
            unitText = string.Empty;
            BackColor = Theme.Background;
            MinimumSize = new Size(110, 118);
            Margin = new Padding(6);
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            RectangleF bounds = new RectangleF(1.5f, 1.5f, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using (GraphicsPath path = RoundedGeometry.Create(bounds, 18f))
            using (LinearGradientBrush background = new LinearGradientBrush(bounds, Theme.CardTop, Theme.CardBottom, 90f))
            using (Pen border = new Pen(Theme.Border))
            {
                e.Graphics.FillPath(background, path);
                e.Graphics.DrawPath(border, path);
            }

            RectangleF accentBounds = new RectangleF(18f, 14f, Math.Max(22f, Width * 0.19f), 3f);
            using (GraphicsPath accentPath = RoundedGeometry.Create(accentBounds, 1.5f))
            using (SolidBrush accentBrush = new SolidBrush(Theme.Accent))
            {
                e.Graphics.FillPath(accentBrush, accentPath);
            }

            int characterCount = Math.Max(2, displayValue.Length);
            float maxByHeight = Math.Max(24f, Height * 0.39f);
            float maxByWidth = Math.Max(20f, (Width - 18f) / (characterCount * 0.68f));
            float valueSize = Math.Min(58f, Math.Min(maxByHeight, maxByWidth));

            using (Font valueFont = new Font("Segoe UI", valueSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font unitFont = new Font("Microsoft YaHei UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush valueBrush = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush unitBrush = new SolidBrush(Theme.TextSecondary))
            using (StringFormat centered = new StringFormat())
            {
                centered.Alignment = StringAlignment.Center;
                centered.LineAlignment = StringAlignment.Center;

                RectangleF valueBounds = new RectangleF(5f, 23f, Math.Max(1, Width - 10f), Math.Max(1, Height - 60f));
                e.Graphics.DrawString(displayValue, valueFont, valueBrush, valueBounds, centered);

                RectangleF unitBounds = new RectangleF(5f, Math.Max(0, Height - 38f), Math.Max(1, Width - 10f), 24f);
                e.Graphics.DrawString(unitText, unitFont, unitBrush, unitBounds, centered);
            }
        }
    }

    internal sealed class RoundedButton : Button
    {
        private bool hovering;
        private bool pressing;

        public Color BaseColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public Color BorderColor { get; set; }
        public int CornerRadius { get; set; }

        public RoundedButton()
        {
            BaseColor = Theme.Accent;
            HoverColor = Theme.AccentHover;
            PressedColor = Theme.AccentPressed;
            BorderColor = Color.Transparent;
            CornerRadius = 11;
            ForeColor = Color.White;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Height = 42;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovering = false;
            pressing = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            pressing = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            pressing = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = pressing ? PressedColor : (hovering ? HoverColor : BaseColor);
            if (!Enabled)
            {
                fill = Color.FromArgb(65, fill);
            }

            RectangleF bounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = RoundedGeometry.Create(bounds, CornerRadius))
            using (SolidBrush brush = new SolidBrush(fill))
            {
                pevent.Graphics.FillPath(brush, path);
                if (BorderColor.A > 0)
                {
                    using (Pen border = new Pen(BorderColor))
                    {
                        pevent.Graphics.DrawPath(border, path);
                    }
                }
            }

            Color textColor = Enabled ? ForeColor : Color.FromArgb(115, ForeColor);
            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                Font,
                ClientRectangle,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                Rectangle focusRectangle = Rectangle.Inflate(ClientRectangle, -5, -5);
                ControlPaint.DrawFocusRectangle(pevent.Graphics, focusRectangle, textColor, Color.Transparent);
            }
        }
    }

    internal sealed class ToggleSwitch : Control
    {
        private bool isChecked;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value)
                {
                    return;
                }

                isChecked = value;
                Invalidate();
                if (CheckedChanged != null)
                {
                    CheckedChanged(this, EventArgs.Empty);
                }
            }
        }

        public ToggleSwitch()
        {
            Size = new Size(46, 25);
            MinimumSize = new Size(38, 22);
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.Selectable, true);
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF track = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            Color trackColor = Checked ? Theme.Accent : Theme.Border;
            using (GraphicsPath trackPath = RoundedGeometry.Create(track, Height / 2f))
            using (SolidBrush trackBrush = new SolidBrush(trackColor))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            float knobSize = Math.Max(10f, Height - 7f);
            float knobX = Checked ? Width - knobSize - 3.5f : 3.5f;
            RectangleF knob = new RectangleF(knobX, (Height - knobSize) / 2f, knobSize, knobSize);
            using (SolidBrush knobBrush = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(knobBrush, knob);
            }

            if (Focused && ShowFocusCues)
            {
                using (Pen focusPen = new Pen(Theme.TextSecondary))
                {
                    focusPen.DashStyle = DashStyle.Dot;
                    e.Graphics.DrawRectangle(focusPen, 0, 0, Width - 1, Height - 1);
                }
            }
        }
    }

    internal sealed class SlimProgressBar : Control
    {
        private double value;

        public double Value
        {
            get { return value; }
            set
            {
                double next = Math.Max(0d, Math.Min(1d, value));
                if (Math.Abs(this.value - next) > 0.0001d)
                {
                    this.value = next;
                    Invalidate();
                }
            }
        }

        public SlimProgressBar()
        {
            Height = 7;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF track = new RectangleF(0f, 0f, Math.Max(1, Width), Math.Max(1, Height));
            using (GraphicsPath trackPath = RoundedGeometry.Create(track, Height / 2f))
            using (SolidBrush trackBrush = new SolidBrush(Theme.SurfaceRaised))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            float progressWidth = (float)(Width * Value);
            if (progressWidth > 1f)
            {
                RectangleF progress = new RectangleF(0f, 0f, progressWidth, Math.Max(1, Height));
                using (GraphicsPath progressPath = RoundedGeometry.Create(progress, Math.Min(Height / 2f, progressWidth / 2f)))
                using (LinearGradientBrush progressBrush = new LinearGradientBrush(progress, Theme.Accent, Theme.Success, 0f))
                {
                    e.Graphics.FillPath(progressBrush, progressPath);
                }
            }
        }
    }

    internal sealed class StatusPill : Control
    {
        private Color dotColor;

        public Color DotColor
        {
            get { return dotColor; }
            set
            {
                dotColor = value;
                Invalidate();
            }
        }

        public StatusPill()
        {
            dotColor = Theme.TextSubtle;
            ForeColor = Theme.TextSecondary;
            Size = new Size(112, 32);
            Font = Theme.Font(9f, FontStyle.Regular);
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            Invalidate();
            base.OnTextChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF bounds = new RectangleF(0.5f, 0.5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = RoundedGeometry.Create(bounds, Height / 2f))
            using (SolidBrush background = new SolidBrush(Theme.SurfaceRaised))
            using (Pen border = new Pen(Theme.Border))
            {
                e.Graphics.FillPath(background, path);
                e.Graphics.DrawPath(border, path);
            }

            using (SolidBrush dotBrush = new SolidBrush(DotColor))
            {
                e.Graphics.FillEllipse(dotBrush, 13f, Height / 2f - 3.5f, 7f, 7f);
            }

            Rectangle textBounds = new Rectangle(27, 0, Math.Max(1, Width - 33), Height);
            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                textBounds,
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
