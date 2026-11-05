using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Platform.Interop;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Applications;

/// <summary>
/// Tracks switchable top-level windows. SetWinEventHook wakes it only when windows actually change,
/// so an idle desktop costs nothing. The rule for "switchable" mirrors what Alt-Tab shows.
/// </summary>
public sealed class RunningWindowTracker(ILogger<RunningWindowTracker> logger) : IRunningWindows, IDisposable
{
    private const uint GW_OWNER = 4;
    private const int OBJID_WINDOW = 0;
    private const int WM_CLOSE = Native.WM_CLOSE;

    private static readonly string[] IgnoredClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow"];

    private readonly List<nint> _hooks = [];
    private readonly ConcurrentDictionary<uint, string?> _exeByPid = new();
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private Native.WinEventProc? _rootedProc;
    private Timer? _debounce;
    private volatile IReadOnlyList<RunningWindow> _windows = [];
    private nint _lastExternalForeground;

    public IReadOnlyList<RunningWindow> Windows => _windows;
    public event EventHandler? Changed;

    /// <summary>Must be called on a thread with a message loop, because hooks are delivered there.</summary>
    public void Start()
    {
        if (_rootedProc is not null) return;
        _rootedProc = OnWinEvent;
        _debounce = new Timer(_ => Refresh());
        foreach (var (min, max) in new (uint, uint)[]
                 {
                     (Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND),
                     (Native.EVENT_SYSTEM_MINIMIZESTART, Native.EVENT_SYSTEM_MINIMIZEEND),
                     (Native.EVENT_OBJECT_DESTROY, Native.EVENT_OBJECT_HIDE),
                     (Native.EVENT_OBJECT_CLOAKED, Native.EVENT_OBJECT_UNCLOAKED),
                 })
        {
            nint hook = Native.SetWinEventHook(min, max, 0, _rootedProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
            if (hook == 0) logger.LogWarning("SetWinEventHook failed for {Min:X}", min);
            else _hooks.Add(hook);
        }
        Refresh();
    }

    private void OnWinEvent(nint hook, uint evt, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != OBJID_WINDOW || idChild != 0 || hwnd == 0) return;
        if (evt == Native.EVENT_SYSTEM_FOREGROUND)
        {
            Native.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != _ownPid) _lastExternalForeground = hwnd;
        }
        _debounce?.Change(120, Timeout.Infinite);
    }

    private void Refresh()
    {
        try
        {
            var list = new List<RunningWindow>();
            nint foreground = _lastExternalForeground != 0 ? _lastExternalForeground : Native.GetForegroundWindow();
            Native.EnumWindows((hwnd, _) =>
            {
                if (TryDescribe(hwnd, foreground) is { } window) list.Add(window);
                return true;
            }, 0);

            var old = _windows;
            _windows = list;
            if (!old.SequenceEqual(list)) Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not enumerate windows");
        }
    }

    private RunningWindow? TryDescribe(nint hwnd, nint foreground)
    {
        if (!Native.IsWindowVisible(hwnd)) return null;

        long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
        bool appWindow = (ex & Native.WS_EX_APPWINDOW) != 0;
        if (!appWindow && ((ex & Native.WS_EX_TOOLWINDOW) != 0 || Native.GetWindow(hwnd, GW_OWNER) != 0)) return null;
        if ((ex & Native.WS_EX_NOACTIVATE) != 0 && !appWindow) return null;

        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return null;

        var buffer = new char[256];
        int classLength = Native.GetClassName(hwnd, buffer, buffer.Length);
        var className = new string(buffer, 0, classLength);
        if (IgnoredClasses.Contains(className)) return null;

        // InternalGetWindowText never sends a message, so a hung app can't stall us.
        int titleLength = Native.InternalGetWindowText(hwnd, buffer, buffer.Length);
        if (titleLength == 0) return null;
        var title = new string(buffer, 0, titleLength);

        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == _ownPid) return null;

        return new RunningWindow(hwnd, title, ExeOf(pid), AumidOf(hwnd), hwnd == foreground, Native.IsIconic(hwnd));
    }

    private string? ExeOf(uint pid) => _exeByPid.GetOrAdd(pid, static p =>
    {
        nint process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, p);
        if (process == 0) return null;
        try
        {
            var name = new char[1024];
            uint size = (uint)name.Length;
            return Native.QueryFullProcessImageName(process, 0, name, ref size) ? new string(name, 0, (int)size) : null;
        }
        finally
        {
            Native.CloseHandle(process);
        }
    });

    private static string? AumidOf(nint hwnd)
    {
        var iid = Shell.IID_IPropertyStore;
        if (Shell.SHGetPropertyStoreForWindow(hwnd, ref iid, out var store) != 0) return null;
        try
        {
            var key = Shell.PKEY_AppUserModel_ID;
            return Shell.ReadStringProperty(store, ref key);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    public void Activate(nint handle)
    {
        if (Native.IsIconic(handle)) Native.ShowWindow(handle, Native.SW_RESTORE);
        Native.SetForegroundWindow(handle);
    }

    public void Minimize(nint handle) => Native.ShowWindow(handle, Native.SW_MINIMIZE);

    public void Close(nint handle) => Native.PostMessage(handle, WM_CLOSE, 0, 0);

    public void Dispose()
    {
        foreach (nint hook in _hooks) Native.UnhookWinEvent(hook);
        _hooks.Clear();
        _debounce?.Dispose();
    }
}
