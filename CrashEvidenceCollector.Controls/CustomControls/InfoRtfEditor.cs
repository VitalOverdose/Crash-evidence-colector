using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Drawing.Imaging;
using System.Windows.Forms.Design;

namespace ProfessorSnowsVideoDownloader.CustomControls
{
    /// <summary>
    /// The property-grid editor behind <see cref="InfoButton.InfoRtf"/>: the "…" button opens
    /// a small rich-text page editor — bold/italic/underline, bullets, size, insert image —
    /// so an info popup can be AUTHORED as a laid-out page (David: "an actual page editor
    /// where i could insert image if i wanted to").
    ///
    /// Shown through IWindowsFormsEditorService, the one channel VS hosts properly — the same
    /// mechanism as the collection editors, and the lesson of the three designer crashes: no
    /// freelance modals from designer code. The editor form is deliberately NATIVE-styled:
    /// design-time tooling matches VS, not the app (David's own taxonomy).
    /// </summary>
    /// <summary>
    /// The value type for <see cref="InfoButton.InfoPage"/>. Exists because the OOP designer
    /// DROPS property-level [Editor] attributes on plain strings crossing the VS boundary —
    /// but honours an editor bound to a custom TYPE (proven by AdaptivePanelItemCollection).
    /// Serializes to Designer.cs through the InstanceDescriptor converter below.
    /// </summary>
    [Editor(typeof(InfoRtfEditor), typeof(UITypeEditor))]
    [TypeConverter(typeof(InfoPageConverter))]
    public sealed class InfoPage
    {

        [Browsable(false)]
        [DefaultValue("")]
        public string Rtf { get; set; } = string.Empty;

        [Browsable(false)]
        public bool IsEmpty => string.IsNullOrWhiteSpace(Rtf);

        public override string ToString() => IsEmpty ? "(empty — click … to write)" : "(page — click … to edit)";
    }

    // Mirrors AdaptivePanelItemCollectionConverter EXACTLY — the one custom-type converter the
    // OOP designer provably round-trips (string display, no property expansion).
    internal sealed class InfoPageConverter : TypeConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
            => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

        public override object? ConvertTo(ITypeDescriptorContext? context,
            System.Globalization.CultureInfo? culture, object? value, Type destinationType)
            => destinationType == typeof(string) && value is InfoPage page
                ? page.ToString()
                : base.ConvertTo(context, culture, value, destinationType);

