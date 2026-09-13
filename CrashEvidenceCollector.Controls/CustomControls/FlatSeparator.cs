using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// What a FlatSeparator is FOR. Replaces the old IsVerticalSplitter + LiveResize pair,
    /// which could express four combinations for only three meaningful states.
    /// </summary>
    public enum SeparatorRole
    {
        /// <summary>Just a line. Not draggable.</summary>
        None,

        /// <summary>Drag resizes the piston/spring live (can smear over heavy content).</summary>
        SplitterLive,

        /// <summary>Drag shows a preview line; the resize commits on mouse-up (no smear).</summary>
        SplitterDelayed
    }

    /// <summary>
    /// Simple horizontal separator with a light selectable surface behind it.
    /// Useful inside AdaptiveRowPanel rows where a plain line would be too hard
    /// to grab in the designer.
    /// </summary>
    public class FlatSeparator : Control, IAdaptiveRowPanelItem
    {
        private int _separatorHeight = 18;
        private int _lineThickness = 1;
        private Color _lineColor = Color.FromArgb(180, 180, 180);

        private bool _hoverColorFx = true;
        private Color _hoverLineColor = Color.FromArgb(70, 130, 220);
        private int _hoverLineThickness;      // 0 = keep LineThickness
        private bool _isHovered;

        public FlatSeparator()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.ResizeRedraw,
                true);

            BackColor = Color.FromArgb(242, 242, 242);
            ForeColor = _lineColor;
            Size = new Size(24, _separatorHeight);
            // Height floor of 0, not 8: a VStack rule with CollapsedHeight = 0 sets the child's
            // height and nothing else — WinForms then clamps it back up to MinimumSize, so an
            // 8px floor made IsCollapsed a no-op for separators and a "hidden" divider stayed
            // on screen, still hit-testable as a splitter. Width keeps its 8.
            MinimumSize = new Size(8, 0);
            Margin = new Padding(6, 0, 6, 0);
        }

        private bool _isVerticalSplitter;
        private bool _liveResize = true;
        private bool _dragging;
        private int _dragLastScreenY;
        private int _dragStartScreenY;
        private SplitterGhost? _ghost;

        [Category("Behavior")]
        [DefaultValue(SeparatorRole.None)]
        [Description("What this separator does. None: just a line. SplitterLive: drag resizes the piston/spring above it live. SplitterDelayed: drag shows a preview line and commits on mouse-up (no smear on heavy content). Splitter roles are inactive when there's nothing to resize.")]
        public SeparatorRole Role
        {
            get => _isVerticalSplitter
                ? (_liveResize ? SeparatorRole.SplitterLive : SeparatorRole.SplitterDelayed)
                : SeparatorRole.None;
            set
            {
                _isVerticalSplitter = value != SeparatorRole.None;
                _liveResize = value != SeparatorRole.SplitterDelayed;
                UpdateSplitterCursor();
                Invalidate();
            }
        }

        // ── Legacy shims ────────────────────────────────────────────────────────────────
        // Superseded by Role. Kept (hidden, non-serialized) so designer files written before
        // the change still deserialize; they self-migrate to Role on the next resave.
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsVerticalSplitter
        {
            get => _isVerticalSplitter;
            set
            {
                if (_isVerticalSplitter == value)
                {
                    return;
                }

                _isVerticalSplitter = value;
                UpdateSplitterCursor();
            }
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool LiveResize
        {
            get => _liveResize;
            set => _liveResize = value;
        }

        private VStackEngine? HostEngine => (Parent as IVStackHost)?.VStackEngine;

        private void UpdateSplitterCursor()
        {
            Cursor = _isVerticalSplitter && HostEngine is { } engine && engine.SplitterCanResize(this)
                ? Cursors.SizeNS
                : Cursors.Default;
        }

        // A translucent "ghost" bar (same idea as the bookmarks drag ghost) that follows the cursor
        // in preview mode; the real resize commits on mouse-up. Reliable vs. XOR-line contrast issues.
        private void ShowSplitterGhost(int screenY)
        {
            int inset = Math.Min(6, Math.Max(2, Width / 8));
            int leftX = PointToScreen(new Point(inset, 0)).X;
            int spanWidth = Math.Max(1, Width - (inset * 2));
            int thickness = Math.Max(1, _lineThickness);

            Size ghostSize = new Size(spanWidth, thickness);

            _ghost = new SplitterGhost
            {
                BackColor = _lineColor,
                Opacity = 0.7
            };

            // Pin the size hard: a Form will otherwise inflate a few-px window on handle creation
            // (auto-scale / minimum tracking size), which made the ghost as tall as the separator
            // instead of as thick as its line. Min+Max lock it, and it's re-asserted after Show().
            _ghost.MinimumSize = new Size(1, 1);
            _ghost.MaximumSize = ghostSize;
            _ghost.Size = ghostSize;
            _ghost.Location = new Point(leftX, screenY - (thickness / 2));
            _ghost.Show();
            _ghost.Size = ghostSize;
        }

        private void MoveSplitterGhost(int screenY)
        {
            if (_ghost != null)
            {
                _ghost.Location = new Point(_ghost.Location.X, screenY - (_ghost.Height / 2));
            }
        }

        private void HideSplitterGhost()
        {
            _ghost?.Close();
            _ghost?.Dispose();
            _ghost = null;
        }

        // Borderless, translucent, no-activate ghost so the separator keeps mouse capture while dragging.
        private sealed class SplitterGhost : Form
        {
            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    const int WS_EX_NOACTIVATE = 0x08000000;
                    const int WS_EX_TOOLWINDOW = 0x00000080;
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                    return cp;
                }
            }

            public SplitterGhost()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                StartPosition = FormStartPosition.Manual;
                Enabled = false;
                // No font/DPI auto-scaling — it would resize the pinned few-px ghost.
                AutoScaleMode = AutoScaleMode.None;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_isVerticalSplitter && e.Button == MouseButtons.Left
                && HostEngine is { } engine && engine.SplitterCanResize(this))
            {
                _dragging = true;
                Capture = true;
                // Screen Y: in live mode the separator moves during layout, so local coords drift.
                _dragStartScreenY = _dragLastScreenY = Cursor.Position.Y;
                if (!_liveResize)
                {
                    ShowSplitterGhost(_dragStartScreenY);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isVerticalSplitter)
            {
                return;
            }

            if (!_dragging)
            {
                UpdateSplitterCursor();
                return;
            }

            int y = Cursor.Position.Y;
            if (_liveResize)
            {
                int delta = y - _dragLastScreenY;
                if (delta != 0 && HostEngine is { } engine)
                {
                    _dragLastScreenY = y;
                    engine.ApplySplitterDrag(this, delta);
                }
            }
            else
            {
                MoveSplitterGhost(y);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging)
            {
                return;
            }

            if (!_liveResize)
            {
                HideSplitterGhost();

                int total = Cursor.Position.Y - _dragStartScreenY;
                if (total != 0 && HostEngine is { } engine)
                {
                    engine.ApplySplitterDrag(this, total);
                }
            }

            _dragging = false;
            Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // Capture lost mid-drag (alt-tab, focus steal): tidy up so no ghost is left behind.
            if (_dragging && !Capture)
            {
                HideSplitterGhost();
                _dragging = false;
            }
        }

        [Category("Layout")]
        [DefaultValue(18)]
        [Description("Height of the selectable separator surface.")]
        public int SeparatorHeight
        {
            get => _separatorHeight;
            set
            {
                int newValue = Math.Max(8, value);
                if (_separatorHeight == newValue)
                {
                    return;
                }

                _separatorHeight = newValue;
                Height = newValue;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(1)]
        [Description("Thickness of the separator line in pixels.")]
        public int LineThickness
        {
            get => _lineThickness;
            set
            {
                int newValue = Math.Max(1, value);
                if (_lineThickness == newValue)
                {
                    return;
                }

                _lineThickness = newValue;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Highlights the line while the mouse is over it. As a splitter the highlight only appears when there is actually something to resize, so it doubles as the affordance; a plain separator highlights whenever hovered.")]
        public bool HoverColorFx
        {
            get => _hoverColorFx;
            set { _hoverColorFx = value; if (_isHovered) Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Line colour while hovered (HoverColorFx on).")]
        public Color HoverLineColor
        {
            get => _hoverLineColor;
            set { _hoverLineColor = value; if (_isHovered) Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(0)]
        [Description("Line thickness while hovered. 0 keeps LineThickness — set it higher to fatten the grab line on hover.")]
        public int HoverLineThickness
        {
            get => _hoverLineThickness;
            set { _hoverLineThickness = Math.Max(0, value); if (_isHovered) Invalidate(); }
        }

        // Hover paint applies when the separator is actually interactive: a splitter with a
        // row to resize, or any non-splitter separator (where it's purely decorative).
        private bool ShowHoverState =>
            _hoverColorFx && (_isHovered || _dragging) &&
            (!_isVerticalSplitter || (HostEngine is { } engine && engine.SplitterCanResize(this)));

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

        [Category("Appearance")]
        [Description("Color of the separator line.")]
        public Color LineColor
        {
            get => _lineColor;
            set
            {
                if (_lineColor == value)
                {
                    return;
                }

                _lineColor = value;
                ForeColor = value;
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (Height != _separatorHeight)
            {
                _separatorHeight = Math.Max(8, Height);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Only fill when the background is opaque. A transparent BackColor lets the parent
            // show through — SupportsTransparentBackColor + OnPaintBackground paint the parent,
            // and skipping the Clear here preserves it.
            if (BackColor.A == 255)
                e.Graphics.Clear(BackColor);

            int y = Height / 2;
            int inset = Math.Min(6, Math.Max(2, Width / 8));

            bool hover = ShowHoverState;
            Color lineColor = hover ? _hoverLineColor : _lineColor;
            int thickness = hover && _hoverLineThickness > 0 ? _hoverLineThickness : _lineThickness;

            using Pen pen = new Pen(lineColor, thickness)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            e.Graphics.DrawLine(pen, inset, y, Math.Max(inset, Width - inset - 1), y);
        }
    }
}
