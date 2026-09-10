namespace CrashEvidenceCollector.App.Views;

partial class MainWorkspaceTemplate
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule3 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule4 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule5 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule6 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule1 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule2 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule7 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule8 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule9 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        workspaceSplitter = new SplitContainer();
        timelinePanel = new Panel();
        vStackPanel2 = new ProfessorSnowsVideoDownloader.CustomControls.vStackPanel();
        rowSearch = new ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel();
        roundedTextBox1 = new ProfessorSnowsVideoDownloader.CustomControls.RoundedTextBox();
        vBtnPaste = new ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton();
        virtualIconButton10 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveLineBreak();
        vBtnSearch = new ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton();
        timelineList = new ListView();
        whenColumn = new ColumnHeader();
        typeColumn = new ColumnHeader();
        codeColumn = new ColumnHeader();
        meaningColumn = new ColumnHeader();
        sourceColumn = new ColumnHeader();
        virtualRowPanel1 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel();
        virtualIconButton3 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel();
        virtualRowPanel3 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel();
        vStackPanel3 = new ProfessorSnowsVideoDownloader.CustomControls.vStackPanel();
        virtualRowPanel4 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel();
        roundedNumericTextBox1 = new ProfessorSnowsVideoDownloader.CustomControls.RoundedNumericTextBox();
        NumbericUpdownValue = new ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel();
        virtualIconButton4 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel();
        virtualIconButton5 = new ProfessorSnowsVideoDownloader.CustomControls.RoundedComboBox();
        lbl6 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel();
        lbl7 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel();
        virtualIconButton6 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveLineBreak();
        virtualRowPanel5 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel();
        virtualIconButton7 = new ProfessorSnowsVideoDownloader.CustomControls.RoundedComboBox();
        virtualIconButton8 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel();
        rightHost = new Panel();
        vStackPanel1 = new ProfessorSnowsVideoDownloader.CustomControls.vStackPanel();
        tabContentControl1 = new ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl.TabContentControl();
        rowNavigation = new ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel();
        btnBack = new ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton();
        btnRefresh = new ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton();
        virtualRowPanel2 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel();
        virtualIconButton1 = new ProfessorSnowsVideoDownloader.CustomControls.RoundedTextBox();
        incBtnGo = new ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton();
        incBtnPaste = new ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton();
        virtualIconButton2 = new ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveLineBreak();
        rowTabHeader = new ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel();
        tabHeadersControl1 = new ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl.TabHeadersControl();
        ((System.ComponentModel.ISupportInitialize)workspaceSplitter).BeginInit();
        workspaceSplitter.Panel1.SuspendLayout();
        workspaceSplitter.Panel2.SuspendLayout();
        workspaceSplitter.SuspendLayout();
        timelinePanel.SuspendLayout();
        vStackPanel2.SuspendLayout();
        rowSearch.SuspendLayout();
        virtualRowPanel1.SuspendLayout();
        virtualRowPanel3.SuspendLayout();
        vStackPanel3.SuspendLayout();
        virtualRowPanel4.SuspendLayout();
        virtualRowPanel5.SuspendLayout();
        rightHost.SuspendLayout();
        vStackPanel1.SuspendLayout();
        rowNavigation.SuspendLayout();
        virtualRowPanel2.SuspendLayout();
        rowTabHeader.SuspendLayout();
        SuspendLayout();
        // 
        // workspaceSplitter
        // 
        workspaceSplitter.BackColor = Color.FromArgb(220, 226, 234);
        workspaceSplitter.Dock = DockStyle.Fill;
        workspaceSplitter.Location = new Point(18, 18);
        workspaceSplitter.Name = "workspaceSplitter";
        // 
        // workspaceSplitter.Panel1
        // 
        workspaceSplitter.Panel1.Controls.Add(timelinePanel);
        workspaceSplitter.Panel1MinSize = 330;
        // 
        // workspaceSplitter.Panel2
        // 
        workspaceSplitter.Panel2.Controls.Add(rightHost);
        workspaceSplitter.Panel2MinSize = 360;
        workspaceSplitter.Size = new Size(1497, 781);
        workspaceSplitter.SplitterDistance = 553;
        workspaceSplitter.SplitterWidth = 7;
        workspaceSplitter.TabIndex = 0;
        // 
        // timelinePanel
        // 
        timelinePanel.BackColor = Color.White;
        timelinePanel.Controls.Add(vStackPanel2);
        timelinePanel.Dock = DockStyle.Fill;
        timelinePanel.Location = new Point(0, 0);
        timelinePanel.Name = "timelinePanel";
        timelinePanel.Padding = new Padding(14);
        timelinePanel.Size = new Size(553, 781);
        timelinePanel.TabIndex = 0;
        // 
        // vStackPanel2
        // 
        vStackPanel2.Controls.Add(rowSearch);
        vStackPanel2.Controls.Add(timelineList);
        vStackPanel2.Controls.Add(virtualRowPanel1);
        vStackPanel2.Controls.Add(virtualRowPanel3);
        vStackPanel2.Dock = DockStyle.Fill;
        vStackPanel2.Location = new Point(14, 14);
        vStackPanel2.Name = "vStackPanel2";
        vStackPanel2.Padding = new Padding(6);
        vStackRule3.CollapsedHeight = 40;
        vStackRule3.ControlName = "virtualRowPanel1";
        vStackRule3.ExpandedHeight = 41;
        vStackRule3.HasCustomCollapsedHeight = true;
        vStackRule3.Id = "da951e4b30684aefbabd910a776a1d24";
        vStackRule3.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackRule4.CollapsedHeight = 36;
        vStackRule4.ControlName = "virtualRowPanel3";
        vStackRule4.ExpandedHeight = 96;
        vStackRule4.HasCustomCollapsedHeight = true;
        vStackRule4.Id = "0b45a1d471854b7db28b74d9726b8342";
        vStackRule4.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackRule5.BottomMargin = 3;
        vStackRule5.CollapsedHeight = 43;
        vStackRule5.ControlName = "rowSearch";
        vStackRule5.ExpandedHeight = 43;
        vStackRule5.HasCustomCollapsedHeight = true;
        vStackRule5.Id = "788c36dd20b34826a42b91de9948cb75";
        vStackRule5.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackRule5.TopMargin = 3;
        vStackRule6.BottomMargin = 3;
        vStackRule6.CollapsedHeight = 179;
        vStackRule6.ControlName = "timelineList";
        vStackRule6.ExpandedHeight = 541;
        vStackRule6.FontBaselineSizeInPoints = 9F;
        vStackRule6.HasCustomCollapsedHeight = true;
        vStackRule6.Id = "c35a36f45a114c2ea33777245f403e47";
        vStackRule6.SpecialLayout = ProfessorSnowsVideoDownloader.CustomControls.VStackSpecialLayout.Spring;
        vStackRule6.TopMargin = 3;
        vStackPanel2.Rules.Add(vStackRule3);
        vStackPanel2.Rules.Add(vStackRule4);
        vStackPanel2.Rules.Add(vStackRule5);
        vStackPanel2.Rules.Add(vStackRule6);
        vStackPanel2.Size = new Size(525, 753);
        vStackPanel2.Spring = "timelineList";
        vStackPanel2.TabIndex = 3;
        // 
        // rowSearch
        // 
        rowSearch.BorderThickness = 2;
        rowSearch.Controls.Add(roundedTextBox1);
        rowSearch.Controls.Add(vBtnPaste);
        rowSearch.Controls.Add(virtualIconButton10);
        rowSearch.Controls.Add(vBtnSearch);
        rowSearch.CornerRadius = 7;
        rowSearch.Location = new Point(9, 154);
        rowSearch.Name = "rowSearch";
        rowSearch.Padding = new Padding(4);
        rowSearch.Size = new Size(507, 43);
        rowSearch.SpringControlName = "roundedTextBox1";
        rowSearch.TabIndex = 6;
        rowSearch.TabStop = true;
        rowSearch.Tag = "788c36dd20b34826a42b91de9948cb75";
        rowSearch.ToolTipSource = null;
        // 
        // roundedTextBox1
        // 
        roundedTextBox1.BackColor = Color.White;
        roundedTextBox1.BorderColor = Color.FromArgb(200, 210, 220);
        roundedTextBox1.ButtonImageSize = new Size(0, 0);
        roundedTextBox1.CornerRadius = 10;
        roundedTextBox1.FlatLabelMode = true;
        roundedTextBox1.FocusColor = Color.FromArgb(150, 180, 255);
        roundedTextBox1.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        roundedTextBox1.HoverBackColor = Color.FromArgb(245, 250, 255);
        roundedTextBox1.IconSize = new Size(24, 24);
        roundedTextBox1.LeftIcon = null;
        roundedTextBox1.Location = new Point(61, 4);
        roundedTextBox1.Multiline = false;
        roundedTextBox1.Name = "roundedTextBox1";
        roundedTextBox1.PlaceholderText = "Search...";
        roundedTextBox1.ReadOnly = false;
        roundedTextBox1.RightButtonA = null;
        roundedTextBox1.RightButtonAHover = null;
        roundedTextBox1.RightButtonAImageSize = new Size(0, 0);
        roundedTextBox1.RightButtonAPressed = null;
        roundedTextBox1.RightButtonB = null;
        roundedTextBox1.RightButtonBHover = null;
        roundedTextBox1.RightButtonBImageSize = new Size(0, 0);
        roundedTextBox1.RightButtonBPressed = null;
        roundedTextBox1.ScrollBars = ScrollBars.None;
        roundedTextBox1.Size = new Size(399, 35);
        roundedTextBox1.TabIndex = 2;
        roundedTextBox1.TextBoxBackColor = Color.White;
        roundedTextBox1.WordWrap = true;
        // 
        // vBtnPaste
        // 
        vBtnPaste.BackColor = Color.Transparent;
        vBtnPaste.ButtonImageA = Properties.Resources.UIPaste40Px;
        vBtnPaste.ButtonImageAHover = Properties.Resources.UIPaste_High_Green40Px;
        vBtnPaste.ButtonImageAPressed = Properties.Resources.UIPaste_High40Px;
        vBtnPaste.ForeColor = Color.FromArgb(60, 60, 60);
        vBtnPaste.ImageSize = new Size(30, 30);
        vBtnPaste.Location = new Point(8, 4);
        vBtnPaste.Margin = new Padding(4, 3, 4, 3);
        vBtnPaste.Name = "vBtnPaste";
        vBtnPaste.Size = new Size(32, 35);
        vBtnPaste.TabIndex = 0;
        vBtnPaste.TextColor = Color.FromArgb(60, 60, 60);
        // 
        // virtualIconButton10
        // 
        virtualIconButton10.BackColor = Color.Transparent;
        virtualIconButton10.LineColor = Color.FromArgb(200, 200, 200);
        virtualIconButton10.LineThickness = 2;
        virtualIconButton10.Location = new Point(44, 4);
        virtualIconButton10.Margin = new Padding(0, 0, 4, 0);
        virtualIconButton10.Name = "virtualIconButton10";
        virtualIconButton10.Size = new Size(10, 35);
        virtualIconButton10.TabIndex = 1;
        virtualIconButton10.TabStop = false;
        // 
        // vBtnSearch
        // 
        vBtnSearch.BackColor = Color.Transparent;
        vBtnSearch.ButtonImageA = Properties.Resources.UiPicsSearch_Norm40Px;
        vBtnSearch.ButtonImageAHover = Properties.Resources.UiPicsSearch_High_Green40Px;
        vBtnSearch.ButtonImageAPressed = Properties.Resources.UiPicsSearch_High40Px;
        vBtnSearch.ForeColor = Color.FromArgb(60, 60, 60);
        vBtnSearch.ImageSize = new Size(30, 30);
        vBtnSearch.Location = new Point(467, 5);
        vBtnSearch.Margin = new Padding(4, 3, 4, 3);
        vBtnSearch.Name = "vBtnSearch";
        vBtnSearch.Size = new Size(32, 32);
        vBtnSearch.TabIndex = 3;
        vBtnSearch.TextColor = Color.FromArgb(60, 60, 60);
        // 
        // timelineList
        // 
        timelineList.BorderStyle = BorderStyle.None;
        timelineList.Columns.AddRange(new ColumnHeader[] { whenColumn, typeColumn, codeColumn, meaningColumn, sourceColumn });
        timelineList.FullRowSelect = true;
        timelineList.Location = new Point(9, 203);
        timelineList.MultiSelect = false;
        timelineList.Name = "timelineList";
        timelineList.Size = new Size(507, 541);
        timelineList.TabIndex = 8;
        timelineList.Tag = "c35a36f45a114c2ea33777245f403e47";
        timelineList.UseCompatibleStateImageBehavior = false;
        timelineList.View = View.Details;
        // 
        // whenColumn
        // 
        whenColumn.Text = "When";
        whenColumn.Width = 180;
        // 
        // typeColumn
        // 
        typeColumn.Text = "Type";
        typeColumn.Width = 150;
        // 
        // codeColumn
        // 
        codeColumn.Text = "Code / incident";
        codeColumn.Width = 330;
        // 
        // meaningColumn
        // 
        meaningColumn.Text = "Plain-English meaning";
        meaningColumn.Width = 560;
        // 
        // sourceColumn
        // 
        sourceColumn.Text = "Source";
        sourceColumn.Width = 300;
        // 
        // virtualRowPanel1
        // 
        virtualRowPanel1.Controls.Add(virtualIconButton3);
        virtualRowPanel1.Location = new Point(9, 6);
        virtualRowPanel1.Margin = new Padding(3, 0, 3, 4);
        virtualRowPanel1.Name = "virtualRowPanel1";
        virtualRowPanel1.Padding = new Padding(4);
        virtualRowPanel1.Size = new Size(507, 41);
        virtualRowPanel1.SpringControlName = "virtualIconButton3";
        virtualRowPanel1.TabIndex = 2;
        virtualRowPanel1.TabStop = true;
        virtualRowPanel1.Tag = "da951e4b30684aefbabd910a776a1d24";
        virtualRowPanel1.ToolTipSource = null;
        // 
        // virtualIconButton3
        // 
        virtualIconButton3.AutoInheritSurfaceColor = false;
        virtualIconButton3.BackgroundColor = Color.White;
        virtualIconButton3.BorderColor = Color.Gray;
        virtualIconButton3.BorderRadius = 6;
        virtualIconButton3.BorderSize = 0;
        virtualIconButton3.ButtonImage = null;
        virtualIconButton3.FlatAppearance.BorderSize = 0;
        virtualIconButton3.FlatAppearance.MouseDownBackColor = Color.Transparent;
        virtualIconButton3.FlatAppearance.MouseOverBackColor = Color.Transparent;
        virtualIconButton3.FlatStyle = FlatStyle.Flat;
        virtualIconButton3.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
        virtualIconButton3.ForeColor = Color.FromArgb(31, 31, 31);
        virtualIconButton3.HoverBorderColor = Color.Gray;
        virtualIconButton3.HoverBorderSize = 0;
        virtualIconButton3.HoverColor = Color.White;
        virtualIconButton3.HoverColorFx = false;
        virtualIconButton3.ImageSize = new Size(24, 24);
        virtualIconButton3.LabelHeight = 33;
        virtualIconButton3.Location = new Point(8, 4);
        virtualIconButton3.Margin = new Padding(4, 0, 4, 0);
        virtualIconButton3.MinimumSize = new Size(24, 18);
        virtualIconButton3.Name = "virtualIconButton3";
        virtualIconButton3.Padding = new Padding(8, 0, 8, 0);
        virtualIconButton3.PressedBorderColor = Color.Gray;
        virtualIconButton3.PressedBorderSize = 0;
        virtualIconButton3.PressedColor = Color.White;
        virtualIconButton3.PressedColorFx = false;
        virtualIconButton3.Size = new Size(491, 33);
        virtualIconButton3.SurfaceColor = Color.White;
        virtualIconButton3.TabIndex = 0;
        virtualIconButton3.TabStop = false;
        virtualIconButton3.Text = "Collected crash evidence";
        virtualIconButton3.TextColor = Color.FromArgb(31, 31, 31);
        // 
        // virtualRowPanel3
        // 
        virtualRowPanel3.BorderThickness = 2;
        virtualRowPanel3.Controls.Add(vStackPanel3);
        virtualRowPanel3.CornerRadius = 7;
        virtualRowPanel3.Location = new Point(9, 51);
        virtualRowPanel3.Margin = new Padding(3, 0, 3, 4);
        virtualRowPanel3.Name = "virtualRowPanel3";
        virtualRowPanel3.Padding = new Padding(4);
        virtualRowPanel3.Size = new Size(507, 96);
        virtualRowPanel3.SpringControlName = "vStackPanel3";
        virtualRowPanel3.TabIndex = 4;
        virtualRowPanel3.TabStop = true;
        virtualRowPanel3.Tag = "0b45a1d471854b7db28b74d9726b8342";
        virtualRowPanel3.ToolTipSource = null;
        // 
        // vStackPanel3
        // 
        vStackPanel3.Controls.Add(virtualRowPanel4);
        vStackPanel3.Controls.Add(virtualRowPanel5);
        vStackPanel3.Location = new Point(7, 4);
        vStackPanel3.Name = "vStackPanel3";
        vStackPanel3.Padding = new Padding(6);
        vStackRule1.CollapsedHeight = 36;
        vStackRule1.ControlName = "virtualRowPanel4";
        vStackRule1.ExpandedHeight = 36;
        vStackRule1.HasCustomCollapsedHeight = true;
        vStackRule1.Id = "1c6ac1b03a5a4528bcddd3b74dd8046a";
        vStackRule1.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackRule2.CollapsedHeight = 36;
        vStackRule2.ControlName = "virtualRowPanel5";
        vStackRule2.ExpandedHeight = 36;
        vStackRule2.HasCustomCollapsedHeight = true;
        vStackRule2.Id = "d7cd2c0f31b5496b888a5cd8667b9ac1";
        vStackRule2.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackPanel3.Rules.Add(vStackRule1);
        vStackPanel3.Rules.Add(vStackRule2);
        vStackPanel3.Size = new Size(493, 88);
        vStackPanel3.TabIndex = 0;
        // 
        // virtualRowPanel4
        // 
        virtualRowPanel4.Controls.Add(roundedNumericTextBox1);
        virtualRowPanel4.Controls.Add(virtualIconButton4);
        virtualRowPanel4.Controls.Add(virtualIconButton5);
        virtualRowPanel4.Controls.Add(lbl6);
        virtualRowPanel4.Controls.Add(NumbericUpdownValue);
        virtualRowPanel4.Controls.Add(lbl7);
        virtualRowPanel4.Controls.Add(virtualIconButton6);
        virtualRowPanel4.Location = new Point(9, 6);
        virtualRowPanel4.Margin = new Padding(3, 0, 3, 4);
        virtualRowPanel4.Name = "virtualRowPanel4";
        virtualRowPanel4.Padding = new Padding(4);
        virtualRowPanel4.Size = new Size(475, 36);
        virtualRowPanel4.TabIndex = 2;
        virtualRowPanel4.TabStop = true;
        virtualRowPanel4.Tag = "1c6ac1b03a5a4528bcddd3b74dd8046a";
        virtualRowPanel4.ToolTipSource = null;
        // 
        // roundedNumericTextBox1
        // 
        roundedNumericTextBox1.BackColor = Color.White;
        roundedNumericTextBox1.BorderColor = Color.FromArgb(200, 210, 220);
        roundedNumericTextBox1.ButtonHoverColor = Color.FromArgb(240, 244, 252);
        roundedNumericTextBox1.ButtonPressColor = Color.FromArgb(220, 230, 245);
        roundedNumericTextBox1.CornerRadius = 10;
        roundedNumericTextBox1.DisabledBorderColor = Color.FromArgb(220, 220, 220);
        roundedNumericTextBox1.DisabledInnerColor = Color.FromArgb(250, 250, 250);
        roundedNumericTextBox1.DisabledTextColor = Color.FromArgb(160, 160, 160);
        roundedNumericTextBox1.ExternalDisplay = NumbericUpdownValue;
        roundedNumericTextBox1.FocusColor = Color.FromArgb(150, 180, 255);
        roundedNumericTextBox1.HoverBorderColor = Color.FromArgb(0, 120, 215);
        roundedNumericTextBox1.HoverInnerColor = Color.FromArgb(245, 250, 255);
        roundedNumericTextBox1.IconSize = new Size(24, 24);
        roundedNumericTextBox1.InnerColor = Color.White;
        roundedNumericTextBox1.LeftIcon = null;
        roundedNumericTextBox1.Location = new Point(368, 4);
        roundedNumericTextBox1.Name = "roundedNumericTextBox1";
        roundedNumericTextBox1.ReadOnly = false;
        roundedNumericTextBox1.SelectedBorderColor = Color.FromArgb(0, 120, 215);
        roundedNumericTextBox1.SelectedInnerColor = Color.FromArgb(230, 240, 250);
        roundedNumericTextBox1.SeparatorColor = Color.FromArgb(220, 220, 220);
        roundedNumericTextBox1.Size = new Size(23, 28);
        roundedNumericTextBox1.SpinnerMode = ProfessorSnowsVideoDownloader.CustomControls.NumericSpinnerMode.SpinnerOnly;
        roundedNumericTextBox1.TabIndex = 5;
        roundedNumericTextBox1.Tag = "51da5de9599e4461969593b5eaae850b";
        roundedNumericTextBox1.Text = "roundedNumericTextBox1";
        // 
        // NumbericUpdownValue
        // 
        NumbericUpdownValue.AutoInheritSurfaceColor = false;
        NumbericUpdownValue.BackgroundColor = Color.White;
        NumbericUpdownValue.BorderColor = Color.Gray;
        NumbericUpdownValue.BorderRadius = 6;
        NumbericUpdownValue.BorderSize = 0;
        NumbericUpdownValue.ButtonImage = null;
        NumbericUpdownValue.FlatAppearance.BorderSize = 0;
        NumbericUpdownValue.FlatAppearance.MouseDownBackColor = Color.Transparent;
        NumbericUpdownValue.FlatAppearance.MouseOverBackColor = Color.Transparent;
        NumbericUpdownValue.FlatStyle = FlatStyle.Flat;
        NumbericUpdownValue.Font = new Font("Segoe UI", 10F);
        NumbericUpdownValue.ForeColor = Color.FromArgb(31, 31, 31);
        NumbericUpdownValue.HoverBorderColor = Color.Gray;
        NumbericUpdownValue.HoverBorderSize = 0;
        NumbericUpdownValue.HoverColor = Color.White;
        NumbericUpdownValue.HoverColorFx = false;
        NumbericUpdownValue.ImageSize = new Size(24, 24);
        NumbericUpdownValue.Location = new Point(320, 4);
        NumbericUpdownValue.Margin = new Padding(4, 0, 4, 0);
        NumbericUpdownValue.MinimumSize = new Size(24, 18);
        NumbericUpdownValue.Name = "NumbericUpdownValue";
        NumbericUpdownValue.Padding = new Padding(8, 0, 8, 0);
        NumbericUpdownValue.PressedBorderColor = Color.Gray;
        NumbericUpdownValue.PressedBorderSize = 0;
        NumbericUpdownValue.PressedColor = Color.White;
        NumbericUpdownValue.PressedColorFx = false;
        NumbericUpdownValue.Size = new Size(41, 28);
        NumbericUpdownValue.SurfaceColor = Color.White;
        NumbericUpdownValue.TabIndex = 4;
        NumbericUpdownValue.TabStop = false;
        NumbericUpdownValue.Text = "0";
        NumbericUpdownValue.TextColor = Color.FromArgb(31, 31, 31);
        // 
        // virtualIconButton4
        // 
        virtualIconButton4.AutoInheritSurfaceColor = false;
        virtualIconButton4.BackgroundColor = Color.White;
        virtualIconButton4.BorderColor = Color.Gray;
        virtualIconButton4.BorderRadius = 6;
        virtualIconButton4.BorderSize = 0;
        virtualIconButton4.ButtonImage = null;
        virtualIconButton4.FlatAppearance.BorderSize = 0;
        virtualIconButton4.FlatAppearance.MouseDownBackColor = Color.Transparent;
        virtualIconButton4.FlatAppearance.MouseOverBackColor = Color.Transparent;
        virtualIconButton4.FlatStyle = FlatStyle.Flat;
        virtualIconButton4.Font = new Font("Segoe UI", 10F);
        virtualIconButton4.ForeColor = Color.FromArgb(31, 31, 31);
        virtualIconButton4.HoverBorderColor = Color.Gray;
        virtualIconButton4.HoverBorderSize = 0;
        virtualIconButton4.HoverColor = Color.White;
        virtualIconButton4.HoverColorFx = false;
        virtualIconButton4.ImageSize = new Size(24, 24);
        virtualIconButton4.Location = new Point(8, 4);
        virtualIconButton4.Margin = new Padding(4, 0, 4, 0);
        virtualIconButton4.MinimumSize = new Size(24, 18);
        virtualIconButton4.Name = "virtualIconButton4";
        virtualIconButton4.Padding = new Padding(8, 0, 8, 0);
        virtualIconButton4.PressedBorderColor = Color.Gray;
        virtualIconButton4.PressedBorderSize = 0;
        virtualIconButton4.PressedColor = Color.White;
        virtualIconButton4.PressedColorFx = false;
        virtualIconButton4.Size = new Size(45, 28);
        virtualIconButton4.SurfaceColor = Color.White;
        virtualIconButton4.TabIndex = 0;
        virtualIconButton4.TabStop = false;
        virtualIconButton4.Text = "Show";
        virtualIconButton4.TextColor = Color.FromArgb(31, 31, 31);
        // 
        // virtualIconButton5
        // 
        virtualIconButton5.BackColor = Color.Transparent;
        virtualIconButton5.BorderColor = Color.FromArgb(180, 180, 180);
        virtualIconButton5.BorderThickness = 1;
        virtualIconButton5.ButtonImageB = null;
        virtualIconButton5.ButtonImageSize = new Size(0, 0);
        virtualIconButton5.ComboBoxWidth = 165;
        virtualIconButton5.CornerRadius = 5;
        virtualIconButton5.DisabledBorderColor = Color.FromArgb(220, 220, 220);
        virtualIconButton5.DisabledInnerColor = Color.FromArgb(250, 250, 250);
        virtualIconButton5.DisabledTextColor = Color.FromArgb(160, 160, 160);
        virtualIconButton5.DropShadowSize = 6;
        virtualIconButton5.Font = new Font("Segoe UI", 10F);
        virtualIconButton5.HoverBorderColor = Color.FromArgb(0, 120, 215);
        virtualIconButton5.HoverBorderThickness = 1;
        virtualIconButton5.HoverInnerColor = Color.FromArgb(245, 245, 245);
        virtualIconButton5.HoverPropertiesEnabled = true;
        virtualIconButton5.InnerColor = Color.White;
        virtualIconButton5.InnerGradientEndColor = Color.FromArgb(225, 232, 240);
        virtualIconButton5.IsSelected = false;
        virtualIconButton5.ItemHeight = 26;
        virtualIconButton5.ItemHoverColor = Color.FromArgb(240, 244, 252);
        virtualIconButton5.Location = new Point(61, 4);
        virtualIconButton5.Margin = new Padding(4, 3, 4, 3);
        virtualIconButton5.MaxVisibleRows = 10;
        virtualIconButton5.Name = "virtualIconButton5";
        virtualIconButton5.SelectedBorderColor = Color.FromArgb(0, 120, 215);
        virtualIconButton5.SelectedBorderThickness = 2;
        virtualIconButton5.SelectedIndex = -1;
        virtualIconButton5.SelectedInnerColor = Color.FromArgb(230, 240, 250);
        virtualIconButton5.SelectedItem = null;
        virtualIconButton5.SeparatorColor = Color.FromArgb(220, 220, 220);
        virtualIconButton5.ShowAllDropDownRows = false;
        virtualIconButton5.Size = new Size(165, 28);
        virtualIconButton5.TabIndex = 1;
        // 
        // lbl6
        // 
        lbl6.AutoInheritSurfaceColor = false;
        lbl6.BackgroundColor = Color.White;
        lbl6.BorderColor = Color.Gray;
        lbl6.BorderRadius = 6;
        lbl6.BorderSize = 0;
        lbl6.ButtonImage = null;
        lbl6.FlatAppearance.BorderSize = 0;
        lbl6.FlatAppearance.MouseDownBackColor = Color.Transparent;
        lbl6.FlatAppearance.MouseOverBackColor = Color.Transparent;
        lbl6.FlatStyle = FlatStyle.Flat;
        lbl6.Font = new Font("Segoe UI", 10F);
        lbl6.ForeColor = Color.FromArgb(31, 31, 31);
        lbl6.HoverBorderColor = Color.Gray;
        lbl6.HoverBorderSize = 0;
        lbl6.HoverColor = Color.White;
        lbl6.HoverColorFx = false;
        lbl6.ImageSize = new Size(24, 24);
        lbl6.Location = new Point(249, 4);
        lbl6.Margin = new Padding(4, 0, 4, 0);
        lbl6.MinimumSize = new Size(24, 18);
        lbl6.Name = "lbl6";
        lbl6.Padding = new Padding(8, 0, 8, 0);
        lbl6.PressedBorderColor = Color.Gray;
        lbl6.PressedBorderSize = 0;
        lbl6.PressedColor = Color.White;
        lbl6.PressedColorFx = false;
        lbl6.Size = new Size(63, 28);
        lbl6.SurfaceColor = Color.White;
        lbl6.TabIndex = 3;
        lbl6.TabStop = false;
        lbl6.Text = "Custom:";
        lbl6.TextColor = Color.FromArgb(31, 31, 31);
        // 
        // lbl7
        // 
        lbl7.AutoInheritSurfaceColor = false;
        lbl7.BackgroundColor = Color.White;
        lbl7.BorderColor = Color.Gray;
        lbl7.BorderRadius = 6;
        lbl7.BorderSize = 0;
        lbl7.ButtonImage = null;
        lbl7.FlatAppearance.BorderSize = 0;
        lbl7.FlatAppearance.MouseDownBackColor = Color.Transparent;
        lbl7.FlatAppearance.MouseOverBackColor = Color.Transparent;
        lbl7.FlatStyle = FlatStyle.Flat;
        lbl7.Font = new Font("Segoe UI", 10F);
        lbl7.ForeColor = Color.FromArgb(31, 31, 31);
        lbl7.HoverBorderColor = Color.Gray;
        lbl7.HoverBorderSize = 0;
        lbl7.HoverColor = Color.White;
        lbl7.HoverColorFx = false;
        lbl7.ImageSize = new Size(24, 24);
        lbl7.Location = new Point(398, 4);
        lbl7.Margin = new Padding(4, 0, 4, 0);
        lbl7.MinimumSize = new Size(24, 18);
        lbl7.Name = "lbl7";
        lbl7.Padding = new Padding(8, 0, 8, 0);
        lbl7.PressedBorderColor = Color.Gray;
        lbl7.PressedBorderSize = 0;
        lbl7.PressedColor = Color.White;
        lbl7.PressedColorFx = false;
        lbl7.Size = new Size(44, 28);
        lbl7.SurfaceColor = Color.White;
        lbl7.TabIndex = 6;
        lbl7.TabStop = false;
        lbl7.Text = "hours";
        lbl7.TextColor = Color.FromArgb(31, 31, 31);
        // 
        // virtualIconButton6
        // 
        virtualIconButton6.BackColor = Color.Transparent;
        virtualIconButton6.LineColor = Color.FromArgb(200, 200, 200);
        virtualIconButton6.LineThickness = 2;
        virtualIconButton6.Location = new Point(234, 4);
        virtualIconButton6.Margin = new Padding(4, 0, 4, 0);
        virtualIconButton6.Name = "virtualIconButton6";
        virtualIconButton6.Size = new Size(7, 28);
        virtualIconButton6.TabIndex = 2;
        virtualIconButton6.TabStop = false;
        // 
        // virtualRowPanel5
        // 
        virtualRowPanel5.Controls.Add(virtualIconButton7);
        virtualRowPanel5.Controls.Add(virtualIconButton8);
        virtualRowPanel5.Location = new Point(9, 46);
        virtualRowPanel5.Margin = new Padding(3, 0, 3, 4);
        virtualRowPanel5.Name = "virtualRowPanel5";
        virtualRowPanel5.Padding = new Padding(4);
        virtualRowPanel5.Size = new Size(475, 36);
        virtualRowPanel5.TabIndex = 4;
        virtualRowPanel5.TabStop = true;
        virtualRowPanel5.Tag = "d7cd2c0f31b5496b888a5cd8667b9ac1";
        virtualRowPanel5.ToolTipSource = null;
        // 
        // virtualIconButton7
        // 
        virtualIconButton7.BackColor = Color.Transparent;
        virtualIconButton7.BorderColor = Color.FromArgb(180, 180, 180);
        virtualIconButton7.BorderThickness = 1;
        virtualIconButton7.ButtonImageB = null;
        virtualIconButton7.ButtonImageSize = new Size(0, 0);
        virtualIconButton7.ComboBoxWidth = 220;
        virtualIconButton7.CornerRadius = 5;
        virtualIconButton7.DisabledBorderColor = Color.FromArgb(220, 220, 220);
        virtualIconButton7.DisabledInnerColor = Color.FromArgb(250, 250, 250);
        virtualIconButton7.DisabledTextColor = Color.FromArgb(160, 160, 160);
        virtualIconButton7.DropShadowSize = 6;
        virtualIconButton7.Font = new Font("Segoe UI", 10F);
        virtualIconButton7.HoverBorderColor = Color.FromArgb(0, 120, 215);
        virtualIconButton7.HoverBorderThickness = 1;
        virtualIconButton7.HoverInnerColor = Color.FromArgb(245, 245, 245);
        virtualIconButton7.HoverPropertiesEnabled = true;
        virtualIconButton7.InnerColor = Color.White;
        virtualIconButton7.InnerGradientEndColor = Color.FromArgb(225, 232, 240);
        virtualIconButton7.IsSelected = false;
        virtualIconButton7.ItemHeight = 26;
        virtualIconButton7.ItemHoverColor = Color.FromArgb(240, 244, 252);
        virtualIconButton7.Location = new Point(8, 4);
        virtualIconButton7.Margin = new Padding(4, 3, 4, 3);
        virtualIconButton7.MaxVisibleRows = 10;
        virtualIconButton7.Name = "virtualIconButton7";
        virtualIconButton7.SelectedBorderColor = Color.FromArgb(0, 120, 215);
        virtualIconButton7.SelectedBorderThickness = 2;
        virtualIconButton7.SelectedIndex = -1;
        virtualIconButton7.SelectedInnerColor = Color.FromArgb(230, 240, 250);
        virtualIconButton7.SelectedItem = null;
        virtualIconButton7.SeparatorColor = Color.FromArgb(220, 220, 220);
        virtualIconButton7.ShowAllDropDownRows = false;
        virtualIconButton7.Size = new Size(220, 28);
        virtualIconButton7.TabIndex = 0;
        // 
        // virtualIconButton8
        // 
        virtualIconButton8.AutoInheritSurfaceColor = false;
        virtualIconButton8.BackgroundColor = Color.White;
        virtualIconButton8.BorderColor = Color.Gray;
        virtualIconButton8.BorderRadius = 6;
        virtualIconButton8.BorderSize = 0;
        virtualIconButton8.ButtonImage = null;
        virtualIconButton8.FlatAppearance.BorderSize = 0;
        virtualIconButton8.FlatAppearance.MouseDownBackColor = Color.Transparent;
        virtualIconButton8.FlatAppearance.MouseOverBackColor = Color.Transparent;
        virtualIconButton8.FlatStyle = FlatStyle.Flat;
        virtualIconButton8.Font = new Font("Segoe UI", 10F);
        virtualIconButton8.ForeColor = Color.FromArgb(31, 31, 31);
        virtualIconButton8.HoverBorderColor = Color.Gray;
        virtualIconButton8.HoverBorderSize = 0;
        virtualIconButton8.HoverColor = Color.White;
        virtualIconButton8.HoverColorFx = false;
        virtualIconButton8.ImageSize = new Size(24, 24);
        virtualIconButton8.Location = new Point(236, 4);
        virtualIconButton8.Margin = new Padding(4, 0, 4, 0);
        virtualIconButton8.MinimumSize = new Size(24, 18);
        virtualIconButton8.Name = "virtualIconButton8";
        virtualIconButton8.Padding = new Padding(8, 0, 8, 0);
        virtualIconButton8.PressedBorderColor = Color.Gray;
        virtualIconButton8.PressedBorderSize = 0;
        virtualIconButton8.PressedColor = Color.White;
        virtualIconButton8.PressedColorFx = false;
        virtualIconButton8.Size = new Size(80, 28);
        virtualIconButton8.SurfaceColor = Color.White;
        virtualIconButton8.TabIndex = 1;
        virtualIconButton8.TabStop = false;
        virtualIconButton8.Text = "0 shown";
        virtualIconButton8.TextColor = Color.FromArgb(31, 31, 31);
        // 
        // rightHost
        // 
        rightHost.BackColor = Color.FromArgb(246, 248, 251);
        rightHost.Controls.Add(vStackPanel1);
        rightHost.Dock = DockStyle.Fill;
        rightHost.Location = new Point(0, 0);
        rightHost.Name = "rightHost";
        rightHost.Padding = new Padding(4, 0, 0, 0);
        rightHost.Size = new Size(937, 781);
        rightHost.TabIndex = 0;
        // 
        // vStackPanel1
        // 
        vStackPanel1.Controls.Add(tabContentControl1);
        vStackPanel1.Controls.Add(rowNavigation);
        vStackPanel1.Controls.Add(rowTabHeader);
        vStackPanel1.Dock = DockStyle.Fill;
        vStackPanel1.Location = new Point(4, 0);
        vStackPanel1.Name = "vStackPanel1";
        vStackPanel1.Padding = new Padding(6);
        vStackRule7.CollapsedHeight = 36;
        vStackRule7.ControlName = "rowTabHeader";
        vStackRule7.ExpandedHeight = 49;
        vStackRule7.HasCustomCollapsedHeight = true;
        vStackRule7.Id = "ace8406a5f5640d2897ea57a67c495d3";
        vStackRule7.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackRule8.CollapsedHeight = 0;
        vStackRule8.ControlName = "rowNavigation";
        vStackRule8.ExpandedHeight = 42;
        vStackRule8.HasCustomCollapsedHeight = true;
        vStackRule8.Id = "ddd34f60260949989fb02a29c0aa0ab5";
        vStackRule8.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackRule9.BottomMargin = 3;
        vStackRule9.CollapsedHeight = 448;
        vStackRule9.ControlName = "TabContentControl";
        vStackRule9.ExpandedHeight = 664;
        vStackRule9.HasCustomCollapsedHeight = true;
        vStackRule9.Id = "3b3a59976ad14e4b8c3c8b63e4e55fd3";
        vStackRule9.SpecialLayout = ProfessorSnowsVideoDownloader.CustomControls.VStackSpecialLayout.Spring;
        vStackRule9.TopMargin = 3;
        vStackPanel1.Rules.Add(vStackRule7);
        vStackPanel1.Rules.Add(vStackRule8);
        vStackPanel1.Rules.Add(vStackRule9);
        vStackPanel1.Size = new Size(933, 781);
        vStackPanel1.Spring = "TabContentControl";
        vStackPanel1.TabIndex = 0;
        // 
        // tabContentControl1
        // 
        tabContentControl1.Dock = DockStyle.Fill;
        tabContentControl1.Location = new Point(9, 108);
        tabContentControl1.Name = "tabContentControl1";
        tabContentControl1.SelectedIndex = -1;
        tabContentControl1.Size = new Size(915, 664);
        tabContentControl1.TabIndex = 6;
        tabContentControl1.Tag = "3b3a59976ad14e4b8c3c8b63e4e55fd3";
        // 
        // rowNavigation
        // 
        rowNavigation.Controls.Add(btnBack);
        rowNavigation.Controls.Add(btnRefresh);
        rowNavigation.Controls.Add(virtualRowPanel2);
        rowNavigation.Location = new Point(9, 59);
        rowNavigation.Margin = new Padding(3, 0, 3, 4);
        rowNavigation.Name = "rowNavigation";
        rowNavigation.Padding = new Padding(4, 0, 4, 0);
        rowNavigation.Size = new Size(915, 42);
        rowNavigation.SpringControlName = "virtualRowPanel2";
        rowNavigation.TabIndex = 4;
        rowNavigation.TabStop = true;
        rowNavigation.Tag = "ddd34f60260949989fb02a29c0aa0ab5";
        rowNavigation.ToolTipSource = null;
        // 
        // btnBack
        // 
        btnBack.BackColor = Color.Transparent;
        btnBack.ButtonImageA = Properties.Resources.UiPicsBack_Norm40Px;
        btnBack.ButtonImageAHover = Properties.Resources.UiSetBack_High40Px;
        btnBack.ButtonImageAPressed = Properties.Resources.UiPicsBack_High_Green40Px1;
        btnBack.ForeColor = Color.FromArgb(60, 60, 60);
        btnBack.HoveredSize = 2;
        btnBack.ImageSize = new Size(30, 30);
        btnBack.Location = new Point(8, 4);
        btnBack.Margin = new Padding(4, 3, 4, 3);
        btnBack.Name = "btnBack";
        btnBack.Size = new Size(36, 34);
        btnBack.TabIndex = 0;
        btnBack.TextColor = Color.FromArgb(60, 60, 60);
        // 
        // btnRefresh
        // 
        btnRefresh.BackColor = Color.Transparent;
        btnRefresh.ButtonImageA = Properties.Resources.UiPicsRefresh_Norm40Px;
        btnRefresh.ButtonImageAHover = Properties.Resources.UiPicsRefresh_High40Px;
        btnRefresh.ButtonImageAPressed = Properties.Resources.UiPicsRefresh_High_Green40Px;
        btnRefresh.ForeColor = Color.FromArgb(60, 60, 60);
        btnRefresh.HoveredSize = 2;
        btnRefresh.ImageSize = new Size(30, 30);
        btnRefresh.Location = new Point(52, 4);
        btnRefresh.Margin = new Padding(4, 3, 4, 3);
        btnRefresh.Name = "btnRefresh";
        btnRefresh.Size = new Size(36, 34);
        btnRefresh.TabIndex = 1;
        btnRefresh.TextColor = Color.FromArgb(60, 60, 60);
        // 
        // virtualRowPanel2
        // 
        virtualRowPanel2.BorderColor = Color.Gray;
        virtualRowPanel2.BorderThickness = 2;
        virtualRowPanel2.Controls.Add(virtualIconButton1);
        virtualRowPanel2.Controls.Add(incBtnGo);
        virtualRowPanel2.Controls.Add(incBtnPaste);
        virtualRowPanel2.Controls.Add(virtualIconButton2);
        virtualRowPanel2.CornerRadius = 7;
        virtualRowPanel2.Location = new Point(95, 2);
        virtualRowPanel2.Margin = new Padding(3, 0, 3, 4);
        virtualRowPanel2.Name = "virtualRowPanel2";
        virtualRowPanel2.Padding = new Padding(4, 0, 4, 0);
        virtualRowPanel2.Size = new Size(813, 38);
        virtualRowPanel2.SpringControlName = "virtualIconButton1";
        virtualRowPanel2.TabIndex = 2;
        virtualRowPanel2.TabStop = true;
        virtualRowPanel2.Tag = "972e03d129284c9e894b75a57bd23eb1";
        virtualRowPanel2.ToolTipSource = null;
        // 
        // virtualIconButton1
        // 
        virtualIconButton1.BackColor = Color.White;
        virtualIconButton1.BorderColor = Color.FromArgb(200, 210, 220);
        virtualIconButton1.ButtonImageSize = new Size(0, 0);
        virtualIconButton1.CornerRadius = 10;
        virtualIconButton1.FlatLabelMode = true;
        virtualIconButton1.FocusColor = Color.FromArgb(150, 180, 255);
        virtualIconButton1.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        virtualIconButton1.ForeColor = SystemColors.WindowFrame;
        virtualIconButton1.HoverBackColor = Color.FromArgb(245, 250, 255);
        virtualIconButton1.IconSize = new Size(24, 24);
        virtualIconButton1.LeftIcon = null;
        virtualIconButton1.Location = new Point(44, 5);
        virtualIconButton1.Margin = new Padding(4, 3, 4, 3);
        virtualIconButton1.Multiline = false;
        virtualIconButton1.Name = "virtualIconButton1";
        virtualIconButton1.PlaceholderText = "";
        virtualIconButton1.ReadOnly = false;
        virtualIconButton1.RightButtonA = null;
        virtualIconButton1.RightButtonAHover = null;
        virtualIconButton1.RightButtonAImageSize = new Size(0, 0);
        virtualIconButton1.RightButtonAPressed = null;
        virtualIconButton1.RightButtonB = null;
        virtualIconButton1.RightButtonBHover = null;
        virtualIconButton1.RightButtonBImageSize = new Size(0, 0);
        virtualIconButton1.RightButtonBPressed = null;
        virtualIconButton1.ScrollBars = ScrollBars.None;
        virtualIconButton1.Size = new Size(712, 28);
        virtualIconButton1.TabIndex = 1;
        virtualIconButton1.Text = "Text...";
        virtualIconButton1.TextBoxBackColor = Color.White;
        virtualIconButton1.WordWrap = true;
        // 
        // incBtnGo
        // 
        incBtnGo.BackColor = Color.Transparent;
        incBtnGo.ButtonImageA = Properties.Resources.UiPicsCopy_Norm40Px;
        incBtnGo.ButtonImageAHover = Properties.Resources.UiSetCopy_High40Px;
        incBtnGo.ForeColor = Color.FromArgb(60, 60, 60);
        incBtnGo.ImageSize = new Size(24, 24);
        incBtnGo.Location = new Point(777, 5);
        incBtnGo.Margin = new Padding(4, 0, 4, 0);
        incBtnGo.Name = "incBtnGo";
        incBtnGo.Size = new Size(28, 28);
        incBtnGo.TabIndex = 3;
        incBtnGo.TextColor = Color.FromArgb(60, 60, 60);
        // 
        // incBtnPaste
        // 
        incBtnPaste.BackColor = Color.Transparent;
        incBtnPaste.ButtonImageA = Properties.Resources.UiPicsGoGreen40Px;
        incBtnPaste.ButtonImageAHover = Properties.Resources.UiPicsGoGreen_High40Px;
        incBtnPaste.ForeColor = Color.FromArgb(60, 60, 60);
        incBtnPaste.ImageSize = new Size(24, 24);
        incBtnPaste.Location = new Point(8, 5);
        incBtnPaste.Margin = new Padding(4, 0, 4, 0);
        incBtnPaste.Name = "incBtnPaste";
        incBtnPaste.Size = new Size(28, 28);
        incBtnPaste.TabIndex = 0;
        incBtnPaste.TextColor = Color.FromArgb(60, 60, 60);
        // 
        // virtualIconButton2
        // 
        virtualIconButton2.BackColor = Color.Transparent;
        virtualIconButton2.LineColor = Color.FromArgb(200, 200, 200);
        virtualIconButton2.LineThickness = 2;
        virtualIconButton2.Location = new Point(764, 4);
        virtualIconButton2.Margin = new Padding(4, 0, 4, 0);
        virtualIconButton2.Name = "virtualIconButton2";
        virtualIconButton2.Size = new Size(5, 30);
        virtualIconButton2.TabIndex = 2;
        virtualIconButton2.TabStop = false;
        // 
        // rowTabHeader
        // 
        rowTabHeader.Controls.Add(tabHeadersControl1);
        rowTabHeader.Location = new Point(9, 6);
        rowTabHeader.Margin = new Padding(3, 0, 3, 4);
        rowTabHeader.Name = "rowTabHeader";
        rowTabHeader.Padding = new Padding(4, 0, 4, 0);
        rowTabHeader.Size = new Size(915, 49);
        rowTabHeader.SpringControlName = "tabHeadersControl1";
        rowTabHeader.TabIndex = 2;
        rowTabHeader.TabStop = true;
        rowTabHeader.Tag = "ace8406a5f5640d2897ea57a67c495d3";
        rowTabHeader.ToolTipSource = null;
        // 
        // tabHeadersControl1
        // 
        tabHeadersControl1.BackColor = Color.FromArgb(237, 245, 250);
        tabHeadersControl1.HeaderBackColor = Color.FromArgb(237, 245, 250);
        tabHeadersControl1.IconSize = 24;
        tabHeadersControl1.Location = new Point(7, 4);
        tabHeadersControl1.Margin = new Padding(3, 0, 3, 0);
        tabHeadersControl1.Name = "tabHeadersControl1";
        tabHeadersControl1.Padding = new Padding(5);
        tabHeadersControl1.ShowCloseButtons = true;
        tabHeadersControl1.ShowNewTabButton = true;
        tabHeadersControl1.Size = new Size(901, 40);
        tabHeadersControl1.SuppressMaxTabWarning = false;
        tabHeadersControl1.TabIndex = 0;
        // 
        // MainWorkspaceTemplate
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(246, 248, 251);
        Controls.Add(workspaceSplitter);
        Name = "MainWorkspaceTemplate";
        Padding = new Padding(18);
        Size = new Size(1533, 817);
        workspaceSplitter.Panel1.ResumeLayout(false);
        workspaceSplitter.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)workspaceSplitter).EndInit();
        workspaceSplitter.ResumeLayout(false);
        timelinePanel.ResumeLayout(false);
        vStackPanel2.ResumeLayout(false);
        rowSearch.ResumeLayout(false);
        virtualRowPanel1.ResumeLayout(false);
        virtualRowPanel3.ResumeLayout(false);
        vStackPanel3.ResumeLayout(false);
        virtualRowPanel4.ResumeLayout(false);
        virtualRowPanel5.ResumeLayout(false);
        rightHost.ResumeLayout(false);
        vStackPanel1.ResumeLayout(false);
        rowNavigation.ResumeLayout(false);
        virtualRowPanel2.ResumeLayout(false);
        rowTabHeader.ResumeLayout(false);
        ResumeLayout(false);
    }

    private SplitContainer workspaceSplitter;
    private Panel timelinePanel;
    private ListView timelineList;
    private ColumnHeader whenColumn;
    private ColumnHeader typeColumn;
    private ColumnHeader codeColumn;
    private ColumnHeader meaningColumn;
    private ColumnHeader sourceColumn;
    private Panel rightHost;
    private ProfessorSnowsVideoDownloader.CustomControls.vStackPanel vStackPanel1;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel rowNavigation;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton btnBack;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton btnRefresh;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel virtualRowPanel2;
    private ProfessorSnowsVideoDownloader.CustomControls.RoundedTextBox virtualIconButton1;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton incBtnGo;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton incBtnPaste;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel rowTabHeader;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveLineBreak virtualIconButton2;
    private ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl.TabHeadersControl tabHeadersControl1;
    private ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl.TabContentControl tabContentControl1;
    private ProfessorSnowsVideoDownloader.CustomControls.vStackPanel vStackPanel2;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel virtualRowPanel1;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel virtualRowPanel3;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel virtualIconButton3;
    private ProfessorSnowsVideoDownloader.CustomControls.vStackPanel vStackPanel3;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel virtualRowPanel4;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel virtualIconButton4;
    private ProfessorSnowsVideoDownloader.CustomControls.RoundedComboBox virtualIconButton5;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel lbl6;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel NumbericUpdownValue;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel lbl7;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel virtualRowPanel5;
    private ProfessorSnowsVideoDownloader.CustomControls.RoundedNumericTextBox roundedNumericTextBox1;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveLineBreak virtualIconButton6;
    private ProfessorSnowsVideoDownloader.CustomControls.RoundedComboBox virtualIconButton7;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveRowLabel virtualIconButton8;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel rowSearch;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton vBtnPaste;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualAdaptiveLineBreak virtualIconButton10;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualIconButton vBtnSearch;
    private ProfessorSnowsVideoDownloader.CustomControls.RoundedTextBox roundedTextBox1;
}