        public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => false;
    }

    /// <summary>
    /// Standalone host for the page editor: the app exe run with "--edit-rtf &lt;file&gt;"
    /// loads the file, shows the editor with a REAL message loop, saves on OK. Exists because
    /// the OOP designer can host no custom modal at all (DesignToolsServer has no interactive
    /// message loop; confirmed after four attempts and three hangs) - the smart tag launches
    /// this as a separate process instead, the sanctioned pattern.
    /// </summary>
    public static class InfoRtfEditorHost
    {
        public static void RunFileEditor(string path)
        {
            string rtf = string.Empty;
            try { if (File.Exists(path)) rtf = File.ReadAllText(path); } catch { }

            using var editor = new frmInfoRtfEditor(rtf);
            if (editor.ShowDialog() == DialogResult.OK)
            {
                try { File.WriteAllText(path, editor.ResultRtf); }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Could not save: " + ex.Message,
                        "Info Page Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    // NOTE: this UITypeEditor never fires in the OOP designer (custom editors need a separate
    // VS-side package). Kept for any future in-process host; the working door is the smart tag
    // below, which edits via a separate process.
    public class InfoRtfEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
            => UITypeEditorEditStyle.Modal;

        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
        {
            var editorService = (IWindowsFormsEditorService?)provider?.GetService(typeof(IWindowsFormsEditorService));
            if (editorService == null)
                return value;

            // Mutate in place, like the collection editors — the property is read-only and
            // serializes as Content (btnInfo.InfoPage.Rtf = "...").
            using var editor = new frmInfoRtfEditor((value as InfoPage)?.Rtf);
            if (editorService.ShowDialog(editor) == DialogResult.OK && value is InfoPage page)
            {
                page.Rtf = editor.ResultRtf;
            }
            return value;
        }
    }

    /// <summary>
    /// The page editor itself. Code-created native UI on purpose — same exception class as
    /// the ARP/v2 collection editor forms (design-time only, never seen by app users).
    /// </summary>
    internal sealed class frmInfoRtfEditor : Form
    {
        private readonly RichTextBox _rtb;

        // Wider than this and an inserted image would overflow the CustomMessageBox body.
        private const int MaxImageWidth = 440;

        public string ResultRtf { get; private set; } = string.Empty;

        public frmInfoRtfEditor(string? rtf)
        {
            Text = "Info Page Editor";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            Size = new Size(560, 520);
            MinimumSize = new Size(420, 320);

            // Launched as a separate process by the designer smart tag, this form has no owner
            // and Windows happily opens it BEHIND Visual Studio — David got an invisible
            // instance that held the build-output lock until killed. Force front once.
            Shown += (_, _) =>
            {
                TopMost = true;
                Activate();
                TopMost = false;
            };

            var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
            toolbar.Items.Add(MakeButton("B", "Bold", (_, _) => ToggleStyle(FontStyle.Bold), bold: true));
            toolbar.Items.Add(MakeButton("I", "Italic", (_, _) => ToggleStyle(FontStyle.Italic), italic: true));
            toolbar.Items.Add(MakeButton("U", "Underline", (_, _) => ToggleStyle(FontStyle.Underline), underline: true));
            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(MakeButton("• List", "Bulleted list", (_, _) =>
            {
                _rtb!.SelectionBullet = !_rtb.SelectionBullet;
                _rtb.Focus();
            }));
            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(MakeButton("A+", "Bigger text", (_, _) => NudgeSize(+2)));
            toolbar.Items.Add(MakeButton("A-", "Smaller text", (_, _) => NudgeSize(-2)));
            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(MakeButton("Image…", "Insert an image at the caret", (_, _) => InsertImage()));

            _rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.None,
                AcceptsTab = true,
            };
            if (!string.IsNullOrWhiteSpace(rtf))
            {
                try { _rtb.Rtf = rtf; }
                catch { _rtb.Text = rtf; }   // a plain string that isn't RTF yet
            }

            var buttonRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 42,
                Padding = new Padding(6),
            };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 84 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 84 };
            ok.Click += (_, _) => ResultRtf = _rtb.TextLength == 0 ? string.Empty : _rtb.Rtf ?? string.Empty;
            buttonRow.Controls.Add(cancel);
            buttonRow.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(_rtb);
            Controls.Add(buttonRow);
            Controls.Add(toolbar);
        }

        private static ToolStripButton MakeButton(string text, string tip, EventHandler onClick,
            bool bold = false, bool italic = false, bool underline = false)
        {
            FontStyle style = (bold ? FontStyle.Bold : 0) | (italic ? FontStyle.Italic : 0)
                | (underline ? FontStyle.Underline : 0);
            var button = new ToolStripButton(text) { ToolTipText = tip, AutoSize = true };
            if (style != 0)
                button.Font = new Font("Segoe UI", 9f, style);
            button.Click += onClick;
            return button;
        }

        private void ToggleStyle(FontStyle style)
        {
            Font current = _rtb.SelectionFont ?? _rtb.Font;
            _rtb.SelectionFont = new Font(current, current.Style ^ style);
            _rtb.Focus();
        }

        private void NudgeSize(int delta)
        {
            // SelectionFont is NULL when the selection mixes styles, and a single write from
            // the base font FLATTENS every run (David lost his bold+underline to A+). Walk the
            // selection one character at a time, resizing each run with ITS OWN style kept.
            int start = _rtb.SelectionStart;
            int length = _rtb.SelectionLength;

            if (length == 0)
            {
                Font caret = _rtb.SelectionFont ?? _rtb.Font;
                _rtb.SelectionFont = new Font(caret.FontFamily,
                    Math.Clamp(caret.SizeInPoints + delta, 7f, 28f), caret.Style);
                _rtb.Focus();
                return;
            }

            for (int i = 0; i < length; i++)
            {
                _rtb.Select(start + i, 1);
                Font runFont = _rtb.SelectionFont ?? _rtb.Font;
                _rtb.SelectionFont = new Font(runFont.FontFamily,
                    Math.Clamp(runFont.SizeInPoints + delta, 7f, 28f), runFont.Style);
            }

            _rtb.Select(start, length);
            _rtb.Focus();
        }

        private void InsertImage()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Insert image",
                Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                using var raw = Image.FromFile(dialog.FileName);
                using Image image = ShrinkToFit(raw);

                // Hand-built \pict — no clipboard round-trip, so the user's clipboard is never
                // stomped by a designer tool.
                _rtb.SelectedRtf = @"{\rtf1" + ImageToRtfPict(image) + @"\par}";
                _rtb.Focus();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(this, $"Could not insert image: {ex.Message}", "Insert image",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static Image ShrinkToFit(Image source)
        {
            if (source.Width <= MaxImageWidth)
                return new Bitmap(source);

            int height = (int)Math.Round(source.Height * (MaxImageWidth / (double)source.Width));
            return new Bitmap(source, MaxImageWidth, Math.Max(1, height));
        }

        private static string ImageToRtfPict(Image image)
        {
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            string hex = Convert.ToHexString(stream.ToArray());

            // himetric (0.01mm) for picw/pich, twips for the goal size — both at 96dpi.
            int widthHimetric = (int)Math.Round(image.Width * 26.4583);
            int heightHimetric = (int)Math.Round(image.Height * 26.4583);
            int widthTwips = image.Width * 15;
            int heightTwips = image.Height * 15;

            return @"{\pict\pngblip\picw" + widthHimetric + @"\pich" + heightHimetric
                + @"\picwgoal" + widthTwips + @"\pichgoal" + heightTwips + " " + hex + "}";
        }
    }


    /// <summary>
    /// The external-process edit, shared by every design-time "Edit info page" action
    /// (InfoButton, UcInfoPanel...): temp file out, app exe with --edit-rtf (real
    /// message loop, VS stays live), await exit, hand the result back. Returns null when
    /// cancelled/unchanged/failed - the caller only pushes on a real edit.
    /// </summary>
    internal static class InfoPageExternalEdit
    {
        public static async Task<string?> RunAsync(Control component, string currentRtf, IUIService? ui)
        {
            try
            {
                string? exePath = FindAppExe(component, out string probed);
                if (exePath == null)
                {
                    ui?.ShowMessage("App exe not found. Probed: " + probed, "Edit info page");
                    return null;
                }

                string tempFile = Path.Combine(Path.GetTempPath(), "infopage_" + Guid.NewGuid().ToString("N") + ".rtf");
                File.WriteAllText(tempFile, currentRtf ?? string.Empty);

                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    ArgumentList = { "--edit-rtf", tempFile },
                    UseShellExecute = false,
                });
                if (process == null) return null;

                await process.WaitForExitAsync();

                string edited = File.Exists(tempFile) ? File.ReadAllText(tempFile) : string.Empty;
                try { File.Delete(tempFile); } catch { }

                return string.Equals(edited, currentRtf, StringComparison.Ordinal) ? null : edited;
            }
            catch (Exception ex)
            {
                ui?.ShowMessage("Edit failed: " + ex.Message, "Edit info page");
                return null;
            }
        }

        /// <summary>
        /// The exe is hunted, not assumed: every environmental source resolves inside the
        /// designer's shadow-copy cache, so the build stamps the real output path into the
        /// assembly (csproj AssemblyMetadata "BuildOutputExe") and that breadcrumb wins.
        /// </summary>
        internal static string? FindAppExe(Control component, out string probed)
        {
            string assemblyFile = typeof(InfoPageExternalEdit).Assembly.GetName().Name + ".exe";
            var candidates = new List<string>();

            try
            {
                foreach (object attr in typeof(InfoPageExternalEdit).Assembly
                    .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false))
                {
                    if (attr is System.Reflection.AssemblyMetadataAttribute meta
                        && meta.Key == "BuildOutputExe" && !string.IsNullOrEmpty(meta.Value))
                    {
                        candidates.Add(meta.Value);
                    }
                }
            }
            catch { }

            try
            {
                var resolver = (System.ComponentModel.Design.ITypeResolutionService?)
                    component.Site?.GetService(typeof(System.ComponentModel.Design.ITypeResolutionService));
                string? viaResolver = resolver?.GetPathOfAssembly(typeof(InfoPageExternalEdit).Assembly.GetName());
                if (!string.IsNullOrEmpty(viaResolver))
                    candidates.Add(Path.ChangeExtension(viaResolver, ".exe"));
            }
            catch { }

            try
            {
                string location = typeof(InfoPageExternalEdit).Assembly.Location;
                if (!string.IsNullOrEmpty(location))
                    candidates.Add(Path.ChangeExtension(location, ".exe"));
            }
            catch { }

            try
            {
                foreach (string arg in Environment.GetCommandLineArgs())
                {
                    string trimmed = arg.Trim(chr34());
                    if (trimmed.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(trimmed))
                        candidates.Add(Path.ChangeExtension(trimmed, ".exe"));
                    else if (Directory.Exists(trimmed))
                        candidates.Add(Path.Combine(trimmed, assemblyFile));
                }
            }
            catch { }

            try { candidates.Add(Path.Combine(Environment.CurrentDirectory, assemblyFile)); } catch { }

            foreach (string candidate in candidates)
            {
                try
                {
                    if (Path.GetFileName(candidate).Equals(assemblyFile, StringComparison.OrdinalIgnoreCase)
                        && File.Exists(candidate))
                    {
                        probed = candidate;
                        return candidate;
                    }
                }
                catch { }
            }

            probed = candidates.Count == 0 ? "(no candidates)" : string.Join(" | ", candidates.Distinct());
            return null;
        }

        private static char chr34() => (char)34;
    }

    /// <summary>
    /// Smart tag: "Edit info page..." - writes the current RTF to a temp file, launches the
    /// app exe with --edit-rtf (a separate process with a real message loop; VS stays live),
    /// and on exit pushes the result through the InfoPage property descriptor on the designer
    /// thread so Content serialization and undo both record it.
    /// </summary>
    public class InfoButtonDesigner : Microsoft.DotNet.DesignTools.Designers.ControlDesigner
    {
        private Microsoft.DotNet.DesignTools.Designers.Actions.DesignerActionListCollection? _actionLists;

        public override Microsoft.DotNet.DesignTools.Designers.Actions.DesignerActionListCollection ActionLists
        {
            get
            {
                if (_actionLists == null)
                {
                    _actionLists = new Microsoft.DotNet.DesignTools.Designers.Actions.DesignerActionListCollection
                    {
                        new InfoButtonActionList(Component),
                    };
                    _actionLists.AddRange(base.ActionLists);
                }
                return _actionLists;
            }
        }
    }

    public class InfoButtonActionList : Microsoft.DotNet.DesignTools.Designers.Actions.DesignerActionList
    {
        public InfoButtonActionList(IComponent component) : base(component) { }

        private InfoButton? Button => Component as InfoButton;

        public async void EditInfoPage()
        {
            if (Button is not { } button) return;

            var ui = (IUIService?)button.Site?.GetService(typeof(IUIService));
            string? edited = await InfoPageExternalEdit.RunAsync(button, button.InfoPage.Rtf, ui);
            if (edited == null) return;

            // Back on the designer thread before touching the component.
            if (button.IsHandleCreated)
                button.BeginInvoke(() => PushEdit(button, edited));
            else
                PushEdit(button, edited);
        }

        private void PushEdit(InfoButton button, string rtf)
        {
            var changeService = (IComponentChangeService?)button.Site?.GetService(typeof(IComponentChangeService));
            PropertyDescriptor? property = TypeDescriptor.GetProperties(button)["InfoPage"];
            changeService?.OnComponentChanging(button, property);
            button.InfoPage.Rtf = rtf;
            changeService?.OnComponentChanged(button, property, null, button.InfoPage);
        }

        public override Microsoft.DotNet.DesignTools.Designers.Actions.DesignerActionItemCollection GetSortedActionItems()
        {
            return new Microsoft.DotNet.DesignTools.Designers.Actions.DesignerActionItemCollection
            {
                new Microsoft.DotNet.DesignTools.Designers.Actions.DesignerActionMethodItem(
                    this, nameof(EditInfoPage), "Edit info page... (opens in its own window)", "Info", true),
            };
        }
    }
}
