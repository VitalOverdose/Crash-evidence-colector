namespace CrashEvidenceCollector.App.Views;

partial class RawDebuggerView
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        outputBox = new RichTextBox();
        SuspendLayout();
        // 
        // outputBox
        // 
        outputBox.BackColor = Color.FromArgb(17, 28, 45);
        outputBox.BorderStyle = BorderStyle.None;
        outputBox.Dock = DockStyle.Fill;
        outputBox.Font = new Font("Consolas", 9F);
        outputBox.ForeColor = Color.FromArgb(220, 232, 248);
        outputBox.Location = new Point(4, 4);
        outputBox.Name = "outputBox";
        outputBox.ReadOnly = true;
        outputBox.Size = new Size(772, 472);
        outputBox.TabIndex = 0;
        outputBox.Text = "";
        outputBox.WordWrap = false;
        // 
        // RawDebuggerView
        // 
        // AutoScaleMode.None: the shell's VStack/ARP engine owns scaling. Letting
        // WinForms autoscale here would fight it and double-scale the contents.
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(17, 28, 45);
        Controls.Add(outputBox);
        Name = "RawDebuggerView";
        Padding = new Padding(4);
        Size = new Size(780, 480);
        ResumeLayout(false);
    }

    private RichTextBox outputBox;
}
