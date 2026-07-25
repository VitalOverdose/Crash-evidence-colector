using System.Text.Json;

namespace CrashEvidenceCollector.Core;

public static class SettingsStore
{
    public static async Task<CollectionOptions> LoadAsync(CancellationToken token = default)
    {
        try { return File.Exists(AppPaths.SettingsFile) ? JsonSerializer.Deserialize<CollectionOptions>(await File.ReadAllTextAsync(AppPaths.SettingsFile, token).ConfigureAwait(false), JsonDefaults.Indented) ?? new() : new(); }
        catch { return new(); }
    }
    public static async Task SaveAsync(CollectionOptions settings, CancellationToken token = default)
    {
        settings.Validate(); Directory.CreateDirectory(AppPaths.StateDirectory);
        await File.WriteAllTextAsync(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, JsonDefaults.Indented), token).ConfigureAwait(false);
    }
}
