namespace CrashEvidenceCollector.App.Views;

partial class EvidenceStatusView
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        evidenceList = new ListView();
        categoryColumn = new ColumnHeader();
        statusColumn = new ColumnHeader();
        detailColumn = new ColumnHeader();
        SuspendLayout();
        // 
        // evidenceList
        // 
        evidenceList.BorderStyle = BorderStyle.None;
        evidenceList.Columns.AddRange(new ColumnHeader[] { categoryColumn, statusColumn, detailColumn });
        evidenceList.Dock = DockStyle.Fill;
        evidenceList.FullRowSelect = true;
        evidenceList.Location = new Point(10, 10);
        evidenceList.Name = "evidenceList";
        evidenceList.Size = new Size(760, 460);
        evidenceList.TabIndex = 0;
        evidenceList.UseCompatibleStateImageBehavior = false;
        evidenceList.View = View.Details;
        // 
        // categoryColumn
        // 
        categoryColumn.Text = "Category";
        categoryColumn.Width = 245;
        // 
        // statusColumn
        // 
        statusColumn.Text = "Status";
        statusColumn.Width = 110;
        // 
        // detailColumn
        // 
        detailColumn.Text = "Details";
        detailColumn.Width = 590;
        // 
        // EvidenceStatusView
        // 
        // AutoScaleMode.None: the shell's VStack/ARP engine owns scaling. Letting
        // WinForms autoscale here would fight it and double-scale the contents.
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.White;
        Controls.Add(evidenceList);
        Name = "EvidenceStatusView";
        Padding = new Padding(10);
        Size = new Size(780, 480);
        ResumeLayout(false);
    }

    private ListView evidenceList;
    private ColumnHeader categoryColumn;
    private ColumnHeader statusColumn;
    private ColumnHeader detailColumn;
}
