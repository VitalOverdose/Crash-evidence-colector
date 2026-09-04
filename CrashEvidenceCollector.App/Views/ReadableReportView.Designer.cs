namespace CrashEvidenceCollector.App.Views;

partial class ReadableReportView
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        toolbarRow = new FlowLayoutPanel();
        copyTextButton = new Button();
        copyJsonButton = new Button();
        saveTextButton = new Button();
        printButton = new Button();
        searchButton = new Button();
        reportHost = new Panel();
        toolbarRow.SuspendLayout();
        SuspendLayout();
        //
        // toolbarRow
        //
        // Plain buttons in a flow row: intended to be re-wrapped as an ARP row with
        // VirtualModernButtons. Behaviour is bound to the fields, not the layout.
        //
        toolbarRow.Controls.Add(copyTextButton);
        toolbarRow.Controls.Add(copyJsonButton);
        toolbarRow.Controls.Add(saveTextButton);
        toolbarRow.Controls.Add(printButton);
        toolbarRow.Controls.Add(searchButton);
        toolbarRow.Dock = DockStyle.Top;
        toolbarRow.Location = new Point(0, 0);
        toolbarRow.Name = "toolbarRow";
        toolbarRow.Padding = new Padding(6);
        toolbarRow.Size = new Size(780, 48);
        toolbarRow.TabIndex = 0;
        toolbarRow.WrapContents = true;
        //
        // toolbar buttons
        //
        copyTextButton.AutoSize = true;
        copyTextButton.Name = "copyTextButton";
        copyTextButton.TabIndex = 0;
        copyTextButton.Text = "Copy report text";
        copyTextButton.UseVisualStyleBackColor = true;
        copyJsonButton.AutoSize = true;
        copyJsonButton.Name = "copyJsonButton";
        copyJsonButton.TabIndex = 1;
        copyJsonButton.Text = "Copy JSON";
        copyJsonButton.UseVisualStyleBackColor = true;
        saveTextButton.AutoSize = true;
        saveTextButton.Name = "saveTextButton";
        saveTextButton.TabIndex = 2;
        saveTextButton.Text = "Save text as…";
        saveTextButton.UseVisualStyleBackColor = true;
        printButton.AutoSize = true;
        printButton.Name = "printButton";
        printButton.TabIndex = 3;
        printButton.Text = "Print / save PDF";
        printButton.UseVisualStyleBackColor = true;
        searchButton.AutoSize = true;
        searchButton.Name = "searchButton";
        searchButton.TabIndex = 4;
        searchButton.Text = "Search copied text";
        searchButton.UseVisualStyleBackColor = true;
        //
        // reportHost
        //
        // The report view owns a WebView2 and is created at runtime, so the designer
        // never has to instantiate a browser control.
        //
        reportHost.BackColor = Color.White;
        reportHost.Dock = DockStyle.Fill;
        reportHost.Location = new Point(0, 48);
        reportHost.Name = "reportHost";
        reportHost.Size = new Size(780, 432);
        reportHost.TabIndex = 1;
        //
        // ReadableReportView
        //
        // AutoScaleMode.None: the shell's VStack/ARP engine owns scaling.
        //
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.White;
        Controls.Add(reportHost);
        Controls.Add(toolbarRow);
        Name = "ReadableReportView";
        Size = new Size(780, 480);
        toolbarRow.ResumeLayout(false);
        toolbarRow.PerformLayout();
        ResumeLayout(false);
    }

    private FlowLayoutPanel toolbarRow;
    private Button copyTextButton;
    private Button copyJsonButton;
    private Button saveTextButton;
    private Button printButton;
    private Button searchButton;
    private Panel reportHost;
}
