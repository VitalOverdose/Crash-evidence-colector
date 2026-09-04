namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// Design template for the main workspace: the existing left pane (headline,
/// filters, incident list) beside an empty right host, separated by a splitter
/// the user can drag.
///
/// The right host is the design surface for the tab shell — action row,
/// navigation bar, tab headers, and the headless tab control. Lay it out in the
/// designer and give each control a meaningful Name; behaviour is wired to those
/// names from code afterwards.
///
/// This template is not yet the live workspace. MainForm still builds the running
/// UI, so the application keeps working while the layout is being designed.
/// </summary>
public partial class MainWorkspaceTemplate : UserControl
{
    public MainWorkspaceTemplate()
    {
        InitializeComponent();
        ApplyInvestigationTheme();
    }

    /// <summary>The design surface that receives the tab shell.</summary>
    public Panel RightHost => rightHost;

    /// <summary>Incident list on the left; the splitter keeps it resizable.</summary>
    public ListView TimelineList => timelineList;

    public Label HeadlineLabel => headlineLabel;
    public ComboBox RangeBox => rangeBox;
    public ComboBox TypeBox => typeBox;
    public NumericUpDown CustomHours => customHours;
    public Label CountLabel => countLabel;
    public SplitContainer WorkspaceSplitter => workspaceSplitter;
}
