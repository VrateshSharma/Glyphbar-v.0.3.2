using System.Runtime.InteropServices.WindowsRuntime;
using Glyphbar.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Glyphbar.App.Services;

/// <summary>Turns icon bytes into XAML images. Call on the UI thread.</summary>
public sealed class IconLoader(IIconProvider provider, ISettingsStore settings, ILogger<IconLoader> logger)
{
    private readonly Dictionary<(string, int, bool), ImageSource?> _cache = [];

    public async Task<ImageSource?> LoadAsync(string? target, int size, string? customIconPath = null)
    {
        if (customIconPath is { Length: > 0 } && File.Exists(customIconPath))
        {
            try
            {
                return await FromBytesAsync(await File.ReadAllBytesAsync(customIconPath));
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Custom icon {Path} could not be read", customIconPath);
            }
        }

        if (string.IsNullOrEmpty(target)) return null;
        bool mono = settings.Current.Appearance.MonochromeAppIcons;
        var key = (target, size, mono);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var bytes = await provider.GetIconPngAsync(target, size, mono);
        var image = bytes is null ? null : await FromBytesAsync(bytes);
        _cache[key] = image;
        return image;
    }

    public static async Task<ImageSource?> FromBytesAsync(byte[] bytes)
    {
        var image = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);
        await image.SetSourceAsync(stream);
        return image;
    }
}
