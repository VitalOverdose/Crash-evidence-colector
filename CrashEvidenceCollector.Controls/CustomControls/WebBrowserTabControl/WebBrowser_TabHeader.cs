using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using CrashEvidenceCollector.Theming;

namespace ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl
{
    public partial class WebBrowser_TabHeader : UserControl
    {
        // Fields
        private bool _isSelected = false;
        private bool _isHovered = false;
        private bool _closeButtonHovered = false;
        private Image? _icon = null;
        private string _tabText = "New Tab";
        private int _cornerRadius = 8;
        private bool _showCloseButton = true;
        private bool _showText = true;
        private int _iconSize = 24;
        private Rectangle _textRect;
        private Point? _dragStartScreenPoint = null;
        private bool _dragDetachTriggered = false;
        private bool _suppressNextClick = false;

        // Colours come from ThemeManager.Current at paint time (CEC change).

        // Layout
        private Rectangle _iconRect;
        private Rectangle _closeRect;

        // Events
        public event EventHandler? TabClicked;
        public event EventHandler? CloseClicked;
        public event EventHandler? DetachRequested;

        public WebBrowser_TabHeader()
        {
            InitializeComponent();

            this.Size = new Size(200, 40);
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            this.Cursor = Cursors.Hand;

            CalculateLayout();
        }

