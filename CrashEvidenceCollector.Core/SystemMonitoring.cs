using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace CrashEvidenceCollector.Core;

public sealed record MonitorSample(
    DateTimeOffset Timestamp,
    double? CpuUtilityPercent,
    double? CpuPerformancePercent,
    double? CpuEffectiveMhz,
    double? MemoryLoadPercent,
    IReadOnlyDictionary<string, double>? ThermalZonesC,
    IReadOnlyDictionary<string, double>? HwInfoSensors,
    string SensorSource,
    double? CommitPercent = null,
    double? PoolNonpagedMb = null,
    double? PoolPagedMb = null);

public sealed record MonitorEventRecord(DateTimeOffset Timestamp, string Provider, int EventId, string Summary);

/// <summary>
/// Windows performance-counter sampler (PDH via P/Invoke; no kernel driver, no
/// packages). English counter paths keep sampling locale-independent. Rate
/// counters legitimately return no value on the first collection.
/// </summary>
public sealed class PdhCpuSampler : IDisposable
{
    private nint _query;
    private nint _utility, _performance;
    private nint _thermal;
    private nint _poolNonpaged, _poolPaged;
    private bool _primed;
    private readonly double _nominalMhz;

    public PdhCpuSampler()
    {
        _nominalMhz = ReadNominalMhz();
        if (PdhOpenQuery(0, 0, out _query) != 0) { _query = 0; return; }
        PdhAddEnglishCounterW(_query, @"\Processor Information(_Total)\% Processor Utility", 0, out _utility);
        PdhAddEnglishCounterW(_query, @"\Processor Information(_Total)\% Processor Performance", 0, out _performance);
        PdhAddEnglishCounterW(_query, @"\Thermal Zone Information(*)\Temperature", 0, out _thermal);
        PdhAddEnglishCounterW(_query, @"\Memory\Pool Nonpaged Bytes", 0, out _poolNonpaged);
        PdhAddEnglishCounterW(_query, @"\Memory\Pool Paged Bytes", 0, out _poolPaged);
    }

    public (double? Utility, double? Performance, double? EffectiveMhz, IReadOnlyDictionary<string, double>? ThermalZonesC, double? PoolNonpagedMb, double? PoolPagedMb) Sample()
    {
        if (_query == 0 || PdhCollectQueryData(_query) != 0) return (null, null, null, null, null, null);
        if (!_primed) { _primed = true; return (null, null, null, ReadThermal(), ReadMb(_poolNonpaged), ReadMb(_poolPaged)); }
        var utility = ReadDouble(_utility);
        var performance = ReadDouble(_performance);
        var mhz = performance is null || _nominalMhz <= 0 ? (double?)null : Math.Round(performance.Value / 100d * _nominalMhz);
        return (utility, performance, mhz, ReadThermal(), ReadMb(_poolNonpaged), ReadMb(_poolPaged));
    }

    private double? ReadMb(nint counter) => ReadDouble(counter) is { } bytes ? Math.Round(bytes / (1024 * 1024), 1) : null;

    private double? ReadDouble(nint counter)
    {
        if (counter == 0) return null;
        var value = default(PdhFmtCounterValue);
        return PdhGetFormattedCounterValue(counter, PdhFmtDouble, out _, ref value) == 0 && value.Status == 0 ? value.DoubleValue : null;
    }

