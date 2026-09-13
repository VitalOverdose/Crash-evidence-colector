using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CrashEvidenceCollector.Theming;

namespace ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl
{
    /// <summary>
    /// Manages tab headers (the visual tab buttons at top).
    /// Designed to be positioned in Row 0 of TableLayoutPanel.
    /// Fires events to coordinate with TabContentControl.
    /// </summary>
    public partial class TabHeadersControl : UserControl
    {
        private List<TabHeaderInfo> tabHeaders = new List<TabHeaderInfo>();
        private Button newTabButton = null!;
        private bool newTabButtonHovered = false;

        private const int FULL_TAB_WIDTH = 200;
        private const int MIN_TAB_WITH_TEXT_AND_CLOSE = 100;
        private const int MIN_TAB_TEXT_WIDTH = 30;
        private const int MIN_TAB_WITH_CLOSE = 66;
        private const int MIN_TAB_ICON_ONLY = 36;
        private const int MAX_TAB_WIDTH = 320;
        private const int NEW_TAB_BUTTON_SIZE = 27;
        private const int MAX_TABS = 50;
        private const int CLOSE_BUTTON_WIDTH = 30;
        private const int SPACING = 2;
        private const int PADDING = 5;

        // Events
        public event EventHandler<TabHeaderEventArgs>? TabSelected;
        public event EventHandler<TabHeaderEventArgs>? TabClosing;
        public event EventHandler<TabHeaderEventArgs>? TabDetachRequested;
        public event EventHandler? NewTabRequested;
        public event EventHandler<TabHeaderEventArgs>? ActiveTabClicked;

        // Properties
        [Category("Appearance")]
        public Color HeaderBackColor { get; set; } = Color.FromArgb(237, 245, 250);

        [Category("Behavior")]
        public bool ShowCloseButtons { get; set; } = true;

        [Category("Behavior")]
        public bool ShowNewTabButton { get; set; } = true;

        [Category("Behavior")]
        public bool SuppressMaxTabWarning { get; set; } = false;

        [Category("Appearance")]
        public int IconSize { get; set; } = 24;

        [Browsable(false)]
        public int SelectedIndex { get; private set; } = -1;

        [Browsable(false)]
        public int TabCount => tabHeaders.Count;

        public TabHeadersControl()
        {
            InitializeComponent();
            InitializeCustomComponents();
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
        }

        private void InitializeCustomComponents()
        {
            this.SuspendLayout();
            this.BackColor = HeaderBackColor;
            this.Padding = new Padding(5);

            // New tab button
            newTabButton = new Button
            {
                Width = NEW_TAB_BUTTON_SIZE,
                Height = 35,
                Text = "",
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                BackColor = HeaderBackColor,
                TextAlign = ContentAlignment.MiddleCenter
            };
            newTabButton.FlatAppearance.BorderSize = 0;
            newTabButton.FlatAppearance.MouseOverBackColor = HeaderBackColor;
            newTabButton.FlatAppearance.MouseDownBackColor = HeaderBackColor;
            newTabButton.Click += (s, e) => NewTabRequested?.Invoke(this, EventArgs.Empty);
            newTabButton.MouseEnter += (s, e) => { newTabButtonHovered = true; newTabButton.Invalidate(); };
            newTabButton.MouseLeave += (s, e) => { newTabButtonHovered = false; newTabButton.Invalidate(); };
            newTabButton.Paint += NewTabButton_Paint;
            this.Controls.Add(newTabButton);
            newTabButton.BringToFront();

            this.Paint += HeadersControl_Paint;
            this.SizeChanged += (s, e) => RecalculateTabSizes();

            this.ResumeLayout();

            // Set initial + button position
            if (ShowNewTabButton)
            {
                newTabButton.Location = new Point(this.Padding.Left + 3, this.Padding.Top);
                newTabButton.Visible = true;
                newTabButton.BringToFront();
            }
        }

