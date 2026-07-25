using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public sealed class Redactor(string accountName, string profilePath)
{
    private readonly string[] _sensitive = [accountName, profilePath, profilePath.Replace('\\', '/')];

    public string Redact(string value)
    {
        var result = value;
        foreach (var sensitive in _sensitive.Where(x => !string.IsNullOrWhiteSpace(x)).OrderByDescending(x => x.Length))
            result = result.Replace(sensitive, "[REDACTED-USER]", StringComparison.OrdinalIgnoreCase);
        return result;
    }

    public EvidenceReport Redact(EvidenceReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.Indented);
        return JsonSerializer.Deserialize<EvidenceReport>(Redact(json), JsonDefaults.Indented) ?? throw new InvalidOperationException("Unable to redact report.");
    }
}
