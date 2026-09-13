namespace ProfessorSnowsVideoDownloader
{
    partial class CustomMessageBox
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            CustomControls.AdaptiveRowItemRule adaptiveRowItemRule1 = new CustomControls.AdaptiveRowItemRule();
            CustomControls.AdaptiveRowItemRule adaptiveRowItemRule2 = new CustomControls.AdaptiveRowItemRule();
            pnlButtons = new ProfessorSnowsVideoDownloader.CustomControls.AdaptiveRowPanel();
            chkOption = new ProfessorSnowsVideoDownloader.CustomControls.RoundedCheckBox();
            rtbMessage = new RichTextBox();
            btnClose = new ProfessorSnowsVideoDownloader.CustomControls.ModernButton();
            adaptiveRowPanel1 = new ProfessorSnowsVideoDownloader.CustomControls.AdaptiveRowPanel();
            pnlButtons.SuspendLayout();
            adaptiveRowPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // pnlButtons
            // 
            pnlButtons.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pnlButtons.BackColor = Color.FromArgb(230, 235, 240);
            pnlButtons.Controls.Add(chkOption);
            adaptiveRowItemRule1.ControlName = "chkOption";
            adaptiveRowItemRule1.ControlType = CustomControls.AdaptiveRowPanelAddControlType.CheckBox;
            adaptiveRowItemRule1.Id = "arp:fd7cd415fe22490e9b8332c6d206f7cc";
            adaptiveRowItemRule1.MarginLeft = 8;
            adaptiveRowItemRule1.Text = "Checkbox option";
            adaptiveRowItemRule1.Width = 194;
            pnlButtons.ItemRules.Add(adaptiveRowItemRule1);
            pnlButtons.Location = new Point(0, 193);
            pnlButtons.Name = "pnlButtons";
            pnlButtons.Padding = new Padding(8, 4, 8, 4);
            pnlButtons.Size = new Size(560, 40);
            pnlButtons.StretchChildHeight = true;
            pnlButtons.TabIndex = 109;
            // 
            // chkOption
            // 
            chkOption.BackColor = Color.FromArgb(230, 235, 240);
            chkOption.BorderColor = Color.FromArgb(170, 170, 170);
            chkOption.CheckBoxBackColor = Color.White;
            chkOption.CheckedColor = Color.FromArgb(220, 255, 255);
            chkOption.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point, 0);
            chkOption.ForeColor = Color.FromArgb(60, 60, 60);
            chkOption.Location = new Point(16, 4);
            chkOption.Margin = new Padding(8, 0, 4, 0);
            chkOption.Name = "chkOption";
            chkOption.Radius = 16;
            chkOption.Size = new Size(194, 32);
            chkOption.TabIndex = 0;
            chkOption.Tag = "arp:fd7cd415fe22490e9b8332c6d206f7cc";
            chkOption.Text = "Checkbox option";
            chkOption.Visible = false;
            // 
            // rtbMessage
            // 
            rtbMessage.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rtbMessage.BackColor = Color.White;
            rtbMessage.BorderStyle = BorderStyle.None;
            rtbMessage.Font = new Font("Segoe UI", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            rtbMessage.ForeColor = Color.FromArgb(31, 31, 31);
            rtbMessage.Location = new Point(9, 54);
            rtbMessage.Name = "rtbMessage";
            rtbMessage.Size = new Size(536, 120);
            rtbMessage.TabIndex = 108;
            rtbMessage.Text = "";
            // 
            // btnClose
            // 
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.BackColor = Color.FromArgb(230, 235, 240);
            btnClose.BackgroundColor = Color.FromArgb(230, 235, 240);
            btnClose.BorderColor = Color.Gray;
            btnClose.BorderRadius = 10;
            btnClose.BorderSize = 0;
            btnClose.ButtonImage = null;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnClose.ForeColor = Color.FromArgb(60, 60, 60);
            btnClose.HoverBorderColor = Color.Gray;
            btnClose.HoverBorderSize = 0;
            btnClose.HoverColor = Color.WhiteSmoke;
            btnClose.ImageSize = new Size(24, 24);
            btnClose.Location = new Point(167, 2);
            btnClose.Margin = new Padding(4, 3, 4, 3);
            btnClose.Name = "btnClose";
            btnClose.PressedBorderColor = Color.Gray;
            btnClose.PressedBorderSize = 0;
            btnClose.PressedColor = Color.Gainsboro;
            btnClose.Size = new Size(28, 28);
            btnClose.TabIndex = 0;
            btnClose.Tag = "arp:e36ee5a6bf1044e195f6564b6509e4cb";
            btnClose.Text = "❌";
            btnClose.TextColor = Color.FromArgb(60, 60, 60);
            btnClose.TextXOffset = 2;
            btnClose.UseVisualStyleBackColor = false;
            // 
            // adaptiveRowPanel1
            // 
            adaptiveRowPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            adaptiveRowPanel1.BackColor = Color.FromArgb(230, 235, 240);
            adaptiveRowPanel1.ContentAlignment = CustomControls.AdaptiveRowContentAlignment.Right;
            adaptiveRowPanel1.Controls.Add(btnClose);
            adaptiveRowItemRule2.ControlName = "btnClose";
            adaptiveRowItemRule2.ControlType = CustomControls.AdaptiveRowPanelAddControlType.Button;
            adaptiveRowItemRule2.Id = "arp:e36ee5a6bf1044e195f6564b6509e4cb";
            adaptiveRowItemRule2.Text = "❌";
            adaptiveRowItemRule2.Width = 28;
            adaptiveRowPanel1.ItemRules.Add(adaptiveRowItemRule2);
            adaptiveRowPanel1.Location = new Point(351, 7);
            adaptiveRowPanel1.Name = "adaptiveRowPanel1";
            adaptiveRowPanel1.Size = new Size(200, 32);
            adaptiveRowPanel1.TabIndex = 110;
            // 
            // CustomMessageBox
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(560, 236);
            Controls.Add(adaptiveRowPanel1);
            Controls.Add(pnlButtons);
            Controls.Add(rtbMessage);
            Name = "CustomMessageBox";
            Text = "CustomMessageBox";
            pnlButtons.ResumeLayout(false);
            adaptiveRowPanel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private CustomControls.AdaptiveRowPanel pnlButtons;
        private RichTextBox rtbMessage;
        private CustomControls.ModernButton btnClose;
        private CustomControls.RoundedCheckBox chkOption;
        private CustomControls.AdaptiveRowPanel adaptiveRowPanel1;
    }
}
