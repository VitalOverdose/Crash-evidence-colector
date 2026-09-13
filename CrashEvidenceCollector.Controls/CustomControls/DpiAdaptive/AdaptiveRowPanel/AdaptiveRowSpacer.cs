using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    [ToolboxItem(true)]
    public class AdaptiveRowSpacer : Control, IAdaptiveRowPanelItem
    {
        public AdaptiveRowSpacer()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Margin = new Padding(4, 0, 4, 0);
            MinimumSize = new Size(0, 0);
            Size = new Size(80, 30);
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (!IsDesignerHosted() || Width <= 14 || Height <= 6)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int centerY = Height / 2;
            int left = 7;
            int right = Width - 8;
            int arrowInset = Math.Min(6, Math.Max(3, Height / 4));

            using var linePen = new Pen(Color.FromArgb(70, 130, 180), 1.35f)
            {
                DashStyle = DashStyle.Dot,
                DashCap = DashCap.Round
            };
            using var arrowPen = new Pen(Color.FromArgb(55, 95, 135), 1.35f);
            using var tickPen = new Pen(Color.FromArgb(150, 165, 178), 1f);

            e.Graphics.DrawLine(linePen, left + arrowInset, centerY, right - arrowInset, centerY);

            e.Graphics.DrawLine(arrowPen, left, centerY, left + arrowInset, centerY - arrowInset);
            e.Graphics.DrawLine(arrowPen, left, centerY, left + arrowInset, centerY + arrowInset);
            e.Graphics.DrawLine(arrowPen, right, centerY, right - arrowInset, centerY - arrowInset);
            e.Graphics.DrawLine(arrowPen, right, centerY, right - arrowInset, centerY + arrowInset);

            int tickTop = Math.Max(1, centerY - arrowInset - 1);
            int tickBottom = Math.Min(Height - 2, centerY + arrowInset + 1);
            e.Graphics.DrawLine(tickPen, left + arrowInset + 2, tickTop, left + arrowInset + 2, tickBottom);
            e.Graphics.DrawLine(tickPen, right - arrowInset - 2, tickTop, right - arrowInset - 2, tickBottom);
        }

        private bool IsDesignerHosted()
        {
            // Walk the parent chain — DesignMode is reliable at every level.
            // LicenseManager.UsageMode is process-wide and can report Designtime
            // even at runtime when launched from the VS debugger.
            Control? c = this;
            while (c != null)
            {
                if (c.Site?.DesignMode == true) return true;
                c = c.Parent;
            }
            return false;
        }
    }
}
