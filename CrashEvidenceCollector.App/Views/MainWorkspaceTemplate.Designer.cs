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
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule1 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule2 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule3 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule4 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        ProfessorSnowsVideoDownloader.CustomControls.VStackRule vStackRule5 = new ProfessorSnowsVideoDownloader.CustomControls.VStackRule();
        workspaceSplitter = new SplitContainer();
        timelinePanel = new Panel();
        timelineList = new ListView();
        whenColumn = new ColumnHeader();
        typeColumn = new ColumnHeader();
        codeColumn = new ColumnHeader();
        meaningColumn = new ColumnHeader();
        sourceColumn = new ColumnHeader();
        filterRow = new FlowLayoutPanel();
        showLabel = new Label();
        rangeBox = new ComboBox();
        customLabel = new Label();
        customHours = new NumericUpDown();
        hoursLabel = new Label();
        typeBox = new ComboBox();
        countLabel = new Label();
        headlineLabel = new Label();
        rightHost = new Panel();
        vStackPanel1 = new ProfessorSnowsVideoDownloader.CustomControls.vStackPanel();
        tabContentControl1 = new ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl.TabContentControl();
        RowActions = new ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel();
        bthCollectEvidence = new ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton();
        bthOpenOutputFolder = new ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton();
        btnPastHistory = new ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton();
        btnCopySummary = new ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton();
        btnCancel = new ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton();
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
        panel1 = new ProfessorSnowsVideoDownloader.CustomControls.FlatSeparator();
        ((System.ComponentModel.ISupportInitialize)workspaceSplitter).BeginInit();
        workspaceSplitter.Panel1.SuspendLayout();
        workspaceSplitter.Panel2.SuspendLayout();
        workspaceSplitter.SuspendLayout();
        timelinePanel.SuspendLayout();
        filterRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)customHours).BeginInit();
        rightHost.SuspendLayout();
        vStackPanel1.SuspendLayout();
        RowActions.SuspendLayout();
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
        workspaceSplitter.Size = new Size(1497, 654);
        workspaceSplitter.SplitterDistance = 642;
        workspaceSplitter.SplitterWidth = 7;
        workspaceSplitter.TabIndex = 0;
        // 
        // timelinePanel
        // 
        timelinePanel.BackColor = Color.White;
        timelinePanel.Controls.Add(timelineList);
        timelinePanel.Controls.Add(filterRow);
        timelinePanel.Controls.Add(headlineLabel);
        timelinePanel.Dock = DockStyle.Fill;
        timelinePanel.Location = new Point(0, 0);
        timelinePanel.Name = "timelinePanel";
        timelinePanel.Padding = new Padding(14);
        timelinePanel.Size = new Size(642, 654);
        timelinePanel.TabIndex = 0;
        // 
        // timelineList
        // 
        timelineList.BorderStyle = BorderStyle.None;
        timelineList.Columns.AddRange(new ColumnHeader[] { whenColumn, typeColumn, codeColumn, meaningColumn, sourceColumn });
        timelineList.Dock = DockStyle.Fill;
        timelineList.FullRowSelect = true;
        timelineList.Location = new Point(14, 130);
        timelineList.MultiSelect = false;
        timelineList.Name = "timelineList";
        timelineList.Size = new Size(614, 510);
        timelineList.TabIndex = 2;
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
        // filterRow
        // 
        filterRow.Controls.Add(showLabel);
        filterRow.Controls.Add(rangeBox);
        filterRow.Controls.Add(customLabel);
        filterRow.Controls.Add(customHours);
        filterRow.Controls.Add(hoursLabel);
        filterRow.Controls.Add(typeBox);
        filterRow.Controls.Add(countLabel);
        filterRow.Dock = DockStyle.Top;
        filterRow.Location = new Point(14, 58);
        filterRow.Name = "filterRow";
        filterRow.Size = new Size(614, 72);
        filterRow.TabIndex = 1;
        // 
        // showLabel
        // 
        showLabel.AutoSize = true;
        showLabel.Location = new Point(3, 0);
        showLabel.Name = "showLabel";
        showLabel.Padding = new Padding(0, 7, 6, 0);
        showLabel.Size = new Size(42, 22);
        showLabel.TabIndex = 0;
        showLabel.Text = "Show";
        // 
        // rangeBox
        // 
        rangeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        rangeBox.Location = new Point(51, 3);
        rangeBox.Name = "rangeBox";
        rangeBox.Size = new Size(150, 23);
        rangeBox.TabIndex = 1;
        // 
        // customLabel
        // 
        customLabel.AutoSize = true;
        customLabel.Location = new Point(207, 0);
        customLabel.Name = "customLabel";
        customLabel.Padding = new Padding(8, 7, 2, 0);
        customLabel.Size = new Size(62, 22);
        customLabel.TabIndex = 2;
        customLabel.Text = "Custom:";
        // 
        // customHours
        // 
        customHours.DecimalPlaces = 1;
        customHours.Location = new Point(275, 3);
        customHours.Name = "customHours";
        customHours.Size = new Size(70, 23);
        customHours.TabIndex = 3;
        // 
        // hoursLabel
        // 
        hoursLabel.AutoSize = true;
        hoursLabel.Location = new Point(351, 0);
        hoursLabel.Name = "hoursLabel";
        hoursLabel.Padding = new Padding(0, 7, 8, 0);
        hoursLabel.Size = new Size(45, 22);
        hoursLabel.TabIndex = 4;
        hoursLabel.Text = "hours";
        // 
        // typeBox
        // 
        typeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        typeBox.Location = new Point(3, 32);
        typeBox.Name = "typeBox";
        typeBox.Size = new Size(220, 23);
        typeBox.TabIndex = 5;
        // 
        // countLabel
        // 
        countLabel.AutoSize = true;
        countLabel.ForeColor = Color.FromArgb(75, 85, 99);
        countLabel.Location = new Point(229, 29);
        countLabel.Name = "countLabel";
        countLabel.Padding = new Padding(8, 7, 0, 0);
        countLabel.Size = new Size(59, 22);
        countLabel.TabIndex = 6;
        countLabel.Text = "0 shown";
        // 
        // headlineLabel
        // 
        headlineLabel.Dock = DockStyle.Top;
        headlineLabel.Font = new Font("Segoe UI Semibold", 18F);
        headlineLabel.Location = new Point(14, 14);
        headlineLabel.Name = "headlineLabel";
        headlineLabel.Size = new Size(614, 44);
        headlineLabel.TabIndex = 0;
        headlineLabel.Text = "Checking recent crash evidence";
        // 
        // rightHost
        // 
        rightHost.BackColor = Color.FromArgb(246, 248, 251);
        rightHost.Controls.Add(vStackPanel1);
        rightHost.Dock = DockStyle.Fill;
        rightHost.Location = new Point(0, 0);
        rightHost.Name = "rightHost";
        rightHost.Padding = new Padding(4, 0, 0, 0);
        rightHost.Size = new Size(848, 654);
        rightHost.TabIndex = 0;
        // 
        // vStackPanel1
        // 
        vStackPanel1.Controls.Add(tabContentControl1);
        vStackPanel1.Controls.Add(RowActions);
        vStackPanel1.Controls.Add(rowNavigation);
        vStackPanel1.Controls.Add(rowTabHeader);
        vStackPanel1.Controls.Add(panel1);
        vStackPanel1.Dock = DockStyle.Fill;
        vStackPanel1.Location = new Point(4, 0);
        vStackPanel1.Name = "vStackPanel1";
        vStackPanel1.Padding = new Padding(6);
        vStackRule1.CollapsedHeight = 36;
        vStackRule1.ControlName = "RowActions";
        vStackRule1.ExpandedHeight = 42;
        vStackRule1.HasCustomCollapsedHeight = true;
        vStackRule1.Id = "88a9385715f64955879e181a14c5d7ce";
        vStackRule1.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackRule2.CollapsedHeight = 7;
        vStackRule2.ControlName = "panel1";
        vStackRule2.ExpandedHeight = 7;
        vStackRule2.FontBaselineSizeInPoints = 9F;
        vStackRule2.HasCustomCollapsedHeight = true;
        vStackRule2.Id = "f44b61fcb604476fa6ef816d67815367";
        vStackRule2.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.FlatSeparator;
        vStackRule3.CollapsedHeight = 36;
        vStackRule3.ControlName = "rowTabHeader";
        vStackRule3.ExpandedHeight = 49;
        vStackRule3.HasCustomCollapsedHeight = true;
        vStackRule3.Id = "ace8406a5f5640d2897ea57a67c495d3";
        vStackRule3.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackRule4.CollapsedHeight = 0;
        vStackRule4.ControlName = "rowNavigation";
        vStackRule4.ExpandedHeight = 42;
        vStackRule4.HasCustomCollapsedHeight = true;
        vStackRule4.Id = "ddd34f60260949989fb02a29c0aa0ab5";
        vStackRule4.ItemType = ProfessorSnowsVideoDownloader.CustomControls.VStackItemType.VirtualizingAdaptiveRowPanel;
        vStackRule5.BottomMargin = 3;
        vStackRule5.CollapsedHeight = 448;
        vStackRule5.ControlName = "TabContentControl";
        vStackRule5.ExpandedHeight = 491;
        vStackRule5.HasCustomCollapsedHeight = true;
        vStackRule5.Id = "3b3a59976ad14e4b8c3c8b63e4e55fd3";
        vStackRule5.SpecialLayout = ProfessorSnowsVideoDownloader.CustomControls.VStackSpecialLayout.Spring;
        vStackRule5.TopMargin = 3;
        vStackPanel1.Rules.Add(vStackRule1);
        vStackPanel1.Rules.Add(vStackRule2);
        vStackPanel1.Rules.Add(vStackRule3);
        vStackPanel1.Rules.Add(vStackRule4);
        vStackPanel1.Rules.Add(vStackRule5);
        vStackPanel1.Size = new Size(844, 654);
        vStackPanel1.Spring = "TabContentControl";
        vStackPanel1.TabIndex = 0;
        // 
        // tabContentControl1
        // 
        tabContentControl1.Location = new Point(9, 165);
        tabContentControl1.Name = "tabContentControl1";
        tabContentControl1.SelectedIndex = -1;
        tabContentControl1.Size = new Size(826, 480);
        tabContentControl1.TabIndex = 10;
        tabContentControl1.Tag = "3b3a59976ad14e4b8c3c8b63e4e55fd3";
        // 
        // RowActions
        // 
        RowActions.BackColor = Color.FromArgb(246, 248, 251);
        RowActions.Controls.Add(bthCollectEvidence);
        RowActions.Controls.Add(bthOpenOutputFolder);
        RowActions.Controls.Add(btnPastHistory);
        RowActions.Controls.Add(btnCopySummary);
        RowActions.Controls.Add(btnCancel);
        RowActions.Location = new Point(9, 6);
        RowActions.Margin = new Padding(3, 0, 3, 4);
        RowActions.Name = "RowActions";
        RowActions.Padding = new Padding(1);
        RowActions.Size = new Size(826, 42);
        RowActions.TabIndex = 2;
        RowActions.TabStop = true;
        RowActions.Tag = "88a9385715f64955879e181a14c5d7ce";
        RowActions.ToolTipSource = null;
        // 
        // bthCollectEvidence
        // 
        bthCollectEvidence.BackColor = Color.Transparent;
        bthCollectEvidence.BackgroundColor = Color.White;
        bthCollectEvidence.BorderColor = Color.FromArgb(150, 150, 150);
        bthCollectEvidence.BorderRadius = 7;
        bthCollectEvidence.BorderSize = 2;
        bthCollectEvidence.ButtonImage = null;
        bthCollectEvidence.FlatAppearance.BorderSize = 0;
        bthCollectEvidence.FlatAppearance.MouseDownBackColor = Color.Transparent;
        bthCollectEvidence.FlatAppearance.MouseOverBackColor = Color.Transparent;
        bthCollectEvidence.FlatStyle = FlatStyle.Flat;
        bthCollectEvidence.Font = new Font("Segoe UI", 10F);
        bthCollectEvidence.ForeColor = Color.FromArgb(60, 60, 60);
        bthCollectEvidence.HoverBorderColor = Color.Gray;
        bthCollectEvidence.HoverBorderSize = 2;
        bthCollectEvidence.HoverColor = Color.WhiteSmoke;
        bthCollectEvidence.ImageSize = new Size(24, 24);
        bthCollectEvidence.Location = new Point(5, 4);
        bthCollectEvidence.Margin = new Padding(4, 3, 4, 3);
        bthCollectEvidence.Name = "bthCollectEvidence";
        bthCollectEvidence.PressedBorderColor = Color.Gray;
        bthCollectEvidence.PressedBorderSize = 2;
        bthCollectEvidence.PressedColor = Color.Gainsboro;
        bthCollectEvidence.Size = new Size(130, 34);
        bthCollectEvidence.TabIndex = 0;
        bthCollectEvidence.Tag = "arp:17654e8702ae48c0b931095a2808af63";
        bthCollectEvidence.Text = "Collect evidence";
        bthCollectEvidence.TextColor = Color.FromArgb(60, 60, 60);
        bthCollectEvidence.UseVisualStyleBackColor = false;
        // 
        // bthOpenOutputFolder
        // 
        bthOpenOutputFolder.BackColor = Color.Transparent;
        bthOpenOutputFolder.BackgroundColor = Color.White;
        bthOpenOutputFolder.BorderColor = Color.FromArgb(150, 150, 150);
        bthOpenOutputFolder.BorderRadius = 7;
        bthOpenOutputFolder.BorderSize = 2;
        bthOpenOutputFolder.ButtonImage = null;
        bthOpenOutputFolder.FlatAppearance.BorderSize = 0;
        bthOpenOutputFolder.FlatAppearance.MouseDownBackColor = Color.Transparent;
        bthOpenOutputFolder.FlatAppearance.MouseOverBackColor = Color.Transparent;
        bthOpenOutputFolder.FlatStyle = FlatStyle.Flat;
        bthOpenOutputFolder.Font = new Font("Segoe UI", 10F);
        bthOpenOutputFolder.ForeColor = Color.FromArgb(60, 60, 60);
        bthOpenOutputFolder.HoverBorderColor = Color.Gray;
        bthOpenOutputFolder.HoverBorderSize = 2;
        bthOpenOutputFolder.HoverColor = Color.WhiteSmoke;
        bthOpenOutputFolder.ImageSize = new Size(24, 24);
        bthOpenOutputFolder.Location = new Point(143, 4);
        bthOpenOutputFolder.Margin = new Padding(4, 3, 4, 3);
        bthOpenOutputFolder.Name = "bthOpenOutputFolder";
        bthOpenOutputFolder.PressedBorderColor = Color.Gray;
        bthOpenOutputFolder.PressedBorderSize = 2;
        bthOpenOutputFolder.PressedColor = Color.Gainsboro;
        bthOpenOutputFolder.Size = new Size(150, 34);
        bthOpenOutputFolder.TabIndex = 1;
        bthOpenOutputFolder.Tag = "arp:5cbd5843fee34875b1742d1856536842";
        bthOpenOutputFolder.Text = "Open output folder";
        bthOpenOutputFolder.TextColor = Color.FromArgb(60, 60, 60);
        bthOpenOutputFolder.UseVisualStyleBackColor = false;
        // 
        // btnPastHistory
        // 
        btnPastHistory.BackColor = Color.Transparent;
        btnPastHistory.BackgroundColor = Color.White;
        btnPastHistory.BorderColor = Color.FromArgb(150, 150, 150);
        btnPastHistory.BorderRadius = 7;
        btnPastHistory.BorderSize = 2;
        btnPastHistory.ButtonImage = null;
        btnPastHistory.FlatAppearance.BorderSize = 0;
        btnPastHistory.FlatAppearance.MouseDownBackColor = Color.Transparent;
        btnPastHistory.FlatAppearance.MouseOverBackColor = Color.Transparent;
        btnPastHistory.FlatStyle = FlatStyle.Flat;
        btnPastHistory.Font = new Font("Segoe UI", 10F);
        btnPastHistory.ForeColor = Color.FromArgb(60, 60, 60);
        btnPastHistory.HoverBorderColor = Color.Gray;
        btnPastHistory.HoverBorderSize = 2;
        btnPastHistory.HoverColor = Color.WhiteSmoke;
        btnPastHistory.ImageSize = new Size(24, 24);
        btnPastHistory.Location = new Point(301, 4);
        btnPastHistory.Margin = new Padding(4, 3, 4, 3);
        btnPastHistory.Name = "btnPastHistory";
        btnPastHistory.PressedBorderColor = Color.Gray;
        btnPastHistory.PressedBorderSize = 2;
        btnPastHistory.PressedColor = Color.Gainsboro;
        btnPastHistory.Size = new Size(150, 34);
        btnPastHistory.TabIndex = 2;
        btnPastHistory.Tag = "arp:2ab97d23876243949f96503b86292556";
        btnPastHistory.Text = "Review past history";
        btnPastHistory.TextColor = Color.FromArgb(60, 60, 60);
        btnPastHistory.UseVisualStyleBackColor = false;
        // 
        // btnCopySummary
        // 
        btnCopySummary.BackColor = Color.Transparent;
        btnCopySummary.BackgroundColor = Color.White;
        btnCopySummary.BorderColor = Color.FromArgb(150, 150, 150);
        btnCopySummary.BorderRadius = 7;
        btnCopySummary.BorderSize = 2;
        btnCopySummary.ButtonImage = null;
        btnCopySummary.FlatAppearance.BorderSize = 0;
        btnCopySummary.FlatAppearance.MouseDownBackColor = Color.Transparent;
        btnCopySummary.FlatAppearance.MouseOverBackColor = Color.Transparent;
        btnCopySummary.FlatStyle = FlatStyle.Flat;
        btnCopySummary.Font = new Font("Segoe UI", 10F);
        btnCopySummary.ForeColor = Color.FromArgb(60, 60, 60);
        btnCopySummary.HoverBorderColor = Color.Gray;
        btnCopySummary.HoverBorderSize = 2;
        btnCopySummary.HoverColor = Color.WhiteSmoke;
        btnCopySummary.ImageSize = new Size(24, 24);
        btnCopySummary.Location = new Point(459, 4);
        btnCopySummary.Margin = new Padding(4, 3, 4, 3);
        btnCopySummary.Name = "btnCopySummary";
        btnCopySummary.PressedBorderColor = Color.Gray;
        btnCopySummary.PressedBorderSize = 2;
        btnCopySummary.PressedColor = Color.Gainsboro;
        btnCopySummary.Size = new Size(150, 34);
        btnCopySummary.TabIndex = 3;
        btnCopySummary.Tag = "arp:44aaa53afa704fa4b0c39e1008babb9a";
        btnCopySummary.Text = "Copy summary";
        btnCopySummary.TextColor = Color.FromArgb(60, 60, 60);
        btnCopySummary.UseVisualStyleBackColor = false;
        // 
        // btnCancel
        // 
        btnCancel.BackColor = Color.Transparent;
        btnCancel.BackgroundColor = Color.White;
        btnCancel.BorderColor = Color.FromArgb(150, 150, 150);
        btnCancel.BorderRadius = 7;
        btnCancel.BorderSize = 2;
        btnCancel.ButtonImage = null;
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.FlatAppearance.MouseDownBackColor = Color.Transparent;
        btnCancel.FlatAppearance.MouseOverBackColor = Color.Transparent;
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.ForeColor = Color.FromArgb(60, 60, 60);
        btnCancel.HoverBorderColor = Color.Gray;
        btnCancel.HoverBorderSize = 2;
        btnCancel.HoverColor = Color.WhiteSmoke;
        btnCancel.ImageSize = new Size(24, 24);
        btnCancel.Location = new Point(617, 4);
        btnCancel.Margin = new Padding(4, 3, 4, 3);
        btnCancel.Name = "btnCancel";
        btnCancel.PressedBorderColor = Color.Gray;
        btnCancel.PressedBorderSize = 2;
        btnCancel.PressedColor = Color.Gainsboro;
        btnCancel.Size = new Size(70, 34);
        btnCancel.TabIndex = 4;
        btnCancel.Text = "Cancel";
        btnCancel.TextColor = Color.FromArgb(60, 60, 60);
        btnCancel.UseVisualStyleBackColor = false;
        // 
        // rowNavigation
        // 
        rowNavigation.Controls.Add(btnBack);
        rowNavigation.Controls.Add(btnRefresh);
        rowNavigation.Controls.Add(virtualRowPanel2);
        rowNavigation.Location = new Point(9, 116);
        rowNavigation.Margin = new Padding(3, 0, 3, 4);
        rowNavigation.Name = "rowNavigation";
        rowNavigation.Padding = new Padding(4, 0, 4, 0);
        rowNavigation.Size = new Size(826, 42);
        rowNavigation.SpringControlName = "virtualRowPanel2";
        rowNavigation.TabIndex = 8;
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
        virtualRowPanel2.BorderColor = Color.FromArgb(150, 150, 150);
        virtualRowPanel2.BorderThickness = 2;
        virtualRowPanel2.Controls.Add(virtualIconButton1);
        virtualRowPanel2.Controls.Add(incBtnGo);
        virtualRowPanel2.Controls.Add(incBtnPaste);
        virtualRowPanel2.Controls.Add(virtualIconButton2);
        virtualRowPanel2.CornerRadius = 10;
        virtualRowPanel2.Location = new Point(95, 2);
        virtualRowPanel2.Margin = new Padding(3, 0, 3, 4);
        virtualRowPanel2.Name = "virtualRowPanel2";
        virtualRowPanel2.Padding = new Padding(4, 0, 4, 0);
        virtualRowPanel2.Size = new Size(724, 38);
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
        virtualIconButton1.Location = new Point(8, 5);
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
        virtualIconButton1.Size = new Size(623, 28);
        virtualIconButton1.TabIndex = 0;
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
        incBtnGo.Location = new Point(688, 5);
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
        incBtnPaste.Location = new Point(652, 5);
        incBtnPaste.Margin = new Padding(4, 0, 4, 0);
        incBtnPaste.Name = "incBtnPaste";
        incBtnPaste.Size = new Size(28, 28);
        incBtnPaste.TabIndex = 2;
        incBtnPaste.TextColor = Color.FromArgb(60, 60, 60);
        // 
        // virtualIconButton2
        // 
        virtualIconButton2.BackColor = Color.Transparent;
        virtualIconButton2.LineColor = Color.FromArgb(200, 200, 200);
        virtualIconButton2.LineThickness = 2;
        virtualIconButton2.Location = new Point(639, 4);
        virtualIconButton2.Margin = new Padding(4, 0, 4, 0);
        virtualIconButton2.Name = "virtualIconButton2";
        virtualIconButton2.Size = new Size(5, 30);
        virtualIconButton2.TabIndex = 1;
        virtualIconButton2.TabStop = false;
        // 
        // rowTabHeader
        // 
        rowTabHeader.Controls.Add(tabHeadersControl1);
        rowTabHeader.Location = new Point(9, 63);
        rowTabHeader.Margin = new Padding(3, 0, 3, 4);
        rowTabHeader.Name = "rowTabHeader";
        rowTabHeader.Padding = new Padding(4, 0, 4, 0);
        rowTabHeader.Size = new Size(826, 49);
        rowTabHeader.SpringControlName = "tabHeadersControl1";
        rowTabHeader.TabIndex = 6;
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
        tabHeadersControl1.Size = new Size(812, 40);
        tabHeadersControl1.SuppressMaxTabWarning = false;
        tabHeadersControl1.TabIndex = 0;
        // 
        // panel1
        // 
        panel1.BackColor = Color.FromArgb(242, 242, 242);
        panel1.ForeColor = Color.FromArgb(150, 150, 150);
        panel1.HoverLineColor = Color.FromArgb(70, 130, 220);
        panel1.LineColor = Color.FromArgb(130, 130, 130);
        panel1.LineThickness = 2;
        panel1.Location = new Point(12, 52);
        panel1.Margin = new Padding(6, 0, 6, 4);
        panel1.MinimumSize = new Size(8, 0);
        panel1.Name = "panel1";
        panel1.SeparatorHeight = 8;
        panel1.Size = new Size(820, 7);
        panel1.TabIndex = 4;
        panel1.Tag = "f44b61fcb604476fa6ef816d67815367";
        // 
        // MainWorkspaceTemplate
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(246, 248, 251);
        Controls.Add(workspaceSplitter);
        Name = "MainWorkspaceTemplate";
        Padding = new Padding(18);
        Size = new Size(1533, 690);
        workspaceSplitter.Panel1.ResumeLayout(false);
        workspaceSplitter.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)workspaceSplitter).EndInit();
        workspaceSplitter.ResumeLayout(false);
        timelinePanel.ResumeLayout(false);
        filterRow.ResumeLayout(false);
        filterRow.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)customHours).EndInit();
        rightHost.ResumeLayout(false);
        vStackPanel1.ResumeLayout(false);
        RowActions.ResumeLayout(false);
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
    private FlowLayoutPanel filterRow;
    private Label showLabel;
    private ComboBox rangeBox;
    private Label customLabel;
    private NumericUpDown customHours;
    private Label hoursLabel;
    private ComboBox typeBox;
    private Label countLabel;
    private Label headlineLabel;
    private Panel rightHost;
    private ProfessorSnowsVideoDownloader.CustomControls.vStackPanel vStackPanel1;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualizingAdaptiveRowPanel RowActions;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton bthCollectEvidence;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton bthOpenOutputFolder;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton btnPastHistory;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton btnCopySummary;
    private ProfessorSnowsVideoDownloader.CustomControls.VirtualModernButton btnCancel;
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
    private ProfessorSnowsVideoDownloader.CustomControls.FlatSeparator panel1;
}