    private IReadOnlyDictionary<string, double>? ReadThermal()
    {
        if (_thermal == 0) return null;
        var size = 0; var count = 0;
        var status = PdhGetFormattedCounterArrayW(_thermal, PdhFmtDouble, ref size, ref count, 0);
        if (status != PdhMoreData || size <= 0) return null;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (PdhGetFormattedCounterArrayW(_thermal, PdhFmtDouble, ref size, ref count, buffer) != 0) return null;
            var zones = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var itemSize = Marshal.SizeOf<PdhFmtCounterItem>();
            for (var index = 0; index < count; index++)
            {
                var item = Marshal.PtrToStructure<PdhFmtCounterItem>(buffer + index * itemSize);
                var name = Marshal.PtrToStringUni(item.Name) ?? $"zone{index}";
                if (item.Value.Status == 0) zones[name] = Math.Round(item.Value.DoubleValue - 273.15, 1);
            }
            return zones.Count == 0 ? null : zones;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static double ReadNominalMhz()
    {
        try { using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"); return key?.GetValue("~MHz") is int mhz ? mhz : 0; }
        catch { return 0; }
    }

    public void Dispose() { if (_query != 0) { PdhCloseQuery(_query); _query = 0; } }

    private const uint PdhFmtDouble = 0x00000200;
    private const int PdhMoreData = unchecked((int)0x800007D2);
    [StructLayout(LayoutKind.Explicit)] private struct PdhFmtCounterValue { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double DoubleValue; }
    [StructLayout(LayoutKind.Sequential)] private struct PdhFmtCounterItem { public nint Name; public PdhFmtCounterValue Value; }
    [DllImport("pdh.dll")] private static extern int PdhOpenQuery(nint dataSource, nuint userData, out nint query);
    [DllImport("pdh.dll")] private static extern int PdhCloseQuery(nint query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhAddEnglishCounterW(nint query, string path, nuint userData, out nint counter);
    [DllImport("pdh.dll")] private static extern int PdhCollectQueryData(nint query);
    [DllImport("pdh.dll")] private static extern int PdhGetFormattedCounterValue(nint counter, uint format, out uint type, ref PdhFmtCounterValue value);
    [DllImport("pdh.dll")] private static extern int PdhGetFormattedCounterArrayW(nint counter, uint format, ref int bufferSize, ref int itemCount, nint buffer);
}

/// <summary>
/// Reads sensors that HWiNFO publishes to HKCU\SOFTWARE\HWiNFO64\VSB when its
/// "Gadget" reporting is enabled. This is the only driver-free way BSODGuru can
/// see pump RPM, Vcore, and true package temperature; absence is reported
/// honestly rather than substituted.
/// </summary>
public static class HwInfoGadgetReader
{
    public static IReadOnlyDictionary<string, double>? Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\HWiNFO64\VSB");
            if (key is null) return null;
            var pairs = new List<(string Label, string Raw)>();
            for (var index = 0; index < 128; index++)
            {
                var label = key.GetValue($"Label{index}") as string;
                var raw = key.GetValue($"ValueRaw{index}") as string;
                if (label is null || raw is null) continue;
                pairs.Add((label, raw));
            }
            return BuildSensorMap(pairs);
        }
        catch { return null; }
    }

    public static IReadOnlyDictionary<string, double>? BuildSensorMap(IEnumerable<(string Label, string Raw)> pairs)
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (label, raw) in pairs)
        {
            if (string.IsNullOrWhiteSpace(label) || !double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) continue;
            var name = label.Trim(); var suffix = 1;
            while (map.ContainsKey(name)) name = $"{label.Trim()} #{++suffix}";
            map[name] = value;
        }
        return map.Count == 0 ? null : map;
    }

    /// <summary>Best-available CPU temperature: HWiNFO package sensor first, hottest ACPI zone second.</summary>
    public static double? SelectCpuTemperature(MonitorSample sample)
    {
        var package = sample.HwInfoSensors?.Where(item => item.Key.Contains("CPU Package", StringComparison.OrdinalIgnoreCase) && !item.Key.Contains("Power", StringComparison.OrdinalIgnoreCase) && item.Value is > 1 and < 130).Select(item => (double?)item.Value).FirstOrDefault();
        return package ?? sample.ThermalZonesC?.Values.Where(value => value is > 1 and < 130).OrderDescending().Cast<double?>().FirstOrDefault();
    }
}

/// <summary>
/// Crash-safe monitoring log: one JSON line per record, appended and flushed
/// per write so a bugcheck loses at most the final sample. Day files rotate;
/// old days are purged. All timestamps are recorded in the sample itself.
/// </summary>
public static class MonitorLog
{
    private static readonly JsonSerializerOptions Line = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string DefaultDirectory => Path.Combine(AppPaths.StateDirectory, "Monitoring");
    public const int RetentionDays = 14;

    public static async Task AppendSampleAsync(string directory, MonitorSample sample, CancellationToken token)
        => await File.AppendAllTextAsync(DayPath(directory, "monitor", sample.Timestamp), JsonSerializer.Serialize(sample, Line) + "\n", token).ConfigureAwait(false);

