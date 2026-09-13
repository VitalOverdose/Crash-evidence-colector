#nullable enable
using ProfessorSnowsVideoDownloader.CustomControls;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ProfessorSnowsVideoDownloader
{
    /// ============ how to use
    /// 
    /// DialogResult result = CustomMessageBox.Show(
    /// $"Are you sure you want to delete this {itemType}?\n\n{itemName}\n\n" +
    /// (isDirectory ? "This will permanently delete the folder and all its contents." : "This action cannot be undone."),
    /// $"Delete {itemType}?",
    /// MessageBoxButtons.YesNo,
    /// MessageBoxIcon.Warning);
    ///
    /// ⭐ NEW - With checkbox:
    /// var result = CustomMessageBox.ShowWithCheckbox(
    ///     "Delete folder 'MyFolder'?",
    ///     "Delete Folder",
    ///     "Delete bookmarks inside folder too",
    ///     MessageBoxButtons.YesNo,
    ///     MessageBoxIcon.Question
    /// );
    /// if (result.DialogResult == DialogResult.Yes)
    /// {
    ///     bool deleteContents = result.CheckboxChecked;
    /// }

    public partial class CustomMessageBox : RoundedForm
    {
        private enum MessageBoxButtonRole
        {
            Primary,
            Secondary,
            Cancel,
            Destructive,
            Neutral
        }

        private DialogResult _result = DialogResult.Cancel;
        private bool _isLayingOutMessageBox;

        private const int MessageBodyMinHeight = 96;
        private const int MessageBodyMaxHeight = 360;
        private const int MessageEdgePadding = 12;
        private const int MessageVerticalPadding = 8;
        private const int MessageContentPadding = 18;
        private const int MessageBaseClientWidth = 560;

        public CustomMessageBox()
        {
            InitializeComponent();

            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScaleMode = AutoScaleMode.None;
            this.MinimumSize = new Size(420, 180);

            // Wire up close button
            btnClose.Click += (s, e) => { _result = DialogResult.Cancel; Close(); };
            LayoutMessageBoxControls();
        }

        // ⭐ NEW - Result class for checkbox variant
        public class CustomMessageBoxResult
        {
            public DialogResult DialogResult { get; set; }
            public bool CheckboxChecked { get; set; }
        }

        // Original Show method (unchanged)
        // ⭐ NEW - Show with parent form (prevents focus stealing)
        // ⭐ NEW - Show with optional parent form (using named parameter to avoid ambiguity)
        public static DialogResult Show(string message, string title = "Message",
                                        MessageBoxButtons buttons = MessageBoxButtons.OK,
                                        MessageBoxIcon icon = MessageBoxIcon.Information,
                                        IWin32Window? owner = null,  // ⭐ Add optional owner parameter at the END
                                        bool infoTheme = false)
        {
            using (var msgBox = new CustomMessageBox())
            {
                // INFO surfaces wear lavender (David's colour vocabulary: lavender =
                // information, not controls — matches ucFileBrowserInfo).
                if (infoTheme)
                {
                    msgBox.TopBarColor = Color.FromArgb(220, 220, 255);
                    msgBox.BottomBarColor = Color.FromArgb(220, 220, 255);
                    msgBox.FormBackColor = Color.FromArgb(240, 240, 255);
                    msgBox.rtbMessage.BackColor = Color.FromArgb(240, 240, 255);
                }

                msgBox.TopText = title;
                msgBox.rtbMessage.Font = new Font("Segoe UI", 14f);

                // RTF payload (InfoButton page content, may embed images): render as rich
                // text, no icon prefix — the page IS the layout.
                if (message.StartsWith(@"{\rtf", StringComparison.Ordinal))
                {
                    msgBox._isRtfMessage = true;
                    try { msgBox.rtbMessage.Rtf = message; }
                    catch { msgBox.rtbMessage.Text = message; msgBox._isRtfMessage = false; }
                }
                else
                {
                    msgBox.rtbMessage.Text = message;

                    // Add icon to message if needed
                    string iconSymbol = icon switch
                    {
                        MessageBoxIcon.Information => "ℹ️ ",
                        MessageBoxIcon.Warning => "⚠️ ",
                        MessageBoxIcon.Error => "❌ ",
                        MessageBoxIcon.Question => "❓ ",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(iconSymbol))
                    {
                        msgBox.rtbMessage.Text = iconSymbol + message;
                    }
                }

                msgBox.AddButtons(buttons);
                msgBox.AutoSizeToFitText();

                // ⭐ Use owner if provided, otherwise show without owner
                if (owner != null)
                    msgBox.ShowDialog(owner);
                else
                    msgBox.ShowDialog();

                return msgBox._result;
            }
        }

        // ⭐ NEW - Show with checkbox
        public static CustomMessageBoxResult ShowWithCheckbox(
            string message,
            string title = "Message",
            string checkboxText = "Option",
            bool checkboxDefaultChecked = false,
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            using (var msgBox = new CustomMessageBox())
            {
                msgBox.TopText = title;
                msgBox.rtbMessage.Font = new Font("Segoe UI", 14f);
                msgBox.rtbMessage.Text = message;

                // Add icon to message if needed
                string iconSymbol = icon switch
                {
                    MessageBoxIcon.Information => "ℹ️ ",
                    MessageBoxIcon.Warning => "⚠️ ",
                    MessageBoxIcon.Error => "❌ ",
                    MessageBoxIcon.Question => "❓ ",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(iconSymbol))
                {
                    msgBox.rtbMessage.Text = iconSymbol + message;
                }

                // ⭐ Show and configure checkbox
                msgBox.chkOption.Visible = true;
                msgBox.chkOption.Text = checkboxText;
                msgBox.chkOption.Checked = checkboxDefaultChecked;
                msgBox.chkOption.Width = Math.Min(260, Math.Max(160, msgBox.ClientSize.Width - 190));

                msgBox.AddButtons(buttons);
                msgBox.AutoSizeToFitText(hasCheckbox: true);  // ⭐ Pass flag
                msgBox.ShowDialog();

                return new CustomMessageBoxResult
                {
                    DialogResult = msgBox._result,
                    CheckboxChecked = msgBox.chkOption.Checked
                };
            }
        }

        private void AddButtons(MessageBoxButtons buttons)
        {
            foreach (ModernButton button in pnlButtons.Controls.OfType<ModernButton>().ToList())
            {
                pnlButtons.Controls.Remove(button);
                button.Dispose();
            }

            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    AddButton("OK", DialogResult.OK, MessageBoxButtonRole.Primary);
                    break;

                case MessageBoxButtons.OKCancel:
                    AddButton("Cancel", DialogResult.Cancel, MessageBoxButtonRole.Cancel);
                    AddButton("OK", DialogResult.OK, MessageBoxButtonRole.Primary);
                    break;

                case MessageBoxButtons.YesNo:
                    AddButton("No", DialogResult.No, MessageBoxButtonRole.Secondary);
                    AddButton("Yes", DialogResult.Yes, MessageBoxButtonRole.Primary);
                    break;

                case MessageBoxButtons.YesNoCancel:
                    AddButton("Cancel", DialogResult.Cancel, MessageBoxButtonRole.Cancel);
                    AddButton("No", DialogResult.No, MessageBoxButtonRole.Secondary);
                    AddButton("Yes", DialogResult.Yes, MessageBoxButtonRole.Primary);
                    break;

                case MessageBoxButtons.RetryCancel:
                    AddButton("Cancel", DialogResult.Cancel, MessageBoxButtonRole.Cancel);
                    AddButton("Retry", DialogResult.Retry, MessageBoxButtonRole.Primary);
                    break;

                case MessageBoxButtons.AbortRetryIgnore:
                    AddButton("Ignore", DialogResult.Ignore, MessageBoxButtonRole.Neutral);
                    AddButton("Retry", DialogResult.Retry, MessageBoxButtonRole.Secondary);
                    AddButton("Abort", DialogResult.Abort, MessageBoxButtonRole.Destructive);
                    break;

                case MessageBoxButtons.CancelTryContinue:
                    AddButton("Cancel", DialogResult.Cancel, MessageBoxButtonRole.Cancel);
                    AddButton("Try Again", DialogResult.TryAgain, MessageBoxButtonRole.Secondary);
                    AddButton("Continue", DialogResult.Continue, MessageBoxButtonRole.Primary);
                    break;
            }
        }

        private void AddButton(string text, DialogResult result, MessageBoxButtonRole role)
        {
            var btn = new ModernButton
            {
                Text = text,
                Size = new Size(90, 32),
                Font = new Font(new FontFamily("Segoe UI"), 12f),
                Cursor = Cursors.Hand,
                Margin = new Padding(5, 6, 3, 0),
                TabIndex = 2 + pnlButtons.Controls.OfType<ModernButton>().Count(),
                BorderRadius = 10,
                BorderSize = 1,
                BorderColor = Color.FromArgb(180, 180, 180),
                BackgroundColor = Color.FromArgb(230, 235, 240),
                HoverBorderColor = Color.DeepSkyBlue,
                HoverColor = Color.FromArgb(237, 245, 250),
                TextColor = Color.FromArgb(50, 50, 50)
            };

            ApplyButtonStyle(btn, role);
            btn.Click += (s, e) => { _result = result; Close(); };
            pnlButtons.Controls.Add(btn);
            pnlButtons.ForceRefreshLayout();
        }

        private static void ApplyButtonStyle(ModernButton button, MessageBoxButtonRole role)
        {
            if (role == MessageBoxButtonRole.Destructive)
            {
                button.BackgroundColor = Color.FromArgb(205, 70, 70);
                button.BorderColor = Color.FromArgb(170, 45, 45);
                button.HoverColor = Color.FromArgb(220, 85, 85);
                button.HoverBorderColor = Color.FromArgb(150, 35, 35);
                button.PressedColor = Color.FromArgb(170, 45, 45);
                button.PressedBorderColor = Color.FromArgb(130, 30, 30);
                button.TextColor = Color.White;
                button.ForeColor = Color.White;
                return;
            }

            if (role == MessageBoxButtonRole.Primary)
            {
                button.BackgroundColor = Color.FromArgb(70, 130, 220);
                button.BorderColor = Color.FromArgb(45, 105, 190);
                button.HoverColor = Color.FromArgb(86, 145, 235);
                button.HoverBorderColor = Color.FromArgb(35, 95, 180);
                button.PressedColor = Color.FromArgb(50, 110, 200);
                button.PressedBorderColor = Color.FromArgb(30, 85, 165);
                button.TextColor = Color.White;
                button.ForeColor = Color.White;
                return;
            }

            if (role == MessageBoxButtonRole.Cancel ||
                role == MessageBoxButtonRole.Secondary ||
                role == MessageBoxButtonRole.Neutral)
            {
                button.BackgroundColor = Color.FromArgb(242, 244, 247);
                button.BorderColor = Color.FromArgb(185, 190, 198);
                button.HoverColor = Color.White;
                button.HoverBorderColor = Color.FromArgb(70, 130, 220);
                button.PressedColor = Color.FromArgb(225, 230, 236);
                button.PressedBorderColor = Color.FromArgb(150, 156, 166);
                button.TextColor = Color.FromArgb(55, 60, 68);
                button.ForeColor = Color.FromArgb(55, 60, 68);
            }
        }

        // ⭐ Updated to handle checkbox
        private void AutoSizeToFitText(bool hasCheckbox = false)
        {
            this.SuspendLayout();
            try
            {
                // Temporarily remove scrollbars
                rtbMessage.ScrollBars = RichTextBoxScrollBars.None;
                SizeToMessageForCurrentDpi();
            }
            finally
            {
                this.ResumeLayout();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutMessageBoxControls();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            RefreshMessageLayoutAfterDpiChange();
        }

        private void RefreshMessageLayoutAfterDpiChange()
        {
            if (IsDisposed)
                return;

            LayoutMessageBoxControls();
            UpdateMessageScrollBars();

            if (!IsHandleCreated)
                return;

            BeginInvoke((MethodInvoker)(() =>
            {
                if (IsDisposed)
                    return;

                LayoutMessageBoxControls();
                UpdateMessageScrollBars();
            }));
        }

        private void LayoutMessageBoxControls()
        {
            if (_isLayingOutMessageBox || IsDisposed)
                return;

            _isLayingOutMessageBox = true;
            try
            {
                int top = Math.Max(0, TopBarHeight + MessageVerticalPadding);
                int footerTop = Math.Max(top + 1, ClientSize.Height - BottomBarHeight + 3);
                int footerHeight = Math.Max(36, BottomBarHeight - 6);
                int bodyBottom = Math.Max(top + 1, footerTop - MessageVerticalPadding);

                rtbMessage.SetBounds(
                    MessageEdgePadding,
                    top,
                    Math.Max(1, ClientSize.Width - (MessageEdgePadding * 2)),
                    Math.Max(1, bodyBottom - top));

                pnlButtons.SetBounds(
                    0,
                    footerTop,
                    Math.Max(1, ClientSize.Width),
                    footerHeight);

                btnClose.SetBounds(
                    Math.Max(MessageEdgePadding, ClientSize.Width - btnClose.Width - 11),
                    Math.Max(4, (TopBarHeight - btnClose.Height) / 2),
                    btnClose.Width,
                    btnClose.Height);

                if (chkOption.Visible)
                    chkOption.Width = Math.Min(260, Math.Max(160, ClientSize.Width - 190));

                UpdateMessageScrollBars();
                Invalidate();
            }
            finally
            {
                _isLayingOutMessageBox = false;
            }
        }

        private int GetDesiredMessageBodyHeight()
        {
            return Math.Clamp(GetMeasuredMessageBodyHeight(), MessageBodyMinHeight, MessageBodyMaxHeight);
        }

        private bool _isRtfMessage;

        private int GetMeasuredMessageBodyHeight()
        {
            // RTF: TextRenderer can't see images (rtbMessage.Text excludes them), so measure
            // from the layout itself — the position after the last character is the bottom of
            // the real content, images included.
            if (_isRtfMessage)
            {
                int endY = rtbMessage.GetPositionFromCharIndex(Math.Max(0, rtbMessage.TextLength)).Y;
                return endY + rtbMessage.Font.Height * 2 + MessageContentPadding;
            }

            int textWidth = Math.Max(1, rtbMessage.ClientSize.Width > 0
                ? rtbMessage.ClientSize.Width - 6
                : ClientSize.Width - (MessageEdgePadding * 2) - 6);

            Size measured = TextRenderer.MeasureText(
                rtbMessage.Text.Length == 0 ? " " : rtbMessage.Text,
                rtbMessage.Font,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);

            return measured.Height + MessageContentPadding;
        }

        private void SizeToMessageForCurrentDpi()
        {
            if (IsDisposed)
                return;

            rtbMessage.ScrollBars = RichTextBoxScrollBars.None;

            if (ClientSize.Width != MessageBaseClientWidth)
                ClientSize = new Size(MessageBaseClientWidth, ClientSize.Height);

            LayoutMessageBoxControls();

            int measuredHeight = GetMeasuredMessageBodyHeight();
            int targetBodyHeight = Math.Clamp(measuredHeight, MessageBodyMinHeight, MessageBodyMaxHeight);
            int targetClientHeight = TopBarHeight + MessageVerticalPadding + targetBodyHeight + MessageVerticalPadding + BottomBarHeight;

            if (ClientSize.Height != targetClientHeight)
                ClientSize = new Size(ClientSize.Width, targetClientHeight);

            LayoutMessageBoxControls();
            UpdateMessageScrollBars();
        }

        private void UpdateMessageScrollBars()
        {
            if (rtbMessage.IsDisposed)
                return;

            bool needsScrollBar = GetMeasuredMessageBodyHeight() > rtbMessage.Height;
            RichTextBoxScrollBars targetScrollBars = needsScrollBar
                ? RichTextBoxScrollBars.Vertical
                : RichTextBoxScrollBars.None;

            if (rtbMessage.ScrollBars != targetScrollBars)
                rtbMessage.ScrollBars = targetScrollBars;
        }
    }
}
