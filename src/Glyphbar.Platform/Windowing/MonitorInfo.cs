using Glyphbar.Core.Services;
using Glyphbar.Platform.Interop;

namespace Glyphbar.Platform.Windowing;

public sealed record MonitorDescriptor(nint Handle, string DeviceName, PixelRect Bounds, PixelRect WorkArea, bool IsPrimary, uint Dpi);

public static class MonitorInfo
{
    public static IReadOnlyList<MonitorDescriptor> GetAll()
    {
        var result = new List<MonitorDescriptor>();
        Native.EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            if (Describe(monitor) is { } d) result.Add(d);
            return true;
        }, 0);
        return result;
    }

    public static MonitorDescriptor? Describe(nint monitor)
    {
        var info = new Native.MONITORINFOEX { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFOEX>() };
        if (!Native.GetMonitorInfo(monitor, ref info)) return null;
        uint dpi = Native.GetDpiForMonitor(monitor, 0, out uint x, out _) == 0 ? x : 96;
        return new MonitorDescriptor(monitor, info.szDevice, ToRect(info.rcMonitor), ToRect(info.rcWork), (info.dwFlags & Native.MONITORINFOF_PRIMARY) != 0, dpi);
    }

    public static MonitorDescriptor? ForWindow(nint hwnd) => Describe(Native.MonitorFromWindow(hwnd, Native.MONITOR_DEFAULTTONEAREST));

    public static MonitorDescriptor? ForPoint(POINT point) => Describe(Native.MonitorFromPoint(point, Native.MONITOR_DEFAULTTONEAREST));

    public static PixelRect ToRect(RECT r) => new(r.Left, r.Top, r.Right, r.Bottom);
}
