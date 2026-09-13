using System.ComponentModel;
using System.Drawing.Design;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// A help "i" button whose entire job is authored in the designer: drop it in, type the
    /// popup text into <see cref="InfoText"/> (the property grid gives the drop-down rectangle
    /// editor, so the text is laid out as it will display — David's ask), and clicking it at
    /// runtime shows the themed CustomMessageBox. No per-form Click handler, no stray raw
    /// MessageBox to drift off-theme.
    ///
    /// Subclasses <see cref="VirtualIconButton"/> so it stays handle-less inside a v2 row and
    /// behaves as a normal IconButton anywhere else. (The v2 sync keeps subclasses now — the
    /// exact-type destroy check was loosened for exactly this control.)
    /// </summary>
    [Designer(typeof(InfoButtonDesigner))]
    // SHELL DIVERGENCE: default icons nulled (app bakes UiMedia_Info_* from its resx);
    // consumers assign ButtonImageA/Hover in the designer.
    public class InfoButton : VirtualIconButton
    {
        public InfoButton()
        {
            // Sensible identity out of the box; all designer-overridable.
            ImageSize = new Size(24, 24);
            Size = new Size(28, 28);
            try
            {
                ButtonImageA = null;
                ButtonImageAHover = null;
            }
            catch
            {
                // Resource missing at design time is not worth failing construction over.
            }
        }

        [Category("Info")]
        [Description("Title of the popup this button shows.")]
        [DefaultValue("Info")]
        public string InfoTitle { get; set; } = "Info";

        [Category("Info")]
        [Description("The text the popup displays. Edited in the drop-down multiline editor, laid out as it will show.")]
        [DefaultValue("")]
        [Editor("System.ComponentModel.Design.MultilineStringEditor, System.Windows.Forms.Design", typeof(UITypeEditor))]
        public string InfoText { get; set; } = string.Empty;

        private readonly InfoPage _infoPage = new();

        [Category("Info")]
        [Description("Rich page content (may embed images) — authored in the page editor via the ... button. When set, it wins over InfoText.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public InfoPage InfoPage => _infoPage;

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            if (DesignMode) return;

            if (!InfoPage.IsEmpty)
            {
                CustomMessageBox.Show(InfoPage.Rtf, InfoTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.None, FindForm(), infoTheme: true);
            }
            else if (!string.IsNullOrWhiteSpace(InfoText))
            {
                CustomMessageBox.Show(InfoText, InfoTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information, FindForm(), infoTheme: true);
            }
        }
    }
}
