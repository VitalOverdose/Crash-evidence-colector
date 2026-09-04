namespace CrashEvidenceCollector.Core;

/// <summary>
/// Turns whatever the user typed — or copied out of a report — into either an
/// address to open or a web search. Crash evidence is long and full of
/// punctuation, so a query is trimmed and escaped rather than assumed to be a URL.
/// </summary>
public static class WebSearchTarget
{
    public const string SearchHome = "https://www.google.com";
    public const int MaximumQueryLength = 300;

    /// A crash report is full of dotted file names, and "KERNELBASE.dll" is a
    /// syntactically valid host name. Those must always be searched, never opened.
    private static readonly HashSet<string> FileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "dll", "sys", "exe", "dmp", "msi", "cab", "inf", "cat", "log", "txt", "json", "xml",
        "etl", "evtx", "ini", "cfg", "bat", "cmd", "ps1", "cs", "zip", "wer", "pdb", "reg"
    };

    public static string Build(string input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0) return SearchHome;
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return value;

        // Only a single dotted token with no whitespace is treated as an address:
        // "nt!KeBugCheckEx" and "0x1E_C0000096_nt!HalpHvTimerStop" must stay searches.
        var firstSegment = value.Split('/')[0];
        var lastLabel = firstSegment.Split('.')[^1];
        var looksLikeHost = !value.Any(char.IsWhiteSpace)
            && value.Contains('.')
            && !value.Contains('!')
            && Uri.CheckHostName(firstSegment) != UriHostNameType.Unknown
            && lastLabel.Length >= 2
            && lastLabel.All(char.IsLetter)
            && !FileExtensions.Contains(lastLabel);
        if (looksLikeHost) return "https://" + value;

        var flattened = value.Replace('\r', ' ').Replace('\n', ' ');
        if (flattened.Length > MaximumQueryLength) flattened = flattened[..MaximumQueryLength];
        return SearchHome + "/search?q=" + Uri.EscapeDataString(flattened.Trim());
    }
}
