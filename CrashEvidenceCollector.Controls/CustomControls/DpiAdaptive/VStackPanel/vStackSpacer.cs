using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    [ToolboxItem(true)]
    public class vStackSpacer : Control
    {
        public vStackSpacer()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Margin = new Padding(0, 2, 0, 2);
            MinimumSize = new Size(0, 0);
            Size = new Size(120, 36);
            TabStop = false;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (!IsDesignerHosted() || Width <= 10 || Height <= 8)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int centerX = Width / 2;
            int top = 0;
            int bottom = Height - 1;
            int arrowInset = 3;

            using var linePen = new Pen(Color.FromArgb(70, 130, 180), 1.35f)
            {
                DashStyle = DashStyle.Dot,
                DashCap = DashCap.Round
            };
            using var arrowPen = new Pen(Color.FromArgb(55, 95, 135), 1.35f);
            using var tickPen = new Pen(Color.FromArgb(150, 165, 178), 1f);

            e.Graphics.DrawLine(linePen, centerX, top + arrowInset, centerX, bottom - arrowInset);

            e.Graphics.DrawLine(arrowPen, centerX, top, centerX - arrowInset, top + arrowInset);
            e.Graphics.DrawLine(arrowPen, centerX, top, centerX + arrowInset, top + arrowInset);
            e.Graphics.DrawLine(arrowPen, centerX, bottom, centerX - arrowInset, bottom - arrowInset);
            e.Graphics.DrawLine(arrowPen, centerX, bottom, centerX + arrowInset, bottom - arrowInset);

            int tickLeft = Math.Max(1, centerX - arrowInset - 1);
            int tickRight = Math.Min(Width - 2, centerX + arrowInset + 1);
            e.Graphics.DrawLine(tickPen, tickLeft, top + arrowInset + 1, tickRight, top + arrowInset + 1);
            e.Graphics.DrawLine(tickPen, tickLeft, bottom - arrowInset - 1, tickRight, bottom - arrowInset - 1);
        }

        private bool IsDesignerHosted()
        {
            Control? c = this;
            while (c != null)
            {
                if (c.Site?.DesignMode == true)
                {
                    return true;
                }

                c = c.Parent;
            }

            return false;
        }
    }
}
