using System.Runtime.InteropServices;
using Glyphbar.Platform.Interop;
using Glyphbar.Platform.Windowing;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.SystemTray;

public enum TrayCommand { None, Open = 1, Settings, ToggleTaskbar, RestartInterface, Exit }

/// <summary>Notification-area icon. It is re-added on TaskbarCreated, since Explorer drops all icons on restart.</summary>
public sealed class TrayIcon : IDisposable
{
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_INFO = 0x10, NIF_SHOWTIP = 0x80;
    private const uint NOTIFYICON_VERSION_4 = 4;
    private const int NIN_SELECT = 0x400, NIN_KEYSELECT = 0x401;
    private const uint MF_STRING = 0, MF_SEPARATOR = 0x800;
    private const uint TPM_RETURNCMD = 0x100, TPM_RIGHTBUTTON = 2, TPM_BOTTOMALIGN = 0x20;
    private const uint IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID, uFlags, uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA data);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint LoadImage(nint inst, string name, uint type, int cx, int cy, uint load);
    [DllImport("user32")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32")] private static extern nint CreatePopupMenu();
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint menu, uint flags, nuint id, string? text);
    [DllImport("user32")] private static extern int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hWnd, nint tpm);
    [DllImport("user32")] private static extern bool DestroyMenu(nint menu);

    private readonly ShellEventWindow _window;
    private readonly ILogger<TrayIcon> _logger;
    private nint _icon;
    private bool _added;

    public TrayIcon(ShellEventWindow window, ILogger<TrayIcon> logger)
    {
        _window = window;
        _logger = logger;
        _window.TrayEvent += OnTrayEvent;
        _window.ShellRestarted += OnShellRestarted;
    }

    public event Action<TrayCommand>? Command;
    public bool IsAdded => _added;

    public void Show(string iconPath)
    {
        _icon = LoadImage(0, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
        if (_icon == 0) _logger.LogWarning("Could not load tray icon {Path}", iconPath);
        Add();
    }

    private NOTIFYICONDATA Data() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
        uCallbackMessage = ShellEventWindow.TrayCallback,
        hIcon = _icon,
        szTip = "Glyphbar",
        szInfo = "",
        szInfoTitle = "",
    };

    private void Add()
    {
        var data = Data();
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
        if (!_added)
        {
            // Explorer may not have finished starting; TaskbarCreated will retry.
            _logger.LogWarning("Shell_NotifyIcon(NIM_ADD) failed");
            return;
        }
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    /// <summary>Shows a Windows notification from the tray icon.</summary>
    public void Notify(string title, string text)
    {
        if (!_added) return;
        var data = Data();
        data.uFlags = NIF_INFO;
        data.szInfoTitle = title;
        data.szInfo = text;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private void OnShellRestarted()
    {
        _added = false;
        Add();
    }

    private void OnTrayEvent(int evt)
    {
        switch (evt)
        {
            case Native.WM_LBUTTONUP or NIN_SELECT or NIN_KEYSELECT:
                Command?.Invoke(TrayCommand.Open);
                break;
            case Native.WM_CONTEXTMENU or Native.WM_RBUTTONUP:
                ShowMenu();
                break;
        }
    }

    private void ShowMenu()
    {
        nint menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING, (nuint)TrayCommand.Open, "Open Glyphbar");
        AppendMenu(menu, MF_STRING, (nuint)TrayCommand.Settings, "Open settings");
        AppendMenu(menu, MF_STRING, (nuint)TrayCommand.ToggleTaskbar, "Toggle taskbar visibility");
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        AppendMenu(menu, MF_STRING, (nuint)TrayCommand.RestartInterface, "Restart interface");
        AppendMenu(menu, MF_STRING, (nuint)TrayCommand.Exit, "Exit Glyphbar");

        Native.GetCursorPos(out var p);
        // Without foreground rights the menu would not dismiss when clicking elsewhere.
        Native.SetForegroundWindow(_window.Handle);
        int cmd = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, p.X, p.Y, _window.Handle, 0);
        Native.PostMessage(_window.Handle, Native.WM_NULL, 0, 0);
        DestroyMenu(menu);
        if (cmd != 0) Command?.Invoke((TrayCommand)cmd);
    }

    public void Dispose()
    {
        _window.TrayEvent -= OnTrayEvent;
        _window.ShellRestarted -= OnShellRestarted;
        if (_added)
        {
            var data = Data();
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != 0) DestroyIcon(_icon);
        _icon = 0;
    }
}
