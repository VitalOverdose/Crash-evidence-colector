using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public sealed class StructuredLog
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public StructuredLog(string? path = null)
    {
        _path = path ?? AppPaths.LogFile;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public async Task WriteAsync(string level, string message, object? data = null, CancellationToken cancellationToken = default)
    {
        var entry = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.Now, level, message, data });
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await File.AppendAllTextAsync(_path, entry + Environment.NewLine, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
}
