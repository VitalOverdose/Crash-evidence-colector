namespace CrashEvidenceCollector.Core;

/// <summary>
/// How much attention an event deserves on its own. Most of what fills a Windows
/// log is routine; saying so is as useful as flagging what is not, because a
/// reader who is warned about everything stops reading.
/// </summary>
public enum EventTone
{
    /// <summary>Normal operation. Worth showing for context, not worth worrying about.</summary>
    Routine,
    /// <summary>Not a fault, but meaningful when it clusters or lands near a crash.</summary>
    Notable,
    /// <summary>Something went wrong and Windows recovered or worked around it.</summary>
    Warning,
    /// <summary>Something failed in a way that matters.</summary>
    Serious
}

/// <summary>Which part of the machine an event came from, for grouping and pivoting.</summary>
public enum EventArea
{
    Unknown,
    Power,
    Boot,
    Hardware,
    Storage,
    FileSystem,
    Graphics,
    Driver,
    Device,
    Network,
    Application,
    Service,
    Memory,
    Performance,
    Security,
    Update,
    Time
}

/// <summary>A Windows event, in English.</summary>
public sealed record EventMeaning(string Provider, int EventId, string PlainTitle, string Meaning, EventArea Area, EventTone Tone);

/// <summary>
/// Plain-English vocabulary for Windows event log records.
///
/// A single System log capture on a normal machine contains dozens of distinct
/// provider/ID pairs. Every one this catalogue cannot name prints an apology, and
/// a reader who meets that apology a dozen times concludes the tool knows nothing.
/// So the goal here is breadth: name the event, say what it means, and be explicit
/// when the honest answer is "this is routine".
/// </summary>
public static class EventCatalog
{
    private static EventMeaning E(string provider, int id, string title, string meaning, EventArea area, EventTone tone) => new(provider, id, title, meaning, area, tone);

