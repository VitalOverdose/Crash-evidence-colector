using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrashEvidenceCollector.Core;

public static partial class ElevatedHelperIpc
{
    public const string ProtocolVersion = "1";

    public static async Task<HelperResponse> RequestDumpCopyAsync(string helperPath, string destination, bool includeFullDump, CancellationToken cancellationToken, DateTimeOffset? incidentTimestamp = null)
    {
        var pipeName = "CrashEvidenceCollector-" + Guid.NewGuid().ToString("N");
        var nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var process = Process.Start(new ProcessStartInfo(helperPath) { UseShellExecute = true, Verb = "runas", Arguments = $"--pipe {pipeName} --nonce {nonce}", WorkingDirectory = Path.GetDirectoryName(helperPath)! }) ?? throw new InvalidOperationException("The elevated helper could not be started.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await pipe.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
        var request = new HelperRequest(ProtocolVersion, nonce, "CopyCrashDumps", Path.GetFullPath(destination), includeFullDump, incidentTimestamp);
        await WriteLineAsync(pipe, JsonSerializer.Serialize(request, JsonDefaults.Indented), timeout.Token).ConfigureAwait(false);
        var responseJson = await ReadLineAsync(pipe, timeout.Token).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize<HelperResponse>(responseJson, JsonDefaults.Indented) ?? throw new InvalidDataException("The helper returned an invalid response.");
        if (!FixedEquals(nonce, response.Nonce)) throw new InvalidDataException("The helper response nonce was invalid.");
        return response;
    }

    public static async Task<int> RunHelperAsync(string[] args, CancellationToken cancellationToken)
    {
        if (!TryReadArguments(args, out var pipeName, out var nonce)) return 2;
        // The unelevated UI owns a current-user-only server pipe. Do not request
        // client-side owner validation here: across a UAC integrity boundary,
        // Windows can expose a different owner token even though the server ACL
        // correctly permits only the same account. Protocol nonce validation still
        // authenticates the exact UI request after connection.
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(30000, cancellationToken).ConfigureAwait(false);
        try
        {
            var json = await ReadLineAsync(pipe, cancellationToken).ConfigureAwait(false);
            var request = JsonSerializer.Deserialize<HelperRequest>(json, JsonDefaults.Indented);
            if (request is null || request.ProtocolVersion != ProtocolVersion || !FixedEquals(request.Nonce, nonce) || request.Operation != "CopyCrashDumps") throw new InvalidDataException("The helper request failed validation.");
            var destination = ValidateDestination(request.Destination); Directory.CreateDirectory(destination);
            var copied = new List<string>(); var errors = new List<string>();
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows); var sources = new List<string>();
            try
            {
                var minidump = Path.Combine(windows, "Minidump");
                if (Directory.Exists(minidump)) sources.AddRange(Directory.EnumerateFiles(minidump, "*.dmp").Where(path => request.IncidentTimestamp is null || Math.Abs((File.GetLastWriteTimeUtc(path) - request.IncidentTimestamp.Value.UtcDateTime).TotalDays) <= 7));
            }
            catch (Exception ex) { errors.Add("Minidumps: " + ex.Message); }
            if (request.IncludeFullDump) sources.Add(Path.Combine(windows, "MEMORY.DMP"));
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!File.Exists(source)) continue;
                    var target = FileEvidenceCollector.UniquePath(destination, Path.GetFileName(source));
                    var sourceInfo = new FileInfo(source);
                    await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                        await input.CopyToAsync(output, 1024 * 1024, cancellationToken).ConfigureAwait(false);
                    // Preserve source times on the evidence copy so timestamp
                    // correlation remains reliable after elevated collection.
                    File.SetCreationTimeUtc(target, sourceInfo.CreationTimeUtc);
                    File.SetLastWriteTimeUtc(target, sourceInfo.LastWriteTimeUtc);
                    copied.Add(target);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { errors.Add($"{Path.GetFileName(source)}: {ex.Message}"); }
            }
            await WriteLineAsync(pipe, JsonSerializer.Serialize(new HelperResponse(errors.Count == 0, nonce, copied, errors), JsonDefaults.Indented), cancellationToken).ConfigureAwait(false);
            return errors.Count == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            try { await WriteLineAsync(pipe, JsonSerializer.Serialize(new HelperResponse(false, nonce, [], [ex.Message]), JsonDefaults.Indented), cancellationToken).ConfigureAwait(false); } catch { }
            return 1;
        }
    }

    private static string ValidateDestination(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value) || value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) throw new InvalidDataException("The destination is invalid.");
        var path = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar); var windows = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).TrimEnd(Path.DirectorySeparatorChar);
        if (path.Equals(Path.GetPathRoot(path)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || path.StartsWith(windows + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The helper will not write to a drive root or Windows directory.");
        return path;
    }
    private static bool TryReadArguments(string[] args, out string pipeName, out string nonce)
    {
        pipeName = string.Empty; nonce = string.Empty; if (args.Length != 4 || args[0] != "--pipe" || args[2] != "--nonce") return false;
        pipeName = args[1]; nonce = args[3]; return PipeNameRegex().IsMatch(pipeName) && NonceRegex().IsMatch(nonce);
    }
    private static bool FixedEquals(string left, string right) => left.Length == right.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    private static async Task WriteLineAsync(Stream stream, string value, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(value.Replace("\r", string.Empty).Replace("\n", string.Empty) + "\n"); if (bytes.Length > 65536) throw new InvalidDataException("IPC message is too large.");
        await stream.WriteAsync(bytes, token).ConfigureAwait(false); await stream.FlushAsync(token).ConfigureAwait(false);
    }
    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken token)
    {
        using var buffer = new MemoryStream(); var one = new byte[1];
        while (buffer.Length <= 65536) { var read = await stream.ReadAsync(one, token).ConfigureAwait(false); if (read == 0 || one[0] == (byte)'\n') break; buffer.WriteByte(one[0]); }
        if (buffer.Length > 65536) throw new InvalidDataException("IPC message is too large."); return Encoding.UTF8.GetString(buffer.ToArray());
    }
    [GeneratedRegex("^CrashEvidenceCollector-[a-f0-9]{32}$", RegexOptions.CultureInvariant)] private static partial Regex PipeNameRegex();
    [GeneratedRegex("^[A-F0-9]{64}$", RegexOptions.CultureInvariant)] private static partial Regex NonceRegex();
}
