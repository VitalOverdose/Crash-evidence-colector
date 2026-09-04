namespace CrashEvidenceCollector.Core;

/// <summary>
/// The one-word answer to "is this machine alright?", derived only from records
/// the app actually holds. <see cref="Unknown"/> is a real answer, not a failure
/// state — a machine with no history yet has not been found healthy.
/// </summary>
public enum MachineHealth
{
    Unknown,
    Stable,
    Watch,
    Degrading,
    Critical
}

/// <summary>Why a reading is drawn the way it is. Colour follows this, never the other way round.</summary>
public enum StatusSeverity
{
    Neutral,
    Good,
    Caution,
    Bad
}

/// <summary>One cell of the status band: a label, a value, and how alarming it is.</summary>
public sealed record StatusReading(string Label, string Value, StatusSeverity Severity, string? Detail = null);

/// <summary>
/// Everything the always-visible band shows, computed in one place so the band
/// itself only has to draw. Nothing here is measured by this type: it is given
/// the incident history and the most recent monitor sample and reports what they
/// mean together.
/// </summary>
public sealed record MachineStatusSnapshot(
    MachineHealth Health,
    string HealthReason,
    IReadOnlyList<StatusReading> Readings,
    IReadOnlyList<string> Attention);

/// <summary>
/// Builds the status band's contents.
///
/// The rules are deliberately blunt and stated in the reason string, because a
/// health word nobody can account for is worse than no health word. Every
/// threshold here is a counting rule over records, not an inference about cause.
/// </summary>
public static class MachineStatusBuilder
{
    /// <summary>Package temperatures at or above this are called out. Sustained operation here is not normal on a desktop.</summary>
    public const double HotTemperatureC = 95;
    /// <summary>Worth watching, but not on its own a fault.</summary>
    public const double WarmTemperatureC = 85;

    private static bool IsSystemIncident(Incident incident) =>
        incident.Kind is IncidentKind.BugCheck or IncidentKind.UnexpectedShutdown or IncidentKind.PowerLossOrFreeze or IncidentKind.HardwareError;

    public static MachineStatusSnapshot Build(
        IReadOnlyList<Incident> incidents,
        MonitorSample? latestSample,
        bool monitorArmed,
        TimeSpan uptime,
        DateTimeOffset now)
    {
        var system = incidents.Where(IsSystemIncident).ToList();
        var lastCrash = system.Count == 0 ? (DateTimeOffset?)null : system.Max(item => item.Timestamp);
        var sinceLastCrash = lastCrash is null ? (TimeSpan?)null : now - lastCrash.Value;
        var lastWeek = system.Where(item => item.Timestamp >= now.AddDays(-7)).ToList();
        var lastDay = lastWeek.Where(item => item.Timestamp >= now.AddDays(-1)).ToList();
        var hardware = lastWeek.Count(item => item.Kind == IncidentKind.HardwareError);
        var temperature = latestSample is null ? null : HwInfoGadgetReader.SelectCpuTemperature(latestSample);

        var (health, reason) = Assess(incidents.Count, lastDay.Count, lastWeek.Count, hardware, temperature);

        var readings = new List<StatusReading>
        {
            new("Uptime", FormatSpan(uptime), StatusSeverity.Neutral, "Time since this machine last started."),
            new("CPU temp", temperature is null ? "—" : $"{temperature.Value:0} °C",
                temperature is null ? StatusSeverity.Neutral : temperature >= HotTemperatureC ? StatusSeverity.Bad : temperature >= WarmTemperatureC ? StatusSeverity.Caution : StatusSeverity.Good,
                temperature is null ? "No temperature sensor is being read. Start monitoring, and run the HWiNFO gadget for package temperature." : "Most recent package temperature from live monitoring."),
            new("Since last crash", sinceLastCrash is null ? "No record" : FormatSpan(sinceLastCrash.Value),
                sinceLastCrash is null ? StatusSeverity.Neutral : sinceLastCrash < TimeSpan.FromDays(1) ? StatusSeverity.Bad : sinceLastCrash < TimeSpan.FromDays(7) ? StatusSeverity.Caution : StatusSeverity.Good,
                "Measured from the most recent system crash, shutdown or hardware error in the timeline."),
            new("Last 7 days", lastWeek.Count == 0 ? "Clear" : $"{lastWeek.Count} incident{(lastWeek.Count == 1 ? string.Empty : "s")}",
                lastWeek.Count == 0 ? StatusSeverity.Good : lastWeek.Count >= 3 ? StatusSeverity.Bad : StatusSeverity.Caution,
                "System crashes, unexpected shutdowns and hardware errors. Application crashes are not counted here."),
            new("Monitor", monitorArmed ? "Armed" : "Off", monitorArmed ? StatusSeverity.Good : StatusSeverity.Caution,
                monitorArmed ? "Live samples are being written crash-safe, so the window before the next crash is recorded." : "Nothing is being recorded, so the next crash will have no live data behind it.")
        };

        return new(health, reason, readings, BuildAttention(lastWeek, hardware, temperature, monitorArmed, sinceLastCrash));
    }

