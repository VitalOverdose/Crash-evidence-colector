using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Http;
using CrashEvidenceCollector.Core;

namespace CrashEvidenceCollector.App;

/// <summary>
/// Tab icons for the web panes.
///
/// Approach adapted from the browser project's favicon subsystem, which learned
/// these lessons the hard way:
///
/// - WebView2's <c>FaviconChanged</c> and <c>FaviconUri</c> are reliable enough to
///   use as the trigger and the source; <c>GetFaviconAsync</c> is not depended on,
///   because the URI is frequently null when the event first fires and the event
///   does not fire at all on some cached, back/forward, or restored loads.
/// - The cache is keyed by <b>host</b>, not URL. Single-page apps change the path
///   without a document load, so a host key keeps the icon valid by construction
///   instead of needing soft-navigation detection.
/// - The cached icon is pushed immediately on navigation, before any event fires,
///   which is what removes the blank-then-pop flicker on revisits.
///
/// Deliberate improvements over the original: icons are decoded at 32px so they
/// still look right on a high-DPI tab strip rather than being softened from 16px,
/// a single shared <see cref="HttpClient"/> is used instead of one per download,
/// and a letter tile is generated when a site offers no usable icon.
///
/// Ownership: every call returns a <b>fresh</b> image. A caller must dispose the
/// previous image when it accepts a new one, and one image instance must never be
/// handed to two controls.
/// </summary>
internal static class FaviconService
{
    public const int IconSize = 32;
    private static readonly HttpClient Http = CreateClient();
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CrashEvidenceCollector/1.0");
        return client;
    }

    public static string CacheDirectory => Path.Combine(AppPaths.StateDirectory, "Favicons");

    /// <summary>Host key for the cache: lower-cased, "www." removed, filename-safe.</summary>
    public static string? HostKey(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
        if (host.Length == 0) return null;
        foreach (var invalid in Path.GetInvalidFileNameChars()) host = host.Replace(invalid, '_');
        return host;
    }

    /// <summary>A previously cached icon for this address, or null. Always a fresh image.</summary>
    public static Image? LoadFromCache(string? url)
    {
        var key = HostKey(url);
        if (key is null) return null;
        try
        {
            var path = Path.Combine(CacheDirectory, key + ".png");
            if (!File.Exists(path)) return null;
            // Copy through memory so the file is never left locked by the Image.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            memory.Position = 0;
            using var loaded = Image.FromStream(memory);
            return new Bitmap(loaded);
        }
        catch { return null; }
    }

    /// <summary>
    /// Downloads the icon at <paramref name="faviconUri"/>, caches it against the
    /// page's host, and returns a fresh image. Returns null when nothing usable is
    /// available — including SVG icons, which are not rasterized here.
    /// </summary>
    public static async Task<Image?> DownloadAndCacheAsync(string? pageUrl, string? faviconUri, CancellationToken token = default)
    {
        var key = HostKey(pageUrl);
        if (key is null || string.IsNullOrWhiteSpace(faviconUri)) return null;
        // SVG needs a rasterizer this application does not carry; callers fall back
        // to a letter tile rather than shipping an imaging dependency for tab icons.
        if (faviconUri.Contains(".svg", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            using var response = await Http.GetAsync(faviconUri, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            var bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
            if (bytes.Length == 0) return null;
            var icon = Decode(bytes);
            if (icon is null) return null;
            await SaveAsync(key, icon, token).ConfigureAwait(false);
            return icon;
        }
        catch { return null; }
    }

    /// <summary>
    /// Decodes to a square icon at <see cref="IconSize"/>. For multi-frame .ico
    /// files the largest frame is chosen, because scaling a 16px frame up for a
    /// high-DPI strip is what makes tab icons look muddy.
    /// </summary>
    public static Image? Decode(byte[] bytes)
    {
        try
        {
            using var memory = new MemoryStream(bytes);
            Image source;
            try
            {
                using var icon = new Icon(memory, new Size(IconSize, IconSize));
                source = icon.ToBitmap();
            }
            catch
            {
                memory.Position = 0;
                source = Image.FromStream(memory);
            }
            using (source) return Square(source);
        }
        catch { return null; }
    }

    /// <summary>A readable placeholder for sites that offer no usable icon.</summary>
    public static Image CreateLetterTile(string? url, Color background)
    {
        var key = HostKey(url);
        var letter = string.IsNullOrEmpty(key) ? "?" : key[..1].ToUpperInvariant();
        var bitmap = new Bitmap(IconSize, IconSize);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using (var brush = new SolidBrush(background)) graphics.FillEllipse(brush, 0, 0, IconSize - 1, IconSize - 1);
        using var font = new Font("Segoe UI Semibold", IconSize * 0.46f, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(letter, font, Brushes.White, new RectangleF(0, 0, IconSize, IconSize), format);
        return bitmap;
    }

    private static Image Square(Image source)
    {
        var bitmap = new Bitmap(IconSize, IconSize);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, IconSize, IconSize));
        return bitmap;
    }

    private static async Task SaveAsync(string key, Image icon, CancellationToken token)
    {
        // Serialised because several tabs can resolve the same host at once; the
        // write is idempotent, so last writer winning is harmless.
        await WriteLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            using var memory = new MemoryStream();
            icon.Save(memory, ImageFormat.Png);
            await File.WriteAllBytesAsync(Path.Combine(CacheDirectory, key + ".png"), memory.ToArray(), token).ConfigureAwait(false);
        }
        catch { /* a missing icon must never interrupt browsing */ }
        finally { WriteLock.Release(); }
    }

    /// <summary>Removes every cached icon; the only way a rebranded site refreshes.</summary>
    public static int ClearCache()
    {
        try
        {
            if (!Directory.Exists(CacheDirectory)) return 0;
            var files = Directory.GetFiles(CacheDirectory, "*.png");
            foreach (var file in files) try { File.Delete(file); } catch { }
            return files.Length;
        }
        catch { return 0; }
    }
}
