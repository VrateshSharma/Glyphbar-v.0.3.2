using Glyphbar.App.ViewModels;
using Glyphbar.App.Views;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Platform.SystemTray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Glyphbar.App.Services;

public sealed partial class ShellCoordinator
{
    private FloatingWindow? _search, _launcher, _widgetPanel, _power;
    private MainWindow? _main;
    private SettingsWindow? _settingsWindow;
    private DesktopWidgetsWindow? _desktopWidgets;
    private BatteryLevel? _lastNotifiedLevel;

    private T Get<T>() where T : notnull => _services.GetRequiredService<T>();

    /// <summary>Creates, moves or removes the desktop widgets to match the settings.</summary>
    private void SyncDesktopWidgets()
    {
        if (_settings.Current.Widgets.ShowOnDesktop)
        {
            if (_desktopWidgets is null) _desktopWidgets = new DesktopWidgetsWindow(Get<WidgetLayoutService>(), _services, _settings);
            else _desktopWidgets.Relayout();
        }
        else
        {
            _desktopWidgets?.Dispose();
            _desktopWidgets = null;
        }
    }

    public void ShowDashboard()
    {
        _main ??= new MainWindow(Get<DashboardViewModel>(), Get<WidgetLayoutService>(), _services, _settings);
        _main.ShowAndFocus();
    }

    public void ToggleSearch()
    {
        _search ??= new FloatingWindow(new SearchView(Get<SearchViewModel>()), _settings, "Glyphbar Search");
        Toggle(_search, 640, 460, FloatingPlacement.UpperCenter);
    }

    public void ToggleLauncher()
    {
        _launcher ??= new FloatingWindow(new LauncherView(Get<LauncherViewModel>()), _settings, "Glyphbar Launcher");
        Toggle(_launcher, 860, 640, FloatingPlacement.Center);
    }

    public void ToggleWidgetPanel()
    {
        _widgetPanel ??= new FloatingWindow(new WidgetPanelView(Get<WidgetLayoutService>(), _services), _settings, "Glyphbar Widgets");
        Toggle(_widgetPanel, 420, 760, FloatingPlacement.BottomRight);
    }

    public void TogglePowerMenu()
    {
        _power ??= new FloatingWindow(new PowerMenuView(Get<PowerMenuViewModel>()), _settings, "Glyphbar Power");
        Toggle(_power, 560, 340, FloatingPlacement.Center);
    }

    private static void Toggle(FloatingWindow window, int width, int height, FloatingPlacement placement)
    {
        if (window.IsOpen) _ = window.HideAsync();
        else window.ShowAt(width, height, placement);
    }

    public void HideOverlays()
    {
        foreach (var w in new[] { _search, _launcher, _widgetPanel, _power })
            if (w is { IsOpen: true }) _ = w.HideAsync();
    }

    public void ShowSettings()
    {
        HideOverlays();
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(Get<SettingsViewModel>(), _settings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.ShowAndFocus();
    }

    private void CloseOverlays()
    {
        foreach (var w in new[] { _search, _launcher, _widgetPanel, _power })
            w?.Destroy();
        _search = _launcher = _widgetPanel = _power = null;
        _main?.Destroy();
        _main = null;
        _desktopWidgets?.Dispose();
        _desktopWidgets = null;
        _settingsWindow?.Close();
        _settingsWindow = null;
    }

    /// <summary>Tries a new gesture; on failure the previous one stays active.</summary>
    public HotkeyRegistration ApplyHotkey(HotkeyAction action, string gesture)
    {
        var h = _settings.Current.Hotkeys;
        string Current(HotkeyAction a) => a switch
        {
            HotkeyAction.Search => h.Search,
            HotkeyAction.Dashboard => h.Dashboard,
            HotkeyAction.WidgetPanel => h.WidgetPanel,
            _ => h.PowerMenu,
        };

        string previous = Current(action);
        bool duplicate = Enum.GetValues<HotkeyAction>().Any(a => a != action && Current(a).Equals(gesture, StringComparison.OrdinalIgnoreCase));
        var result = duplicate ? HotkeyRegistration.Conflict : _hotkeys.Register(action, gesture);
        if (result != HotkeyRegistration.Registered)
        {
            _hotkeys.Register(action, previous);
            return result;
        }

        _activeFallbacks.Remove(action);
        _settings.Update(s =>
        {
            switch (action)
            {
                case HotkeyAction.Search: s.Hotkeys.Search = gesture; break;
                case HotkeyAction.Dashboard: s.Hotkeys.Dashboard = gesture; break;
                case HotkeyAction.WidgetPanel: s.Hotkeys.WidgetPanel = gesture; break;
                case HotkeyAction.PowerMenu: s.Hotkeys.PowerMenu = gesture; break;
            }
        });
        return result;
    }

    public async Task PickIconAsync(string appId)
    {
        string? path = await PickFileAsync(".png", ".jpg", ".ico");
        if (path is not null) await _pins.SetIconAsync(appId, path);
    }

    public async Task<string?> PickFileAsync(params string[] extensions)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, _events.Handle);
        foreach (string x in extensions) picker.FileTypeFilter.Add(x);
        bool wasSuppressed = FloatingWindow.SuppressLightDismiss;
        try
        {
            FloatingWindow.SuppressLightDismiss = true;
            return (await picker.PickSingleFileAsync())?.Path;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "File picker failed");
            return null;
        }
        finally
        {
            FloatingWindow.SuppressLightDismiss = wasSuppressed;
        }
    }

    private void OnBatteryChangedForNotification(object? sender, EventArgs e) => _ui.Post(() =>
    {
        var b = _monitor.Battery;
        if (b.Percent is not { } percent || b.OnAcPower != false)
        {
            _lastNotifiedLevel = null;
            return;
        }
        var level = Core.Services.BatteryInterpreter.Classify(percent, _settings.Current.Widgets.BatteryThresholds);
        if (level is not (BatteryLevel.Low or BatteryLevel.Critical) || level == _lastNotifiedLevel) return;
        _lastNotifiedLevel = level;
        if (_settings.Current.System.ShowNotifications)
            _tray.Notify(level == BatteryLevel.Critical ? "Battery critical" : "Battery low", $"{percent}% remaining");
    });
}