    private static (MachineHealth, string) Assess(int totalRecords, int lastDay, int lastWeek, int hardware, double? temperature)
    {
        if (totalRecords == 0) return (MachineHealth.Unknown, "No incident history has been read yet, so nothing has been assessed.");
        if (lastDay > 0) return (MachineHealth.Critical, $"{lastDay} system incident{(lastDay == 1 ? string.Empty : "s")} in the last 24 hours.");
        if (hardware > 0) return (MachineHealth.Degrading, $"{hardware} hardware error{(hardware == 1 ? string.Empty : "s")} recorded in the last 7 days. Hardware errors are reported by the machine itself, not inferred.");
        if (temperature >= HotTemperatureC) return (MachineHealth.Degrading, $"The processor is at {temperature:0} °C. Sustained temperatures this high are not normal on a desktop.");
        if (lastWeek >= 3) return (MachineHealth.Degrading, $"{lastWeek} system incidents in the last 7 days.");
        if (lastWeek > 0) return (MachineHealth.Watch, $"{lastWeek} system incident{(lastWeek == 1 ? string.Empty : "s")} in the last 7 days.");
        if (temperature >= WarmTemperatureC) return (MachineHealth.Watch, $"No recent incidents, but the processor is at {temperature:0} °C.");
        return (MachineHealth.Stable, "No system crashes, unexpected shutdowns or hardware errors in the last 7 days.");
    }

    /// <summary>
    /// Things worth acting on, in the order they deserve attention. Each line
    /// states an observation; none of them names a cause.
    /// </summary>
    private static IReadOnlyList<string> BuildAttention(IReadOnlyList<Incident> lastWeek, int hardware, double? temperature, bool monitorArmed, TimeSpan? sinceLastCrash)
    {
        var attention = new List<string>();
        if (temperature >= HotTemperatureC) attention.Add($"Processor at {temperature:0} °C — check cooling before drawing conclusions from crashes.");
        if (hardware > 0) attention.Add($"{hardware} hardware error{(hardware == 1 ? string.Empty : "s")} in 7 days — the machine reported these itself.");
        if (sinceLastCrash is not null && sinceLastCrash < TimeSpan.FromHours(24)) attention.Add("A system incident happened in the last 24 hours — collect evidence while the logs still hold it.");
        if (lastWeek.Count >= 3) attention.Add($"{lastWeek.Count} system incidents in 7 days — compare them to find what they share.");
        if (!monitorArmed) attention.Add("Monitoring is off — the next crash will have no live temperature or load data behind it.");
        if (temperature is null && monitorArmed) attention.Add("No temperature sensor is readable — run the HWiNFO gadget to record package temperature.");
        return attention;
    }

    /// <summary>Compact durations for a band that has very little room.</summary>
    public static string FormatSpan(TimeSpan span)
    {
        if (span < TimeSpan.Zero) return "—";
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{span.Minutes}m";
        if (span.TotalDays < 1) return $"{span.Hours}h {span.Minutes:00}m";
        if (span.TotalDays < 100) return $"{(int)span.TotalDays}d {span.Hours:00}h";
        return $"{(int)span.TotalDays}d";
    }

    public static string HealthLabel(MachineHealth health) => health switch
    {
        MachineHealth.Stable => "Stable",
        MachineHealth.Watch => "Watch",
        MachineHealth.Degrading => "Degrading",
        MachineHealth.Critical => "Crashed recently",
        _ => "Not yet assessed"
    };
}
