namespace CrashEvidenceCollector.App.Views;

/// <summary>Unmodified debugger output for the associated dump, shown verbatim.</summary>
public partial class RawDebuggerView : UserControl
{
    public RawDebuggerView()
    {
        InitializeComponent();
        SetOutput(null);
    }

    public void SetOutput(string? text)
        => outputBox.Text = string.IsNullOrWhiteSpace(text)
            ? "No debugger output yet." + Environment.NewLine + Environment.NewLine
              + "Select an incident on the left and choose Collect Evidence. If a crash dump is found and the Microsoft debugger is installed, its unmodified output appears here so every conclusion in the report can be checked against its source."
            : text;
}
