using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    public sealed class VStackRow
    {
        private readonly VStackEngine _engine;
        private readonly string _controlName;

        internal VStackRow(VStackEngine engine, string controlName)
        {
            _engine = engine;
            _controlName = controlName ?? string.Empty;
        }

        public string ControlName => _controlName;

        public bool Exists => _engine.RowExists(_controlName);

        public Control? Control => _engine.GetRowControl(_controlName);

        public bool IsCollapsed
        {
            get => _engine.GetRowIsCollapsed(_controlName);
            set => _engine.SetRowCollapsed(_controlName, value);
        }

        public bool IsSpring
        {
            get => _engine.GetRowIsSpring(_controlName);
            set => _engine.SetRowSpring(_controlName, value);
        }

        // Setting this true clears IsSpring and vice versa — SpecialLayout is one tri-state.
        public bool IsPiston
        {
            get => _engine.GetRowIsPiston(_controlName);
            set => _engine.SetRowPiston(_controlName, value);
        }

        public int ExpandedHeight
        {
            get => _engine.GetRowExpandedHeight(_controlName);
            set => _engine.SetRowExpandedHeight(_controlName, value);
        }

        public int CollapsedHeight
        {
            get => _engine.GetRowCollapsedHeight(_controlName);
            set => _engine.SetRowCollapsedHeight(_controlName, value);
        }

        public bool Expand() => _engine.SetRowCollapsed(_controlName, false);

        public bool Collapse() => _engine.SetRowCollapsed(_controlName, true);

        public bool Toggle() => _engine.ToggleRowCollapsed(_controlName);
    }
}
