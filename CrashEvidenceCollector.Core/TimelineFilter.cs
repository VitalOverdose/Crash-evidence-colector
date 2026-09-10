namespace CrashEvidenceCollector.Core;

public enum TimelineKindFilter { System, All, Applications, Hardware }

public sealed record TimelineFilter(string Range = "All time", decimal CustomHours = 1,
    TimelineKindFilter Kind = TimelineKindFilter.System, string Search = "")
{
    public static readonly string[] Ranges = ["Last 30 minutes", "Last hour", "Last 2 hours", "Last 3 hours", "Last 6 hours", "Last 12 hours", "Last 24 hours", "Last 3 days", "Last 7 days", "Last 30 days", "Last 90 days", "Last 6 months", "Last year", "All time", "Custom hours"];
    public TimeSpan Duration => Range switch
    {
        "Last 30 minutes" => TimeSpan.FromMinutes(30),
        "Last hour" => TimeSpan.FromHours(1),
        "Last 2 hours" => TimeSpan.FromHours(2),
        "Last 3 hours" => TimeSpan.FromHours(3),
        "Last 6 hours" => TimeSpan.FromHours(6),
        "Last 12 hours" => TimeSpan.FromHours(12),
        "Last 24 hours" => TimeSpan.FromDays(1),
        "Last 3 days" => TimeSpan.FromDays(3),
        "Last 7 days" => TimeSpan.FromDays(7),
        "Last 30 days" => TimeSpan.FromDays(30),
        "Last 90 days" => TimeSpan.FromDays(90),
        "Last 6 months" => TimeSpan.FromDays(183),
        "Last year" => TimeSpan.FromDays(365),
        "Custom hours" => TimeSpan.FromHours((double)CustomHours),
        _ => TimeSpan.FromDays(36500)
    };
}

public sealed record TimelineQueryResult(List<Incident> MatchingType, List<Incident> InRange, List<Incident> Visible);

public static class IncidentQueryService
{
    public static TimelineQueryResult Query(IEnumerable<Incident> incidents, TimelineFilter filter, DateTimeOffset now)
    {
        var matching = incidents.Where(i => filter.Kind switch
        {
            TimelineKindFilter.All => true,
            TimelineKindFilter.Applications => i.Kind == IncidentKind.ApplicationCrash,
            TimelineKindFilter.Hardware => i.Kind == IncidentKind.HardwareError,
            _ => i.Kind is IncidentKind.BugCheck or IncidentKind.UnexpectedShutdown or IncidentKind.PowerLossOrFreeze or IncidentKind.HardwareError
        }).ToList();
        var inRange = matching.Where(i => i.Timestamp >= now - filter.Duration && i.Timestamp <= now.AddMinutes(1)).ToList();
        var query = filter.Search.Trim();
        var visible = inRange.Where(i => query.Length == 0 ||
            string.Join(" ", i.Timestamp.LocalDateTime.ToString("g"), i.Kind, i.Title, i.Summary, i.Source, i.BugCheckCode, CodeDecoder.DescribeIncident(i))
                .Contains(query, StringComparison.OrdinalIgnoreCase)).OrderByDescending(i => i.Timestamp).ToList();
        return new(matching, inRange, visible);
    }
}
