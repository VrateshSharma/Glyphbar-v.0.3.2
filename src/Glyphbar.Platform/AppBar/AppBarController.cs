using System.Runtime.InteropServices;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Glyphbar.Platform.Interop;
using Glyphbar.Platform.Windowing;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.AppBar;

/// <summary>
/// Owns one window's registration with the shell through SHAppBarMessage.
///
/// Sequence: ABM_NEW registers the window and a callback message; ABM_QUERYPOS lets the shell adjust a
/// proposed rectangle around the taskbar and other appbars; ABM_SETPOS reserves the final rectangle out of
/// the work area; ABM_REMOVE gives it back. The window then has to be moved to the rectangle by hand.
///
/// The shell forgets every appbar when Explorer restarts, so <see cref="Reregister"/> runs on TaskbarCreated.
/// </summary>
public sealed class AppBarController : IDisposable
{
    private const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3, ABM_ACTIVATE = 6,
        ABM_WINDOWPOSCHANGED = 9, ABM_SETAUTOHIDEBAREX = 12;
    private const uint ABE_TOP = 1, ABE_BOTTOM = 3;
    private const int ABN_STATECHANGE = 0, ABN_POSCHANGED = 1, ABN_FULLSCREENAPP = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public nint lParam;
    }

    [DllImport("shell32")] private static extern nuint SHAppBarMessage(uint msg, ref APPBARDATA data);

    private readonly nint _hwnd;
    private readonly uint _callback;
    private readonly ILogger _logger;
    private readonly Native.SubclassProc _subclass;  // rooted for the native callback
    private readonly Func<TaskbarSettings> _settings;
    private nint _monitor;
    private bool _registered;
    private bool _autoHideRegistered;
    private bool _autoHideAttempted;
    private bool _repositioning;

    public AppBarController(nint hwnd, nint monitor, Func<TaskbarSettings> settings, ILogger logger)
    {
        _hwnd = hwnd;
        _monitor = monitor;
        _settings = settings;
        _logger = logger;
        _callback = Native.RegisterWindowMessage("GlyphbarAppBarNotify");
        _subclass = SubclassProc;
        Native.SetWindowSubclass(hwnd, _subclass, 1, 0);
    }

    public bool IsRegistered => _registered;
    public PixelRect WindowRect { get; private set; }
    public PixelRect MonitorBounds { get; private set; }
    public uint Dpi { get; private set; } = 96;

    /// <summary>The shell moved something; the owner should schedule <see cref="Reposition"/>.</summary>
    public event Action? PositionInvalidated;
    /// <summary>True while a full-screen app covers this bar's monitor.</summary>
    public event Action<bool>? FullscreenChanged;
    /// <summary>Raised after the window has been moved to <see cref="WindowRect"/>.</summary>
    public event Action? Positioned;

    private APPBARDATA Data() => new()
    {
        cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
        hWnd = _hwnd,
        uCallbackMessage = _callback,
    };

    public bool Register()
    {
        if (_registered) return true;
        var data = Data();
        _registered = SHAppBarMessage(ABM_NEW, ref data) != 0;
        if (!_registered) _logger.LogWarning("ABM_NEW was refused");
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        ReleaseAutoHide();
        var data = Data();
        SHAppBarMessage(ABM_REMOVE, ref data);
        _registered = false;
    }

    /// <summary>After an Explorer restart the shell has no record of us, so start from scratch.</summary>
    public void Reregister()
    {
        _registered = false;
        _autoHideRegistered = false;
        _autoHideAttempted = false;
        Register();
        Reposition();
    }

    public void SetMonitor(nint monitor) => _monitor = monitor;

    /// <summary>Negotiates a position with the shell and moves the window there.</summary>
    public void Reposition()
    {
        if (_repositioning) return;
        var monitor = MonitorInfo.Describe(_monitor);
        if (monitor is null)
        {
            _logger.LogWarning("Monitor for the bar is gone");
            return;
        }

        _repositioning = true;
        try
        {
            var settings = _settings();
            MonitorBounds = monitor.Bounds;
            Dpi = monitor.Dpi;
            int thickness = TaskbarGeometry.ReservedThickness(settings, monitor.Dpi);
            var strip = TaskbarGeometry.RequestedStrip(monitor.Bounds, settings.Edge, thickness);

            if (settings.AutoHide)
            {
                // An auto-hide bar reserves nothing. Restart the registration to drop any earlier reservation.
                if (!_autoHideAttempted)
                {
                    Unregister();
                    Register();
                    TryRegisterAutoHide(settings.Edge, monitor);
                }
            }
            else
            {
                if (_autoHideAttempted) ReleaseAutoHide();
                if (!_registered) Register();
                strip = Negotiate(strip, settings.Edge, thickness);
            }

            var rect = TaskbarGeometry.WindowRect(strip, settings, monitor.Dpi);
            WindowRect = rect;
            Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, rect.Left, rect.Top, rect.Width, rect.Height, Native.SWP_NOACTIVATE);
            Positioned?.Invoke();
        }
        finally
        {
            _repositioning = false;
        }
    }

    private PixelRect Negotiate(PixelRect strip, TaskbarEdge edge, int thickness)
    {
        var data = Data();
        data.uEdge = edge == TaskbarEdge.Bottom ? ABE_BOTTOM : ABE_TOP;
        data.rc = new RECT { Left = strip.Left, Top = strip.Top, Right = strip.Right, Bottom = strip.Bottom };
        SHAppBarMessage(ABM_QUERYPOS, ref data);

        var fitted = TaskbarGeometry.FitStrip(MonitorInfo.ToRect(data.rc), edge, thickness);
        data.rc = new RECT { Left = fitted.Left, Top = fitted.Top, Right = fitted.Right, Bottom = fitted.Bottom };
        SHAppBarMessage(ABM_SETPOS, ref data);
        return MonitorInfo.ToRect(data.rc);
    }

    private void TryRegisterAutoHide(TaskbarEdge edge, MonitorDescriptor monitor)
    {
        var data = Data();
        data.uEdge = edge == TaskbarEdge.Bottom ? ABE_BOTTOM : ABE_TOP;
        data.rc = new RECT { Left = monitor.Bounds.Left, Top = monitor.Bounds.Top, Right = monitor.Bounds.Right, Bottom = monitor.Bounds.Bottom };
        data.lParam = 1;
        // Only one auto-hide bar may own an edge. If the Windows taskbar is auto-hidden on the same edge this is
        // refused; our own slide behaviour still works, the shell just will not count us as an auto-hide bar.
        _autoHideRegistered = SHAppBarMessage(ABM_SETAUTOHIDEBAREX, ref data) != 0;
        if (!_autoHideRegistered)
            _logger.LogInformation("Edge already has an auto-hide bar; using unregistered auto-hide");
        _autoHideAttempted = true;
    }

    private void ReleaseAutoHide()
    {
        _autoHideAttempted = false;
        if (!_autoHideRegistered) return;
        var data = Data();
        data.uEdge = _settings().Edge == TaskbarEdge.Bottom ? ABE_BOTTOM : ABE_TOP;
        data.lParam = 0;
        SHAppBarMessage(ABM_SETAUTOHIDEBAREX, ref data);
        _autoHideRegistered = false;
    }

    private nint SubclassProc(nint hWnd, uint msg, nint wParam, nint lParam, nuint id, nuint refData)
    {
        try
        {
            if (msg == _callback)
            {
                switch ((int)wParam)
                {
                    case ABN_POSCHANGED:
                    case ABN_STATECHANGE:
                        PositionInvalidated?.Invoke();
                        break;
                    case ABN_FULLSCREENAPP:
                        FullscreenChanged?.Invoke(lParam != 0);
                        break;
                }
                return 0;
            }

            switch ((int)msg)
            {
                case Native.WM_ACTIVATE when _registered:
                {
                    var data = Data();
                    SHAppBarMessage(ABM_ACTIVATE, ref data);
                    break;
                }
                case Native.WM_WINDOWPOSCHANGED when _registered:
                {
                    var data = Data();
                    SHAppBarMessage(ABM_WINDOWPOSCHANGED, ref data);
                    break;
                }
                case Native.WM_DPICHANGED:
                    PositionInvalidated?.Invoke();
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unhandled exception in appbar message {Message}", msg);
        }
        return Native.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        Unregister();
        Native.RemoveWindowSubclass(_hwnd, _subclass, 1);
    }
}
