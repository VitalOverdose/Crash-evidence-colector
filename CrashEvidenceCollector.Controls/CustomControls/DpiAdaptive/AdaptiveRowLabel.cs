using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    [Designer(typeof(AdaptiveRowLabelDesigner))]
    public class AdaptiveRowLabel : ModernButton
    {
        private bool _autoInheritSurfaceColor = true;
        private bool _syncingSurfaceColor;

        public AdaptiveRowLabel()
        {
            // A label is not a button: no border in any state, no hover/press colour
            // reactions. Without these, a fresh label (ARP collection editor included)
            // arrives wearing ModernButton's interactive defaults.
            BorderSize = 0;
            BorderRadius = 6;
            HoverColorFx = false;
            HoverBorderSize = 0;
            PressedColorFx = false;
            PressedBorderSize = 0;

            Color surface = Color.White;
            SetSurfaceColor(surface, disableAutoInherit: false);

            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            TextColor = Color.FromArgb(31, 31, 31);
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(8, 0, 8, 0);
            Margin = new Padding(4, 0, 4, 0);
            Size = new Size(80, 28);
            MinimumSize = new Size(24, 18);
            TabStop = false;
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("When true, the label uses the nearest solid parent background color instead of a transparent background.")]
        public bool AutoInheritSurfaceColor
        {
            get => _autoInheritSurfaceColor;
            set
            {
                if (_autoInheritSurfaceColor == value) return;
                _autoInheritSurfaceColor = value;
                if (_autoInheritSurfaceColor)
                    SyncSurfaceColorFromParent();
            }
        }

        [Category("Appearance")]
        [Description("Base background color of the label surface.")]
        public new Color BackgroundColor
        {
            get => base.BackgroundColor;
            set => SetSurfaceColor(value, disableAutoInherit: true);
        }

        [Category("Appearance")]
        [Description("WinForms background color of the label surface.")]
        public override Color BackColor
        {
            get => base.BackColor;
            set => SetSurfaceColor(value, disableAutoInherit: true);
        }

        [Category("Layout")]
        [DefaultValue(28)]
        [Description("Preferred control height for the row label.")]
        public int LabelHeight
        {
            get => Height;
            set => Height = Math.Max(18, value);
        }

        [Category("Appearance")]
        [DefaultValue(ContentAlignment.MiddleLeft)]
        [Description("Alignment of the label text.")]
        public new ContentAlignment TextAlign
        {
            get => base.TextAlign;
            set => base.TextAlign = value;
        }

        [Category("Appearance")]
        [Description("Background color of the label surface.")]
        public Color SurfaceColor
        {
            get => BackgroundColor;
            set => SetSurfaceColor(value, disableAutoInherit: true);
        }

        [Category("Appearance")]
        [DefaultValue(6)]
        [Description("Corner radius of the label surface.")]
        public int CornerRadius
        {
            get => BorderRadius;
            set => BorderRadius = Math.Max(0, value);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            SyncSurfaceColorFromParent();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            SyncSurfaceColorFromParent();
        }

        protected override void OnParentBackColorChanged(EventArgs e)
        {
            base.OnParentBackColorChanged(e);
            SyncSurfaceColorFromParent();
        }

        private void SyncSurfaceColorFromParent()
        {
            if (!_autoInheritSurfaceColor)
                return;

            Color surface = ResolveParentSurfaceColor();
            if (BackgroundColor == surface && HoverColor == surface && PressedColor == surface)
                return;

            SetSurfaceColor(surface, disableAutoInherit: false);
        }

        private void SetSurfaceColor(Color color, bool disableAutoInherit)
        {
            if (_syncingSurfaceColor)
            {
                base.BackColor = color;
                return;
            }

            if (disableAutoInherit)
                _autoInheritSurfaceColor = false;

            _syncingSurfaceColor = true;
            try
            {
                base.BackgroundColor = color;
                base.HoverColor = color;
                base.PressedColor = color;
            }
            finally
            {
                _syncingSurfaceColor = false;
            }

            Invalidate();
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);

            if (_syncingSurfaceColor)
                return;

            SetSurfaceColor(base.BackColor, disableAutoInherit: true);
        }

        private Color ResolveParentSurfaceColor()
        {
            Control child = this;
            Control? parent = Parent;

            while (parent != null)
            {
                if (parent is RoundedPanelFaster faster)
                    return faster.InnerColor;

                if (parent is RoundedForm form)
                {
                    if (form.TopBarVisible && form.TopBarHeight > 0 && child.Top < form.TopBarHeight)
                        return form.TopBarColor;

                    if (form.BottomBarVisible && form.BottomBarHeight > 0 &&
                        child.Bottom > form.Height - form.BottomBarHeight)
                        return form.BottomBarColor;

                    return form.FormBackColor;
                }

                if (parent.BackColor != Color.Transparent)
                    return parent.BackColor;

                child = parent;
                parent = parent.Parent;
            }

            return Color.White;
        }
    }
}
