using ProfessorSnowsVideoDownloader.CustomControls;
using ProfessorSnowsVideoDownloader.CustomControls.WebBrowserTabControl;

namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// Behaviour for the designer-authored workspace layout, kept in its own file so
/// the designer never has to round-trip hand-written code.
///
/// The layout is a vStackPanel of four rows: actions, tab headers, navigation, and
/// the content area (the spring). The navigation row is only meaningful on a web
/// tab, so it collapses to nothing elsewhere — its rule already carries
/// CollapsedHeight = 0.
/// </summary>
public partial class MainWorkspaceTemplate
{
    /// <summary>Row names as authored in the designer; the vStack API addresses rows by name.</summary>
    public const string ActionsRowName = "RowActions";
    public const string TabHeaderRowName = "rowTabHeader";
    public const string NavigationRowName = "rowNavigation";
    /// <summary>
    /// Must match the vStack rule's ControlName and the panel's Spring, or the
    /// engine cannot resolve the row and spawns a replacement control.
    /// </summary>
    public const string ContentRowName = "tabContentControl1";

    public vStackPanel Stack => vStackPanel1;
    public VirtualizingAdaptiveRowPanel ActionsRow => RowActions;
    public VirtualizingAdaptiveRowPanel TabHeaderRow => rowTabHeader;
    public VirtualizingAdaptiveRowPanel NavigationRow => rowNavigation;
    public TabHeadersControl TabHeaders => tabHeadersControl1;
    public TabContentControl TabContent => tabContentControl1;
    public FlowLayoutPanel TimelineFilters => filterRow;

    public VirtualModernButton CollectButton => bthCollectEvidence;
    public VirtualModernButton CancelButton => btnCancel;
    public VirtualModernButton PastHistoryButton => btnPastHistory;
    public VirtualModernButton OpenOutputFolderButton => bthOpenOutputFolder;
    public VirtualModernButton CopySummaryButton => btnCopySummary;

    public VirtualIconButton BackButton => btnBack;
    public VirtualIconButton RefreshButton => btnRefresh;
    public VirtualIconButton GoButton => incBtnGo;
    public VirtualIconButton PasteButton => incBtnPaste;

    /// <summary>The address/search field in the navigation row.</summary>
    public RoundedTextBox AddressBox => virtualIconButton1;

    private void ApplyInvestigationTheme()
    {
        BackColor = UiTheme.Canvas;
        workspaceSplitter.BackColor = UiTheme.Border;
        timelinePanel.BackColor = UiTheme.Surface;
        timelinePanel.Padding = new Padding(18);
        rightHost.BackColor = UiTheme.Canvas;
        headlineLabel.ForeColor = UiTheme.Ink;
        headlineLabel.Font = new Font("Segoe UI Semibold", 17f);
        countLabel.ForeColor = UiTheme.Muted;
        filterRow.BackColor = UiTheme.Surface;
        timelineList.BackColor = UiTheme.Surface;
        timelineList.ForeColor = UiTheme.Ink;
        timelineList.Font = new Font("Segoe UI", 9.2f);
        timelineList.ShowItemToolTips = true;
        timelineList.HideSelection = false;
        RowActions.BackColor = UiTheme.SurfaceRaised;
        rowNavigation.BackColor = UiTheme.Surface;
        rowTabHeader.BackColor = UiTheme.SurfaceRaised;
        tabHeadersControl1.BackColor = UiTheme.SurfaceRaised;
        tabHeadersControl1.HeaderBackColor = UiTheme.SurfaceRaised;
        panel1.LineColor = UiTheme.Border;
        panel1.HoverLineColor = UiTheme.Accent;

        foreach (Control control in filterRow.Controls)
        {
            control.ForeColor = control == countLabel ? UiTheme.Muted : UiTheme.Ink;
            if (control is ComboBox or NumericUpDown) control.BackColor = UiTheme.Surface;
        }
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
