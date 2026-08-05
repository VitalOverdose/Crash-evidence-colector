using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;

namespace CrashEvidenceCollector.Core;

public sealed class SystemEvidenceCollector
{
    private const string InventoryScript = "$ErrorActionPreference='Stop'; $os=Get-CimInstance Win32_OperatingSystem; $bios=Get-CimInstance Win32_BIOS; $board=Get-CimInstance Win32_BaseBoard; $cpu=Get-CimInstance Win32_Processor; $ram=Get-CimInstance Win32_PhysicalMemory; $gpu=Get-CimInstance Win32_VideoController; $disk=Get-CimInstance Win32_DiskDrive; $pd=Get-PhysicalDisk -ErrorAction SilentlyContinue; [ordered]@{Windows=($os.Caption+' '+$os.Version+' build '+$os.BuildNumber);LastBoot=$os.LastBootUpTime;Bios=($bios.Manufacturer+' '+$bios.SMBIOSBIOSVersion+' '+$bios.ReleaseDate);Board=($board.Manufacturer+' '+$board.Product);Cpu=($cpu|ForEach-Object Name)-join'; ';Memory=(($ram|Measure-Object Capacity -Sum).Sum.ToString()+' bytes; speeds '+(($ram|ForEach-Object Speed)-join', ')+' MT/s');Gpu=($gpu|ForEach-Object { $_.Name+' driver '+$_.DriverVersion })-join'; ';Storage=($disk|ForEach-Object { $_.Model+' '+$_.Size+' bytes' })-join'; ';Health=($pd|ForEach-Object { $_.FriendlyName+' health='+$_.HealthStatus+' operational='+($_.OperationalStatus-join',') })-join'; '}|ConvertTo-Json -Compress -Depth 4";
    private const string VirtualizationScript = "$ErrorActionPreference='SilentlyContinue'; $dg=Get-CimInstance -Namespace root/Microsoft/Windows/DeviceGuard -Class Win32_DeviceGuard; [ordered]@{HypervisorPresent=(Get-CimInstance Win32_ComputerSystem).HypervisorPresent;VbsStatus=$dg.VirtualizationBasedSecurityStatus;SecurityServicesConfigured=$dg.SecurityServicesConfigured;SecurityServicesRunning=$dg.SecurityServicesRunning;DeviceGuardRequiredSecurityProperties=$dg.RequiredSecurityProperties}|ConvertTo-Json -Compress -Depth 5";

    public async Task<MachineSnapshot> CollectAsync(string rawDirectory, string? testDataDirectory, List<string> errors, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(rawDirectory);
        var inventory = await GetOutputAsync("machine-inventory.json", InventoryScript, testDataDirectory, cancellationToken).ConfigureAwait(false);
        var virtualization = await GetOutputAsync("virtualization.json", VirtualizationScript, testDataDirectory, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(rawDirectory, "machine-inventory.json"), inventory.Output, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(rawDirectory, "virtualization.json"), virtualization.Output, cancellationToken).ConfigureAwait(false);
        if (!inventory.Success) errors.Add("Machine inventory: " + inventory.Error);
        if (!virtualization.Success) errors.Add("Virtualization inventory: " + virtualization.Error);
        var snapshot = ParseInventory(inventory.Output);
        snapshot.Virtualization = virtualization.Success ? virtualization.Output : "Unavailable: " + virtualization.Error;
        snapshot.CrashDumpConfiguration = ReadCrashControl(errors);
        snapshot.PageFiles = ReadPageFiles(errors);
        snapshot.Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        snapshot.BootTime ??= DateTimeOffset.Now - snapshot.Uptime;
        return snapshot;
    }

