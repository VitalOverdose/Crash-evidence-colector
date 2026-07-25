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
