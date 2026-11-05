using System.Runtime.InteropServices;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Services;
using Glyphbar.Platform.Windowing;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Hotkeys;

public sealed class HotkeyService : IHotkeyService, IDisposable
{
    private const uint MOD_NOREPEAT = 0x4000;
    [DllImport("user32", SetLastError = true)] private static extern bool RegisterHotKey(nint hWnd, int id, uint mods, uint vk);
    [DllImport("user32", SetLastError = true)] private static extern bool UnregisterHotKey(nint hWnd, int id);

    private readonly ShellEventWindow _window;
    private readonly ILogger<HotkeyService> _logger;
    private readonly Dictionary<HotkeyAction, HotkeyRegistration> _status = [];
    private readonly HashSet<int> _active = [];

    public HotkeyService(ShellEventWindow window, ILogger<HotkeyService> logger)
    {
        _window = window;
        _logger = logger;
        _window.HotkeyPressed += OnPressed;
    }

    public event EventHandler<HotkeyAction>? Pressed;
    public IReadOnlyDictionary<HotkeyAction, HotkeyRegistration> Status => _status;

    private static int IdOf(HotkeyAction action) => 0x4700 + (int)action;

    private void OnPressed(int id)
    {
        if (_active.Contains(id)) Pressed?.Invoke(this, (HotkeyAction)(id - 0x4700));
    }

    public HotkeyRegistration Register(HotkeyAction action, string gesture)
    {
        int id = IdOf(action);
        if (_active.Remove(id)) UnregisterHotKey(_window.Handle, id);

        HotkeyRegistration result;
        if (!HotkeyGesture.TryParse(gesture, out var parsed))
            result = HotkeyRegistration.Invalid;
        else if (parsed.IsReserved)
            result = HotkeyRegistration.Reserved;
        else if (RegisterHotKey(_window.Handle, id, (uint)parsed.Modifiers | MOD_NOREPEAT, parsed.VirtualKey))
        {
            _active.Add(id);
            result = HotkeyRegistration.Registered;
        }
        else
        {
            // 1409 ERROR_HOTKEY_ALREADY_REGISTERED: another process owns this gesture.
            _logger.LogWarning("Hotkey {Gesture} for {Action} refused (error {Error})", gesture, action, Marshal.GetLastWin32Error());
            result = HotkeyRegistration.Conflict;
        }
        _status[action] = result;
        return result;
    }

    public void UnregisterAll()
    {
        foreach (int id in _active) UnregisterHotKey(_window.Handle, id);
        _active.Clear();
    }

    public void Dispose()
    {
        _window.HotkeyPressed -= OnPressed;
        UnregisterAll();
    }
}
