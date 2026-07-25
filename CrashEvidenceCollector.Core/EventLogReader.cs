using System.Globalization;
using System.Xml.Linq;

namespace CrashEvidenceCollector.Core;

public sealed class EventLogReader
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/win/2004/08/events/event";

    public async Task<IReadOnlyList<EvidenceEvent>> ReadAsync(string logName, DateTimeOffset from, DateTimeOffset to, string? testDataDirectory, CancellationToken cancellationToken)
        => (await ReadBatchAsync(logName, from, to, testDataDirectory, cancellationToken).ConfigureAwait(false)).Events;

    public async Task<EventLogBatch> ReadBatchAsync(string logName, DateTimeOffset from, DateTimeOffset to, string? testDataDirectory, CancellationToken cancellationToken)
    {
        string xml;
        if (!string.IsNullOrWhiteSpace(testDataDirectory))
        {
            var safeName = logName.Replace('/', '_').Replace('\\', '_');
            var path = Path.Combine(Path.GetFullPath(testDataDirectory), $"{safeName}.xml");
            if (!File.Exists(path)) return new([], string.Empty);
            xml = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // The XPath expression is generated only from UTC ticks and never from user-provided script text.
            var start = from.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var end = to.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var query = $"*[System[TimeCreated[@SystemTime>='{start}' and @SystemTime<='{end}']]]";
            var result = await CommandRunner.RunAsync("wevtutil.exe", ["qe", logName, $"/q:{query}", "/f:xml", "/uni:true"], TimeSpan.FromSeconds(45), cancellationToken).ConfigureAwait(false);
            if (!result.Success) throw new InvalidOperationException($"wevtutil could not read {logName}: {result.StandardError.Trim()}");
            xml = result.StandardOutput;
        }

        var events = Parse(xml, logName).Where(e => e.Timestamp >= from && e.Timestamp <= to).OrderBy(e => e.Timestamp).ToList();
        return new(events, xml);
    }

    public static IReadOnlyList<EvidenceEvent> Parse(string xml, string fallbackLogName)
    {
        if (string.IsNullOrWhiteSpace(xml)) return [];
        // wevtutil emits adjacent Event elements, so wrap them in a synthetic root.
        var sanitized = xml.Replace("<?xml version=\"1.0\" encoding=\"utf-16\"?>", string.Empty, StringComparison.OrdinalIgnoreCase)
                           .Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", string.Empty, StringComparison.OrdinalIgnoreCase);
        var document = XDocument.Parse($"<Events>{sanitized}</Events>", LoadOptions.None);
        var result = new List<EvidenceEvent>();
        foreach (var node in document.Descendants(Ns + "Event"))
        {
            var system = node.Element(Ns + "System");
            if (system is null) continue;
            if (!int.TryParse(system.Element(Ns + "EventID")?.Value, out var id)) continue;
            if (!DateTimeOffset.TryParse(system.Element(Ns + "TimeCreated")?.Attribute("SystemTime")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)) continue;
            var data = node.Descendants(Ns + "Data").Select((element, index) => new { Key = element.Attribute("Name")?.Value ?? $"Data{index}", element.Value })
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
            var provider = system.Element(Ns + "Provider")?.Attribute("Name")?.Value ?? "Unknown";
            var log = system.Element(Ns + "Channel")?.Value ?? fallbackLogName;
            var level = system.Element(Ns + "Level")?.Value ?? "Unknown";
            var message = string.Join("; ", data.Select(pair => $"{pair.Key}={pair.Value}"));
            result.Add(new(timestamp, log, provider, id, level, message, system.Element(Ns + "EventRecordID")?.Value, data));
        }
        return result;
    }
}

public sealed record EventLogBatch(IReadOnlyList<EvidenceEvent> Events, string RawXml);
