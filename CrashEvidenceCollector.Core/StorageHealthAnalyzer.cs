using System.Text.Json;

namespace CrashEvidenceCollector.Core;

/// <summary>
/// Converts supported Windows storage reliability data and vendor ATA SMART bytes
/// into typed evidence. SMART thresholds and raw encodings are vendor-specific, so
/// the analyzer reports counters and concerns without declaring a crash cause.
/// </summary>
public static class StorageHealthAnalyzer
{
    private static readonly IReadOnlyDictionary<int, string> KnownAttributes = new Dictionary<int, string>
    {
        [5] = "Reallocated sector count",
        [9] = "Power-on hours",
        [187] = "Reported uncorrectable errors",
        [188] = "Command timeout",
        [194] = "Temperature",
        [197] = "Current pending sector count",
        [198] = "Offline uncorrectable sector count",
        [199] = "UltraDMA CRC error count"
    };

    private static readonly HashSet<int> ConcernWhenNonZero = [5, 187, 188, 197, 198, 199];

    public static async Task<IReadOnlyList<StorageDeviceHealth>> LoadAsync(string rawDirectory, CancellationToken cancellationToken)
    {
        var devices = new List<StorageDeviceHealth>();
        var reliabilityPath = Path.Combine(rawDirectory, "storage-reliability.json");
        var smartPath = Path.Combine(rawDirectory, "ata-smart-raw.json");
        if (File.Exists(reliabilityPath))
            ParseReliability(await File.ReadAllTextAsync(reliabilityPath, cancellationToken).ConfigureAwait(false), devices);
        if (File.Exists(smartPath))
            ParseAtaSmart(await File.ReadAllTextAsync(smartPath, cancellationToken).ConfigureAwait(false), devices);
        return devices;
    }

    public static IReadOnlyList<StorageDeviceHealth> Parse(string? reliabilityJson, string? ataSmartJson)
    {
        var devices = new List<StorageDeviceHealth>();
        if (!string.IsNullOrWhiteSpace(reliabilityJson)) ParseReliability(reliabilityJson, devices);
        if (!string.IsNullOrWhiteSpace(ataSmartJson)) ParseAtaSmart(ataSmartJson, devices);
        return devices;
    }

    private static void ParseReliability(string json, List<StorageDeviceHealth> devices)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var item in EnumerateObjects(document.RootElement))
            {
                var device = new StorageDeviceHealth
                {
                    Device = Text(item, "FriendlyName") ?? "Unknown physical disk",
                    DeviceId = Text(item, "DeviceId"),
                    MediaType = Text(item, "MediaType"),
                    HealthStatus = Text(item, "HealthStatus"),
                    OperationalStatus = Text(item, "OperationalStatus"),
                    TemperatureCelsius = Signed(item, "Temperature"),
                    WearPercent = Signed(item, "Wear"),
                    PowerOnHours = Unsigned(item, "PowerOnHours"),
                    ReadErrorsTotal = Unsigned(item, "ReadErrorsTotal"),
                    WriteErrorsTotal = Unsigned(item, "WriteErrorsTotal")
                };
                if (!string.IsNullOrWhiteSpace(device.HealthStatus) && !device.HealthStatus.Equals("Healthy", StringComparison.OrdinalIgnoreCase))
                    device.Warnings.Add($"Windows reports health status '{device.HealthStatus}'.");
                if (!string.IsNullOrWhiteSpace(device.OperationalStatus) && !device.OperationalStatus.Contains("OK", StringComparison.OrdinalIgnoreCase))
                    device.Warnings.Add($"Windows reports operational status '{device.OperationalStatus}'.");
                devices.Add(device);
            }
        }
        catch (JsonException ex)
        {
            devices.Add(new() { Device = "Storage reliability data", Warnings = [$"The Windows reliability JSON could not be parsed: {ex.Message}"] });
        }
    }

    private static void ParseAtaSmart(string json, List<StorageDeviceHealth> devices)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var item in EnumerateObjects(document.RootElement))
            {
                var instance = Text(item, "InstanceName") ?? "Unknown ATA device";
                var device = devices.FirstOrDefault(candidate => instance.Contains(candidate.Device, StringComparison.OrdinalIgnoreCase))
                    ?? new StorageDeviceHealth { Device = instance, Source = "MSStorageDriver_FailurePredictData (vendor ATA SMART)" };
                if (!devices.Contains(device)) devices.Add(device);
                if (!item.TryGetProperty("VendorSpecific", out var bytesElement) || bytesElement.ValueKind != JsonValueKind.Array)
                {
                    device.Warnings.Add("The ATA provider returned no readable VendorSpecific SMART byte array.");
                    continue;
                }

                var bytes = bytesElement.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.Number && value.TryGetByte(out var parsed) ? parsed : (byte)0).ToArray();
                // The 512-byte ATA SMART block begins with a two-byte revision. Each
                // following attribute entry is 12 bytes: ID, flags, current, worst,
                // six raw bytes, and one reserved byte.
                for (var offset = 2; offset + 11 < bytes.Length && offset < 362; offset += 12)
                {
                    var id = bytes[offset];
                    if (id == 0) continue;
                    ulong raw = 0;
                    for (var index = 0; index < 6; index++) raw |= (ulong)bytes[offset + 5 + index] << (index * 8);
                    var name = KnownAttributes.GetValueOrDefault(id, $"Vendor attribute {id}");
                    var concern = ConcernWhenNonZero.Contains(id) && raw > 0;
                    var interpretation = concern
                        ? "The raw counter is non-zero. Confirm meaning and threshold with the drive vendor before treating it as a fault."
                        : "Raw and normalized values are preserved; interpretation is vendor-specific.";
                    device.SmartAttributes.Add(new(id, name, bytes[offset + 3], bytes[offset + 4], raw, concern, interpretation));
                    if (concern) device.Warnings.Add($"{name} (SMART {id}) has raw value {raw}.");
                }
            }
        }
        catch (JsonException ex)
        {
            devices.Add(new() { Device = "ATA SMART data", Source = "MSStorageDriver_FailurePredictData", Warnings = [$"The ATA SMART JSON could not be parsed: {ex.Message}"] });
        }
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object) yield return root;
        if (root.ValueKind == JsonValueKind.Array)
            foreach (var item in root.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object) yield return item;
    }

    private static string? Text(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? value.ToString() : null;

    private static int? Signed(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed : null;

    private static ulong? Unsigned(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var parsed) ? parsed : null;
}