    public async Task<IReadOnlyList<EvidenceItem>> CollectTextInventoriesAsync(string rawDirectory, string? testDataDirectory, List<string> errors, CancellationToken cancellationToken)
    {
        var items = new List<EvidenceItem>();
        var commands = new (string Category, string File, string Program, string[] Args)[]
        {
            ("Windows update history", "windows-updates.txt", "powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Get-HotFix | Sort-Object InstalledOn -Descending | Format-List * | Out-String -Width 300"]),
            ("Signed drivers", "signed-drivers.txt", "powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Get-CimInstance Win32_PnPSignedDriver | Sort-Object DeviceName | Select-Object DeviceName,DriverVersion,DriverDate,Manufacturer,IsSigned,InfName | Format-Table -AutoSize | Out-String -Width 400"]),
            ("Driver metadata", "driver-metadata.json", "powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Get-CimInstance Win32_PnPSignedDriver | Select-Object DeviceName,DriverName,DriverVersion,DriverDate,Manufacturer,IsSigned,InfName,DriverProviderName,DeviceClass | ConvertTo-Json -Compress -Depth 4"]),
            ("Loaded drivers", "loaded-drivers.txt", "driverquery.exe", ["/v", "/fo", "csv"]),
            ("Filesystem minifilters", "minifilters.txt", "fltmc.exe", ["filters"]),
            ("Storage health", "storage-health.txt", "powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Get-PhysicalDisk | Select FriendlyName,MediaType,BusType,HealthStatus,OperationalStatus,Size | Format-List | Out-String -Width 300"]),
            ("Storage reliability counters", "storage-reliability.json", "powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Get-PhysicalDisk -ErrorAction SilentlyContinue | ForEach-Object { $d=$_; $r=$_ | Get-StorageReliabilityCounter -ErrorAction SilentlyContinue; [pscustomobject]@{DeviceId=$d.DeviceId;FriendlyName=$d.FriendlyName;MediaType=$d.MediaType;HealthStatus=[string]$d.HealthStatus;OperationalStatus=($d.OperationalStatus -join ',');Temperature=$r.Temperature;Wear=$r.Wear;PowerOnHours=$r.PowerOnHours;ReadErrorsTotal=$r.ReadErrorsTotal;WriteErrorsTotal=$r.WriteErrorsTotal} } | ConvertTo-Json -Compress -Depth 5"]),
            ("ATA SMART raw data", "ata-smart-raw.json", "powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Get-CimInstance -Namespace root/wmi -ClassName MSStorageDriver_FailurePredictData -ErrorAction SilentlyContinue | Select-Object InstanceName,Active,VendorSpecific | ConvertTo-Json -Compress -Depth 6"]),
            ("Recent device installation", "pnp-devices.txt", "powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", "Get-PnpDevice | Sort-Object Status,Class,FriendlyName | Select Status,Class,FriendlyName,InstanceId | Format-Table -AutoSize | Out-String -Width 400"])
        };
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var output = await GetCommandOrFixtureAsync(command.File, command.Program, command.Args, testDataDirectory, cancellationToken).ConfigureAwait(false);
                var path = Path.Combine(rawDirectory, command.File);
                await File.WriteAllTextAsync(path, output.StandardOutput + (output.StandardError.Length > 0 ? Environment.NewLine + "ERROR: " + output.StandardError : string.Empty), cancellationToken).ConfigureAwait(false);
                var state = output.Success ? EvidenceState.Success : EvidenceState.Warning;
                var failure = output.StandardError.Trim().Length > 0 ? output.StandardError.Trim() : output.StandardOutput.Trim().Length > 0 ? output.StandardOutput.Trim() : $"{command.Program} exited with code {output.ExitCode}.";
                if (!output.Success) errors.Add($"{command.Category}: {failure}");
                items.Add(new(command.Category, state, output.Success ? "Collected" : "Partially available", [path], output.Success ? null : failure));
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { errors.Add($"{command.Category}: {ex.Message}"); items.Add(new(command.Category, EvidenceState.Failure, "Collection failed", Error: ex.Message)); }
        }
        return items;
    }

    public async Task<(IReadOnlyList<InstalledProgram> Programs, EvidenceItem Evidence)> CollectInstalledProgramsAsync(string rawDirectory, string? testDataDirectory, List<string> errors, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<InstalledProgram> programs;
            if (!string.IsNullOrWhiteSpace(testDataDirectory))
            {
                var fixture = Path.Combine(Path.GetFullPath(testDataDirectory), "installed-programs.json");
                if (!File.Exists(fixture))
                {
                    errors.Add("Installed programs: Fixture not found: installed-programs.json");
                    return ([], new("Installed programs", EvidenceState.Warning, "Fixture not found", Error: "installed-programs.json is missing from the test-data folder."));
                }
                programs = InstalledProgramInventory.Parse(await File.ReadAllTextAsync(fixture, cancellationToken).ConfigureAwait(false));
            }
            else programs = InstalledProgramInventory.ReadFromRegistry();
            var path = Path.Combine(rawDirectory, "installed-programs.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(programs, JsonDefaults.Indented), cancellationToken).ConfigureAwait(false);
            return (programs, new("Installed programs", EvidenceState.Success, $"Recorded {programs.Count} installed program(s) from the registry uninstall inventory. Windows updates and hidden system components are excluded; they are inventoried separately.", [path]));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add("Installed programs: " + ex.Message);
            return ([], new("Installed programs", EvidenceState.Failure, "Collection failed", Error: ex.Message));
        }
    }

    private static async Task<(bool Success, string Output, string Error)> GetOutputAsync(string fixture, string script, string? testDataDirectory, CancellationToken token)
    {
        if (!string.IsNullOrWhiteSpace(testDataDirectory))
        {
            var path = Path.Combine(Path.GetFullPath(testDataDirectory), fixture);
            return File.Exists(path) ? (true, await File.ReadAllTextAsync(path, token).ConfigureAwait(false), string.Empty) : (false, string.Empty, $"Fixture not found: {fixture}");
        }
        var result = await CommandRunner.RunAsync("powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script], TimeSpan.FromSeconds(45), token).ConfigureAwait(false);
        return (result.Success, result.StandardOutput.Trim(), result.StandardError.Trim());
    }

    private static async Task<CommandResult> GetCommandOrFixtureAsync(string fixture, string program, string[] arguments, string? testDataDirectory, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(testDataDirectory)) return await CommandRunner.RunAsync(program, arguments, TimeSpan.FromSeconds(60), token).ConfigureAwait(false);
        var path = Path.Combine(Path.GetFullPath(testDataDirectory), fixture);
        return File.Exists(path) ? new(0, await File.ReadAllTextAsync(path, token).ConfigureAwait(false), string.Empty) : new(2, string.Empty, $"Fixture not found: {fixture}");
    }

    private static MachineSnapshot ParseInventory(string json)
    {
        var snapshot = new MachineSnapshot();
        try
        {
            using var doc = JsonDocument.Parse(json);
            string Get(string name) => doc.RootElement.TryGetProperty(name, out var value) ? value.ToString() : "Unavailable";
            snapshot.Windows = Get("Windows"); snapshot.Bios = Get("Bios"); snapshot.Motherboard = Get("Board"); snapshot.Cpu = Get("Cpu"); snapshot.Memory = Get("Memory"); snapshot.Gpu = Get("Gpu"); snapshot.Storage = Get("Storage") + "; " + Get("Health");
            if (doc.RootElement.TryGetProperty("LastBoot", out var boot) && DateTimeOffset.TryParse(boot.ToString(), out var parsed)) snapshot.BootTime = parsed;
        }
        catch { snapshot.RawSections["Inventory"] = json; }
        return snapshot;
    }

    private static string ReadCrashControl(List<string> errors)
    {
        try { using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CrashControl"); return string.Join("; ", new[] { "CrashDumpEnabled", "DumpFile", "MinidumpDir", "AlwaysKeepMemoryDump", "Overwrite" }.Select(name => $"{name}={key?.GetValue(name) ?? "(not set)"}")); }
        catch (Exception ex) { errors.Add("Crash-dump configuration: " + ex.Message); return "Unavailable: " + ex.Message; }
    }
    private static string ReadPageFiles(List<string> errors)
    {
        try { using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"); return string.Join("; ", (key?.GetValue("PagingFiles") as string[]) ?? ["(not set)"]); }
        catch (Exception ex) { errors.Add("Page-file configuration: " + ex.Message); return "Unavailable: " + ex.Message; }
    }
}

/// <summary>
/// Reads the user-visible installed-program inventory from the registry uninstall
/// keys. Win32_Product is deliberately never queried: enumerating it triggers MSI
/// consistency checks and self-repair, which violates the read-only guarantee.
/// </summary>
public static class InstalledProgramInventory
{
    public static readonly TimeSpan RecentInstallWindow = TimeSpan.FromDays(14);

    public static IReadOnlyList<InstalledProgram> ReadFromRegistry()
    {
        var programs = new List<InstalledProgram>();
        var roots = new (RegistryHive Hive, RegistryView View, string Source)[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM 64-bit uninstall key"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM 32-bit uninstall key"),
            (RegistryHive.CurrentUser, RegistryView.Default, "HKCU uninstall key")
        };
        foreach (var root in roots)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(root.Hive, root.View);
                using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var subKeyName in uninstall.GetSubKeyNames())
                {
                    try { using var key = uninstall.OpenSubKey(subKeyName); var program = FromRegistryKey(key, root.Source); if (program is not null) programs.Add(program); }
                    catch { /* one unreadable entry must not abort the inventory */ }
                }
            }
            catch { /* an inaccessible hive/view must not abort the other roots */ }
        }
        return Normalize(programs);
    }

    private static InstalledProgram? FromRegistryKey(RegistryKey? key, string source)
    {
        var displayName = (key?.GetValue("DisplayName") as string)?.Trim();
        if (key is null || string.IsNullOrWhiteSpace(displayName)) return null;
        // Updates and hidden servicing entries belong to windows-updates.txt, not
        // the user-visible program list Programs & Features would show.
        if (key.GetValue("SystemComponent") is int component && component == 1) return null;
        if (key.GetValue("ParentKeyName") is string parent && !string.IsNullOrWhiteSpace(parent)) return null;
        if (key.GetValue("ReleaseType") is string releaseType && releaseType is "Security Update" or "Update Rollup" or "Hotfix") return null;
        return new(displayName, (key.GetValue("DisplayVersion") as string)?.Trim(), (key.GetValue("Publisher") as string)?.Trim(), ParseInstallDate(key.GetValue("InstallDate") as string), (key.GetValue("InstallLocation") as string)?.Trim(), source);
    }

    // The registry records install dates as a local "yyyyMMdd" string with no time
    // of day; midnight local time is therefore a date-resolution value, not an
    // instant, and same-day installs are treated as before a later-that-day crash.
    public static DateTimeOffset? ParseInstallDate(string? value)
        => DateTime.TryParseExact(value?.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) ? new DateTimeOffset(parsed) : null;

    public static IReadOnlyList<InstalledProgram> Parse(string json)
        => Normalize(JsonSerializer.Deserialize<List<InstalledProgram>>(json, JsonDefaults.Indented) ?? []);

    public static IReadOnlyList<InstalledProgram> RecentInstalls(IReadOnlyList<InstalledProgram> programs, DateTimeOffset incidentTimestamp)
        => programs.Where(program => program.InstallDate is not null && program.InstallDate.Value <= incidentTimestamp && incidentTimestamp - program.InstallDate.Value <= RecentInstallWindow).ToList();

    private static IReadOnlyList<InstalledProgram> Normalize(IEnumerable<InstalledProgram> programs)
        => programs.Where(program => !string.IsNullOrWhiteSpace(program.Name))
            .DistinctBy(program => $"{program.Name}|{program.Version}", StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(program => program.InstallDate ?? DateTimeOffset.MinValue)
            .ThenBy(program => program.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