        private void HeadersControl_Paint(object? sender, PaintEventArgs e)
        {
            // Draw separators between tabs
            var g = e.Graphics;
            using (Pen pen = new Pen(ThemeManager.Current.Border, 2))
            {
                for (int i = 0; i < tabHeaders.Count - 1; i++)
                {
                    var header = tabHeaders[i].Header;
                    int x = header.Right + 3;
                    int y1 = this.Padding.Top + 5;
                    int y2 = this.Height - this.Padding.Bottom - 5;
                    g.DrawLine(pen, x, y1, x, y2);
                }

                // Draw separator before + button
                if (tabHeaders.Count > 0 && ShowNewTabButton && newTabButton.Visible)
                {
                    int x = newTabButton.Left - 3;
                    int y1 = this.Padding.Top + 5;
                    int y2 = this.Height - this.Padding.Bottom - 5;
                    g.DrawLine(pen, x, y1, x, y2);
                }
            }
        }

        private void NewTabButton_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(HeaderBackColor);

            var rect = new Rectangle(0, 0, newTabButton.Width - 1, newTabButton.Height - 1);
            Color bgColor = newTabButtonHovered ? ThemeManager.Current.TabHover : ThemeManager.Current.Tab;

            using (var path = GetRoundedRectPath(rect, 8))
            {
                using (var brush = new SolidBrush(bgColor))
                    g.FillPath(brush, path);

                using (var pen = new Pen(ThemeManager.Current.TabBorder, 1))
                    g.DrawPath(pen, path);
            }

            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;

                var textRect = new RectangleF(-1, 0, newTabButton.ClientRectangle.Width, newTabButton.ClientRectangle.Height);
                using (var plus = new SolidBrush(ThemeManager.Current.Text))
                    g.DrawString("+", newTabButton.Font, plus, textRect, sf);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;

