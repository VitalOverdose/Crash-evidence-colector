using ProfessorSnowsVideoDownloader.CustomControls;
using ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl;

namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// Behaviour for the designer-authored workspace layout, kept in its own file so
/// the designer never has to round-trip hand-written code.
///
/// The layout is a vStackPanel of three rows: tab headers, navigation, and
/// the content area (the spring). The navigation row is only meaningful on a web
/// tab, so it collapses to nothing elsewhere — its rule already carries
/// CollapsedHeight = 0.
/// </summary>
public partial class MainWorkspaceTemplate
{
    /// <summary>Row names as authored in the designer; the vStack API addresses rows by name.</summary>
    public const string TabHeaderRowName = "rowTabHeader";
    public const string NavigationRowName = "rowNavigation";
    /// <summary>
    /// Must match the vStack rule's ControlName and the panel's Spring, or the
    /// engine cannot resolve the row and spawns a replacement control.
    /// </summary>
    public const string ContentRowName = "tabContentControl1";

    public vStackPanel Stack => vStackPanel1;
    public VirtualizingAdaptiveRowPanel TabHeaderRow => rowTabHeader;
    public VirtualizingAdaptiveRowPanel NavigationRow => rowNavigation;
    public TabHeadersControl TabHeaders => tabHeadersControl1;
    public TabContentControl TabContent => tabContentControl1;

    /// <summary>
    /// The left pane itself. Its designer children dock in this order, bottom-up
    /// by child index: the list fills, the filter row and headline sit above it.
    /// Anything inserted here must set its own child index to land in the right
    /// place rather than simply appending.
    /// </summary>
    public Panel TimelinePanel => timelinePanel;

    public VirtualIconButton BackButton => btnBack;
    public VirtualIconButton RefreshButton => btnRefresh;
    // The designer field names predate the final artwork: incBtnPaste carries
    // the green Go glyph and incBtnGo carries the clipboard glyph.
    public VirtualIconButton GoButton => incBtnPaste;
    public VirtualIconButton PasteButton => incBtnGo;

    /// <summary>The address/search field in the navigation row.</summary>
    public RoundedTextBox AddressBox => virtualIconButton1;

    private void ApplyInvestigationTheme()
    {
        BackColor = UiTheme.Canvas;
        workspaceSplitter.BackColor = UiTheme.Border;
        timelinePanel.BackColor = UiTheme.Surface;
        timelinePanel.Padding = new Padding(18);
        rightHost.BackColor = UiTheme.Canvas;
        virtualIconButton3.ForeColor = UiTheme.Ink;
        virtualIconButton3.Font = new Font("Segoe UI Semibold", 17f);
        virtualIconButton8.ForeColor = UiTheme.Muted;
        timelineList.BackColor = UiTheme.Surface;
        timelineList.ForeColor = UiTheme.Ink;
        timelineList.Font = new Font("Segoe UI", 9.2f);
        timelineList.ShowItemToolTips = true;
        timelineList.HideSelection = false;
        rowNavigation.BackColor = UiTheme.Surface;
        rowTabHeader.BackColor = UiTheme.SurfaceRaised;
        tabHeadersControl1.BackColor = UiTheme.SurfaceRaised;
        tabHeadersControl1.HeaderBackColor = UiTheme.SurfaceRaised;
    }

    /// <summary>True when the navigation row is currently expanded.</summary>
    public bool IsNavigationRowVisible { get; private set; } = true;

    /// <summary>
    /// Shows or hides the navigation row. Web tabs need it; every other tab would
    /// only be showing dead chrome, so it collapses to zero height there.
    /// </summary>
    public void ShowNavigationRow(bool visible)
    {
        if (visible == IsNavigationRowVisible) return;
        IsNavigationRowVisible = visible;
        if (visible) vStackPanel1.Expand(NavigationRowName);
        else vStackPanel1.Collapse(NavigationRowName);
    }
}
