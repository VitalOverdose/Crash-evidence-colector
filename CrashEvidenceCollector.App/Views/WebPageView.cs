using CrashEvidenceCollector.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// A single web page with no chrome of its own: the shell's navigation row drives
/// it. Everything a navigation bar needs is exposed as methods and events, so the
/// same pane works whether it is driven by buttons, keyboard, or the report's
/// right-click search.
///
/// Nothing is fetched unless the user asks for it, and browsing state is kept in
/// its own WebView2 profile so it never shares storage with rendered evidence.
/// </summary>
public sealed class WebPageView : UserControl
{
    private readonly WebView2 _view = new() { Dock = DockStyle.Fill };
    private readonly Queue<Action> _pending = new();
    private bool _configured;

    /// <summary>Current address after each navigation, for the address box.</summary>
    public event Action<WebPageView, string>? AddressChanged;

    /// <summary>Document title, suitable for a tab caption.</summary>
    public event Action<WebPageView, string>? TitleChanged;

    /// <summary>Raised when back/forward availability changes, for enabling buttons.</summary>
    public event Action<WebPageView>? HistoryChanged;

    /// <summary>
    /// A tab icon for the current page. The image is freshly created for this pane,
    /// so the consumer owns it: dispose the previous icon when a new one arrives,
    /// and never hand one instance to two controls.
    /// </summary>
    public event Action<WebPageView, Image>? FaviconChanged;

    public WebPageView()
    {
        // AutoScaleMode.None: the shell's VStack/ARP engine owns scaling.
        AutoScaleMode = AutoScaleMode.None;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.Surface;
        Controls.Add(_view);
    }

    public string Address => _view.Source?.AbsoluteUri ?? string.Empty;
    public string Title => _view.CoreWebView2?.DocumentTitle ?? string.Empty;
    public bool CanGoBack => _view.CoreWebView2?.CanGoBack ?? false;
    public bool CanGoForward => _view.CoreWebView2?.CanGoForward ?? false;

    protected override async void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (DesignMode || _configured) return;
        _configured = true;
        try
        {
            var profile = Path.Combine(AppPaths.StateDirectory, "WebSearchProfile");
            Directory.CreateDirectory(profile);
            await _view.EnsureCoreWebView2Async(await CoreWebView2Environment.CreateAsync(null, profile));
            _view.CoreWebView2.SourceChanged += (_, _) => { AddressChanged?.Invoke(this, Address); HistoryChanged?.Invoke(this); };
            _view.CoreWebView2.DocumentTitleChanged += (_, _) => TitleChanged?.Invoke(this, Title);
            _view.CoreWebView2.HistoryChanged += (_, _) => HistoryChanged?.Invoke(this);
            // Cache-first: push a known icon the moment navigation completes, before
            // any favicon event fires. This is what removes the blank-then-pop flicker
            // on revisits, and covers cached loads where the event never fires at all.
            _view.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                if (FaviconService.LoadFromCache(Address) is { } cached) FaviconChanged?.Invoke(this, cached);
            };
            _view.CoreWebView2.FaviconChanged += async (_, _) => await ResolveFaviconAsync();
            // Popups are re-routed into this view rather than opening stray windows.
            _view.CoreWebView2.NewWindowRequested += (_, args) => { args.Handled = true; _view.CoreWebView2.Navigate(args.Uri); };
            while (_pending.Count > 0) _pending.Dequeue()();
            if (_view.Source is null) _view.CoreWebView2.Navigate(WebSearchTarget.SearchHome);
        }
        catch { /* a browsing pane must never take the app down */ }
    }

    /// <summary>Opens an address, or searches the text when it is not one.</summary>
    public void Navigate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;
        var target = WebSearchTarget.Build(input);
        RunWhenReady(() => _view.CoreWebView2.Navigate(target));
    }

    /// <summary>Searches whatever text is on the clipboard, for report selections.</summary>
    public bool NavigateFromClipboard()
    {
        string text;
        try { text = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty; }
        catch { return false; }
        if (string.IsNullOrWhiteSpace(text)) return false;
        Navigate(text);
        return true;
    }

    public void GoBack() => RunWhenReady(() => { if (_view.CoreWebView2.CanGoBack) _view.CoreWebView2.GoBack(); });
    public void GoForward() => RunWhenReady(() => { if (_view.CoreWebView2.CanGoForward) _view.CoreWebView2.GoForward(); });
    public void Reload() => RunWhenReady(() => _view.CoreWebView2.Reload());
    public void GoHome() => Navigate(WebSearchTarget.SearchHome);

    /// <summary>
    /// Resolves the page icon: the reported URI is downloaded and cached, and a
    /// letter tile stands in when a site offers nothing usable (including SVG icons,
    /// which are not rasterized here).
    /// </summary>
    private async Task ResolveFaviconAsync()
    {
        var page = Address;
        var reported = _view.CoreWebView2?.FaviconUri;
        var icon = await FaviconService.DownloadAndCacheAsync(page, reported).ConfigureAwait(true);
        icon ??= FaviconService.LoadFromCache(page) ?? FaviconService.CreateLetterTile(page, Color.FromArgb(90, 110, 140));
        if (!IsDisposed) FaviconChanged?.Invoke(this, icon);
        else icon.Dispose();
    }

    private void RunWhenReady(Action action)
    {
        if (_view.CoreWebView2 is not null) { action(); return; }
        _pending.Enqueue(action);
    }
}
