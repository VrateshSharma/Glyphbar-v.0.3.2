using System.Runtime.InteropServices;
using Glyphbar.Platform.Interop;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Windowing;

/// <summary>
/// A hidden top-level window owned by the UI thread. It receives what WinUI does not expose:
/// hotkeys, tray callbacks and system broadcasts. A message-only window would miss broadcasts such as
/// TaskbarCreated and WM_DISPLAYCHANGE, so this is deliberately a real, never-shown window.
/// </summary>
public sealed class ShellEventWindow : IDisposable
{
    private const string ClassName = "GlyphbarShellEvents";
    public const int TrayCallback = Native.WM_APP + 1;
    private const int PBT_APMSUSPEND = 4, PBT_APMRESUMEAUTOMATIC = 0x12;
    private const int SPI_SETWORKAREA = 0x2F;

    private readonly Native.WndProc _proc;   // rooted: the OS holds the only other reference
    private readonly uint _taskbarCreated;
    private readonly ILogger<ShellEventWindow> _logger;

    public ShellEventWindow(ILogger<ShellEventWindow> logger)
    {
        _logger = logger;
        _proc = WndProc;
        _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");

        var wc = new Native.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = Native.GetModuleHandle(null),
            lpszClassName = ClassName,
        };
        Native.RegisterClassEx(ref wc);
        Handle = Native.CreateWindowEx(0, ClassName, "Glyphbar", unchecked((uint)Native.WS_POPUP), 0, 0, 0, 0, 0, 0, wc.hInstance, 0);
        if (Handle == 0)
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
    }

    public nint Handle { get; private set; }

    public event Action<int>? HotkeyPressed;
    public event Action<int>? TrayEvent;
    /// <summary>Explorer started or restarted: appbars and tray icons must be registered again.</summary>
    public event Action? ShellRestarted;
    public event Action? DisplayChanged;
    public event Action? DevicesChanged;
    public event Action? Resumed;
    public event Action? SessionEnding;

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            if (msg == _taskbarCreated)
            {
                _logger.LogInformation("TaskbarCreated received");
                ShellRestarted?.Invoke();
                return 0;
            }

            switch ((int)msg)
            {
                case Native.WM_HOTKEY:
                    HotkeyPressed?.Invoke((int)wParam);
                    return 0;
                case TrayCallback:
                    TrayEvent?.Invoke((int)(lParam & 0xFFFF));
                    return 0;
                case Native.WM_DISPLAYCHANGE:
                    DisplayChanged?.Invoke();
                    break;
                case Native.WM_SETTINGCHANGE when (int)wParam == SPI_SETWORKAREA:
                    DisplayChanged?.Invoke();
                    break;
                case Native.WM_DEVICECHANGE:
                    DevicesChanged?.Invoke();
                    break;
                case Native.WM_POWERBROADCAST when (int)wParam == PBT_APMRESUMEAUTOMATIC:
                    Resumed?.Invoke();
                    break;
                case Native.WM_QUERYENDSESSION:
                    return 1;
                case Native.WM_ENDSESSION when wParam != 0:
                    SessionEnding?.Invoke();
                    return 0;
            }
        }
        catch (Exception e)
        {
            // An exception must not unwind through native frames.
            _logger.LogError(e, "Unhandled exception in shell event handler for message {Message}", msg);
        }
        return Native.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (Handle == 0) return;
        Native.DestroyWindow(Handle);
        Handle = 0;
    }
}
