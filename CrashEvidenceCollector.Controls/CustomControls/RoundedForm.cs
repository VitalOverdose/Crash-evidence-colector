using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public enum RoundedFormResizeMode
    {
        None,
        Width,
        Height,
        Both
    }

    public class RoundedForm : Form
    {
        private const int ResizeGripSize = 8;

        private int _cornerRadius = 20;
        private Color _formBackColor = Color.White;
        private RoundedFormResizeMode _resizeMode = RoundedFormResizeMode.None;
        private bool _topCloseButtonVisible;
        private int _topCloseButtonSize = 24;
        private int _topCloseButtonMargin = 8;
        private Rectangle _topCloseButtonBounds = Rectangle.Empty;
        private bool _topCloseButtonHovered;
        private bool _topCloseButtonPressed;

        // =================================================================================
        // APPEARANCE SETTINGS
        // =================================================================================

        [Category("Appearance")]
        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                int radius = Math.Max(0, value);
                if (_cornerRadius == radius)
                {
                    return;
                }

                _cornerRadius = radius;
                UpdateRoundedRegion();
                Invalidate();
            }
        }

        [Category("Appearance")]
        public Color BorderColor { get; set; } = Color.DarkGray;

        [Category("Appearance")]
        public int BorderThickness { get; set; } = 2;

        [Category("Appearance")]
        public Color FormBackColor
        {
            get => _formBackColor;
            set
            {
                if (_formBackColor == value)
                {
                    return;
                }

                _formBackColor = value;
                BackColor = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [Description("Controls which edges of the borderless rounded form can be resized by dragging.")]
        public RoundedFormResizeMode ResizeMode
        {
            get => _resizeMode;
            set
            {
                if (_resizeMode == value)
                {
                    return;
                }

                _resizeMode = value;
                Invalidate();
            }
        }

        // NOTE: These properties are kept so your existing code doesn't break,
        // but they are ignored because we are now using the native Windows shadow
        // which is cleaner and doesn't cause the "Purple Corner" bug.
        [Browsable(false)] public int ShadowSize { get; set; } = 14;
        [Browsable(false)] public int ShadowOpacity { get; set; } = 120;

        // =================================================================================
        // TOP BAR PROPERTIES
        // =================================================================================
        [Category("Top Bar")]
        public bool TopBarVisible { get; set; } = true;
        [Category("Top Bar")]
        public int TopBarHeight { get; set; } = 46;
        [Category("Top Bar")]
        public Color TopBarColor { get; set; } = Color.FromArgb(230, 235, 240);
        [Category("Top Bar")]
        public bool TopSeparatorVisible { get; set; } = true;
        [Category("Top Bar")]
        public Color TopSeparatorColor { get; set; } = Color.DarkGray;

        [Category("Top Bar Text")]
        public string TopText { get; set; } = "Title";
        [Category("Top Bar Text")]
        public Font TopTextFont { get; set; } = new Font("Segoe UI Semibold", 14F, FontStyle.Bold);
        [Category("Top Bar Text")]
        public Color TopTextColor { get; set; } = Color.FromArgb(31, 31, 31);
        [Category("Top Bar Text")]
        public int TopTextOffsetX { get; set; } = 10;

        [Category("Top Bar Close Button")]
        [DefaultValue(false)]
        public bool TopCloseButtonVisible
        {
            get => _topCloseButtonVisible;
            set
            {
                if (_topCloseButtonVisible == value)
                {
                    return;
                }

                _topCloseButtonVisible = value;
                if (!value)
                {
                    _topCloseButtonBounds = Rectangle.Empty;
                    _topCloseButtonHovered = false;
                    _topCloseButtonPressed = false;
                }

                Invalidate();
            }
        }

        [Category("Top Bar Close Button")]
        [DefaultValue(24)]
        public int TopCloseButtonSize
        {
            get => _topCloseButtonSize;
            set
            {
                int newValue = Math.Max(12, value);
                if (_topCloseButtonSize == newValue)
                {
                    return;
                }

                _topCloseButtonSize = newValue;
                Invalidate();
            }
        }

        [Category("Top Bar Close Button")]
        [DefaultValue(8)]
        public int TopCloseButtonMargin
        {
            get => _topCloseButtonMargin;
            set
            {
                int newValue = Math.Max(0, value);
                if (_topCloseButtonMargin == newValue)
                {
                    return;
                }

                _topCloseButtonMargin = newValue;
                Invalidate();
            }
        }

        [Category("Top Bar Close Button")]
        public Color TopCloseButtonHoverColor { get; set; } = Color.FromArgb(245, 250, 255);

        [Category("Top Bar Close Button")]
        public Color TopCloseButtonPressedColor { get; set; } = Color.Gainsboro;

        [Category("Top Bar Close Button")]
        public Color TopCloseButtonColor { get; set; } = Color.FromArgb(50, 50, 50);

        [Category("Top Bar Close Button")]
        [DefaultValue(DialogResult.Cancel)]
        public DialogResult TopCloseButtonDialogResult { get; set; } = DialogResult.Cancel;

        // =================================================================================
        // BOTTOM BAR PROPERTIES
        // =================================================================================
        [Category("Bottom Bar")]
        public bool BottomBarVisible { get; set; } = true;
        [Category("Bottom Bar")]
        public int BottomBarHeight { get; set; } = 46;
        [Category("Bottom Bar")]
        public Color BottomBarColor { get; set; } = Color.FromArgb(230, 235, 240);
        [Category("Bottom Bar")]
        public bool BottomSeparatorVisible { get; set; } = true;
        [Category("Bottom Bar")]
        public Color BottomSeparatorColor { get; set; } = Color.DarkGray;

        [Category("Bottom Bar Text")]
        public string BottomText { get; set; } = "";
        [Category("Bottom Bar Text")]
        public Font BottomTextFont { get; set; } = new Font("Segoe UI", 12F);
        [Category("Bottom Bar Text")]
        public Color BottomTextColor { get; set; } = Color.FromArgb(31, 31, 31);
        [Category("Bottom Bar Text")]
        public int BottomTextOffsetX { get; set; } = 10;

        // =================================================================================
        // NATIVE WINDOWS SHADOW (The Fix)
        // =================================================================================
        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DROPSHADOW = 0x20000;
                CreateParams cp = base.CreateParams;
                // This adds a real shadow without needing a second form
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        public RoundedForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = _formBackColor; // Ensures no black background artifacts
            this.DoubleBuffered = true;
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        // A modeless popup (Show(owner)) that sat above a BORDERLESS owner can leave the whole app
        // dropped behind other windows when it closes — a borderless main window doesn't reclaim its
        // z-order the way a framed one does. Bring the owner back to the foreground on close. Modal
        // dialogs already do this via WinForms; forms with no owner (e.g. the main window) no-op.
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            Form? owner = Owner;
            if (owner != null && !owner.IsDisposed && owner.Visible && owner.WindowState != FormWindowState.Minimized)
            {
                try { owner.Activate(); } catch { /* owner tearing down; nothing to restore */ }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRoundedRegion();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateRoundedRegion();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            UpdateRoundedRegion();
            base.OnSizeChanged(e);
        }

        // =================================================================================
        // PAINTING
        // =================================================================================
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = this.ClientRectangle;

            // 1. Fill Main Background
            using (SolidBrush bgBrush = new SolidBrush(FormBackColor))
            {
                g.FillRectangle(bgBrush, rect);
            }

            // 2. Draw Bottom Bar
            if (BottomBarVisible && BottomBarHeight > 0)
            {
                Rectangle bottomRect = new Rectangle(0, rect.Height - BottomBarHeight, rect.Width, BottomBarHeight);
                using (SolidBrush barBrush = new SolidBrush(BottomBarColor))
                {
                    g.FillRectangle(barBrush, bottomRect);
                }

                if (BottomSeparatorVisible)
                {
                    using (Pen sepPen = new Pen(BottomSeparatorColor, 1))
                        g.DrawLine(sepPen, 0, bottomRect.Top, rect.Width, bottomRect.Top);
                }

                DrawTextWithTruncation(g, BottomText, BottomTextFont, BottomTextColor, bottomRect, BottomTextOffsetX);
            }

            // 3. Draw Top Bar
            if (TopBarVisible && TopBarHeight > 0)
            {
                Rectangle topRect = new Rectangle(0, 0, rect.Width, TopBarHeight);
                using (SolidBrush barBrush = new SolidBrush(TopBarColor))
                {
                    g.FillRectangle(barBrush, topRect);
                }

                if (TopSeparatorVisible)
                {
                    using (Pen sepPen = new Pen(TopSeparatorColor, 1))
                        g.DrawLine(sepPen, 0, topRect.Bottom - 1, rect.Width, topRect.Bottom - 1);
                }

                UpdateTopCloseButtonBounds(topRect);
                int closeButtonInset = TopCloseButtonVisible && !_topCloseButtonBounds.IsEmpty
                    ? rect.Width - _topCloseButtonBounds.Left + 4
                    : TopTextOffsetX;

                DrawTextWithTruncation(g, TopText, TopTextFont, TopTextColor, topRect, TopTextOffsetX, closeButtonInset);
                DrawTopCloseButton(g);
            }
            else
            {
                _topCloseButtonBounds = Rectangle.Empty;
            }

        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using SolidBrush brush = new SolidBrush(FormBackColor);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        // =================================================================================
        // HELPERS
        // =================================================================================
        private void DrawTextWithTruncation(Graphics g, string text, Font font, Color color, Rectangle barRect, int xOffset, int rightInset = -1)
        {
            if (string.IsNullOrEmpty(text)) return;
            int effectiveRightInset = rightInset >= 0 ? rightInset : xOffset;

            Rectangle textRect = new Rectangle(
                barRect.X + xOffset,
                barRect.Y,
                Math.Max(0, barRect.Width - xOffset - effectiveRightInset),
                barRect.Height);

            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;

                g.DrawString(text, font, brush, textRect, sf);
            }
        }

        private void UpdateTopCloseButtonBounds(Rectangle topRect)
        {
            if (!TopCloseButtonVisible || !TopBarVisible || TopBarHeight <= 0)
            {
                _topCloseButtonBounds = Rectangle.Empty;
                return;
            }

            int maxSize = Math.Max(12, topRect.Height - 8);
            int size = Math.Min(TopCloseButtonSize, maxSize);
            int x = topRect.Right - TopCloseButtonMargin - size;
            int y = topRect.Top + ((topRect.Height - size) / 2);
            _topCloseButtonBounds = new Rectangle(x, y, size, size);
        }

        private void DrawTopCloseButton(Graphics g)
        {
            if (!TopCloseButtonVisible || _topCloseButtonBounds.IsEmpty)
            {
                return;
            }

            if (_topCloseButtonHovered || _topCloseButtonPressed)
            {
                Color fill = _topCloseButtonPressed ? TopCloseButtonPressedColor : TopCloseButtonHoverColor;
                using SolidBrush brush = new SolidBrush(fill);
                using GraphicsPath hoverPath = GetRoundedPath(_topCloseButtonBounds, Math.Max(4, _topCloseButtonBounds.Height / 4));
                g.FillPath(brush, hoverPath);
            }

            int inset = Math.Max(7, _topCloseButtonBounds.Width / 3);
            Point p1 = new Point(_topCloseButtonBounds.Left + inset, _topCloseButtonBounds.Top + inset);
            Point p2 = new Point(_topCloseButtonBounds.Right - inset - 1, _topCloseButtonBounds.Bottom - inset - 1);
            Point p3 = new Point(_topCloseButtonBounds.Right - inset - 1, _topCloseButtonBounds.Top + inset);
            Point p4 = new Point(_topCloseButtonBounds.Left + inset, _topCloseButtonBounds.Bottom - inset - 1);

            using Pen pen = new Pen(TopCloseButtonColor, 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            g.DrawLine(pen, p1, p2);
            g.DrawLine(pen, p3, p4);
        }

        private void DrawBorder(Graphics g)
        {
            int drawThickness = BorderThickness < 2 ? 2 : BorderThickness;
            Color activeBorderColor = ResizeMode == RoundedFormResizeMode.None
                ? BorderColor
                : BlendColors(BorderColor, FormBackColor, 0.35f);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using GraphicsPath path = GetRoundedPath(ClientRectangle, CornerRadius);
            using Pen pen = new Pen(activeBorderColor, drawThickness);
            pen.Alignment = PenAlignment.Inset;
            g.DrawPath(pen, path);
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (radius < 1) { path.AddRectangle(rect); return path; }

            path.StartFigure();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void UpdateRoundedRegion()
        {
            if (!IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            {
                return;
            }

            using GraphicsPath path = GetRoundedPath(ClientRectangle, CornerRadius);
            Region?.Dispose();
            Region = new Region(path);
        }

        private static Color BlendColors(Color foreground, Color background, float foregroundAmount)
        {
            foregroundAmount = Math.Clamp(foregroundAmount, 0f, 1f);
            float backgroundAmount = 1f - foregroundAmount;

            return Color.FromArgb(
                (int)((foreground.A * foregroundAmount) + (background.A * backgroundAmount)),
                (int)((foreground.R * foregroundAmount) + (background.R * backgroundAmount)),
                (int)((foreground.G * foregroundAmount) + (background.G * backgroundAmount)),
                (int)((foreground.B * foregroundAmount) + (background.B * backgroundAmount)));
        }

        // =================================================================================
        // DRAGGING LOGIC
        // =================================================================================
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        private const int WM_NCHITTEST = 0x84;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hovered = IsOverTopCloseButton(e.Location);
            if (_topCloseButtonHovered != hovered)
            {
                _topCloseButtonHovered = hovered;
                Invalidate(_topCloseButtonBounds);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_topCloseButtonHovered)
            {
                _topCloseButtonHovered = false;
                Invalidate(_topCloseButtonBounds);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (IsOverTopCloseButton(e.Location))
                {
                    _topCloseButtonPressed = true;
                    Invalidate(_topCloseButtonBounds);
                    return;
                }

                BeginFormDrag();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !_topCloseButtonPressed)
            {
                return;
            }

            bool closeClicked = IsOverTopCloseButton(e.Location);
            _topCloseButtonPressed = false;
            Invalidate(_topCloseButtonBounds);

            if (closeClicked)
            {
                if (TopCloseButtonDialogResult != DialogResult.None)
                {
                    DialogResult = TopCloseButtonDialogResult;
                }

                Close();
            }
        }

        private bool IsOverTopCloseButton(Point location)
        {
            if (!TopCloseButtonVisible || !TopBarVisible || TopBarHeight <= 0)
            {
                return false;
            }

            if (_topCloseButtonBounds.IsEmpty)
            {
                UpdateTopCloseButtonBounds(new Rectangle(0, 0, ClientSize.Width, TopBarHeight));
            }

            return _topCloseButtonBounds.Contains(location);
        }

        public void BeginFormDrag()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        public void EnableDrag(Control control)
        {
            if (control != null)
            {
                control.MouseDown += (s, e) =>
                {
                    // A v2 row paints its virtual children onto its own surface, so this
                    // MouseDown fires over them too — starting a drag there eats the click and
                    // the button goes dead. Yield to the panel's click routing.
                    if (s is VirtualizingAdaptiveRowPanel row && row.IsPointOnVirtualChild(e.Location))
                    {
                        return;
                    }

                    if (e.Button == MouseButtons.Left)
                    {
                        BeginFormDrag();
                    }
                };
            }
        }

        /// <summary>
        /// EnableDrag hooks only the named control, but composites (RoundedTextBox, panels with
        /// children) cover their own surface with child HWNDs that swallow the mouse. This
        /// overload hooks the whole tree so the entire visual area is a drag handle.
        /// </summary>
        public void EnableDrag(Control control, bool includeChildren)
        {
            EnableDrag(control);
            if (includeChildren && control != null)
            {
                foreach (Control child in control.Controls)
                {
                    EnableDrag(child, true);
                }
            }
        }

        /// <summary>
        /// Supplies resize hit zones for the borderless rounded form.
        /// Width enables left/right edges, Height enables top/bottom edges, and Both enables corners too.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && ResizeMode != RoundedFormResizeMode.None)
            {
                base.WndProc(ref m);

                if ((int)m.Result == HT_CAPTION)
                {
                    return;
                }

                Point clientPoint = PointToClient(new Point(m.LParam.ToInt32()));
                bool allowWidth = ResizeMode is RoundedFormResizeMode.Width or RoundedFormResizeMode.Both;
                bool allowHeight = ResizeMode is RoundedFormResizeMode.Height or RoundedFormResizeMode.Both;

                bool left = allowWidth && clientPoint.X <= ResizeGripSize;
                bool right = allowWidth && clientPoint.X >= ClientSize.Width - ResizeGripSize;
                bool top = allowHeight && clientPoint.Y <= ResizeGripSize;
                bool bottom = allowHeight && clientPoint.Y >= ClientSize.Height - ResizeGripSize;

                if (ResizeMode == RoundedFormResizeMode.Both)
                {
                    if (top && left)
                    {
                        m.Result = HTTOPLEFT;
                        return;
                    }

                    if (top && right)
                    {
                        m.Result = HTTOPRIGHT;
                        return;
                    }

                    if (bottom && left)
                    {
                        m.Result = HTBOTTOMLEFT;
                        return;
                    }

                    if (bottom && right)
                    {
                        m.Result = HTBOTTOMRIGHT;
                        return;
                    }
                }

                if (left)
                {
                    m.Result = HTLEFT;
                    return;
                }

                if (right)
                {
                    m.Result = HTRIGHT;
                    return;
                }

                if (top)
                {
                    m.Result = HTTOP;
                    return;
                }

                if (bottom)
                {
                    m.Result = HTBOTTOM;
                    return;
                }

                return;
            }

            base.WndProc(ref m);

            if (m.Msg == 0x000F) // WM_PAINT — redraw border after all children
            {
                using Graphics g = CreateGraphics();
                DrawBorder(g);
            }
        }
    }
}
