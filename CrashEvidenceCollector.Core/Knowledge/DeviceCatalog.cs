using System.Text.RegularExpressions;

namespace CrashEvidenceCollector.Core;

/// <summary>What a hardware id was resolved to, and how much of it was resolved.</summary>
public sealed record DeviceIdentity(string RawId, string Bus, string? VendorId, string? VendorName, string? ProductId)
{
    /// <summary>True when the vendor was actually named rather than left as a number.</summary>
    public bool IsVendorKnown => VendorName is not null;

    /// <summary>A one-line description safe to show anywhere, never fabricated.</summary>
    public string Describe() => VendorName is null
        ? VendorId is null ? RawId : $"{Bus} device from unlisted vendor {VendorId}"
        : ProductId is null ? $"{VendorName} ({Bus})" : $"{VendorName} ({Bus} device {ProductId})";
}

/// <summary>
/// Turns the identifiers Windows prints for hardware into names.
///
/// A device row that reads <c>PCI\VEN_10DE&amp;DEV_2684</c> tells the user nothing;
/// "NVIDIA Corporation" tells them what they are looking at. Vendor IDs are
/// administered centrally and do not change, so this is a lookup, not a guess —
/// and an unlisted id is reported as unlisted rather than described.
/// </summary>
public static partial class DeviceCatalog
{
    // PCI-SIG assigned vendor identifiers, limited to vendors that actually appear
    // in consumer and workstation machines.
    private static readonly IReadOnlyDictionary<int, string> PciVendors = new Dictionary<int, string>
    {
        [0x1000] = "Broadcom / LSI",
        [0x1002] = "AMD (ATI Graphics)",
        [0x1013] = "Cirrus Logic",
        [0x1022] = "AMD",
        [0x1033] = "NEC",
        [0x1039] = "Silicon Integrated Systems",
        [0x1043] = "ASUSTeK",
        [0x1095] = "Silicon Image",
        [0x102B] = "Matrox",
        [0x1102] = "Creative Labs",
        [0x1106] = "VIA Technologies",
        [0x1179] = "Toshiba",
        [0x1197] = "Gigabyte",
        [0x11AB] = "Marvell",
        [0x1234] = "QEMU / Bochs (virtual)",
        [0x1274] = "Ensoniq",
        [0x1283] = "ITE Tech",
        [0x1344] = "Micron",
        [0x13F6] = "C-Media Electronics",
        [0x1414] = "Microsoft (virtual)",
        [0x144D] = "Samsung",
        [0x1462] = "MSI",
        [0x14C3] = "MediaTek",
        [0x14E4] = "Broadcom",
        [0x1509] = "First International Computer",
        [0x1558] = "Clevo",
        [0x15AD] = "VMware (virtual)",
        [0x15B3] = "NVIDIA / Mellanox",
        [0x15B7] = "SanDisk / Western Digital",
        [0x1631] = "Packard Bell",
        [0x168C] = "Qualcomm Atheros",
        [0x1687] = "KYE Systems",
        [0x1849] = "ASRock",
        [0x1854] = "LG Electronics",
        [0x1987] = "Phison Electronics",
        [0x1912] = "Renesas",
        [0x197B] = "JMicron",
        [0x1AF4] = "Red Hat virtio (virtual)",
        [0x1B21] = "ASMedia",
        [0x1B4B] = "Marvell",
        [0x1B73] = "Fresco Logic",
        [0x1B85] = "OCZ / Toshiba",
        [0x1C5C] = "SK hynix",
        [0x1CC1] = "ADATA",
        [0x1CC4] = "Union Memory",
        [0x1D6A] = "Aquantia / Marvell",
        [0x1D94] = "Hygon",
        [0x1DBE] = "Ampere",
        [0x1E0F] = "KIOXIA",
        [0x1E4B] = "MAXIO Technology",
        [0x104C] = "Texas Instruments",
        [0x10B5] = "PLX Technology",
        [0x10B7] = "3Com",
        [0x10DE] = "NVIDIA Corporation",
        [0x10EC] = "Realtek",
        [0x126F] = "Silicon Motion",
        [0x1C00] = "Shenzhen Longsys",
        [0x2646] = "Kingston Technology",
        [0x5333] = "S3 Graphics",
        [0x5853] = "XenSource (virtual)",
        [0x8086] = "Intel Corporation",
        [0x8087] = "Intel Corporation",
        [0x9005] = "Adaptec"
    };

