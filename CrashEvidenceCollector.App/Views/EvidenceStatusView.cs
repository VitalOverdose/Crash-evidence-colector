using CrashEvidenceCollector.Core;

namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// Collection progress and per-category evidence status. Rows update in place so
/// a category that starts as Running becomes Success or Warning without repeating.
/// </summary>
public partial class EvidenceStatusView : UserControl
{
    public EvidenceStatusView()
    {
        InitializeComponent();
        ShowPlaceholder();
    }

    /// <summary>Explains the empty state rather than presenting a blank grid.</summary>
    private void ShowPlaceholder()
    {
        var row = evidenceList.Items.Add("Waiting for a collection");
        row.SubItems.Add("Idle");
        row.SubItems.Add("Select an incident and choose Collect Evidence. Every evidence category then reports here as it succeeds, warns, or fails — including what could not be collected.");
        row.ForeColor = UiTheme.Muted;
    }

    public void Clear() => evidenceList.Items.Clear();

    /// <summary>Restores the explanatory empty state.</summary>
    public void Reset() { evidenceList.Items.Clear(); ShowPlaceholder(); }

    public void Show(EvidenceReport report)
    {
        Clear();
        foreach (var item in report.Evidence)
            AddOrUpdate(item.Category, item.State, item.Summary + (item.Error is null ? string.Empty : " — " + item.Error));
    }

    public void AddOrUpdate(string category, EvidenceState state, string detail)
    {
        var item = evidenceList.Items.Cast<ListViewItem>().FirstOrDefault(row => row.Text == category) ?? evidenceList.Items.Add(category);
        while (item.SubItems.Count < 3) item.SubItems.Add(string.Empty);
        item.SubItems[1].Text = state.ToString();
        item.SubItems[2].Text = detail;
        item.ForeColor = state switch
        {
            EvidenceState.Failure => UiTheme.Danger,
            EvidenceState.Warning => UiTheme.Warning,
            EvidenceState.Success => UiTheme.Success,
            _ => UiTheme.Ink
        };
    }
}
