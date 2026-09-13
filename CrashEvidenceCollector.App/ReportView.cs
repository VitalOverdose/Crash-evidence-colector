using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CrashEvidenceCollector.App;

/// <summary>
/// Hosts a generated report. Report content includes strings that came out of
/// crash dumps, so it is rendered as untrusted input: no dev tools, no browser
/// accelerator keys, no new windows, and no navigation except through this API.
///
/// The context menu is deliberately rebuilt rather than disabled: an investigator
/// needs to copy evidence and look it up, so selection-driven search is offered
/// here instead of forcing a copy-and-switch.
/// </summary>
public sealed class ReportView : UserControl
{
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private readonly Queue<Action> _pending = new();
    private bool _configured;

    /// <summary>Selected text the user asked to look up; true when a new tab was requested.</summary>
    public event Action<string, bool>? SearchRequested;
    public event EventHandler<string>? Navigated;
    public event EventHandler<string>? NavigationFailed;

    public ReportView()
    {
        Font = new Font("Segoe UI", 11f);
        Dock = DockStyle.Fill;
        Controls.Add(_view);
    }

    protected override async void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (DesignMode || _configured) return;
        _configured = true;
        try
        {
            var profile = Path.Combine(CrashEvidenceCollector.Core.AppPaths.StateDirectory, "ReportViewProfile");
            Directory.CreateDirectory(profile);
            await _view.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(null, profile));
            var settings = _view.CoreWebView2.Settings;
            settings.AreDevToolsEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.IsStatusBarEnabled = false;
            // Default menus stay enabled so the customisation event fires; the menu
            // itself is then reduced to the items an evidence reader should have.
            settings.AreDefaultContextMenusEnabled = true;
            _view.CoreWebView2.NewWindowRequested += (_, args) => { args.Handled = true; _view.CoreWebView2.Navigate(args.Uri); };
            _view.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess) Navigated?.Invoke(this, _view.Source?.AbsoluteUri ?? string.Empty);
                else NavigationFailed?.Invoke(this, args.WebErrorStatus.ToString());
            };
            _view.CoreWebView2.ContextMenuRequested += OnContextMenuRequested;
            while (_pending.Count > 0) _pending.Dequeue()();
        }
        catch (Exception ex) { NavigationFailed?.Invoke(this, ex.Message); }
    }

    /// <summary>Menu entries that make sense for reading evidence; everything else is removed.</summary>
    private static readonly HashSet<string> KeptMenuItems = new(StringComparer.OrdinalIgnoreCase)
    { "copy", "cut", "paste", "selectAll", "print", "back", "forward", "reload", "copyLinkLocation" };

    private void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        var items = e.MenuItems;
        for (var index = items.Count - 1; index >= 0; index--)
            if (!KeptMenuItems.Contains(items[index].Name)) items.RemoveAt(index);

        var selection = e.ContextMenuTarget.SelectionText?.Trim() ?? string.Empty;
        if (selection.Length > 0)
        {
            var label = selection.Length > 40 ? selection[..40].Replace(Environment.NewLine, " ") + "…" : selection.Replace(Environment.NewLine, " ");
            items.Insert(0, CreateCommand($"Search the web for \"{label}\"", () => SearchRequested?.Invoke(selection, false)));
            items.Insert(1, CreateCommand("Search in a new web tab", () => SearchRequested?.Invoke(selection, true)));
            items.Insert(2, _view.CoreWebView2.Environment.CreateContextMenuItem(string.Empty, null, CoreWebView2ContextMenuItemKind.Separator));
        }
        else items.Insert(0, CreateCommand("Open a new web tab", () => SearchRequested?.Invoke(string.Empty, true)));
    }

    private CoreWebView2ContextMenuItem CreateCommand(string label, Action action)
    {
        var item = _view.CoreWebView2.Environment.CreateContextMenuItem(label, null, CoreWebView2ContextMenuItemKind.Command);
        item.CustomItemSelected += (_, _) => BeginInvoke(action);
        return item;
    }

    public void ShowReportFile(string path) { if (!string.IsNullOrWhiteSpace(path)) RunWhenReady(() => _view.CoreWebView2.Navigate(new Uri(path).AbsoluteUri)); }
    public void ShowHtml(string html) => RunWhenReady(() => _view.CoreWebView2.NavigateToString(html ?? string.Empty));

    /// <summary>Opens the browser print dialog, which offers Microsoft Print to PDF.</summary>
    public bool Print()
    {
        if (_view.CoreWebView2 is null) return false;
        _view.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser);
        return true;
    }

    private void RunWhenReady(Action action)
    {
        if (_view.CoreWebView2 is not null) { action(); return; }
        _pending.Enqueue(action);
    }
}
