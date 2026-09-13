using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public class AdaptiveLineBreak : Control, IAdaptiveRowPanelItem
    {
        private Color _lineColor = Color.FromArgb(200, 200, 200);
        private int _lineThickness = 1;
        private int _verticalPadding = 4;

        public AdaptiveLineBreak()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
            Width = 10;
        }

        [DefaultValue(typeof(Color), "200, 200, 200"), Category("Appearance")]
        public Color LineColor
        {
            get => _lineColor;
            set { _lineColor = value; Invalidate(); }
        }

        [DefaultValue(1), Category("Appearance")]
        public int LineThickness
        {
            get => _lineThickness;
            set { _lineThickness = value < 1 ? 1 : value; Invalidate(); }
        }

        [DefaultValue(4), Category("Appearance")]
        public int VerticalPadding
        {
            get => _verticalPadding;
            set { _verticalPadding = value < 0 ? 0 : value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int x = Width / 2;
            using var pen = new Pen(_lineColor, _lineThickness);
            e.Graphics.DrawLine(pen, x, _verticalPadding, x, Height - _verticalPadding);
        }
    }
}
