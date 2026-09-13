using System.ComponentModel;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// A <see cref="RoundedComboBox"/> that by default does NOT open its own item list —
    /// it fires <see cref="RoundedComboBox.DropDownButtonClicked"/> instead, so the host
    /// can show whatever custom popup it likes (home-pages menu, etc.).
    ///
    /// Looks like a combo (rounded body + dropdown arrow), behaves like a split/dropdown
    /// button. Reuses ALL of RoundedComboBox's rendering and layout — this is just the
    /// combo with <see cref="RoundedComboBox.DropDownEnabled"/> defaulted off, exposed as
    /// its own control for the set.
    /// </summary>
    internal class RoundedDropdownButton : RoundedComboBox
    {
        public RoundedDropdownButton()
        {
            SplitButtonMode = true;     // left button area + arrow zone, with a divider
            DropDownEnabled = false;    // mode 2: arrow raises DropDownButtonClicked for a custom popup
        }

        // These two exist for ONE reason: RoundedComboBox declares [DefaultValue(true)] on
        // DropDownEnabled and [DefaultValue(false)] on SplitButtonMode, and this class turns both
        // around in its constructor. The designer decides whether to write a property out by
        // comparing it with the DefaultValue attribute, so an edit BACK to the base's default was
        // judged redundant and never written - and the constructor then reimposed this class's
        // opposite value on reload. The property appeared to refuse to stay set.
        //
        // Re-declaring them here puts the attribute on the class that actually owns the default.
        // Both forward to base rather than carrying storage of their own, so there is exactly one
        // value: code inside RoundedComboBox and code holding a RoundedDropdownButton cannot end
        // up reading different answers.

        [DefaultValue(false)]
        [Category("Behavior")]
        public new bool DropDownEnabled
        {
            get => base.DropDownEnabled;
            set => base.DropDownEnabled = value;
        }

        [DefaultValue(true)]
        [Category("Behavior")]
        public new bool SplitButtonMode
        {
            get => base.SplitButtonMode;
            set => base.SplitButtonMode = value;
        }
    }
}