    // USB-IF assigned vendor identifiers for devices commonly found on a PC.
    private static readonly IReadOnlyDictionary<int, string> UsbVendors = new Dictionary<int, string>
    {
        [0x0079] = "DragonRise",
        [0x03F0] = "HP",
        [0x0403] = "FTDI",
        [0x0409] = "NEC",
        [0x041E] = "Creative Technology",
        [0x0424] = "Microchip (SMSC hubs)",
        [0x0451] = "Texas Instruments",
        [0x045B] = "Hitachi",
        [0x045E] = "Microsoft",
        [0x046A] = "Cherry",
        [0x046D] = "Logitech",
        [0x0483] = "STMicroelectronics",
        [0x0489] = "Foxconn / Hon Hai",
        [0x04B4] = "Cypress Semiconductor",
        [0x04CA] = "Lite-On",
        [0x04D9] = "Holtek",
        [0x04E8] = "Samsung",
        [0x04F2] = "Chicony",
        [0x054C] = "Sony",
        [0x057E] = "Nintendo",
        [0x05AC] = "Apple",
        [0x05E3] = "Genesys Logic",
        [0x0644] = "TEAC",
        [0x0763] = "M-Audio",
        [0x0781] = "SanDisk",
        [0x07CA] = "AVerMedia",
        [0x0846] = "NetGear",
        [0x090C] = "Silicon Motion",
        [0x093A] = "PixArt",
        [0x0951] = "Kingston / HyperX",
        [0x0A5C] = "Broadcom (Bluetooth)",
        [0x0B05] = "ASUSTeK",
        [0x0B95] = "ASIX Electronics",
        [0x0BC2] = "Seagate",
        [0x0BDA] = "Realtek",
        [0x0C45] = "Microdia",
        [0x0CF3] = "Qualcomm Atheros (Bluetooth)",
        [0x0D8C] = "C-Media Electronics",
        [0x0E8D] = "MediaTek",
        [0x0FCE] = "Sony",
        [0x1004] = "LG Electronics",
        [0x1038] = "SteelSeries",
        [0x1058] = "Western Digital",
        [0x10C4] = "Silicon Labs",
        [0x1235] = "Focusrite / Novation",
        [0x12D1] = "Huawei",
        [0x13D3] = "IMC Networks / AzureWave",
        [0x148F] = "Ralink",
        [0x152D] = "JMicron",
        [0x1532] = "Razer",
        [0x17EF] = "Lenovo",
        [0x174C] = "ASMedia",
        [0x18D1] = "Google",
        [0x1A40] = "Terminus Technology",
        [0x1A86] = "QinHeng Electronics",
        [0x1B1C] = "Corsair",
        [0x1BCF] = "Sunplus",
        [0x1E7D] = "ROCCAT",
        [0x2109] = "VIA Labs",
        [0x22B8] = "Motorola",
        [0x2341] = "Arduino",
        [0x2357] = "TP-Link",
        [0x2516] = "Cooler Master",
        [0x2717] = "Xiaomi",
        [0x28DE] = "Valve",
        [0x413C] = "Dell",
        [0x8086] = "Intel Corporation",
        [0x8087] = "Intel Corporation"
    };