    public static async Task AppendEventAsync(string directory, MonitorEventRecord record, CancellationToken token)
        => await File.AppendAllTextAsync(DayPath(directory, "monitor-events", record.Timestamp), JsonSerializer.Serialize(record, Line) + "\n", token).ConfigureAwait(false);

    public static async Task<IReadOnlyList<MonitorSample>> LoadSamplesAsync(string directory, DateTimeOffset from, DateTimeOffset to, CancellationToken token)
        => await LoadAsync<MonitorSample>(directory, "monitor", from, to, item => item.Timestamp, token).ConfigureAwait(false);

    public static async Task<IReadOnlyList<MonitorEventRecord>> LoadEventsAsync(string directory, DateTimeOffset from, DateTimeOffset to, CancellationToken token)
        => await LoadAsync<MonitorEventRecord>(directory, "monitor-events", from, to, item => item.Timestamp, token).ConfigureAwait(false);

    public static void Purge(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.EnumerateFiles(directory, "monitor*.jsonl"))
                try { if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-RetentionDays)) File.Delete(file); } catch { }
        }
        catch { }
    }

    private static string DayPath(string directory, string prefix, DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{prefix}-{timestamp.ToUniversalTime():yyyyMMdd}.jsonl");
    }

    private static async Task<IReadOnlyList<T>> LoadAsync<T>(string directory, string prefix, DateTimeOffset from, DateTimeOffset to, Func<T, DateTimeOffset> timestamp, CancellationToken token)
    {
        var result = new List<T>();
        if (!Directory.Exists(directory)) return result;
        for (var day = from.ToUniversalTime().Date; day <= to.ToUniversalTime().Date; day = day.AddDays(1))
        {
            var path = Path.Combine(directory, $"{prefix}-{day:yyyyMMdd}.jsonl");
            if (!File.Exists(path)) continue;
            foreach (var line in await File.ReadAllLinesAsync(path, token).ConfigureAwait(false))
            {
                // A torn final line from a bugcheck mid-write must never poison the history.
                try { var item = JsonSerializer.Deserialize<T>(line, Line); if (item is not null && timestamp(item) >= from && timestamp(item) <= to) result.Add(item); }
                catch (JsonException) { }
            }
        }
        return result;
    }
}

/// <summary>
/// Background monitoring loop: samples every interval, records hardware events
/// (WHEA, processor throttling, thermal power events) as they appear, keeps a
/// rolling in-memory window for live charts, and appends everything to the
/// crash-safe log. Read-only against the system; writes only its own log.
/// </summary>
public sealed class SystemMonitorService : IDisposable
{
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EventScanInterval = TimeSpan.FromSeconds(20);
    private readonly string _directory;
    private readonly EventLogReader _eventReader = new();
    private readonly List<MonitorSample> _window = [];
    private readonly List<MonitorEventRecord> _recentEvents = [];
    private readonly Lock _sync = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private DateTimeOffset _lastEventScan = DateTimeOffset.Now.AddHours(-12);

    public event Action? Updated;
    public bool IsRunning => _loop is { IsCompleted: false };
    public string Directory { get; }