        // Properties
        [Category("Appearance")]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                this.Invalidate();
            }
        }

        [Category("Appearance")]
        public Image? Icon
        {
            get => _icon;
            set
            {
                // Dispose old icon if it exists
                if (_icon != null && _icon != value)
                {
                    _icon.Dispose();
                }

                // Clone the new icon to prevent disposal issues
                _icon = value != null ? (Image)value.Clone() : null;
                CalculateLayout(); // an icon changes where the title starts
                this.Invalidate();
            }
        }

        [Category("Appearance")]
        public string TabText
        {
            get => _tabText;
            set
            {
                _tabText = value ?? "New Tab";
                this.Invalidate();
            }
        }

        [Category("Appearance")]
        public bool ShowCloseButton
        {
            get => _showCloseButton;
            set
            {
                _showCloseButton = value;
                CalculateLayout();
                this.Invalidate();
            }
        }

        [Category("Appearance")]
        public bool ShowText
        {
            get => _showText;
            set
            {
                _showText = value;
                CalculateLayout();
                this.Invalidate();
            }
        }

        [Category("Appearance")]
        public int IconSize
        {
            get => _iconSize;
            set
            {
                _iconSize = value;
                CalculateLayout();
                this.Invalidate();
            }
        }

        private void CalculateLayout()
        {
            int padding = 5;
            int iconSize = _iconSize;
            int closeSize = 30;
            int spacing = 2;

            // Icon on left
            _iconRect = new Rectangle(padding, (this.Height - iconSize) / 2, iconSize, iconSize);

            // A tab without an icon starts its title at the padding instead of after an empty slot.
            int currentX = _icon != null ? _iconRect.Right + spacing : padding + 4;

            // Title in the middle, sized around the close button
            int textWidth = _showCloseButton ?
                this.Width - currentX - closeSize - spacing - padding :
                this.Width - currentX - padding;

            _textRect = new Rectangle(currentX, 0, Math.Max(0, textWidth), this.Height);

            // Close button on right (if showing)
            if (_showCloseButton)
            {
                _closeRect = new Rectangle(this.Width - closeSize - padding, (this.Height - closeSize) / 2, closeSize, closeSize);
            }
            else
            {
                _closeRect = Rectangle.Empty;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CalculateLayout();
            UpdateRegion();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _closeButtonHovered = false;
            this.Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            bool wasCloseHovered = _closeButtonHovered;
            _closeButtonHovered = _showCloseButton && _closeRect.Contains(e.Location);

            if (wasCloseHovered != _closeButtonHovered)
                this.Invalidate();

            TryDetachFromDrag();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left ||
                (_showCloseButton && _closeRect.Contains(e.Location)) ||
                DetachRequested == null)
            {
                return;
            }

            _dragStartScreenPoint = PointToScreen(e.Location);
            _dragDetachTriggered = false;
            Capture = true;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            EndDragTracking();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_suppressNextClick)
            {
                _suppressNextClick = false;
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                // Show context menu
                ShowContextMenu(e.Location);
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (_showCloseButton && _closeRect.Contains(e.Location))
                {
                    CloseClicked?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    TabClicked?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void ShowContextMenu(Point location)
        {
            var menu = new ContextMenuStrip();
            if (DetachRequested != null)
            {
                menu.Items.Add("Open Tab In New Window", null, (s, e) => DetachRequested?.Invoke(this, EventArgs.Empty));
                menu.Items.Add(new ToolStripSeparator());
            }

            menu.Items.Add("Close Tab", null, (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty));
            menu.Show(this, location);
        }

        private void TryDetachFromDrag()
        {
            if (_dragStartScreenPoint == null ||
                _dragDetachTriggered ||
                DetachRequested == null ||
                (Control.MouseButtons & MouseButtons.Left) != MouseButtons.Left)
            {
                return;
            }

            Point current = MousePosition;
            if (!HasMovedBeyondDragThreshold(_dragStartScreenPoint.Value, current) ||
                IsInsideTabStripDetachZone(current))
            {
                return;
            }

            _dragDetachTriggered = true;
            _suppressNextClick = true;
            EndDragTracking();
            DetachRequested?.Invoke(this, EventArgs.Empty);
        }

        private void EndDragTracking()
        {
            _dragStartScreenPoint = null;
            Capture = false;
        }

        private static bool HasMovedBeyondDragThreshold(Point start, Point current)
        {
            Size dragSize = SystemInformation.DragSize;
            Rectangle dragRect = new Rectangle(
                start.X - dragSize.Width / 2,
                start.Y - dragSize.Height / 2,
                dragSize.Width,
                dragSize.Height);

            return !dragRect.Contains(current);
        }

        private bool IsInsideTabStripDetachZone(Point screenPoint)
        {
            if (Parent == null)
                return false;

            Rectangle tabStripBounds = Parent.RectangleToScreen(Parent.ClientRectangle);
            tabStripBounds.Inflate(16, 16);
            return tabStripBounds.Contains(screenPoint);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            // The rounded edge anti-aliases into whatever sits behind it; paint that with the
            // strip's colour so the edge blends into the strip instead of leaving a pale halo.
            g.Clear(Parent?.BackColor ?? ThemeManager.Current.TabStrip);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Determine colors based on state
            Theme theme = ThemeManager.Current;
            Color backColor = theme.Tab;
            bool drawBorder = false;

            if (_isSelected)
            {
                backColor = theme.TabSelected;
                drawBorder = true;
            }
            else if (_isHovered)
            {
                backColor = theme.TabHover;
                drawBorder = false; // No border on hover
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            // Draw background
            using (GraphicsPath path = GetRoundedRect(rect, _cornerRadius))
            {
                using (SolidBrush brush = new SolidBrush(backColor))
                    g.FillPath(brush, path);

                // Draw border only if selected
                if (drawBorder)
                {
                    using (Pen pen = new Pen(theme.TabBorder, 1))
                        g.DrawPath(pen, path);
                }
            }

            // Draw icon
            if (_icon != null)
            {
                try
                {
                    g.DrawImage(_icon, _iconRect);
                }
                catch (ArgumentException)
                {
                    // Icon was disposed - clear it
                    _icon = null;
                }
            }

            // Title: drawn directly rather than through a disabled TextBox, which Windows
            // always paints grey. The selected tab reads strongest; the rest recede.
            if (_showText && !string.IsNullOrEmpty(_tabText) && _textRect.Width > 0)
            {
                Color textColor = _isSelected ? theme.TextStrong : _isHovered ? theme.Text : theme.TextMuted;
                TextRenderer.DrawText(g, _tabText, this.Font, _textRect, textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }

            // Draw close button (only if showing AND tab is hovered)
            if (_showCloseButton && !_closeRect.IsEmpty && _isHovered)
            {
                Color closeBackColor = _closeButtonHovered ? theme.BorderStrong : theme.Border;

                // Shrink rect by 2px on all sides
                Rectangle shrunkRect = new Rectangle(
                    _closeRect.X + 2,
                    _closeRect.Y + 2,
                    _closeRect.Width - 4,
                    _closeRect.Height - 4);

                // Draw rounded rectangle (not circle)
                using (GraphicsPath closePath = GetRoundedRect(shrunkRect, 6))
                {
                    using (SolidBrush brush = new SolidBrush(closeBackColor))
                        g.FillPath(brush, closePath);
                }

                // Draw X - centered in original rect
                using (Pen pen = new Pen(theme.TextMuted, 2))
                {
                    int offset = 10; // Larger offset = smaller X
                    g.DrawLine(pen,
                        _closeRect.Left + offset, _closeRect.Top + offset,
                        _closeRect.Right - offset, _closeRect.Bottom - offset);
                    g.DrawLine(pen,
                        _closeRect.Right - offset, _closeRect.Top + offset,
                        _closeRect.Left + offset, _closeRect.Bottom - offset);
                }
            }
        }

        private void UpdateRegion()
        {
            if (this.Width <= 0 || this.Height <= 0)
                return;

            try
            {
                var oldRegion = this.Region;
                using (GraphicsPath path = GetRoundedRect(new Rectangle(0, 0, this.Width, this.Height), _cornerRadius))
                {
                    this.Region = new Region(path);
                }
                oldRegion?.Dispose();
            }
            catch { }
        }

        private static GraphicsPath GetRoundedRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();

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
    }
}
