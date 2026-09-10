using CrashEvidenceCollector.Core;
using ProfessorSnowsVideoDownloader.CustomControls;

namespace CrashEvidenceCollector.App.Views;

// Owns filter controls and their interaction rules. Consumers exchange values,
// never dropdown indices or individual buttons.
public partial class MainWorkspaceTemplate
{
    private TimelineFilter _filter = new();
    private bool _applyingFilter;
    private bool _filtersInitialized;
    private readonly Stack<TimelineFilter> _filterHistory = new();

    public event Action<TimelineFilter>? FilterChanged;
    public TimelineFilter GetFilter() => _filter;

    public void InitializeTimelineFilters()
    {
        if (_filtersInitialized) return;
        _filtersInitialized = true;
        foreach (var text in TimelineFilter.Ranges)
            virtualIconButton5.Items.Add(new RoundedComboBoxItem { Text = text });
        foreach (var text in new[] { "System crashes and shutdowns", "All incident types", "Application crashes only", "Hardware errors only" })
            virtualIconButton7.Items.Add(new RoundedComboBoxItem { Text = text });
        roundedNumericTextBox1.Minimum = 1;
        roundedNumericTextBox1.Maximum = 8760;
        RenderFilter();
        virtualIconButton5.SelectedIndexChanged += (_, _) => ReadFilter();
        virtualIconButton7.SelectedIndexChanged += (_, _) => ReadFilter();
        roundedNumericTextBox1.ValueChanged += (_, _) =>
        {
            if (!_applyingFilter) SetFilter(_filter with { Range = "Custom hours", CustomHours = roundedNumericTextBox1.Value });
        };
        roundedTextBox1.TextChanged += (_, _) => ReadFilter();
        vBtnPaste.Click += (_, _) =>
        {
            try { if (Clipboard.ContainsText()) SetFilter(_filter with { Search = Clipboard.GetText() }); }
            catch (System.Runtime.InteropServices.ExternalException) { }
        };
        vBtnSearch.Click += (_, _) => ReadFilter();
        roundedTextBox1.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ReadFilter(); }
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; ClearTimelineSearch(); }
        };
    }

    public void SetFilter(TimelineFilter filter)
    {
        if (!TimelineFilter.Ranges.Contains(filter.Range)) throw new ArgumentException("Unknown timeline range.", nameof(filter));
        filter = filter with { CustomHours = Math.Clamp(filter.CustomHours, 1, 8760), Search = filter.Search ?? "" };
        if (filter == _filter) return;
        _filterHistory.Push(_filter);
        _filter = filter;
        RenderFilter();
        FilterChanged?.Invoke(_filter);
    }

    public void SelectTimelineRange(string range) => SetFilter(_filter with { Range = range });
    public void ClearTimelineSearch() => SetFilter(_filter with { Search = "" });
    public void FocusTimelineSearch() => roundedTextBox1.Focus();
    public void GoBackInTimeline()
    {
        if (!_filterHistory.TryPop(out var previous)) return;
        _filter = previous;
        RenderFilter();
        FilterChanged?.Invoke(_filter);
    }

    public void ShowTimelineSummary(string headline, string count)
    {
        virtualIconButton3.Text = headline;
        virtualIconButton8.Text = count;
    }

    private void ReadFilter()
    {
        if (_applyingFilter) return;
        SetFilter(new(virtualIconButton5.SelectedItem?.Text ?? "All time",
            roundedNumericTextBox1.Value, (TimelineKindFilter)Math.Max(0, virtualIconButton7.SelectedIndex), roundedTextBox1.Text));
    }

    private void RenderFilter()
    {
        _applyingFilter = true;
        try
        {
            virtualIconButton5.SelectedIndex = Array.IndexOf(TimelineFilter.Ranges, _filter.Range);
            virtualIconButton7.SelectedIndex = (int)_filter.Kind;
            roundedNumericTextBox1.Value = _filter.CustomHours;
            NumbericUpdownValue.Text = _filter.CustomHours.ToString("0.##");
            roundedTextBox1.Text = _filter.Search;
        }
        finally { _applyingFilter = false; }
    }
}