    public SystemMonitorService(string? directory = null) { Directory = _directory = directory ?? MonitorLog.DefaultDirectory; }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); _loop?.Wait(TimeSpan.FromSeconds(3)); } catch { }
        _cts?.Dispose(); _cts = null; _loop = null;
    }

    public (IReadOnlyList<MonitorSample> Samples, IReadOnlyList<MonitorEventRecord> Events) Snapshot()
    {
        lock (_sync) return (_window.ToList(), _recentEvents.ToList());
    }

    private async Task RunAsync(CancellationToken token)
    {
        MonitorLog.Purge(_directory);
        using var sampler = new PdhCpuSampler();
        sampler.Sample(); // prime rate counters
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SampleInterval, token).ConfigureAwait(false);
                var (utility, performance, mhz, zones, poolNonpaged, poolPaged) = sampler.Sample();
                var hwinfo = HwInfoGadgetReader.Read();
                var source = hwinfo is not null && zones is not null ? "PDH + ACPI zones + HWiNFO gadget" : hwinfo is not null ? "PDH + HWiNFO gadget" : zones is not null ? "PDH + ACPI zones" : "PDH";
                var memory = ReadMemory();
                var sample = new MonitorSample(DateTimeOffset.Now, utility, performance, mhz, memory.LoadPercent, zones, hwinfo, source, memory.CommitPercent, poolNonpaged, poolPaged);
                await MonitorLog.AppendSampleAsync(_directory, sample, token).ConfigureAwait(false);
                if (DateTimeOffset.Now - _lastEventScan >= EventScanInterval) await ScanEventsAsync(token).ConfigureAwait(false);
                lock (_sync)
                {
                    _window.Add(sample);
                    if (_window.Count > 1800) _window.RemoveRange(0, _window.Count - 1800);
                }
                Updated?.Invoke();
            }
            catch (OperationCanceledException) { return; }
            catch { /* an individual failed sample must never end the monitoring loop */ }
        }
    }

    private async Task ScanEventsAsync(CancellationToken token)
    {
        var from = _lastEventScan; _lastEventScan = DateTimeOffset.Now;
        IReadOnlyList<EvidenceEvent> events;
        try { events = await _eventReader.ReadAsync("System", from, _lastEventScan.AddSeconds(1), null, token).ConfigureAwait(false); }
        catch { return; }
        foreach (var entry in events.Where(IsHardwareSignal))
        {
            var record = new MonitorEventRecord(entry.Timestamp, entry.Provider, entry.EventId, Summarize(entry));
            await MonitorLog.AppendEventAsync(_directory, record, token).ConfigureAwait(false);
            lock (_sync) { _recentEvents.Add(record); if (_recentEvents.Count > 200) _recentEvents.RemoveRange(0, _recentEvents.Count - 200); }
        }
    }

    public static bool IsHardwareSignal(EvidenceEvent entry)
        => entry.Provider.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase)
           || (entry.Provider.Equals("Microsoft-Windows-Kernel-Processor-Power", StringComparison.OrdinalIgnoreCase) && entry.EventId == 37)
           || (entry.Provider.Equals("Microsoft-Windows-Kernel-Power", StringComparison.OrdinalIgnoreCase) && entry.EventId is 125 or 41);

    public static string Summarize(EvidenceEvent entry)
    {
        if (entry.Provider.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase))
        {
            var component = entry.Data?.TryGetValue("ErrorSourceType", out var type) == true ? $" (source type {type})" : string.Empty;
            var apic = entry.Data?.TryGetValue("ApicId", out var id) == true ? $", APIC {id}" : string.Empty;
            return entry.EventId switch
            {
                19 => $"Corrected machine check{apic} — corrected hardware error reported by the processor.",
                18 or 20 => $"Fatal/uncorrected machine check{apic}.",
                17 => "Corrected PCI Express error.",
                _ => $"WHEA hardware event {entry.EventId}{component}{apic}."
            };
        }
        if (entry.EventId == 37) return "Processor speed limited by firmware/thermal control.";
        if (entry.EventId == 125) return "Kernel-Power thermal event.";
        if (entry.EventId == 41) return "System rebooted without a clean shutdown (crash, freeze, or power loss).";
        return $"{entry.Provider} event {entry.EventId}.";
    }

    private static (double? LoadPercent, double? CommitPercent) ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status)) return (null, null);
        // TotalPageFile is the commit limit; commit charge running out crashes
        // processes long before physical memory is exhausted.
        var commit = status.TotalPageFile > 0 ? Math.Round((status.TotalPageFile - status.AvailPageFile) / (double)status.TotalPageFile * 100, 1) : (double?)null;
        return (status.MemoryLoad, commit);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx { public uint Length; public uint MemoryLoad; public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    public void Dispose() => Stop();
}

