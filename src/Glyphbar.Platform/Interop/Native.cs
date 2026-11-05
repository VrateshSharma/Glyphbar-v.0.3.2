using System.Runtime.InteropServices;

namespace Glyphbar.Platform.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left, Top, Right, Bottom;
}

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X, Y;
}

/// <summary>Win32 declarations shared across the platform layer.</summary>
public static class Native
{
    public const int WM_NCCALCSIZE = 0x0083, WM_NCPAINT = 0x0085, WM_NCACTIVATE = 0x0086;
    public const int WM_CLOSE = 0x0010, WM_ACTIVATE = 0x0006, WM_WINDOWPOSCHANGED = 0x0047, WM_DISPLAYCHANGE = 0x007E,
        WM_SETTINGCHANGE = 0x001A, WM_ENDSESSION = 0x0016, WM_QUERYENDSESSION = 0x0011, WM_POWERBROADCAST = 0x0218,
        WM_DEVICECHANGE = 0x0219, WM_HOTKEY = 0x0312, WM_DPICHANGED = 0x02E0, WM_NULL = 0, WM_APP = 0x8000,
        WM_CONTEXTMENU = 0x007B, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;

    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_POPUP = 0x80000000, WS_VISIBLE = 0x10000000, WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000;
    public const long WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8;

    public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40;
    public static readonly nint HWND_TOPMOST = -1, HWND_NOTOPMOST = -2, HWND_TOP = 0, HWND_BOTTOM = 1;

    public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4, SW_SHOW = 5, SW_MINIMIZE = 6, SW_RESTORE = 9;

    public delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);
    public delegate nint SubclassProc(nint hWnd, uint msg, nint wParam, nint lParam, nuint idSubclass, nuint refData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize, style;
        public nint lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public nint hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)] public static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32", SetLastError = true)] public static extern bool DestroyWindow(nint hWnd);
    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);
    [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)] public static extern uint RegisterWindowMessage(string name);
    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandle(string? name);

    [DllImport("comctl32", SetLastError = true)] public static extern bool SetWindowSubclass(nint hWnd, SubclassProc proc, nuint id, nuint data);
    [DllImport("comctl32", SetLastError = true)] public static extern bool RemoveWindowSubclass(nint hWnd, SubclassProc proc, nuint id);
    [DllImport("comctl32")] public static extern nint DefSubclassProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32", EntryPoint = "GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint hWnd, int index);
    [DllImport("user32", EntryPoint = "SetWindowLongPtrW")] public static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);
    [DllImport("user32", SetLastError = true)] public static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32")] public static extern bool GetWindowRect(nint hWnd, out RECT rect);
    [DllImport("user32")] public static extern bool ShowWindow(nint hWnd, int cmd);
    [DllImport("user32")] public static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32")] public static extern bool IsWindow(nint hWnd);
    [DllImport("user32")] public static extern bool IsIconic(nint hWnd);
    [DllImport("user32")] public static extern bool SetForegroundWindow(nint hWnd);
    [DllImport("user32")] public static extern nint GetForegroundWindow();
    [DllImport("user32")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32")] public static extern nint GetWindow(nint hWnd, uint cmd);
    [DllImport("user32", SetLastError = true)] public static extern uint GetWindowThreadProcessId(nint hWnd, out uint pid);
    [DllImport("user32")] public static extern uint GetDpiForWindow(nint hWnd);
    [DllImport("user32")] public static extern int SetWindowRgn(nint hWnd, nint rgn, bool redraw);
    [DllImport("gdi32")] public static extern nint CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [DllImport("gdi32")] public static extern bool DeleteObject(nint obj);

    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern int InternalGetWindowText(nint hWnd, char[] text, int max);
    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern int GetClassName(nint hWnd, char[] text, int max);
    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);
    [DllImport("user32")] public static extern bool EnumWindows(EnumWindowsProc proc, nint lParam);

    [DllImport("dwmapi")] public static extern int DwmSetWindowAttribute(nint hWnd, int attr, ref int value, int size);
    [DllImport("dwmapi")] public static extern int DwmGetWindowAttribute(nint hWnd, int attr, out int value, int size);
    [StructLayout(LayoutKind.Sequential)] public struct MARGINS { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi")] public static extern int DwmExtendFrameIntoClientArea(nint hWnd, ref MARGINS margins);
    public const int DWMWA_CLOAKED = 14, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_BORDER_COLOR = 34, DWMWA_NCRENDERING_POLICY = 2;
    public const int DWMWCP_DONOTROUND = 1, DWMWCP_ROUND = 2;
    public const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    // Monitors
    public const uint MONITOR_DEFAULTTOPRIMARY = 1, MONITOR_DEFAULTTONEAREST = 2, MONITORINFOF_PRIMARY = 1;
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }
    public delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint rect, nint data);
    [DllImport("user32")] public static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc proc, nint data);
    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX info);
    [DllImport("user32")] public static extern nint MonitorFromWindow(nint hWnd, uint flags);
    [DllImport("user32")] public static extern nint MonitorFromPoint(POINT point, uint flags);
    [DllImport("shcore")] public static extern int GetDpiForMonitor(nint hMonitor, int type, out uint x, out uint y);

    // Processes
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    [DllImport("kernel32", SetLastError = true)] public static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32", SetLastError = true)] public static extern bool CloseHandle(nint handle);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool QueryFullProcessImageName(nint process, uint flags, char[] name, ref uint size);
    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    public static extern int GetApplicationUserModelId(nint process, ref uint length, char[]? id);
    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    public static extern int GetCurrentPackageFullName(ref uint length, char[]? name);

    // WinEvents
    public const uint EVENT_SYSTEM_FOREGROUND = 3, EVENT_SYSTEM_MINIMIZESTART = 0x16, EVENT_SYSTEM_MINIMIZEEND = 0x17,
        EVENT_OBJECT_CREATE = 0x8000, EVENT_OBJECT_DESTROY = 0x8001, EVENT_OBJECT_SHOW = 0x8002, EVENT_OBJECT_HIDE = 0x8003,
        EVENT_OBJECT_CLOAKED = 0x8017, EVENT_OBJECT_UNCLOAKED = 0x8018, WINEVENT_OUTOFCONTEXT = 0;
    public delegate void WinEventProc(nint hook, uint evt, nint hWnd, int idObject, int idChild, uint thread, uint time);
    [DllImport("user32")] public static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc proc, uint pid, uint tid, uint flags);
    [DllImport("user32")] public static extern bool UnhookWinEvent(nint hook);

    public static bool IsPackaged()
    {
        uint length = 0;
        // ERROR_INSUFFICIENT_BUFFER (122) means a package name exists; 15700 means none.
        return GetCurrentPackageFullName(ref length, null) == 122;
    }
}