    /// <summary>
    /// Device Manager problem codes. This is a fixed, documented set, so an
    /// unlisted value genuinely means "not a code Windows defines".
    /// </summary>
    private static readonly IReadOnlyDictionary<int, string> ProblemCodes = new Dictionary<int, string>
    {
        [0] = "The device is working normally.",
        [1] = "The device is not configured correctly.",
        [3] = "The driver may be damaged, or the system is low on memory.",
        [9] = "Windows cannot identify this hardware because it has no valid identification.",
        [10] = "The device cannot start.",
        [12] = "The device cannot find enough free resources to use.",
        [14] = "The device will not work properly until the computer is restarted.",
        [16] = "Windows cannot identify all the resources this device uses.",
        [18] = "The drivers for this device need to be reinstalled.",
        [19] = "The registry configuration for this device is incomplete or damaged.",
        [21] = "Windows is in the middle of removing this device.",
        [22] = "The device is disabled.",
        [24] = "The device is not present, is not working properly, or does not have all its drivers installed.",
        [28] = "The drivers for this device are not installed.",
        [29] = "The device is disabled because its firmware did not give it the resources it needs.",
        [31] = "The device is not working properly because Windows cannot load its drivers.",
        [32] = "The start type for this device's driver service is set to disabled.",
        [33] = "Windows cannot determine which resources this device requires.",
        [34] = "Windows cannot determine this device's settings; they must be set manually.",
        [35] = "The system firmware does not include enough information to configure and use this device.",
        [36] = "This device is requesting a type of interrupt the system cannot provide.",
        [37] = "Windows cannot initialise the driver for this device.",
        [38] = "Windows cannot load the driver because a previous instance is still in memory.",
        [39] = "The driver for this device is damaged or missing.",
        [40] = "Windows cannot access this hardware because its registry service key information is missing or recorded incorrectly.",
        [41] = "Windows loaded the driver but cannot find the device.",
        [42] = "Windows cannot run this device because a duplicate is already running.",
        [43] = "Windows stopped this device because it reported problems.",
        [44] = "An application or service has shut this device down.",
        [45] = "This device is not currently connected to the computer.",
        [46] = "The device is unavailable because the system is shutting down.",
        [47] = "The device was prepared for safe removal but has not been removed.",
        [48] = "The software for this device has been blocked from starting because of a known problem.",
        [49] = "Windows cannot start new devices because the system registry hive is too large.",
        [50] = "Windows cannot apply all the properties for this device.",
        [51] = "This device is waiting on another device or set of devices to start.",
        [52] = "Windows cannot verify the digital signature for this device's drivers.",
        [53] = "This device has been reserved for use by the Windows kernel debugger.",
        [54] = "This device has failed and is undergoing a reset."
    };

    /// <summary>Names a PCI vendor id, or null when it is not in the list.</summary>
    public static string? PciVendor(int vendorId) => PciVendors.TryGetValue(vendorId, out var name) ? name : null;

    /// <summary>Names a USB vendor id, or null when it is not in the list.</summary>
    public static string? UsbVendor(int vendorId) => UsbVendors.TryGetValue(vendorId, out var name) ? name : null;

    /// <summary>Explains a Device Manager problem code, or null when it is not a defined one.</summary>
    public static string? ProblemCode(int code) => ProblemCodes.TryGetValue(code, out var text) ? text : null;

    public static int PciVendorCount => PciVendors.Count;
    public static int UsbVendorCount => UsbVendors.Count;
    public static int ProblemCodeCount => ProblemCodes.Count;

    /// <summary>
    /// Resolves a Windows hardware or instance id such as
    /// <c>PCI\VEN_10DE&amp;DEV_2684&amp;SUBSYS_...</c> or <c>USB\VID_046D&amp;PID_C08B</c>.
    /// Returns null for ids that carry no vendor field at all (ACPI, ROOT, SW).
    /// </summary>
    public static DeviceIdentity? Identify(string? hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId)) return null;
        var raw = hardwareId.Trim();
        var bus = raw.Split('\\')[0].ToUpperInvariant();

        var pci = PciPattern().Match(raw);
        if (pci.Success)
        {
            var vendor = Convert.ToInt32(pci.Groups["ven"].Value, 16);
            var device = pci.Groups["dev"].Success ? "0x" + pci.Groups["dev"].Value.ToUpperInvariant() : null;
            return new(raw, bus.Length == 0 ? "PCI" : bus, "0x" + pci.Groups["ven"].Value.ToUpperInvariant(), PciVendor(vendor), device);
        }

        var usb = UsbPattern().Match(raw);
        if (usb.Success)
        {
            var vendor = Convert.ToInt32(usb.Groups["vid"].Value, 16);
            var product = usb.Groups["pid"].Success ? "0x" + usb.Groups["pid"].Value.ToUpperInvariant() : null;
            return new(raw, bus.Length == 0 ? "USB" : bus, "0x" + usb.Groups["vid"].Value.ToUpperInvariant(), UsbVendor(vendor), product);
        }

        return null;
    }

    [GeneratedRegex(@"VEN_(?<ven>[0-9A-Fa-f]{4})(?:&DEV_(?<dev>[0-9A-Fa-f]{4}))?", RegexOptions.CultureInvariant)]
    private static partial Regex PciPattern();

    [GeneratedRegex(@"VID_(?<vid>[0-9A-Fa-f]{4})(?:&PID_(?<pid>[0-9A-Fa-f]{4}))?", RegexOptions.CultureInvariant)]
    private static partial Regex UsbPattern();
}
