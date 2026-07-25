using System.Diagnostics;
using System.Text;

namespace CrashEvidenceCollector.Core;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}

public sealed record CommandExecutionProgress(int StandardOutputLines, int StandardErrorLines, long CapturedCharacters, string LastLine);

public static class CommandRunner
{
    public static async Task<CommandResult> RunAsync(string fileName, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken, IProgress<CommandExecutionProgress>? progress = null)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo { FileName = fileName, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        // Both pipes are consumed line by line so long-running debugger work can
        // expose visible activity without waiting for CDB to exit. The readers do
        // not share the timeout token: killing the process closes both pipes and
        // still allows partial output to be preserved.
        var output = new StringBuilder(); var error = new StringBuilder();
        var outputLines = 0; var errorLines = 0; long capturedCharacters = 0;
        async Task PumpAsync(StreamReader reader, StringBuilder target, bool isError)
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                target.AppendLine(line);
                if (isError) Interlocked.Increment(ref errorLines); else Interlocked.Increment(ref outputLines);
                Interlocked.Add(ref capturedCharacters, line.Length + Environment.NewLine.Length);
                progress?.Report(new(Volatile.Read(ref outputLines), Volatile.Read(ref errorLines), Interlocked.Read(ref capturedCharacters), line));
            }
        }
        var outputPump = PumpAsync(process.StandardOutput, output, false);
        var errorPump = PumpAsync(process.StandardError, error, true);
        try { await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            // Both user cancellation and timeout must terminate the complete debugger
            // process tree; leaving CDB or symbol helpers behind can lock dump files.
            try { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            await Task.WhenAll(outputPump, errorPump).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) throw;
            return new(-1, output.ToString(), error + (error.Length > 0 ? Environment.NewLine : string.Empty) + "Command timed out.");
        }
        await Task.WhenAll(outputPump, errorPump).ConfigureAwait(false);
        return new(process.ExitCode, output.ToString(), error.ToString());
    }
}

/// <summary>
/// Reports synchronously on the producer thread. This is useful for internal
/// progress aggregation where posting through a captured UI context would make
/// line counts lag behind the heartbeat.
/// </summary>
internal sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
