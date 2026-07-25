using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public sealed record DumpCopyManifestEntry(string SourcePath, string CopiedPath, long FileSize = 0, DateTimeOffset SourceCreationTime = default, DateTimeOffset SourceModificationTime = default);
public sealed record TestDumpMetadata(string FileName, DateTimeOffset CreationTime, DateTimeOffset ModificationTime);

public sealed class FileEvidenceCollector
{
    public async Task<EvidenceItem> CopyDumpsAsync(Incident incident, string destination, CollectionOptions options, CancellationToken cancellationToken, IProgress<CollectionProgress>? progress = null)
    {
        Directory.CreateDirectory(destination);
        var copied = new List<string>();
        var failures = new List<string>();
        var sources = new List<string>();
        var testMetadata = new Dictionary<string, TestDumpMetadata>(StringComparer.OrdinalIgnoreCase);
        if (options.IsTestDataMode)
        {
            var dumpFolder = Path.Combine(Path.GetFullPath(options.TestDataDirectory!), "Dumps");
            if (Directory.Exists(dumpFolder)) sources.AddRange(Directory.EnumerateFiles(dumpFolder, "*.dmp", SearchOption.TopDirectoryOnly));
            var metadataPath = Path.Combine(dumpFolder, "dump-metadata.json");
            if (File.Exists(metadataPath))
            {
                try { testMetadata = (JsonSerializer.Deserialize<List<TestDumpMetadata>>(await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false), JsonDefaults.Indented) ?? []).Where(item => !string.IsNullOrWhiteSpace(item.FileName)).ToDictionary(item => item.FileName, StringComparer.OrdinalIgnoreCase); }
                catch (Exception ex) { failures.Add("Test dump metadata: " + ex.Message); }
            }
        }
        else
        {
            var minidump = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump");
            try { if (Directory.Exists(minidump)) sources.AddRange(Directory.EnumerateFiles(minidump, "*.dmp").Where(path => IsNearIncident(path, incident))); }
            catch (Exception ex) { failures.Add("Minidump listing: " + ex.Message); }
            AddRecursiveDumps(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "LiveKernelReports"), "Live-kernel dump listing");
            AddRecursiveDumps(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\WER\ReportArchive"), "Machine WER dump listing");
            AddRecursiveDumps(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\WER\ReportArchive"), "User WER dump listing");
            if (options.IncludeFullMemoryDump) sources.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "MEMORY.DMP"));
        }
        var manifest = new List<DumpCopyManifestEntry>();
        foreach (var source in sources.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!File.Exists(source)) { failures.Add($"Not found: {source}"); continue; }
                var sourceInfo = new FileInfo(source);
                var fixtureMetadata = testMetadata.GetValueOrDefault(Path.GetFileName(source));
                var sourceCreation = fixtureMetadata?.CreationTime ?? sourceInfo.CreationTimeUtc;
                var sourceModification = fixtureMetadata?.ModificationTime ?? sourceInfo.LastWriteTimeUtc;
                var target = UniquePath(destination, Path.GetFileName(source));
                await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var length = input.Length; long transferred = 0; long lastReported = 0; var buffer = new byte[1024 * 1024];
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                        if (read == 0) break;
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        transferred += read;
                        if (transferred == length || transferred - lastReported >= 16L * 1024 * 1024)
                        {
                            lastReported = transferred;
                            var filePercent = length == 0 ? 100 : (int)Math.Clamp(transferred * 100L / length, 0, 100);
                            progress?.Report(new(72 + filePercent * 5 / 100, "Dumps", $"Copying {Path.GetFileName(source)} — {FormatBytes(transferred)} / {FormatBytes(length)} ({filePercent}%)"));
                        }
                    }
                }
                File.SetCreationTimeUtc(target, sourceCreation.UtcDateTime);
                File.SetLastWriteTimeUtc(target, sourceModification.UtcDateTime);
                copied.Add(target); manifest.Add(new(source, target, sourceInfo.Length, sourceCreation, sourceModification));
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { failures.Add($"{source}: {ex.Message}"); }
        }
        var manifestPath = Path.Combine(destination, "dump-manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonDefaults.Indented), cancellationToken).ConfigureAwait(false);
        var state = failures.Count == 0 ? EvidenceState.Success : copied.Count > 0 ? EvidenceState.Warning : EvidenceState.Failure;
        var fullDumpNote = options.IncludeFullMemoryDump ? "Full MEMORY.DMP was requested." : "Full MEMORY.DMP was excluded by the privacy/size setting.";
        if (sources.Count == 0) return new("Crash dumps", EvidenceState.Warning, $"No relevant minidumps were found. {fullDumpNote} This is normal after power loss or when dump creation was unavailable.");
        return new("Crash dumps", state, $"Copied {copied.Count} dump(s) using streaming I/O. {fullDumpNote}", copied.Append(manifestPath).ToList(), failures.Count == 0 ? null : string.Join(Environment.NewLine, failures), copied.Sum(path => new FileInfo(path).Length));

        void AddRecursiveDumps(string root, string label)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                var enumeration = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, ReturnSpecialDirectories = false };
                sources.AddRange(Directory.EnumerateFiles(root, "*.dmp", enumeration).Where(path => IsNearIncident(path, incident)).Take(30));
            }
            catch (Exception ex) { failures.Add($"{label}: {ex.Message}"); }
        }
    }

    private static bool IsNearIncident(string path, Incident incident)
    {
        try { return Math.Abs((File.GetLastWriteTimeUtc(path) - incident.Timestamp.UtcDateTime).TotalDays) <= 1; }
        catch { return false; }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"]; var value = (double)bytes; var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.0} {units[unit]}";
    }

    public async Task<EvidenceItem> CopyWerAsync(Incident incident, string destination, CollectionOptions options, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        var roots = options.IsTestDataMode ? [Path.Combine(Path.GetFullPath(options.TestDataDirectory!), "WER")] : new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\WER\ReportArchive"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\WER\ReportArchive") };
        var copied = new List<string>(); var errors = new List<string>();
        foreach (var root in roots)
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                var enumeration = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, ReturnSpecialDirectories = false };
                foreach (var file in Directory.EnumerateFiles(root, "Report.wer", enumeration).Where(path => options.IsTestDataMode || Math.Abs((File.GetLastWriteTimeUtc(path) - incident.Timestamp.UtcDateTime).TotalDays) <= 7).Take(100))
                {
                    var target = UniquePath(destination, Path.GetFileName(Path.GetDirectoryName(file)) + "-Report.wer");
                    await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous);
                    await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
                    await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false); copied.Add(target);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { errors.Add($"{root}: {ex.Message}"); }
        }
        var state = errors.Count == 0 ? EvidenceState.Success : copied.Count > 0 ? EvidenceState.Warning : EvidenceState.Failure;
        return new("Reliability Monitor / WER", state, $"Copied {copied.Count} WER report(s).", copied, errors.Count == 0 ? null : string.Join(Environment.NewLine, errors));
    }

    internal static string UniquePath(string folder, string fileName)
    {
        fileName = string.Concat(fileName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(folder, fileName); var counter = 1;
        while (File.Exists(path) || Directory.Exists(path)) path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(fileName)}-{counter++}{Path.GetExtension(fileName)}");
        return path;
    }
}
