using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ProfessorSnowsVideoDownloader.Composites
{
    /// <summary>
    /// Sealed WebView2 report surface for host apps (BSODGuru contract, 2026-08-03).
    /// Locked configuration — hosts get the API surface below, never the internals:
    /// no dev tools, no context menu, no accelerator keys, no new windows (popups are
    /// re-routed into this view), external navigation only through the API.
    /// Pins its own Font so an ambient host font never cascades in.
    /// </summary>
    public class ReportViewerPanel : UserControl
    {
        private readonly WebView2 _view = new();
        private bool _configured;

        /// <summary>Absolute URL of the current document after each successful navigation.</summary>
        public event EventHandler<string>? Navigated;

        /// <summary>Raised when a navigation fails (network, file missing, etc.).</summary>
        public event EventHandler<string>? NavigationFailed;

        public ReportViewerPanel()
        {
            Font = new Font("Segoe UI", 10F);   // contract: immune to ambient host fonts
            _view.Dock = DockStyle.Fill;
            Controls.Add(_view);
        }

        protected override async void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DesignMode || _configured)
                return;

            _configured = true;
            await _view.EnsureCoreWebView2Async();

            CoreWebView2Settings s = _view.CoreWebView2.Settings;
            s.AreDevToolsEnabled = false;
            s.AreDefaultContextMenusEnabled = false;
            s.AreBrowserAcceleratorKeysEnabled = false;
            s.IsStatusBarEnabled = false;

            // Reports open links; hosts don't get surprise windows.
            _view.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                _view.CoreWebView2.Navigate(args.Uri);
            };

            _view.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess)
                    Navigated?.Invoke(this, _view.Source?.AbsoluteUri ?? string.Empty);
                else
                    NavigationFailed?.Invoke(this, args.WebErrorStatus.ToString());
            };
        }

        /// <summary>Navigates to a URL (http/https/file).</summary>
        public void OpenUrl(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
                RunWhenReady(() => _view.CoreWebView2.Navigate(url));
        }

        /// <summary>Renders an HTML string directly (the report path).</summary>
        public void ShowHtml(string html) =>
            RunWhenReady(() => _view.CoreWebView2.NavigateToString(html ?? string.Empty));

        /// <summary>Opens an HTML report file from disk.</summary>
        public void ShowReportFile(string path) => OpenUrl(new Uri(path).AbsoluteUri);

        // API calls can arrive before EnsureCoreWebView2Async completes — queue them.
        private readonly Queue<Action> _pending = new();

        private void RunWhenReady(Action action)
        {
            if (_view.CoreWebView2 != null)
            {
                action();
                return;
            }

            _pending.Enqueue(action);
            _view.CoreWebView2InitializationCompleted += (_, _) =>
            {
                while (_pending.Count > 0)
                    _pending.Dequeue()();
            };
        }
    }
}