    private static readonly IReadOnlyDictionary<string, EventMeaning> Catalog = new[]
    {
        // ---- Power, shutdown and restart -------------------------------------------------
        E("Kernel-Power", 41, "Windows restarted without shutting down first", "The previous session ended abruptly. This records that it happened; it does not distinguish power loss, a reset, a freeze or a bugcheck. If a stop code is present in this record, the crash came first.", EventArea.Power, EventTone.Serious),
        E("Kernel-Power", 42, "The system went to sleep", "Windows entered a sleep state normally.", EventArea.Power, EventTone.Routine),
        E("Kernel-Power", 105, "The power source changed", "The machine switched between mains and battery, or the power policy changed.", EventArea.Power, EventTone.Routine),
        E("Kernel-Power", 107, "The system woke from sleep", "Windows resumed from a sleep state normally.", EventArea.Power, EventTone.Routine),
        E("Kernel-Power", 109, "The kernel began a shutdown", "The power manager started an orderly shutdown or restart — this is what a clean reboot looks like.", EventArea.Power, EventTone.Routine),
        E("Kernel-Power", 131, "A power request could not be completed", "A power transition request failed or was refused.", EventArea.Power, EventTone.Warning),
        E("Kernel-Power", 506, "The system entered modern standby", "Windows entered its low-power connected standby state.", EventArea.Power, EventTone.Routine),
        E("Kernel-Power", 507, "The system left modern standby", "Windows exited connected standby.", EventArea.Power, EventTone.Routine),
        E("Power-Troubleshooter", 1, "The system returned from low power", "Windows resumed, and this record says what woke it and how long it slept.", EventArea.Power, EventTone.Routine),
        E("Kernel-Processor-Power", 55, "The processor changed power or performance state", "Normal processor power management. Worth noting only if it repeats unusually often, which can indicate thermal or firmware throttling.", EventArea.Power, EventTone.Routine),
        E("Kernel-Processor-Power", 6, "Processor performance was limited by firmware", "The firmware capped processor performance — commonly for heat or power-delivery reasons.", EventArea.Power, EventTone.Notable),
        E("EventLog", 6005, "The event log service started", "Windows finished starting. This is the standard marker for the beginning of a session.", EventArea.Boot, EventTone.Routine),
        E("EventLog", 6006, "The event log service stopped", "Windows shut down cleanly. Its absence before a restart is what makes a shutdown 'unexpected'.", EventArea.Boot, EventTone.Routine),
        E("EventLog", 6008, "The previous shutdown was unexpected", "Windows noticed on this boot that the last session did not end cleanly, and records when it thinks it stopped.", EventArea.Power, EventTone.Serious),
        E("EventLog", 6009, "Windows version at startup", "Records the operating system version and build at boot. Useful for spotting when an update changed things.", EventArea.Boot, EventTone.Routine),
        E("EventLog", 6013, "System uptime", "A daily record of how long the machine has been running.", EventArea.Boot, EventTone.Routine),
        E("Kernel-General", 1, "The system clock was changed", "The time was adjusted. Large jumps can make event ordering misleading.", EventArea.Time, EventTone.Notable),
        E("Kernel-General", 12, "The operating system started", "Windows started at the recorded time — a boot boundary.", EventArea.Boot, EventTone.Routine),
        E("Kernel-General", 13, "The operating system is shutting down", "Windows began shutting down at the recorded time.", EventArea.Boot, EventTone.Routine),
        E("Kernel-Boot", 20, "Boot options recorded", "Records the boot configuration Windows started with, including test signing and debug settings.", EventArea.Boot, EventTone.Routine),
        E("Kernel-Boot", 27, "Boot type recorded", "Records whether this was a full boot, a hybrid (fast startup) boot or a resume from hibernation.", EventArea.Boot, EventTone.Routine),
        E("Wininit", 1001, "Disk check results", "The output of a chkdsk run is recorded here in full. If Windows repaired the file system, this is where it says so.", EventArea.Storage, EventTone.Notable),

        // ---- Crashes -------------------------------------------------------------------
        E("BugCheck", 1001, "Windows recorded a stop error", "Windows restarted after a bugcheck and wrote the stop code, its parameters and the dump file location.", EventArea.Power, EventTone.Serious),
        E("WER-SystemErrorReporting", 1001, "A system crash report was saved", "Windows Error Reporting stored information about a bugcheck or fatal system error.", EventArea.Power, EventTone.Serious),
        E("WER-SystemErrorReporting", 1018, "A crash report could not be saved", "Error reporting failed to record a system error, so the dump for that crash may be missing.", EventArea.Power, EventTone.Warning),
        E("Application Error", 1000, "A program crashed", "An application terminated on an unhandled fault. The faulting module and exception code in this record say where it failed — which is often not the same program that appears in the title.", EventArea.Application, EventTone.Warning),
        E("Application Hang", 1002, "A program stopped responding", "An application stopped responding and was closed. Hangs and crashes have different causes; a hang often points at something the program was waiting for.", EventArea.Application, EventTone.Warning),
        E("Windows Error Reporting", 1001, "An error report was created", "Windows classified a failure and created a report bucket for it. The bucket name is a useful search term.", EventArea.Application, EventTone.Notable),
        E(".NET Runtime", 1026, "A .NET application crashed", "A managed application terminated with an unhandled exception; the stack trace is included in full.", EventArea.Application, EventTone.Warning),

        // ---- Hardware errors -----------------------------------------------------------
        E("WHEA-Logger", 17, "The hardware corrected an error by itself", "A hardware error occurred and was corrected automatically. One is normal background noise on many machines; a pattern on the same component is not.", EventArea.Hardware, EventTone.Notable),
        E("WHEA-Logger", 18, "The hardware reported an error it could not correct", "A fatal hardware error was reported. The processor, bank and machine-check fields narrow it to a component far better than the stop code does.", EventArea.Hardware, EventTone.Serious),
        E("WHEA-Logger", 19, "The hardware corrected an error by itself", "A corrected hardware error occurred. Windows carried on, but repetition — especially on the same processor or bank — is meaningful evidence.", EventArea.Hardware, EventTone.Notable),
        E("WHEA-Logger", 20, "A hardware error record was logged", "WHEA recorded a hardware error event whose severity is carried inside the record itself.", EventArea.Hardware, EventTone.Notable),
        E("WHEA-Logger", 47, "A memory error was corrected", "A correctable memory error was detected and fixed. Repeats at the same physical address point at a specific DIMM.", EventArea.Hardware, EventTone.Notable),
        E("HAL", 12, "The hardware layer reported a fatal error", "The hardware abstraction layer recorded a fatal hardware condition.", EventArea.Hardware, EventTone.Serious),
        E("Microsoft-Windows-Kernel-Processor-Power", 37, "A processor spent time throttled", "One or more cores ran below their nominal speed because of a firmware or thermal limit.", EventArea.Hardware, EventTone.Notable),

        // ---- Storage -------------------------------------------------------------------
        E("disk", 7, "The drive reported a bad block", "A block on the device could not be read or written.", EventArea.Storage, EventTone.Serious),
        E("disk", 11, "The drive reported a controller error", "The storage controller reported an error on this device. Cabling, the controller and the drive itself are all candidates.", EventArea.Storage, EventTone.Serious),
        E("disk", 51, "A disk operation had to be retried", "An error occurred during a paging operation and the request was retried.", EventArea.Storage, EventTone.Warning),
        E("disk", 52, "The drive is predicting its own failure", "SMART reported that the device expects to fail. Back up first, diagnose second.", EventArea.Storage, EventTone.Serious),
        E("disk", 153, "A disk operation was retried after taking too long", "An I/O request exceeded its expected time and was retried. Occasional entries happen; clusters indicate a struggling drive or link.", EventArea.Storage, EventTone.Warning),
        E("disk", 157, "A disk was surprise-removed", "The drive disappeared without being ejected — a loose cable, a power problem, or a link reset.", EventArea.Storage, EventTone.Serious),
        E("storahci", 129, "The storage controller reset a drive", "The AHCI driver had to reset the device because it stopped responding. Repeats point at the drive, its cable or its power.", EventArea.Storage, EventTone.Warning),
        E("stornvme", 129, "The NVMe controller reset a drive", "The NVMe driver reset the device after it stopped responding in time.", EventArea.Storage, EventTone.Warning),
        E("iaStorA", 129, "The Intel storage driver reset a drive", "The Intel RST driver reset a device that stopped responding.", EventArea.Storage, EventTone.Warning),
        E("Storage-Storport", 500, "A storage request took far too long", "Storport recorded an I/O request exceeding its latency threshold — the drive or link stalled.", EventArea.Storage, EventTone.Warning),
        E("Storage-Storport", 505, "Storage performance is degraded", "Storport reported sustained degraded performance from a device.", EventArea.Storage, EventTone.Warning),
        E("volmgr", 46, "The crash dump could not be initialised", "Windows could not set up crash dump collection, which is why a crash may have left no dump file. The paging file size or location is the usual reason.", EventArea.Storage, EventTone.Warning),
        E("volmgr", 161, "The crash dump could not be written", "A dump file could not be created for a crash.", EventArea.Storage, EventTone.Warning),
        E("Ntfs", 55, "The file system found damage on a volume", "NTFS detected structural corruption. It records which volume, and whether a repair was scheduled.", EventArea.FileSystem, EventTone.Serious),
        E("Ntfs", 98, "The file system found a metadata inconsistency", "NTFS found metadata that did not match what it expected.", EventArea.FileSystem, EventTone.Warning),
        E("Ntfs", 137, "A transaction was rolled back", "The file system undid an incomplete transaction, usually after an abrupt stop.", EventArea.FileSystem, EventTone.Notable),
        E("Ntfs", 140, "The system could not flush data to a volume", "Writes could not be flushed to disk — the drive stopped responding or was removed.", EventArea.FileSystem, EventTone.Serious),
        E("Ntfs", 142, "Self-healing repaired the volume", "NTFS repaired minor damage on the volume without needing a full disk check.", EventArea.FileSystem, EventTone.Notable),

        // ---- Graphics ------------------------------------------------------------------
        E("Display", 4101, "The display driver stopped responding and recovered", "The graphics driver hung and Windows reset it — the screen flickered and carried on. Repeats are the classic signature of an unstable GPU, driver or power delivery.", EventArea.Graphics, EventTone.Warning),
        E("nvlddmkm", 13, "The NVIDIA driver reported an error", "The NVIDIA display driver logged an internal error condition.", EventArea.Graphics, EventTone.Warning),
        E("nvlddmkm", 14, "The NVIDIA driver reported an error", "The NVIDIA display driver logged an error while servicing the graphics stack.", EventArea.Graphics, EventTone.Warning),
        E("amdkmdag", 1, "The AMD driver reported an error", "The AMD display driver logged an internal error condition.", EventArea.Graphics, EventTone.Warning),

        // ---- Devices and drivers ---------------------------------------------------------
        E("Kernel-PnP", 219, "A driver failed to load for a device", "Windows could not load the driver for a device. The status code in the record says why.", EventArea.Device, EventTone.Warning),
        E("Kernel-PnP", 225, "A device could not be stopped", "An application or driver blocked a device from being removed or restarted.", EventArea.Device, EventTone.Notable),
        E("Kernel-PnP", 411, "A device did not start", "A device reported a problem starting; the problem code identifies what.", EventArea.Device, EventTone.Warning),
        E("Kernel-PnP", 442, "A device could not be migrated", "Device settings could not be carried across an upgrade.", EventArea.Device, EventTone.Notable),
        E("UserPnp", 20001, "A device driver was installed", "A driver installation finished. The status says whether it succeeded — and the timestamp is what makes 'it started after I updated the driver' checkable.", EventArea.Device, EventTone.Notable),
        E("UserPnp", 20003, "A device service was installed", "A service belonging to a device driver package was installed.", EventArea.Device, EventTone.Routine),
        E("DriverFrameworks-UserMode", 10110, "A user-mode driver had a problem", "A user-mode driver framework device reported a fault.", EventArea.Driver, EventTone.Warning),
        E("DriverFrameworks-UserMode", 10111, "A user-mode driver failed to stop", "A user-mode driver did not complete a stop or removal request.", EventArea.Driver, EventTone.Warning),

        // ---- Services ------------------------------------------------------------------
        E("Service Control Manager", 7000, "A service failed to start", "Windows could not start a service; the error in the record says why.", EventArea.Service, EventTone.Warning),
        E("Service Control Manager", 7001, "A service could not start because another failed", "A service was not started because a service it depends on failed.", EventArea.Service, EventTone.Warning),
        E("Service Control Manager", 7009, "A service timed out starting", "Windows gave up waiting for a service to connect.", EventArea.Service, EventTone.Warning),
        E("Service Control Manager", 7011, "A service stopped responding", "A service did not answer a control request in time — often a symptom of the machine being stalled rather than the service being at fault.", EventArea.Service, EventTone.Warning),
        E("Service Control Manager", 7022, "A service hung on starting", "A service started but then stopped responding.", EventArea.Service, EventTone.Warning),
        E("Service Control Manager", 7023, "A service stopped with an error", "A service terminated and reported an error status.", EventArea.Service, EventTone.Warning),
        E("Service Control Manager", 7024, "A service stopped with a service-specific error", "A service terminated with an error defined by the service itself.", EventArea.Service, EventTone.Warning),
        E("Service Control Manager", 7026, "Boot-start drivers were loaded or skipped", "Records which boot-start drivers did not load this session.", EventArea.Driver, EventTone.Notable),
        E("Service Control Manager", 7031, "A service crashed", "A service terminated unexpectedly, and this records how many times and what Windows did about it.", EventArea.Service, EventTone.Warning),
        E("Service Control Manager", 7034, "A service crashed with no recovery action", "A service terminated unexpectedly and no recovery was configured.", EventArea.Service, EventTone.Warning),
        E("Service Control Manager", 7036, "A service started or stopped", "Routine service state change. Extremely common — it is context, not a finding.", EventArea.Service, EventTone.Routine),
        E("Service Control Manager", 7040, "A service start type was changed", "The start type of a service was reconfigured.", EventArea.Service, EventTone.Notable),
        E("Service Control Manager", 7045, "A new service was installed", "A service was installed on the system. Worth noticing when it appears just before problems started.", EventArea.Service, EventTone.Notable),

        // ---- Memory and resources ---------------------------------------------------------
        E("Resource-Exhaustion-Detector", 2004, "The system ran out of memory", "Windows ran critically low on virtual memory and named the processes consuming the most. This is a real finding, not a warning to skim past.", EventArea.Memory, EventTone.Serious),
        E("Resource-Exhaustion-Resolver", 1003, "Windows closed programs to reclaim memory", "The resource exhaustion resolver terminated applications to recover memory.", EventArea.Memory, EventTone.Serious),

        // ---- Boot and shutdown performance -------------------------------------------------
        E("Diagnostics-Performance", 100, "Startup performance was measured", "Records how long this boot took overall and where the time went — the single most useful record for 'why is it slow to start'.", EventArea.Performance, EventTone.Notable),
        E("Diagnostics-Performance", 101, "An application slowed startup", "A named application took longer than expected to start during boot, with the delay in milliseconds.", EventArea.Performance, EventTone.Notable),
        E("Diagnostics-Performance", 102, "A driver slowed startup", "A named driver took longer than expected to initialise during boot.", EventArea.Performance, EventTone.Notable),
        E("Diagnostics-Performance", 103, "A service slowed startup", "A named service took longer than expected to start during boot.", EventArea.Performance, EventTone.Notable),
        E("Diagnostics-Performance", 106, "A background task slowed startup", "A scheduled task delayed the boot process.", EventArea.Performance, EventTone.Notable),
        E("Diagnostics-Performance", 109, "A device slowed startup", "A device took longer than expected to initialise during boot.", EventArea.Performance, EventTone.Notable),
        E("Diagnostics-Performance", 200, "Shutdown performance was measured", "Records how long shutdown took and what held it up.", EventArea.Performance, EventTone.Notable),
        E("Diagnostics-Performance", 201, "An application slowed shutdown", "A named application delayed shutdown.", EventArea.Performance, EventTone.Notable),
        E("Diagnostics-Performance", 203, "A service slowed shutdown", "A named service delayed shutdown.", EventArea.Performance, EventTone.Notable),

        // ---- Updates, network, security -----------------------------------------------------
        E("WindowsUpdateClient", 19, "An update installed successfully", "A Windows update finished installing. The timestamp is what makes 'it started after an update' testable.", EventArea.Update, EventTone.Notable),
        E("WindowsUpdateClient", 20, "An update failed to install", "A Windows update did not install; the error code says why.", EventArea.Update, EventTone.Warning),
        E("WindowsUpdateClient", 43, "An update started installing", "Installation of an update began.", EventArea.Update, EventTone.Routine),
        E("DistributedCOM", 10016, "A component was refused permission", "A DCOM permission check failed. These are extremely common on healthy machines and are almost never the cause of a crash.", EventArea.Security, EventTone.Routine),
        E("DistributedCOM", 10005, "A DCOM service failed to start", "DCOM could not start a service it needed.", EventArea.Service, EventTone.Warning),
        E("DistributedCOM", 10010, "A component did not register in time", "A DCOM server did not register within the timeout.", EventArea.Service, EventTone.Notable),
        E("DNS Client Events", 1014, "A name lookup timed out", "A DNS query got no answer in time.", EventArea.Network, EventTone.Notable),
        E("Time-Service", 129, "The time service could not reach a peer", "Time synchronisation failed to contact its source.", EventArea.Time, EventTone.Notable),
        E("Time-Service", 134, "The time source was rejected", "The time service refused an offered time because it was too far out.", EventArea.Time, EventTone.Notable),
        E("Schannel", 36871, "A secure connection could not be created", "TLS could not establish a secure channel.", EventArea.Network, EventTone.Notable),
        E("Schannel", 36874, "A secure connection was refused", "A TLS connection failed because no cipher suite was common to both ends.", EventArea.Network, EventTone.Notable),
        E("CAPI2", 4107, "A certificate list could not be updated", "Windows could not retrieve updated certificate trust data.", EventArea.Security, EventTone.Routine),
        E("RestartManager", 10000, "A restart session was started", "Restart Manager began tracking applications so an installer could close and reopen them.", EventArea.Application, EventTone.Routine),
        E("RestartManager", 10001, "An application was shut down by an installer", "Restart Manager closed an application on behalf of an installer.", EventArea.Application, EventTone.Routine),
        E("User Profiles Service", 1530, "A user profile was left in use", "Windows found registry handles still open when unloading a profile.", EventArea.Application, EventTone.Routine),
        E("User Profiles Service", 1542, "A profile section could not be loaded", "Part of a user profile could not be loaded.", EventArea.Application, EventTone.Warning)
    }.ToDictionary(item => Key(item.Provider, item.EventId), StringComparer.OrdinalIgnoreCase);