            if (rect.Width < d || rect.Height < d)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public void AddTab(string text, Image? icon = null)
        {
            if (tabHeaders.Count >= MAX_TABS && !SuppressMaxTabWarning)
            {
                MessageBox.Show(
                    $"Maximum of {MAX_TABS} tabs reached.\n\nPlease close some tabs for continued smooth operation.",
                    "Too Many Tabs",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var header = new WebBrowser_TabHeader
            {
                TabText = text,
                Icon = icon,
                Width = FULL_TAB_WIDTH,
                Height = 35,
                Margin = new Padding(0),
                ShowCloseButton = ShowCloseButtons,
                IconSize = IconSize
            };

            header.TabClicked += (s, e) => HandleTabClicked(header);
            header.CloseClicked += (s, e) => HandleCloseClicked(header);
            header.DetachRequested += (s, e) => HandleDetachRequested(header);

            tabHeaders.Add(new TabHeaderInfo { Header = header, Index = tabHeaders.Count });
            this.Controls.Add(header);

            newTabButton.BringToFront();

            RecalculateTabSizes();
            this.Refresh();
            InvokeDeferredLayout();

            SelectTab(tabHeaders.Count - 1);
        }

        /// <summary>Tab index (list position) of the header under the given point, or -1.</summary>
        public int GetTabIndexAt(Point p)
        {
            for (int i = 0; i < tabHeaders.Count; i++)
                if (tabHeaders[i].Header.Bounds.Contains(p))
                    return i;
            return -1;
        }

        public string GetTabText(int index)
        {
            if (index < 0 || index >= tabHeaders.Count)
                return string.Empty;

            return tabHeaders[index].Header.TabText;
        }

        /// <summary>True when the point is blank tab chrome, not a tab or the new-tab button.</summary>
        public bool IsBlankHeaderArea(Point p)
        {
            if (!ClientRectangle.Contains(p))
                return false;

            if (GetTabIndexAt(p) >= 0)
                return false;

            return !(ShowNewTabButton && newTabButton.Visible && newTabButton.Bounds.Contains(p));
        }

        private void HandleTabClicked(WebBrowser_TabHeader header)
        {
            var info = tabHeaders.FirstOrDefault(x => x.Header == header);
            if (info == null) return;

            int index = tabHeaders.IndexOf(info);
            bool wasAlreadySelected = (index == SelectedIndex);

            SelectTab(index);

            if (wasAlreadySelected)
            {
                ActiveTabClicked?.Invoke(this, new TabHeaderEventArgs(index));
            }
        }

        private void HandleCloseClicked(WebBrowser_TabHeader header)
        {
            var info = tabHeaders.FirstOrDefault(x => x.Header == header);
            if (info == null) return;

            int index = tabHeaders.IndexOf(info);
            CloseTab(index);
        }

        private void HandleDetachRequested(WebBrowser_TabHeader header)
        {
            var info = tabHeaders.FirstOrDefault(x => x.Header == header);
            if (info == null) return;

            int index = tabHeaders.IndexOf(info);
            TabDetachRequested?.Invoke(this, new TabHeaderEventArgs(index));
        }

        public void SelectTab(int index)
        {
            if (index < 0 || index >= tabHeaders.Count) return;

            foreach (var tab in tabHeaders)
                tab.Header.IsSelected = false;

            tabHeaders[index].Header.IsSelected = true;
            SelectedIndex = index;

            RecalculateTabSizes();

            TabSelected?.Invoke(this, new TabHeaderEventArgs(index));
        }

        public void RemoveTab(int index)
        {
            if (index < 0 || index >= tabHeaders.Count) return;

            var info = tabHeaders[index];
            tabHeaders.RemoveAt(index);
            this.Controls.Remove(info.Header);
            info.Header.Dispose();

            // Update indices
            for (int i = index; i < tabHeaders.Count; i++)
            {
                tabHeaders[i].Index = i;
            }

            if (tabHeaders.Count > 0)
            {
                int newIndex = Math.Min(index, tabHeaders.Count - 1);
                SelectTab(newIndex);
            }
            else
            {
                SelectedIndex = -1;
            }

            RecalculateTabSizes();
        }

        public void CloseTab(int index)
        {
            if (index < 0 || index >= tabHeaders.Count) return;

            var args = new TabHeaderEventArgs(index);
            TabClosing?.Invoke(this, args);

            if (args.Cancel) return;

            RemoveTab(index);
        }

        public void SetTabText(int index, string text)
        {
            if (index < 0 || index >= tabHeaders.Count) return;
            tabHeaders[index].Header.TabText = text;
            RecalculateTabSizes();
        }

        public void SetTabIcon(int index, Image? icon)
        {
            if (index < 0 || index >= tabHeaders.Count) return;

            if (icon != null)
            {
                tabHeaders[index].Header.Icon = (Image)icon.Clone();
            }
            else
            {
                tabHeaders[index].Header.Icon = null;
            }
        }

        private void RecalculateTabSizes()
        {
            if (tabHeaders.Count == 0)
            {
                newTabButton.Visible = ShowNewTabButton;
                if (ShowNewTabButton)
                {
                    newTabButton.Location = new Point(this.Padding.Left + 3, this.Padding.Top);
                    newTabButton.BringToFront();
                }
                return;
            }

            const int GAP_BETWEEN_TABS = 6;
            int gapsTotalWidth = (tabHeaders.Count - 1) * GAP_BETWEEN_TABS;
            int newTabButtonWidth = ShowNewTabButton ? NEW_TAB_BUTTON_SIZE : 0;
            int availableWidth = this.Width - this.Padding.Horizontal - newTabButtonWidth - 13 - gapsTotalWidth;
            int selectedIndex = tabHeaders.FindIndex(x => x.Header.IsSelected);
            int tabCount = tabHeaders.Count;
            bool allowClose = ShowCloseButtons && tabHeaders.Count > 1;
            int[] preferredWidths = tabHeaders
                .Select(tab => GetPreferredTabWidth(tab.Header, allowClose))
                .ToArray();

            if (preferredWidths.Sum() <= availableWidth)
            {
                for (int i = 0; i < tabHeaders.Count; i++)
                {
                    tabHeaders[i].Header.Width = preferredWidths[i];
                    tabHeaders[i].Header.ShowText = true;
                    tabHeaders[i].Header.ShowCloseButton = allowClose;
                }

                newTabButton.Visible = ShowNewTabButton;
                PositionTabs();
                this.Invalidate();
                return;
            }

            if (FULL_TAB_WIDTH * tabCount <= availableWidth)
            {
                SetAllTabSizes(FULL_TAB_WIDTH, showText: true, showClose: ShowCloseButtons);
                newTabButton.Visible = ShowNewTabButton;
            }
            else if (MIN_TAB_WITH_TEXT_AND_CLOSE * tabCount <= availableWidth)
            {
                int tabWidth = availableWidth / tabCount;
                SetAllTabSizes(tabWidth, showText: true, showClose: ShowCloseButtons);
                newTabButton.Visible = ShowNewTabButton;
            }
            else
            {
                int selectedTabWidth = IconSize + SPACING + MIN_TAB_TEXT_WIDTH + SPACING + CLOSE_BUTTON_WIDTH + (PADDING * 2);
                int unselectedTabWidth = IconSize + SPACING + MIN_TAB_TEXT_WIDTH + (PADDING * 2);

                int totalNeededWidth = selectedTabWidth + (unselectedTabWidth * (tabCount - 1));

                if (totalNeededWidth <= availableWidth)
                {
                    int extraSpace = availableWidth - totalNeededWidth;
                    int extraPerTab = extraSpace / tabCount;

                    for (int i = 0; i < tabHeaders.Count; i++)
                    {
                        bool isSelected = i == selectedIndex;
                        int baseWidth = isSelected ? selectedTabWidth : unselectedTabWidth;

                        tabHeaders[i].Header.Width = baseWidth + extraPerTab;
                        tabHeaders[i].Header.ShowText = true;
                        tabHeaders[i].Header.ShowCloseButton = isSelected && ShowCloseButtons && tabHeaders.Count > 1;
                    }
                    newTabButton.Visible = ShowNewTabButton;
                }
                else
                {
                    int tabWidth = availableWidth / tabCount;

                    for (int i = 0; i < tabHeaders.Count; i++)
                    {
                        bool isSelected = i == selectedIndex;
                        tabHeaders[i].Header.Width = tabWidth;
                        tabHeaders[i].Header.ShowText = true;
                        tabHeaders[i].Header.ShowCloseButton = isSelected && ShowCloseButtons && tabHeaders.Count > 1;
                    }

                    newTabButton.Visible = tabWidth >= MIN_TAB_WITH_CLOSE && ShowNewTabButton;
                }
            }

            PositionTabs();
            this.Invalidate();
        }

        private void SetAllTabSizes(int width, bool showText, bool showClose)
        {
            bool allowClose = showClose && tabHeaders.Count > 1;

            foreach (var info in tabHeaders)
            {
                info.Header.Width = width;
                info.Header.ShowText = showText;
                info.Header.ShowCloseButton = allowClose;
            }
        }

        private int GetPreferredTabWidth(WebBrowser_TabHeader header, bool showClose)
        {
            int textWidth = TextRenderer.MeasureText(
                header.TabText,
                header.Font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            int closeWidth = showClose ? CLOSE_BUTTON_WIDTH + SPACING : 0;
            int width = IconSize + SPACING + textWidth + closeWidth + (PADDING * 2);
            return Math.Clamp(width, MIN_TAB_WITH_TEXT_AND_CLOSE, MAX_TAB_WIDTH);
        }

        private void PositionTabs()
        {
            int x = this.Padding.Left;
            int y = this.Padding.Top;
            const int TOTAL_GAP = 6;

            for (int i = 0; i < tabHeaders.Count; i++)
            {
                tabHeaders[i].Header.Location = new Point(x, y);
                x += tabHeaders[i].Header.Width;

                if (i < tabHeaders.Count - 1)
                    x += TOTAL_GAP;
            }

            if (ShowNewTabButton && newTabButton.Visible)
            {
                if (tabHeaders.Count > 0)
                {
                    newTabButton.Location = new Point(x + 6, y);
                }
                else
                {
                    newTabButton.Location = new Point(this.Padding.Left + 3, y);
                }
                newTabButton.BringToFront();
                newTabButton.Update();
            }
        }

        private void InvokeDeferredLayout()
        {
            if (!IsHandleCreated)
            {
                HandleCreated += (s, e) => InvokeDeferredLayout();
                return;
            }

            BeginInvoke((MethodInvoker)(() =>
            {
                RecalculateTabSizes();
                PositionTabs();
                this.PerformLayout();
                this.Refresh();
            }));
        }

        private class TabHeaderInfo
        {
            public WebBrowser_TabHeader Header { get; set; } = null!;
            public int Index { get; set; }
        }
    }

    public class TabHeaderEventArgs : EventArgs
    {
        public int TabIndex { get; }
        public bool Cancel { get; set; }

        public TabHeaderEventArgs(int tabIndex)
        {
            TabIndex = tabIndex;
        }
    }
}
