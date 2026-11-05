using System.Runtime.InteropServices;
using Glyphbar.Platform.Interop;

namespace Glyphbar.Platform.Windowing;

/// <summary>
/// Keeps a window on the desktop: directly above the wallpaper and icons, and below every app.
///
/// HWND_BOTTOM can't do this, because the desktop is itself a window and that would land behind it. Instead the window
/// is inserted just above the desktop window, and the position is enforced in WM_WINDOWPOSCHANGING so clicking a widget
/// doesn't raise it. Show Desktop brings the desktop window forward; the foreground hook follows it, so widgets stay
/// visible. This uses only z-order APIs, rather than re-parenting into Explorer's windows.
/// </summary>
public sealed class DesktopLayerKeeper : IDisposable
{
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const uint GW_HWNDNEXT = 2, GW_HWNDPREV = 3;
    private const uint MoveFlags = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;
    private const int MaxAttempts = 12;

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public nint hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string? className, string? title);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);

    private readonly nint _hwnd;
    private readonly Native.SubclassProc _subclass;  // rooted for the native callback
    private readonly Native.WinEventProc _foreground;
    private readonly nint _hook;
    private bool _placing;

    public DesktopLayerKeeper(nint hwnd)
    {
        _hwnd = hwnd;
        _subclass = OnMessage;
        _foreground = (_, _, _, _, _, _, _) => Place();
        Native.SetWindowSubclass(hwnd, _subclass, 3, 0);
        _hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, 0, _foreground, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
        Place();
    }

    /// <summary>The top-level window showing the icons. Looked up each time, because Explorer can restart.</summary>
    private static nint FindDesktop()
    {
        nint progman = FindWindow("Progman", null);
        if (FindWindowEx(progman, 0, "SHELLDLL_DefView", null) != 0) return progman;

        nint worker = 0;
        while ((worker = FindWindowEx(0, worker, "WorkerW", null)) != 0)
            if (FindWindowEx(worker, 0, "SHELLDLL_DefView", null) != 0)
                return worker;
        return progman;
    }

    /// <summary>
    /// Moves the window just above the desktop, then checks it really got there. SetWindowPos quietly does nothing when
    /// the reference window won't take part, as with some windows from elevated processes, so each attempt is verified
    /// and the next window up is tried. A few places higher is still below every app.
    /// </summary>
    public void Place()
    {
        nint desktop = FindDesktop();
        if (desktop == 0) return;

        // Already within reach of the desktop: leave it, so this check costs almost nothing.
        nint candidate = desktop;
        for (int i = 0; i < MaxAttempts; i++)
        {
            candidate = Native.GetWindow(candidate, GW_HWNDPREV);
            if (candidate == _hwnd) return;
            if (candidate == 0) break;
        }

        _placing = true;
        try
        {
            candidate = Native.GetWindow(desktop, GW_HWNDPREV);
            if (candidate == 0)
            {
                // Nothing above the desktop means it was brought to the front.
                Native.SetWindowPos(_hwnd, Native.HWND_TOP, 0, 0, 0, 0, MoveFlags);
                return;
            }

            for (int i = 0; i < MaxAttempts && candidate != 0; i++)
            {
                Native.SetWindowPos(_hwnd, candidate, 0, 0, 0, 0, MoveFlags);
                if (Native.GetWindow(candidate, GW_HWNDNEXT) == _hwnd) return;
                candidate = Native.GetWindow(candidate, GW_HWNDPREV);
            }
        }
        finally
        {
            _placing = false;
        }
    }

    private nint OnMessage(nint hWnd, uint msg, nint wParam, nint lParam, nuint id, nuint data)
    {
        if (msg == WM_WINDOWPOSCHANGING && !_placing)
        {
            // Clicking a widget would otherwise raise it over the user's apps.
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            pos.flags |= Native.SWP_NOZORDER;
            Marshal.StructureToPtr(pos, lParam, false);
        }
        return Native.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != 0) Native.UnhookWinEvent(_hook);
        Native.RemoveWindowSubclass(_hwnd, _subclass, 3);
    }
}