    private static string Key(string provider, int id) => $"{Normalise(provider)}|{id}";

    /// <summary>
    /// Providers appear under long and short names in different logs — "Ntfs" and
    /// "Microsoft-Windows-Ntfs" are the same source. Matching is done on the bare
    /// name so one entry serves both.
    /// </summary>
    private static string Normalise(string provider)
    {
        var name = provider.Trim();
        const string prefix = "Microsoft-Windows-";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name[prefix.Length..] : name;
    }

    /// <summary>Looks up an event. Returns null rather than inventing a meaning.</summary>
    public static EventMeaning? Find(string? provider, int eventId)
    {
        if (string.IsNullOrWhiteSpace(provider)) return null;
        if (Catalog.TryGetValue(Key(provider, eventId), out var direct)) return direct;
        // Some providers arrive fully qualified with a channel suffix.
        var trimmed = Normalise(provider).Split('/')[0];
        return Catalog.TryGetValue($"{trimmed}|{eventId}", out var fallback) ? fallback : null;
    }

    /// <summary>How many event types this build can name. Surfaced in the help pane.</summary>
    public static int Count => Catalog.Count;

    public static IReadOnlyCollection<EventMeaning> All => (IReadOnlyCollection<EventMeaning>)Catalog.Values;

    public static string AreaLabel(EventArea area) => area switch
    {
        EventArea.Power => "Power & shutdown",
        EventArea.Boot => "Startup",
        EventArea.Hardware => "Hardware",
        EventArea.Storage => "Storage",
        EventArea.FileSystem => "File system",
        EventArea.Graphics => "Graphics",
        EventArea.Driver => "Drivers",
        EventArea.Device => "Devices",
        EventArea.Network => "Network",
        EventArea.Application => "Applications",
        EventArea.Service => "Services",
        EventArea.Memory => "Memory",
        EventArea.Performance => "Performance",
        EventArea.Security => "Security",
        EventArea.Update => "Updates",
        EventArea.Time => "Time",
        _ => "Other"
    };

    public static string ToneLabel(EventTone tone) => tone switch
    {
        EventTone.Routine => "Routine",
        EventTone.Notable => "Worth noting",
        EventTone.Warning => "Something went wrong",
        EventTone.Serious => "Serious",
        _ => "Unclassified"
    };
}
