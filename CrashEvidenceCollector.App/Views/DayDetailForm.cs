using CrashEvidenceCollector.Core;

namespace CrashEvidenceCollector.App.Views;

/// <summary>
/// What actually happened on one day of the metrics chart. A count of
/// "application incidents" is not useful on its own: this names the applications,
/// the stop codes, and the plain-English meaning of each record.
/// </summary>
public sealed class DayDetailForm : Form
{
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        BorderStyle = BorderStyle.None
    };

    /// <summary>The incident the user double-clicked, so the caller can select it.</summary>
    public Incident? ChosenIncident { get; private set; }

    public DayDetailForm(DateTimeOffset day, IReadOnlyList<Incident> incidents)
    {
        Text = $"Incidents on {day.ToLocalTime():d MMMM yyyy}";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = true;
        Size = new Size(1000, 520);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(246, 248, 251);

        _list.Columns.Add("Time", 90);
        _list.Columns.Add("Type", 140);
        _list.Columns.Add("What failed", 330);
        _list.Columns.Add("Plain-English meaning", 620);

        var ordered = incidents.OrderBy(item => item.Timestamp).ToList();
        foreach (var incident in ordered)
        {
            var row = new ListViewItem(incident.Timestamp.ToLocalTime().ToString("HH:mm:ss")) { Tag = incident };
            row.SubItems.Add(incident.Kind.ToString());
            row.SubItems.Add(incident.Title);
            row.SubItems.Add(CodeDecoder.DescribeIncident(incident));
            row.ForeColor = incident.Kind switch
            {
                IncidentKind.BugCheck => Color.Firebrick,
                IncidentKind.PowerLossOrFreeze or IncidentKind.UnexpectedShutdown => Color.DarkGoldenrod,
                IncidentKind.HardwareError => Color.FromArgb(150, 40, 40),
                _ => Color.FromArgb(35, 45, 58)
            };
            _list.Items.Add(row);
        }
        _list.DoubleClick += (_, _) =>
        {
            if (_list.SelectedItems.Count == 0) return;
            ChosenIncident = _list.SelectedItems[0].Tag as Incident;
            DialogResult = DialogResult.OK;
            Close();
        };

        var summary = new Label
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(14, 10, 14, 0),
            ForeColor = Color.FromArgb(75, 85, 99),
            Text = Describe(ordered)
        };
        var footer = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            Padding = new Padding(14, 6, 14, 0),
            ForeColor = Color.FromArgb(120, 132, 148),
            Text = "Double-click an incident to select it in the timeline. Repeated application failures are context, not proof of a cause."
        };

        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10) };
        host.Controls.Add(_list);
        Controls.Add(host);
        Controls.Add(footer);
        Controls.Add(summary);
    }

    private static string Describe(IReadOnlyList<Incident> incidents)
    {
        if (incidents.Count == 0) return "No incidents were recorded on this day.";
        var kernel = incidents.Count(item => item.Kind is IncidentKind.BugCheck);
        var power = incidents.Count(item => item.Kind is IncidentKind.PowerLossOrFreeze or IncidentKind.UnexpectedShutdown);
        var apps = incidents.Where(item => item.Kind == IncidentKind.ApplicationCrash).ToList();
        var parts = new List<string>();
        if (kernel > 0) parts.Add($"{kernel} bugcheck(s)");
        if (power > 0) parts.Add($"{power} power/shutdown event(s)");
        if (apps.Count > 0) parts.Add($"{apps.Count} application failure(s)");
        var line = $"{incidents.Count} incident(s): {string.Join(", ", parts)}.";
        // Naming the repeat offenders is the point: a bare count says nothing.
        var repeats = apps.GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderByDescending(group => group.Count())
            .Take(4)
            .Select(group => $"{group.Key} ×{group.Count()}")
            .ToList();
        return repeats.Count == 0 ? line : line + Environment.NewLine + "Repeated: " + string.Join("; ", repeats);
    }
}
