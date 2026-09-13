#nullable enable

namespace CrashEvidenceCollector.App;

public sealed partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;
    private Panel navigationPanel = null!;
    private Label productNameLabel = null!;
    private Label productCaptionLabel = null!;
    private Panel navigationDivider = null!;
    private Button commandCenterButton = null!;
    private Button summaryIntelligenceButton = null!;
    private Button systemSettingsButton = null!;
    private Label collectionSafetyLabel = null!;
    private Label _modeBanner = null!;
    private TabControl _pages = null!;
    private Views.MainWorkspaceTemplate _workspace = null!;
    private TabPage commandCenterDesignPage = null!;
    private TabPage summaryDesignPage = null!;
    private TabPage settingsDesignPage = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        navigationPanel = new Panel();
        collectionSafetyLabel = new Label();
        systemSettingsButton = new CrashEvidenceCollector.Theming.ThemedFlatButton();
        summaryIntelligenceButton = new CrashEvidenceCollector.Theming.ThemedFlatButton();
        commandCenterButton = new CrashEvidenceCollector.Theming.ThemedFlatButton();
        navigationDivider = new Panel();
        productCaptionLabel = new Label();
        productNameLabel = new Label();
        _modeBanner = new Label();
        _pages = new TabControl();
        commandCenterDesignPage = new TabPage();
        _workspace = new Views.MainWorkspaceTemplate();
        summaryDesignPage = new TabPage();
        settingsDesignPage = new TabPage();
        navigationPanel.SuspendLayout();
        _pages.SuspendLayout();
        SuspendLayout();
        // 
        // navigationPanel
        // 
        navigationPanel.BackColor = Color.FromArgb(13, 24, 39);
        navigationPanel.Controls.Add(collectionSafetyLabel);
        navigationPanel.Controls.Add(systemSettingsButton);
        navigationPanel.Controls.Add(summaryIntelligenceButton);
        navigationPanel.Controls.Add(commandCenterButton);
        navigationPanel.Controls.Add(navigationDivider);
        navigationPanel.Controls.Add(productCaptionLabel);
        navigationPanel.Controls.Add(productNameLabel);
        navigationPanel.Dock = DockStyle.Left;
        navigationPanel.Location = new Point(0, 0);
        navigationPanel.Margin = new Padding(2, 2, 2, 2);
        navigationPanel.Name = "navigationPanel";
        navigationPanel.Padding = new Padding(10, 12, 10, 9);
        navigationPanel.Size = new Size(240, 624);
        navigationPanel.TabIndex = 2;
        // 
        // collectionSafetyLabel
        // 
        collectionSafetyLabel.Dock = DockStyle.Bottom;
        collectionSafetyLabel.Font = new Font("Segoe UI", 11F);
        collectionSafetyLabel.ForeColor = Color.FromArgb(153, 179, 207);
        collectionSafetyLabel.Location = new Point(10, 553);
        collectionSafetyLabel.Margin = new Padding(2, 0, 2, 0);
        collectionSafetyLabel.Name = "collectionSafetyLabel";
        collectionSafetyLabel.Size = new Size(220, 130);
        collectionSafetyLabel.TabIndex = 0;
        collectionSafetyLabel.Text = "●  COLLECTION SAFE\r\n\r\nRead-only analysis. No drivers, services, registry settings, or crash configuration are changed.";
        // 
        // systemSettingsButton
        // 
        systemSettingsButton.BackColor = Color.FromArgb(13, 24, 39);
        systemSettingsButton.Cursor = Cursors.Hand;
        systemSettingsButton.Dock = DockStyle.Top;
        systemSettingsButton.FlatAppearance.BorderSize = 0;
        systemSettingsButton.FlatStyle = FlatStyle.Flat;
        systemSettingsButton.Font = new Font("Segoe UI Semibold", 11F);
        systemSettingsButton.ForeColor = Color.FromArgb(164, 184, 207);
        systemSettingsButton.Location = new Point(10, 147);
        systemSettingsButton.Margin = new Padding(2, 2, 2, 2);
        systemSettingsButton.Name = "systemSettingsButton";
        systemSettingsButton.Padding = new Padding(9, 0, 0, 0);
        systemSettingsButton.Size = new Size(220, 44);
        systemSettingsButton.TabIndex = 1;
        systemSettingsButton.Text = "SYSTEM SETTINGS";
        systemSettingsButton.TextAlign = ContentAlignment.MiddleLeft;
        systemSettingsButton.UseVisualStyleBackColor = false;
        // 
        // summaryIntelligenceButton
        // 
        summaryIntelligenceButton.BackColor = Color.FromArgb(13, 24, 39);
        summaryIntelligenceButton.Cursor = Cursors.Hand;
        summaryIntelligenceButton.Dock = DockStyle.Top;
        summaryIntelligenceButton.FlatAppearance.BorderSize = 0;
        summaryIntelligenceButton.FlatStyle = FlatStyle.Flat;
        summaryIntelligenceButton.Font = new Font("Segoe UI Semibold", 11F);
        summaryIntelligenceButton.ForeColor = Color.FromArgb(164, 184, 207);
        summaryIntelligenceButton.Location = new Point(10, 113);
        summaryIntelligenceButton.Margin = new Padding(2, 2, 2, 2);
        summaryIntelligenceButton.Name = "summaryIntelligenceButton";
        summaryIntelligenceButton.Padding = new Padding(9, 0, 0, 0);
        summaryIntelligenceButton.Size = new Size(220, 44);
        summaryIntelligenceButton.TabIndex = 2;
        summaryIntelligenceButton.Text = "SUMMARY INTELLIGENCE";
        summaryIntelligenceButton.TextAlign = ContentAlignment.MiddleLeft;
        summaryIntelligenceButton.UseVisualStyleBackColor = false;
        // 
        // commandCenterButton
        // 
        commandCenterButton.BackColor = Color.FromArgb(24, 42, 64);
        commandCenterButton.Cursor = Cursors.Hand;
        commandCenterButton.Dock = DockStyle.Top;
        commandCenterButton.FlatAppearance.BorderColor = Color.FromArgb(37, 112, 202);
        commandCenterButton.FlatStyle = FlatStyle.Flat;
        commandCenterButton.Font = new Font("Segoe UI Semibold", 11F);
        commandCenterButton.ForeColor = Color.White;
        commandCenterButton.Location = new Point(10, 79);
        commandCenterButton.Margin = new Padding(2, 2, 2, 2);
        commandCenterButton.Name = "commandCenterButton";
        commandCenterButton.Padding = new Padding(9, 0, 0, 0);
        commandCenterButton.Size = new Size(220, 44);
        commandCenterButton.TabIndex = 3;
        commandCenterButton.Text = "COMMAND CENTER";
        commandCenterButton.TextAlign = ContentAlignment.MiddleLeft;
        commandCenterButton.UseVisualStyleBackColor = false;
        // 
        // navigationDivider
        // 
        navigationDivider.BackColor = Color.FromArgb(48, 70, 95);
        navigationDivider.Dock = DockStyle.Top;
        navigationDivider.Location = new Point(10, 71);
        navigationDivider.Margin = new Padding(2, 2, 2, 2);
        navigationDivider.Name = "navigationDivider";
        navigationDivider.Size = new Size(220, 8);
        navigationDivider.TabIndex = 4;
        // 
        // productCaptionLabel
        // 
        productCaptionLabel.Dock = DockStyle.Top;
        productCaptionLabel.Font = new Font("Segoe UI", 11F);
        productCaptionLabel.ForeColor = Color.FromArgb(153, 179, 207);
        productCaptionLabel.Location = new Point(10, 50);
        productCaptionLabel.Margin = new Padding(2, 0, 2, 0);
        productCaptionLabel.Name = "productCaptionLabel";
        productCaptionLabel.Padding = new Padding(1, 3, 0, 0);
        productCaptionLabel.Size = new Size(220, 28);
        productCaptionLabel.TabIndex = 5;
        productCaptionLabel.Text = "INCIDENT ANALYSIS WORKSPACE";
        // 
        // productNameLabel
        // 
        productNameLabel.Dock = DockStyle.Top;
        productNameLabel.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
        productNameLabel.ForeColor = Color.White;
        productNameLabel.Location = new Point(10, 12);
        productNameLabel.Margin = new Padding(2, 0, 2, 0);
        productNameLabel.Name = "productNameLabel";
        productNameLabel.Size = new Size(220, 72);
        productNameLabel.TabIndex = 6;
        productNameLabel.Text = "CRASH EVIDENCE\r\nCOLLECTOR";
        // 
        // _modeBanner
        // 
        _modeBanner.BackColor = Color.FromArgb(255, 238, 184);
        _modeBanner.Dock = DockStyle.Top;
        _modeBanner.ForeColor = Color.FromArgb(95, 63, 0);
        _modeBanner.Location = new Point(240, 0);
        _modeBanner.Margin = new Padding(2, 0, 2, 0);
        _modeBanner.Name = "_modeBanner";
        _modeBanner.Size = new Size(1360, 34);
        _modeBanner.TabIndex = 1;
        _modeBanner.Text = "MODE / STATUS BANNER — shown for test-data or administrator mode at runtime";
        _modeBanner.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // _pages
        // 
        _pages.Appearance = TabAppearance.FlatButtons;
        _pages.Controls.Add(commandCenterDesignPage);
        _pages.Controls.Add(summaryDesignPage);
        _pages.Controls.Add(settingsDesignPage);
        _pages.Dock = DockStyle.Fill;
        _pages.ItemSize = new Size(0, 1);
        _pages.Location = new Point(240, 34);
        _pages.Margin = new Padding(2, 2, 2, 2);
        _pages.Name = "_pages";
        _pages.SelectedIndex = 0;
        _pages.Size = new Size(1013, 602);
        _pages.SizeMode = TabSizeMode.Fixed;
        _pages.TabIndex = 0;
        // 
        // commandCenterDesignPage
        // 
        commandCenterDesignPage.BackColor = Color.FromArgb(242, 245, 249);
        commandCenterDesignPage.Controls.Add(_workspace);
        commandCenterDesignPage.Location = new Point(4, 5);
        commandCenterDesignPage.Margin = new Padding(2, 2, 2, 2);
        commandCenterDesignPage.Name = "commandCenterDesignPage";
        commandCenterDesignPage.Size = new Size(1005, 593);
        commandCenterDesignPage.TabIndex = 0;
        commandCenterDesignPage.Text = "Command center";
        // 
        // _workspace
        // 
        _workspace.Dock = DockStyle.Fill;
        _workspace.Location = new Point(0, 0);
        _workspace.Name = "_workspace";
        _workspace.Size = new Size(1005, 593);
        _workspace.TabIndex = 0;
        // 
        // summaryDesignPage
        // 
        summaryDesignPage.Location = new Point(4, 5);
        summaryDesignPage.Margin = new Padding(2, 2, 2, 2);
        summaryDesignPage.Name = "summaryDesignPage";
        summaryDesignPage.Size = new Size(1006, 593);
        summaryDesignPage.TabIndex = 1;
        // 
        // settingsDesignPage
        // 
        settingsDesignPage.Location = new Point(4, 5);
        settingsDesignPage.Margin = new Padding(2, 2, 2, 2);
        settingsDesignPage.Name = "settingsDesignPage";
        settingsDesignPage.Size = new Size(1006, 593);
        settingsDesignPage.TabIndex = 2;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(5F, 11F);
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(242, 245, 249);
        ClientSize = new Size(1600, 900);
        Controls.Add(_pages);
        Controls.Add(_modeBanner);
        Controls.Add(navigationPanel);
        Font = new Font("Segoe UI", 11F);
        Margin = new Padding(2, 2, 2, 2);
        MinimumSize = new Size(1100, 700);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Crash Evidence Collector";
        navigationPanel.ResumeLayout(false);
        _pages.ResumeLayout(false);
        ResumeLayout(false);
    }

    private static void ConfigureNavigationButton(Button button, string name, string text, bool active)
    {
        button.BackColor = active ? Color.FromArgb(24, 42, 64) : Color.FromArgb(13, 24, 39);
        button.Cursor = Cursors.Hand;
        button.Dock = DockStyle.Top;
        button.FlatAppearance.BorderColor = Color.FromArgb(37, 112, 202);
        button.FlatAppearance.BorderSize = active ? 1 : 0;
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Segoe UI Semibold", 11F);
        button.ForeColor = active ? Color.White : Color.FromArgb(164, 184, 207);
        button.Name = name;
        button.Padding = new Padding(12, 0, 0, 0);
        button.Size = new Size(220, 44);
        button.Text = text;
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.UseVisualStyleBackColor = false;
    }

    private static void ConfigurePreviewPage(TabPage page, string name, string message)
    {
        page.BackColor = Color.FromArgb(242, 245, 249);
        page.Name = name;
        page.Padding = new Padding(28);
        page.Text = name;
        var label = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F),
            ForeColor = Color.FromArgb(91, 104, 122),
            Text = message
        };
        page.Controls.Add(label);
    }
}
