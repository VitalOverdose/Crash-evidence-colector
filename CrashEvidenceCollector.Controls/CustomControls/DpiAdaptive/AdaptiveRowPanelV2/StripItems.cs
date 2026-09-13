using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// Base class for all drawn items. Deriving from Component means the designer
    /// sites, names, and serializes items automatically, and the Events tab works.
    /// There is exactly ONE representation: these items. No child controls, no sync.
    /// </summary>
    [DesignTimeVisible(true)]
    [ToolboxItem(false)]
    public class StripItem : Component
    {
        private string _text = string.Empty;
        private int _width = 80;
        private int _minWidth = 12;
        private bool _visible = true;
        private bool _enabled = true;
        private bool _isSpring;
        private Padding _margin = new(3, 0, 3, 0);

        /// <summary>Set by the strip during layout. Runtime state, never serialized.</summary>
        [Browsable(false)]
        public Rectangle Bounds { get; internal set; }

        [Browsable(false)]
        internal AdaptiveRowStrip? Owner { get; set; }

        /// <summary>Whether this item participates in hit-testing / focus / clicks.</summary>
        [Browsable(false)]
        public virtual bool Interactive => true;

        [Category("Appearance"), DefaultValue("")]
        public string Text
        {
            get => _text;
            set { if (_text != value) { _text = value ?? string.Empty; Invalidate(); } }
        }

        /// <summary>Authored width — the strip's fit pass may compress it toward MinWidth.</summary>
        [Category("Layout"), DefaultValue(80)]
        public int Width
        {
            get => _width;
            set { if (_width != value) { _width = Math.Max(1, value); InvalidateLayout(); } }
        }

        [Category("Layout"), DefaultValue(12)]
        public int MinWidth
        {
            get => _minWidth;
            set { if (_minWidth != value) { _minWidth = Math.Max(1, value); InvalidateLayout(); } }
        }

        [Category("Layout")]
        [DefaultValue(typeof(Padding), "3, 0, 3, 0")]
        [Description("Logical-DPI outer spacing. Facing horizontal margins are added together.")]
        public Padding Margin
        {
            get => _margin;
            set
            {
                Padding normalized = new(
                    Math.Max(0, value.Left), Math.Max(0, value.Top),
                    Math.Max(0, value.Right), Math.Max(0, value.Bottom));
                if (_margin == normalized) return;
                _margin = normalized;
                InvalidateLayout();
            }
        }

        /// <summary>Spring items absorb spare width / give up width first, like ARP's spring.</summary>
        [Category("Layout"), DefaultValue(false)]
        public bool IsSpring
        {
            get => _isSpring;
            set { if (_isSpring != value) { _isSpring = value; InvalidateLayout(); } }
        }

        [Category("Behavior"), DefaultValue(true)]
        public bool Visible
        {
            get => _visible;
            set { if (_visible != value) { _visible = value; InvalidateLayout(); } }
        }

        [Category("Behavior"), DefaultValue(true)]
        public bool Enabled
        {
            get => _enabled;
            set { if (_enabled != value) { _enabled = value; InvalidateLayout(); } }
        }

        /// <summary>Per-item click delegate (assign in code) — raised alongside the strip-level ItemClicked event.</summary>
        [Browsable(false)]
        public Action<StripItem>? ClickAction { get; set; }

        [Category("Action")]
        public event EventHandler? Click;

        internal void PerformClick()
        {
            if (!Enabled) return;
            OnClick();
            Click?.Invoke(this, EventArgs.Empty);
            ClickAction?.Invoke(this);
        }

        /// <summary>Item-type-specific click behaviour (toggle flips, dropdown opens...).</summary>
        protected virtual void OnClick() { }

        /// <summary>Draw this item into its Bounds. state carries hot/pressed/focused.</summary>
        public virtual void Draw(Graphics g, StripItemState state, StripStyle style)
        {
            StripPaint.DrawButtonFace(g, Bounds, state, style);
            StripPaint.DrawCenteredText(g, Text, Bounds, style, Enabled);
            if ((state & StripItemState.Focused) != 0)
                StripPaint.DrawFocusRect(g, Bounds);
        }

        /// <summary>Repaint just this item.</summary>
        protected void Invalidate() => Owner?.InvalidateItem(this);

        /// <summary>Something affecting geometry changed — schedule a relayout.</summary>
        protected void InvalidateLayout() => Owner?.InvalidateStripLayout();
    }

    [Flags]
    public enum StripItemState
    {
        None = 0,
        Hot = 1,
        Pressed = 2,
        Focused = 4,
    }

    /// <summary>Shared style values so items match; extend to taste (fonts, radii, colors).</summary>
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public sealed class StripStyle
    {
        private readonly AdaptiveRowStrip _owner;
        private Font? _font;
        private Color _foreColor = Color.FromArgb(40, 40, 40);
        private Color _disabledForeColor = Color.FromArgb(160, 160, 160);
        private Color _hotBackColor = Color.FromArgb(229, 241, 251);
        private Color _pressedBackColor = Color.FromArgb(204, 228, 247);
        private Color _borderColor = Color.FromArgb(120, 160, 200);
        private int _cornerRadius = 4;

        internal StripStyle(AdaptiveRowStrip owner) => _owner = owner;

        [DefaultValue(null)]
        public Font Font { get => _font ?? _owner.Font; set { if (ReferenceEquals(_font, value)) return; _font = value; Changed(); } }
        public Color ForeColor { get => _foreColor; set { if (_foreColor == value) return; _foreColor = value; Changed(); } }
        public Color DisabledForeColor { get => _disabledForeColor; set { if (_disabledForeColor == value) return; _disabledForeColor = value; Changed(); } }
        public Color HotBackColor { get => _hotBackColor; set { if (_hotBackColor == value) return; _hotBackColor = value; Changed(); } }
        public Color PressedBackColor { get => _pressedBackColor; set { if (_pressedBackColor == value) return; _pressedBackColor = value; Changed(); } }
        public Color BorderColor { get => _borderColor; set { if (_borderColor == value) return; _borderColor = value; Changed(); } }
        [DefaultValue(4)]
        public int CornerRadius { get => _cornerRadius; set { int next = Math.Max(0, value); if (_cornerRadius == next) return; _cornerRadius = next; Changed(); } }

        internal int Scale(int logicalPixels) => _owner.LogicalToDeviceUnitsCached(logicalPixels);
        private void Changed() => _owner.Invalidate();
        public override string ToString() => "Adaptive strip style";
    }

    // ---------------------------------------------------------------- items

    /// <summary>Plain text button.</summary>
    public class StripButton : StripItem
    {
        public StripButton() { }
        public StripButton(string text) { Text = text; }

        public override void Draw(Graphics g, StripItemState state, StripStyle style)
        {
            StripPaint.DrawButtonFace(g, Bounds, state, style);
            StripPaint.DrawCenteredText(g, Text, Bounds, style, Enabled);
            if ((state & StripItemState.Focused) != 0)
                StripPaint.DrawFocusRect(g, Bounds);
        }
    }

    /// <summary>Icon with caption underneath — replaces the icon-control + label pairs.</summary>
    public class StripIconButton : StripItem
    {
        private Image? _icon;

        [Category("Appearance"), DefaultValue(null)]
        public Image? Icon
        {
            get => _icon;
            set { _icon = value; RefreshScaledIcon(); Invalidate(); }
        }

        /// <summary>Pre-scaled per-DPI cache — scaled once, not per paint.</summary>
        [Browsable(false)]
        internal Image? ScaledIcon { get; private set; }

        internal void RefreshScaledIcon()
        {
            ScaledIcon?.Dispose();
            ScaledIcon = null;
            if (_icon == null || Owner == null) return;
            int size = Owner.LogicalToDeviceUnitsCached(24);
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(_icon, 0, 0, size, size);
            ScaledIcon = bmp;
        }

        public override void Draw(Graphics g, StripItemState state, StripStyle style)
        {
            StripPaint.DrawButtonFace(g, Bounds, state, style);

            int inset = style.Scale(2);
            int captionHeight = style.Scale(16);
            var iconArea = new Rectangle(Bounds.X, Bounds.Y + inset, Bounds.Width, Math.Max(1, Bounds.Height - captionHeight - inset));
            if (ScaledIcon != null)
            {
                int x = iconArea.X + (iconArea.Width - ScaledIcon.Width) / 2;
                int y = iconArea.Y + (iconArea.Height - ScaledIcon.Height) / 2;
                if (Enabled)
                    g.DrawImageUnscaled(ScaledIcon, x, y);
                else
                    System.Windows.Forms.ControlPaint.DrawImageDisabled(g, ScaledIcon, x, y, Color.Transparent);
            }

            var captionArea = new Rectangle(Bounds.X, Bounds.Bottom - captionHeight, Bounds.Width, captionHeight);
            StripPaint.DrawCenteredText(g, Text, captionArea, style, Enabled);

            if ((state & StripItemState.Focused) != 0)
                StripPaint.DrawFocusRect(g, Bounds);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ScaledIcon?.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Non-interactive text, e.g. "&lt;--Markers--&gt;". Pure paint, no HWND, no layout events.</summary>
    public class StripCaption : StripItem
    {
        public StripCaption() { }
        public StripCaption(string text) { Text = text; }

        public override bool Interactive => false;

        public override void Draw(Graphics g, StripItemState state, StripStyle style)
            => StripPaint.DrawCenteredText(g, Text, Bounds, style, enabled: true);
    }

    /// <summary>Decorative dotted-arrow spacer; typically IsSpring = true so it stretches.</summary>
    public class StripSpacer : StripItem
    {
        public StripSpacer() { IsSpring = true; Width = 40; MinWidth = 8; }

        public override bool Interactive => false;

        public override void Draw(Graphics g, StripItemState state, StripStyle style)
            => StripPaint.DrawDottedArrow(g, Bounds);
    }

    /// <summary>Two-state toggle drawn as a pill switch.</summary>
    public class StripToggle : StripItem
    {
        private bool _checked;

        [Category("Behavior"), DefaultValue(false)]
        public bool Checked
        {
            get => _checked;
            set { if (_checked != value) { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
        }

        [Category("Action")]
        public event EventHandler? CheckedChanged;

        protected override void OnClick() => Checked = !Checked;

        public override void Draw(Graphics g, StripItemState state, StripStyle style)
        {
            StripPaint.DrawToggle(g, Bounds, Checked, Text, state, style, Enabled);
            if ((state & StripItemState.Focused) != 0)
                StripPaint.DrawFocusRect(g, Bounds);
        }
    }

    /// <summary>
    /// Closed state of a dropdown. Opening delegates to OpenDropDown — wire it to your
    /// existing custom popup: strip draws the shell, your popup does what it already does.
    /// </summary>
    public class StripDropDown : StripItem
    {
        private string _selectedText = string.Empty;

        [Category("Appearance"), DefaultValue("")]
        public string SelectedText
        {
            get => _selectedText;
            set { if (_selectedText != value) { _selectedText = value ?? string.Empty; Invalidate(); } }
        }

        /// <summary>Called with the item's screen anchor when clicked / F4 / Alt+Down. Show your popup here.</summary>
        [Browsable(false)]
        public Action<StripDropDown, Point>? OpenDropDown { get; set; }

        protected override void OnClick()
        {
            if (Owner == null) return;
            Point anchor = Owner.PointToScreen(new Point(Bounds.Left, Bounds.Bottom));
            OpenDropDown?.Invoke(this, anchor);
        }

        public override void Draw(Graphics g, StripItemState state, StripStyle style)
        {
            StripPaint.DrawDropDownShell(g, Bounds, SelectedText, state, style, Enabled);
            if ((state & StripItemState.Focused) != 0)
                StripPaint.DrawFocusRect(g, Bounds);
        }
    }

    // ---------------------------------------------------------------- collection

    public sealed class StripItemCollection : Collection<StripItem>
    {
        private readonly AdaptiveRowStrip _owner;
        internal StripItemCollection(AdaptiveRowStrip owner) => _owner = owner;

        protected override void InsertItem(int index, StripItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Owner != null)
                throw new InvalidOperationException("A strip item cannot belong to more than one AdaptiveRowStrip.");
            base.InsertItem(index, item);
            item.Owner = _owner;
            if (item is StripIconButton ib) ib.RefreshScaledIcon();
            _owner.InvalidateStripLayout();
        }

        protected override void RemoveItem(int index)
        {
            this[index].Bounds = Rectangle.Empty;
            this[index].Owner = null;
            base.RemoveItem(index);
            _owner.InvalidateStripLayout();
        }

        protected override void SetItem(int index, StripItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Owner != null && !ReferenceEquals(item.Owner, _owner))
                throw new InvalidOperationException("A strip item cannot belong to more than one AdaptiveRowStrip.");
            this[index].Bounds = Rectangle.Empty;
            this[index].Owner = null;
            base.SetItem(index, item);
            item.Owner = _owner;
            if (item is StripIconButton ib) ib.RefreshScaledIcon();
            _owner.InvalidateStripLayout();
        }

        protected override void ClearItems()
        {
            foreach (var item in this) { item.Bounds = Rectangle.Empty; item.Owner = null; }
            base.ClearItems();
            _owner.InvalidateStripLayout();
        }

        public void Move(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return;
            var item = this[fromIndex];
            base.RemoveItem(fromIndex);
            base.InsertItem(toIndex, item);
            _owner.InvalidateStripLayout();
        }
    }
}
