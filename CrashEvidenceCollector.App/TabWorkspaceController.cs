using CrashEvidenceCollector.App.Views;
using ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl;

namespace CrashEvidenceCollector.App;

/// <summary>
/// Drives the tab shell: the headers and the content control are the two halves
/// of the same system, and both are imperative — tabs exist only at runtime. This
/// owns the tab list and keeps header index, content index and pane in step.
///
/// Fixed panes — metrics, monitor, evidence, raw output, report — cannot be
/// closed. Web tabs can, and the "+" button opens more of them. The navigation
/// row is collapsed for every non-web tab, since it would only be dead chrome.
/// </summary>
internal sealed class TabWorkspaceController
{
    private sealed class TabEntry
    {
        public required Control Pane { get; init; }
        public required bool Closable { get; init; }
        public WebPageView? Web { get; init; }
        public Image? Icon { get; set; }
        public bool IsWeb => Web is not null;
    }

    private readonly TabHeadersControl _headers;
    private readonly TabContentControl _content;
    private readonly MainWorkspaceTemplate _template;
    private readonly List<TabEntry> _tabs = [];

    public TabWorkspaceController(TabHeadersControl headers, TabContentControl content, MainWorkspaceTemplate template)
    {
        _headers = headers;
        _content = content;
        _template = template;
        _headers.TabSelected += (_, e) => ShowTab(e.TabIndex);
        _headers.NewTabRequested += (_, _) => OpenWebTab(null);
        _headers.TabClosing += OnTabClosing;
    }

    /// <summary>The web pane of the selected tab, or null when a fixed pane is shown.</summary>
    public WebPageView? CurrentWeb => Current?.Web;

    private TabEntry? Current => _headers.SelectedIndex >= 0 && _headers.SelectedIndex < _tabs.Count ? _tabs[_headers.SelectedIndex] : null;

    /// <summary>Adds a permanent pane. Fixed tabs refuse to close.</summary>
    public void AddFixedTab(string title, Control pane, Image? icon = null)
    {
        pane.Dock = DockStyle.Fill;
        _content.AddTabPage(pane);
        _tabs.Add(new TabEntry { Pane = pane, Closable = false, Icon = icon });
        _headers.AddTab(title, icon);
        if (_tabs.Count == 1) SelectTab(0);
    }

    /// <summary>Opens a web tab, optionally navigating it straight away.</summary>
    public WebPageView OpenWebTab(string? input, bool select = true)
    {
        var pane = new WebPageView();
        _content.AddTabPage(pane);
        var entry = new TabEntry { Pane = pane, Closable = true, Web = pane };
        _tabs.Add(entry);
        _headers.AddTab("New tab", null);
        var index = _tabs.Count - 1;

        pane.TitleChanged += (_, title) =>
        {
            var at = _tabs.IndexOf(entry);
            if (at >= 0 && !string.IsNullOrWhiteSpace(title)) _headers.SetTabText(at, title.Length > 28 ? title[..28] + "…" : title);
        };
        pane.FaviconChanged += (_, icon) =>
        {
            var at = _tabs.IndexOf(entry);
            if (at < 0) { icon.Dispose(); return; }
            // The pane hands over a fresh image each time; release the previous one
            // rather than accumulating bitmaps across a long session.
            entry.Icon?.Dispose();
            entry.Icon = icon;
            _headers.SetTabIcon(at, icon);
        };
        pane.AddressChanged += (_, address) => { if (ReferenceEquals(Current, entry)) _template.AddressBox.Text = address; };

        if (select) SelectTab(index);
        if (!string.IsNullOrWhiteSpace(input)) pane.Navigate(input);
        return pane;
    }

    /// <summary>Searches text in the current web tab, or in a new one.</summary>
    public void Search(string? text, bool newTab)
    {
        if (newTab || CurrentWeb is null) { OpenWebTab(string.IsNullOrWhiteSpace(text) ? null : text); return; }
        SelectTab(_tabs.FindIndex(entry => entry.IsWeb && ReferenceEquals(entry.Web, CurrentWeb)));
        CurrentWeb.Navigate(text);
    }

    public void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _headers.SelectTab(index);
        ShowTab(index);
    }

    private void ShowTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        // The content control owns page visibility; it only needs the index.
        if (_content.SelectedIndex != index && index < _content.TabCount) _content.SelectedIndex = index;
        var entry = _tabs[index];
        _template.ShowNavigationRow(entry.IsWeb);
        if (entry.IsWeb) _template.AddressBox.Text = entry.Web!.Address;
    }

    private void OnTabClosing(object? sender, TabHeaderEventArgs e)
    {
        if (e.TabIndex < 0 || e.TabIndex >= _tabs.Count) return;
        var entry = _tabs[e.TabIndex];
        if (!entry.Closable) { e.Cancel = true; return; }
        _tabs.RemoveAt(e.TabIndex);
        _content.RemoveTabPage(e.TabIndex);
        entry.Pane.Dispose();
        entry.Icon?.Dispose();
    }
}
