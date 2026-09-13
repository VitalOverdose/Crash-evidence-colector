using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public class RoundedMultiSelectComboBox : Control, IAdaptiveRowPanelItem
    {
        private static readonly Font DefaultComboFont = new("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        private readonly BindingList<RoundedComboBoxItem> _items = new();
        private readonly HashSet<string> _selectedTexts = new(StringComparer.OrdinalIgnoreCase);
        private ToolStripDropDown? _dropDown;
        private bool _isHovered;
        private bool _isDropDownOpen;

        public event EventHandler? SelectionChanged;
        public event EventHandler<MultiSelectComboBoxItemChangingEventArgs>? ItemSelectionChanging;

        public RoundedMultiSelectComboBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(140, 34);
            Font = DefaultComboFont;
            Cursor = Cursors.Hand;
            _items.ListChanged += (s, e) => Invalidate();
        }

        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public BindingList<RoundedComboBoxItem> Items => _items;

        [Browsable(false)]
        public IReadOnlyList<string> SelectedTexts => _items
            .Select(item => item.Text)
            .Where(text => _selectedTexts.Contains(text))
            .ToList();

        [Category("Appearance")]
        [DefaultValue("Search Engine")]
        public string PlaceholderText { get; set; } = "Search Engine";

        [Category("Appearance")]
        [DefaultValue(10)]
        public int CornerRadius { get; set; } = 10;

        [Category("Appearance")]
        [DefaultValue(1)]
        public int BorderThickness { get; set; } = 1;

        [Category("Appearance")]
        public Color BorderColor { get; set; } = Color.FromArgb(180, 190, 210);

        [Category("Appearance")]
        public Color InnerColor { get; set; } = Color.White;

        [Category("Appearance")]
        public Color HoverInnerColor { get; set; } = Color.FromArgb(245, 245, 245);

        [Category("Appearance")]
        public Color HoverBorderColor { get; set; } = Color.FromArgb(0, 120, 215);

        [Category("Appearance")]
        [DefaultValue(1)]
        public int HoverBorderThickness { get; set; } = 1;

        [Category("Appearance")]
        public Color SelectedInnerColor { get; set; } = Color.FromArgb(230, 240, 250);

        [Category("Appearance")]
        public Color SelectedBorderColor { get; set; } = Color.FromArgb(0, 120, 215);

        [Category("Appearance")]
        [DefaultValue(2)]
        public int SelectedBorderThickness { get; set; } = 2;

        [Category("Appearance")]
        public Color DisabledInnerColor { get; set; } = Color.FromArgb(250, 250, 250);

        [Category("Appearance")]
        public Color DisabledBorderColor { get; set; } = Color.FromArgb(220, 220, 220);

        [Category("Appearance")]
        public Color DisabledTextColor { get; set; } = Color.FromArgb(160, 160, 160);

        [Category("Appearance")]
        public Color ItemHoverColor { get; set; } = Color.FromArgb(240, 244, 252);

        [Category("Appearance")]
        public Color SeparatorColor { get; set; } = Color.FromArgb(220, 220, 220);

        [Category("Appearance")]
        [DefaultValue(26)]
        public int ItemHeight { get; set; } = 26;

        [Category("Appearance")]
        [DefaultValue(10)]
        public int MaxVisibleRows { get; set; } = 10;

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool ShowAllDropDownRows { get; set; } = false;

        [Category("Appearance")]
        [DefaultValue(6)]
        public int DropShadowSize { get; set; } = 6;

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool HoverPropertiesEnabled { get; set; } = true;

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsSelected { get; set; } = false;

        [Browsable(false)]
        public int SelectedIndex
        {
            get
            {
                string? selected = SelectedTexts.FirstOrDefault();
                if (selected == null)
                    return -1;

                for (int i = 0; i < _items.Count; i++)
                {
                    if (string.Equals(_items[i].Text, selected, StringComparison.OrdinalIgnoreCase))
                        return i;
                }

                return -1;
            }
            set
            {
                _selectedTexts.Clear();
                if (value >= 0 && value < _items.Count)
                {
                    _selectedTexts.Add(_items[value].Text);
                }

                OnSelectionChanged();
            }
        }

        [Browsable(false)]
        public RoundedComboBoxItem? SelectedItem
        {
            get
            {
                int index = SelectedIndex;
                return index >= 0 ? _items[index] : null;
            }
            set
            {
                _selectedTexts.Clear();
                if (value != null)
                {
                    _selectedTexts.Add(value.Text);
                }

                OnSelectionChanged();
            }
        }

        public void SetSelectedText(string text, bool selected)
        {
            if (selected)
            {
                _selectedTexts.Add(text);
            }
            else
            {
                _selectedTexts.Remove(text);
            }

            OnSelectionChanged();
        }

        public void SetSelectedTexts(IEnumerable<string> texts)
        {
            _selectedTexts.Clear();
            foreach (string text in texts.Where(text => !string.IsNullOrWhiteSpace(text)))
            {
                _selectedTexts.Add(text);
            }

            OnSelectionChanged();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowDropDown();
        }

        // Outside the radius shows the PARENT, exactly as the rounded rows and panels do - so a
        // row whose face changes (FocusBackColor) never leaves four stale BackColor corners
        // around the combo. BackColor is no longer part of the picture.
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var backdrop = new SolidBrush(Parent?.BackColor ?? BackColor);
            e.Graphics.FillRectangle(backdrop, ClientRectangle);
        }

        protected override void OnParentBackColorChanged(EventArgs e)
        {
            base.OnParentBackColorChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            bool active = _isDropDownOpen || IsSelected;
            Color backColor = !Enabled
                ? DisabledInnerColor
                : active ? SelectedInnerColor : (_isHovered && HoverPropertiesEnabled ? HoverInnerColor : InnerColor);
            Color borderColor = !Enabled
                ? DisabledBorderColor
                : active ? SelectedBorderColor : (_isHovered && HoverPropertiesEnabled ? HoverBorderColor : BorderColor);
            int borderThickness = active ? SelectedBorderThickness : (_isHovered && HoverPropertiesEnabled ? HoverBorderThickness : BorderThickness);

            using (GraphicsPath path = GetRoundedRectPath(rect, CornerRadius))
            using (SolidBrush brush = new(backColor))
            using (Pen pen = new(borderColor, borderThickness))
            {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            string displayText = GetDisplayText();
            Color textColor = Enabled ? ForeColor : DisabledTextColor;
            Rectangle textRect = new Rectangle(10, 0, Math.Max(0, Width - 34), Height);
            TextRenderer.DrawText(e.Graphics, displayText, Font, textRect, textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            DrawArrow(e.Graphics, new Rectangle(Width - 24, 0, 18, Height), textColor);
        }

        private string GetDisplayText()
        {
            int count = SelectedTexts.Count;
            if (count == 0)
                return PlaceholderText;

            if (count == 1)
                return SelectedTexts[0];

            return $"{count} engines";
        }

        private void ShowDropDown()
        {
            if (_dropDown?.Visible == true)
            {
                _dropDown.Close();
                return;
            }

            var panel = new Panel
            {
                BackColor = Color.White,
                ForeColor = ForeColor,
                Padding = new Padding(4),
                Size = GetDropDownSize()
            };

            int y = 4;
            foreach (var item in _items)
            {
                var checkBox = new RoundedCheckBox
                {
                    Text = item.Text,
                    Checked = _selectedTexts.Contains(item.Text),
                    Location = new Point(6, y),
                    Size = new Size(panel.Width - 12, ItemHeight),
                    Font = Font,
                    ForeColor = ForeColor,
                    BackColor = Color.White,
                    CheckBoxBackColor = Color.White,
                    BorderColor = Color.FromArgb(200, 200, 200),
                    CheckedColor = Color.FromArgb(220, 255, 255),
                    Radius = Math.Min(18, Math.Max(14, ItemHeight - 8))
                };

                bool reverting = false;
                checkBox.CheckedChanged += (s, e) =>
                {
                    if (reverting)
                    {
                        return;
                    }

                    bool isSelecting = checkBox.Checked;
                    var args = new MultiSelectComboBoxItemChangingEventArgs(item.Text, isSelecting);
                    ItemSelectionChanging?.Invoke(this, args);
                    if (args.Cancel)
                    {
                        reverting = true;
                        checkBox.Checked = !isSelecting;
                        reverting = false;
                        return;
                    }

                    if (isSelecting)
                        _selectedTexts.Add(item.Text);
                    else
                        _selectedTexts.Remove(item.Text);

                    OnSelectionChanged();
                };

                panel.Controls.Add(checkBox);
                y += ItemHeight + 4;
            }

            _dropDown = new ToolStripDropDown
            {
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = Color.White,
                ForeColor = ForeColor,
                AutoClose = true,
                Renderer = LightDropDownRenderer.Instance
            };
            var host = new ToolStripControlHost(panel)
            {
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = Color.White,
                ForeColor = ForeColor,
                AutoSize = false,
                Size = panel.Size
            };
            _dropDown.Items.Add(host);
            _dropDown.Closed += (s, e) =>
            {
                _isDropDownOpen = false;
                Invalidate();
            };

            _isDropDownOpen = true;
            Invalidate();
            _dropDown.Show(this, new Point(0, Height + 1));
        }

        private Size GetDropDownSize()
        {
            int rowCount = ShowAllDropDownRows ? _items.Count : Math.Min(Math.Max(1, MaxVisibleRows), _items.Count);
            int height = Math.Max(ItemHeight + 8, rowCount * (ItemHeight + 4) + 4);
            return new Size(Math.Max(Width, 150), height);
        }

        private void OnSelectionChanged()
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        private static void DrawArrow(Graphics g, Rectangle rect, Color color)
        {
            int centerX = rect.Left + rect.Width / 2;
            int centerY = rect.Top + rect.Height / 2;
            Point[] points =
            {
                new(centerX - 4, centerY - 2),
                new(centerX + 4, centerY - 2),
                new(centerX, centerY + 3)
            };

            using SolidBrush brush = new(color);
            g.FillPolygon(brush, points);
        }

        private static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new();
            int diameter = Math.Max(1, radius * 2);

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class LightDropDownRenderer : ToolStripProfessionalRenderer
        {
            public static readonly LightDropDownRenderer Instance = new();

            private LightDropDownRenderer()
            {
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using SolidBrush brush = new(Color.White);
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                Rectangle rect = new(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                using Pen pen = new(Color.FromArgb(200, 210, 220));
                e.Graphics.DrawRectangle(pen, rect);
            }
        }
    }

    public class MultiSelectComboBoxItemChangingEventArgs : EventArgs
    {
        public string Text { get; }
        public bool IsSelecting { get; }
        public bool Cancel { get; set; }

        public MultiSelectComboBoxItemChangingEventArgs(string text, bool isSelecting)
        {
            Text = text;
            IsSelecting = isSelecting;
        }
    }
}
