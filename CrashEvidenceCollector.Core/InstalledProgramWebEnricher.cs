using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrashEvidenceCollector.Core;

/// <summary>
/// Looks up recently installed programs in the Microsoft winget community source
/// via the local winget client. Results are online package metadata matched by
/// name — investigative context, never local evidence, and never proof that the
/// matched package is the installed product. Lookups are capped and time-boxed;
/// only program names ever leave the machine, and only when the setting is on.
/// </summary>
public static partial class InstalledProgramWebEnricher
{
    public const int MaximumLookups = 15;
    private static readonly TimeSpan TotalBudget = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan PerLookupTimeout = TimeSpan.FromSeconds(75);
    private const string SourceLabel = "winget community source (online lookup by name)";

    public static async Task<(IReadOnlyList<ProgramWebInfo> Results, EvidenceItem Evidence)> EnrichAsync(IReadOnlyList<InstalledProgram> programs, DateTimeOffset incidentTimestamp, string rawDirectory, CollectionOptions options, CancellationToken cancellationToken)
    {
        try
        {
            if (options.IsTestDataMode)
            {
                var fixture = Path.Combine(Path.GetFullPath(options.TestDataDirectory!), "program-web-info.json");
                if (!File.Exists(fixture)) return ([], new("Program web metadata", EvidenceState.Skipped, "No program-web-info.json fixture; online lookup never runs in test-data mode."));
                var parsed = JsonSerializer.Deserialize<List<ProgramWebInfo>>(await File.ReadAllTextAsync(fixture, cancellationToken).ConfigureAwait(false), JsonDefaults.Indented) ?? [];
                return await FinishAsync(parsed, rawDirectory, $"Loaded {parsed.Count} fixture record(s); no online lookup was performed.", EvidenceState.Success, cancellationToken).ConfigureAwait(false);
            }
            if (!options.LookUpInstalledProgramsOnline) return ([], new("Program web metadata", EvidenceState.Skipped, "Online program lookup is disabled in settings."));
            var targets = InstalledProgramInventory.RecentInstalls(programs, incidentTimestamp);
            if (targets.Count == 0) return ([], new("Program web metadata", EvidenceState.Skipped, "No recently installed programs required an online lookup."));

            var results = new List<ProgramWebInfo>();
            var deadline = DateTimeOffset.Now + TotalBudget;
            var skipped = 0;
            foreach (var target in targets.Take(MaximumLookups))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTimeOffset.Now >= deadline) { skipped++; continue; }
                results.Add(await LookUpAsync(target, cancellationToken).ConfigureAwait(false));
            }
            skipped += Math.Max(0, targets.Count - MaximumLookups);
            var matched = results.Count(item => item.MatchedId is not null);
            var summary = $"Looked up {results.Count} recently installed program(s) in the winget source; {matched} matched by name." + (skipped > 0 ? $" {skipped} lookup(s) were skipped by the {MaximumLookups}-program cap or the {TotalBudget.TotalMinutes:0}-minute budget." : string.Empty) + " Matches are online metadata, not local evidence.";
            return await FinishAsync(results, rawDirectory, summary, EvidenceState.Success, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return ([], new("Program web metadata", EvidenceState.Warning, "Online lookup unavailable; collection continued without it.", Error: ex.Message));
        }
    }

    private static async Task<(IReadOnlyList<ProgramWebInfo>, EvidenceItem)> FinishAsync(IReadOnlyList<ProgramWebInfo> results, string rawDirectory, string summary, EvidenceState state, CancellationToken token)
    {
        var path = Path.Combine(rawDirectory, "program-web-info.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(results, JsonDefaults.Indented), token).ConfigureAwait(false);
        return (results, new("Program web metadata", state, summary, [path]));
    }

    private static async Task<ProgramWebInfo> LookUpAsync(InstalledProgram target, CancellationToken token)
    {
        var query = NormalizeQuery(target.Name);
        // Exact name match first; a general query only as fallback, and an
        // ambiguous fallback result is reported as ambiguous, never guessed.
        var exact = await RunWingetAsync(["show", "--name", query, "--exact", "--source", "winget", "--disable-interactivity", "--accept-source-agreements"], token).ConfigureAwait(false);
        var parsed = ParseWingetShow(target.Name, query, exact.StandardOutput);
        if (parsed.MatchedId is null)
        {
            var loose = await RunWingetAsync(["show", query, "--source", "winget", "--disable-interactivity", "--accept-source-agreements"], token).ConfigureAwait(false);
            parsed = ParseWingetShow(target.Name, query, loose.StandardOutput);
        }
        return AnnotatePublisherMismatch(parsed, target.Publisher);
    }

    // A name match against the wrong vendor is worse than no match (validated
    // live: the Windows "Copilot" app name-matches the GitHub.Copilot CLI).
    public static ProgramWebInfo AnnotatePublisherMismatch(ProgramWebInfo info, string? registryPublisher)
    {
        if (info.MatchedId is null || string.IsNullOrWhiteSpace(registryPublisher) || string.IsNullOrWhiteSpace(info.Publisher) || SharesPublisherToken(registryPublisher, info.Publisher)) return info;
        return info with { Note = $"Caution: the matched package's publisher ('{info.Publisher}') differs from the installed program's registry publisher ('{registryPublisher}'). This may be a different product with a similar name." };
    }

    public static bool SharesPublisherToken(string left, string right)
    {
        static HashSet<string> Tokens(string value) => Regex.Split(value.ToLowerInvariant(), @"[^a-z0-9]+")
            .Where(token => token.Length >= 3 && token is not ("inc" or "llc" or "ltd" or "corp" or "corporation" or "gmbh" or "the" or "software" or "version" or "limited" or "company"))
            .ToHashSet(StringComparer.Ordinal);
        var leftTokens = Tokens(left); var rightTokens = Tokens(right);
        return leftTokens.Count == 0 || rightTokens.Count == 0 || leftTokens.Overlaps(rightTokens);
    }

    private static Task<CommandResult> RunWingetAsync(string[] arguments, CancellationToken token)
        => CommandRunner.RunAsync("winget.exe", arguments, PerLookupTimeout, token);

    public static ProgramWebInfo ParseWingetShow(string programName, string query, string output)
    {
        if (output.Contains("No package found matching input criteria", StringComparison.OrdinalIgnoreCase))
            return new(programName, query, null, null, null, null, null, null, SourceLabel, "No winget package matched this name.");
        if (output.Contains("Multiple packages found matching input criteria", StringComparison.OrdinalIgnoreCase))
            return new(programName, query, null, null, null, null, null, null, SourceLabel, "Multiple winget packages matched; no single package could be identified safely.");
        var found = FoundRegex().Match(output);
        if (!found.Success)
            return new(programName, query, null, null, null, null, null, null, SourceLabel, "The winget response did not identify a package.");
        // Only column-0 keys are top-level package fields; indented keys belong
        // to Installer/Documentation subsections and must not be captured.
        string? Field(string name) { var match = Regex.Match(output, $@"(?m)^{name}:\s*(.+)$"); return match.Success ? match.Groups[1].Value.Trim() : null; }
        return new(programName, query, found.Groups[1].Value.Trim(), found.Groups[2].Value.Trim(), Field("Version"), Field("Publisher"), Field("Homepage"), Field("Description"), SourceLabel);
    }

    // Registry display names often carry version/architecture suffixes that
    // prevent an exact winget name match ("Malwarebytes version 5.6.2.268").
    public static string NormalizeQuery(string name)
    {
        var value = Regex.Replace(name.Trim(), @"\s*\((x64|x86|64-bit|32-bit|arm64|user)\)\s*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\s+version\s+[\d.]+\s*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"\s+\d+(\.\d+){1,3}\s*$", string.Empty, RegexOptions.CultureInvariant);
        return value.Trim();
    }

    [GeneratedRegex(@"(?m)^Found\s+(.+?)\s+\[([^\]]+)\]\s*$", RegexOptions.CultureInvariant)] private static partial Regex FoundRegex();
}