/// <summary>
/// Turns retained monitoring history around a crash window into report
/// evidence. When no monitoring data exists for the window, that absence is
/// stated; sensor values are never estimated or backfilled.
/// </summary>
public static class MonitorWindowAnalyzer
{
    public static async Task<(EvidenceItem Item, IReadOnlyList<Observation> Observations, IReadOnlyList<MonitorSample> Samples)> ExportWindowAsync(string monitorDirectory, string rawDirectory, DateTimeOffset from, DateTimeOffset to, CancellationToken token)
    {
        var samples = await MonitorLog.LoadSamplesAsync(monitorDirectory, from, to, token).ConfigureAwait(false);
        var events = await MonitorLog.LoadEventsAsync(monitorDirectory, from, to, token).ConfigureAwait(false);
        if (samples.Count == 0 && events.Count == 0)
            return (new("Background monitoring", EvidenceState.Skipped, "No background-monitoring samples exist for the crash window. Enable Live monitor to capture temperatures, clocks, and hardware events before future incidents."), [], samples);

        var line = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var samplesPath = Path.Combine(rawDirectory, "monitoring-window.jsonl");
        await File.WriteAllLinesAsync(samplesPath, samples.Select(sample => JsonSerializer.Serialize(sample, line)), token).ConfigureAwait(false);
        var files = new List<string> { samplesPath };
        if (events.Count > 0)
        {
            var eventsPath = Path.Combine(rawDirectory, "monitoring-window-events.jsonl");
            await File.WriteAllLinesAsync(eventsPath, events.Select(record => JsonSerializer.Serialize(record, line)), token).ConfigureAwait(false);
            files.Add(eventsPath);
        }

        var observations = BuildObservations(samples, events);
        var item = new EvidenceItem("Background monitoring", EvidenceState.Success, $"Preserved {samples.Count} monitor sample(s) and {events.Count} hardware event record(s) covering {from.ToLocalTime():G} to {to.ToLocalTime():G}.", files);
        return (item, observations, samples);
    }

    public static IReadOnlyList<Observation> BuildObservations(IReadOnlyList<MonitorSample> samples, IReadOnlyList<MonitorEventRecord> events)
    {
        var observations = new List<Observation>();
        var temperatures = samples.Select(HwInfoGadgetReader.SelectCpuTemperature).OfType<double>().ToList();
        if (temperatures.Count > 0)
        {
            var maximum = temperatures.Max(); var average = temperatures.Average();
            var severity = maximum >= 95 ? "Warning" : "Info";
            observations.Add(new(severity, "Monitored temperature during window",
                $"Background monitoring recorded temperature max {maximum:0} °C / average {average:0} °C across {temperatures.Count} sample(s) in the window (best available source: HWiNFO package sensor when published, otherwise the hottest ACPI thermal zone, which on desktop boards may track the chipset rather than the CPU die). " +
                (maximum >= 95 ? "Sustained or spiking temperatures at this level engage thermal throttling and can indicate a cooling fault; they are corroborating context for hardware-family failures, not proof of the bugcheck cause." : "Temperatures alone do not establish causation; they are recorded for correlation.")));
        }
        var utilities = samples.Select(sample => sample.CpuUtilityPercent).OfType<double>().ToList();
        if (utilities.Count > 0 && temperatures.Count > 0 && temperatures.Max() >= 95 && utilities.Average() < 30)
            observations.Add(new("Warning", "High temperature at low load",
                $"CPU temperature reached {temperatures.Max():0} °C while average CPU utility was only {utilities.Average():0}%. Heat generated at low load that is not removed indicates a cooler pump, contact, or mounting fault rather than workload."));
        var commits = samples.Select(sample => sample.CommitPercent).OfType<double>().ToList();
        if (commits.Count > 0 && commits.Max() >= 90)
            observations.Add(new("Warning", "Commit charge neared exhaustion",
                $"Commit charge peaked at {commits.Max():0}% of the commit limit during the monitored window. Allocation failures near the commit limit can destabilise drivers and precede memory-management bugchecks; check the page-file configuration and which processes held the commit."));
        foreach (var group in events.GroupBy(record => (record.Provider, record.EventId)))
            observations.Add(new(group.Key.EventId is 18 or 20 ? "Warning" : "Info", "Hardware event during monitored window",
                $"{group.Count()}× {group.First().Summary} Recorded live by background monitoring between {group.Min(record => record.Timestamp).ToLocalTime():G} and {group.Max(record => record.Timestamp).ToLocalTime():G}."));
        return observations;
    }
}
