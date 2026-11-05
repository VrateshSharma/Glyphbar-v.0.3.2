using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Glyphbar.Core.Helpers;
using Glyphbar.Core.Interfaces;
using Glyphbar.Platform.Interop;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Applications;

public sealed class ShellIconProvider(ShellWorker worker, ILogger<ShellIconProvider> logger) : IIconProvider
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public nint bmBits;
    }

    [DllImport("gdi32")] private static extern int GetObject(nint obj, int size, out BITMAP bitmap);
    [DllImport("gdi32")] private static extern int GetDIBits(nint hdc, nint bitmap, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER info, uint usage);
    [DllImport("gdi32")] private static extern nint CreateCompatibleDC(nint hdc);
    [DllImport("gdi32")] private static extern bool DeleteDC(nint hdc);

    private readonly ConcurrentDictionary<(string, int, bool), byte[]?> _cache = new();

    public async Task<byte[]?> GetIconPngAsync(string launchTarget, int size, bool monochrome, CancellationToken ct = default)
    {
        var key = (launchTarget, size, monochrome);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var png = await worker.RunAsync(() => Extract(launchTarget, size, monochrome), ct).ConfigureAwait(false);
        _cache[key] = png;
        return png;
    }

    private byte[]? Extract(string target, int size, bool monochrome)
    {
        object? shellItem = null;
        nint bitmap = 0;
        try
        {
            var iid = Shell.IID_IShellItem;
            if (Shell.SHCreateItemFromParsingName(target, 0, ref iid, out shellItem) != 0) return null;

            var factory = (IShellItemImageFactory)shellItem;
            if (factory.GetImage(new SIZE { cx = size, cy = size }, Shell.SIIGBF_ICONONLY | Shell.SIIGBF_BIGGERSIZEOK, out bitmap) != 0)
                return null;

            return Encode(bitmap, monochrome);
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            logger.LogDebug(e, "No icon for {Target}", target);
            return null;
        }
        finally
        {
            if (bitmap != 0) Native.DeleteObject(bitmap);
            if (shellItem is not null) Marshal.ReleaseComObject(shellItem);
        }
    }

    private static byte[]? Encode(nint bitmap, bool monochrome)
    {
        if (GetObject(bitmap, Marshal.SizeOf<BITMAP>(), out var info) == 0) return null;
        int width = info.bmWidth, height = Math.Abs(info.bmHeight);
        if (width <= 0 || height <= 0) return null;

        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height,   // negative asks GDI for top-down rows, whatever the source's order
            biPlanes = 1,
            biBitCount = 32,
        };

        var pixels = new byte[width * height * 4];
        nint dc = CreateCompatibleDC(0);
        try
        {
            if (GetDIBits(dc, bitmap, 0, (uint)height, pixels, ref header, 0) == 0) return null;
        }
        finally
        {
            DeleteDC(dc);
        }

        IconImage.FixMissingAlpha(pixels);
        IconImage.Unpremultiply(pixels);
        if (monochrome) IconImage.ToMonochrome(pixels);
        return IconImage.EncodePng(pixels, width, height);
    }
}
