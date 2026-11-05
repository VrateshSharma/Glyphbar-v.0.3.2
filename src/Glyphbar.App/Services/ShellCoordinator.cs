using Glyphbar.App.ViewModels;
using Glyphbar.App.Views;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Platform.SystemTray;
using Glyphbar.Platform.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Glyphbar.App.Services;

/// <summary>
/// Owns every window and reacts to the shell: monitors, Explorer restarts, sleep and resume, hotkeys, the tray.
/// </summary>
public sealed partial class ShellCoordinator
{
    private readonly IServiceProvider _services;
    private readonly ISettingsStore _settings;
    private readonly ShellEventWindow _events;
    private readonly TrayIcon _tray;
    private readonly IHotkeyService _hotkeys;
    private readonly IRunningWindows _running;
    private readonly IAppCatalog _catalog;
    private readonly IMediaService _media;
    private readonly PinService _pins;
    private readonly SystemMonitor _monitor;
    private readonly UiDispatcher _ui;
    private readonly ILogger<ShellCoordinator> _logger;
    private readonly Dictionary<string, TaskbarWindow> _bars = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherQueueTimer _displayDebounce;
    private bool _stopping;

    private static readonly Dictionary<HotkeyAction, string[]> Fallbacks = new()
    {
        [HotkeyAction.Search] = ["Win+Alt+Space", "Control+Alt+G"],
        [HotkeyAction.Dashboard] = ["Win+Alt+G"],
        [HotkeyAction.WidgetPanel] = ["Win+Alt+W", "Control+Alt+W"],
        [HotkeyAction.PowerMenu] = ["Win+Alt+P"],
    };

    private readonly Dictionary<HotkeyAction, string> _activeFallbacks = [];

    public ShellCoordinator(
        IServiceProvider services, ISettingsStore settings, ShellEventWindow events, TrayIcon tray,
        IHotkeyService hotkeys, IRunningWindows running, IAppCatalog catalog, IMediaService media,
        PinService pins, SystemMonitor monitor, UiDispatcher ui, ILogger<ShellCoordinator> logger)
    {
        _services = services;
        _settings = settings;
        _events = events;
        _tray = tray;
        _hotkeys = hotkeys;
        _running = running;
        _catalog = catalog;
        _media = media;
        _pins = pins;
        _monitor = monitor;
        _ui = ui;
        _logger = logger;
        _displayDebounce = ui.Queue.CreateTimer();
        _displayDebounce.IsRepeating = false;
        _displayDebounce.Interval = TimeSpan.FromMilliseconds(350);
        _displayDebounce.Tick += (_, _) => UiDispatcher.Guard(SyncBars);
    }

    public async Task StartAsync(CommandLine args)
    {
        Motion.Settings = _settings;

        _events.ShellRestarted += OnShellRestarted;
        _events.DisplayChanged += ScheduleBarSync;
        _events.Resumed += OnResumed;
        _events.DevicesChanged += _monitor.RefreshStorageNow;
        _events.SessionEnding += OnSessionEnding;
        _settings.Changed += OnSettingsChanged;
        _tray.Command += OnTrayCommand;
        _hotkeys.Pressed += OnHotkey;
        _monitor.BatteryChanged += OnBatteryChangedForNotification;

        _tray.Show(Path.Combine(AppContext.BaseDirectory, "Assets", "glyphbar.ico"));
        RegisterHotkeys();

        // Each step stands alone: an unavailable database must not cost the user their taskbar.
        await StepAsync("pinned apps", _pins.LoadAsync);
        await StepAsync("window tracking", () => { _running.Start(); return Task.CompletedTask; });
        SyncBars();

        // Everything below is background warm-up; the bar is already on screen.
        await StepAsync("application catalog", () => _catalog.RefreshAsync());
        await StepAsync("default pins", _pins.SeedDefaultsAsync);
        await StepAsync("media sessions", _media.StartAsync);

        if (!_settings.Current.System.FirstRunGuideShown)
        {
            _settings.Update(s => s.System.FirstRunGuideShown = true);
            if (!args.Startup) ShowDashboard();
        }
        HandleArguments(args);
    }

    private async Task StepAsync(string name, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Startup step failed: {Step}", name);
        }
    }

    public void HandleArguments(CommandLine args)
    {
        if (args.Exit)
        {
            _ = ExitAsync();
            return;
        }
        switch (args.Show)
        {
            case "dashboard": ShowDashboard(); break;
            case "search": ToggleSearch(); break;
            case "launcher": ToggleLauncher(); break;
            case "widgets": ToggleWidgetPanel(); break;
            case "power": TogglePowerMenu(); break;
            case "settings": ShowSettings(); break;
            case "taskbar": _settings.Update(s => s.Taskbar.Visible = true); break;
        }
    }

    /// <summary>
    /// Registers all shortcuts. If another app already owns a default, as launchers often do with Alt+Space, a
    /// fallback keeps the feature reachable. The setting is left alone, so the preferred shortcut returns if it frees up.
    /// </summary>
    public IReadOnlyDictionary<HotkeyAction, HotkeyRegistration> RegisterHotkeys()
    {
        var h = _settings.Current.Hotkeys;
        var substituted = new List<string>();
        _activeFallbacks.Clear();
        foreach (var (action, gesture) in new[]
                 {
                     (HotkeyAction.Search, h.Search), (HotkeyAction.Dashboard, h.Dashboard),
                     (HotkeyAction.WidgetPanel, h.WidgetPanel), (HotkeyAction.PowerMenu, h.PowerMenu),
                 })
        {
            if (_hotkeys.Register(action, gesture) != HotkeyRegistration.Conflict) continue;
            foreach (string fallback in Fallbacks[action])
            {
                if (_hotkeys.Register(action, fallback) != HotkeyRegistration.Registered) continue;
                _activeFallbacks[action] = fallback;
                substituted.Add($"{gesture} is taken, so {action} is on {fallback}");
                break;
            }
        }

        foreach (string note in substituted) _logger.LogInformation("Hotkey fallback: {Note}", note);
        foreach (var (action, status) in _hotkeys.Status)
            if (status != HotkeyRegistration.Registered)
                _logger.LogWarning("Hotkey {Action}: {Status}", action, status);
        if (substituted.Count > 0 && _settings.Current.System.ShowNotifications)
            _tray.Notify("Some shortcuts changed", string.Join(". ", substituted));
        return _hotkeys.Status;
    }

    public string? ActiveFallback(HotkeyAction action) => _activeFallbacks.GetValueOrDefault(action);

    private void OnHotkey(object? sender, HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.Search: ToggleSearch(); break;
            case HotkeyAction.Dashboard: ShowDashboard(); break;
            case HotkeyAction.WidgetPanel: ToggleWidgetPanel(); break;
            case HotkeyAction.PowerMenu: TogglePowerMenu(); break;
        }
    }

    private void OnTrayCommand(TrayCommand command)
    {
        switch (command)
        {
            case TrayCommand.Open: ShowDashboard(); break;
            case TrayCommand.Settings: ShowSettings(); break;
            case TrayCommand.ToggleTaskbar: _settings.Update(s => s.Taskbar.Visible = !s.Taskbar.Visible); break;
            case TrayCommand.RestartInterface: RestartInterface(); break;
            case TrayCommand.Exit: _ = ExitAsync(); break;
        }
    }

    private void ScheduleBarSync()
    {
        _displayDebounce.Stop();
        _displayDebounce.Start();
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => SyncBars();

    /// <summary>Creates, moves or removes bars so there is exactly one per wanted monitor.</summary>
    private void SyncBars()
    {
        if (_stopping) return;
        var settings = _settings.Current;
        var monitors = MonitorInfo.GetAll();
        var wanted = !settings.Taskbar.Visible
            ? []
            : settings.System.MultiMonitor == MultiMonitorMode.AllMonitors
                ? monitors.ToList()
                : monitors.Where(m => m.IsPrimary).ToList();

        foreach (var key in _bars.Keys.Where(k => wanted.All(m => !m.DeviceName.Equals(k, StringComparison.OrdinalIgnoreCase))).ToList())
        {
            _bars[key].Dispose();
            _bars.Remove(key);
        }

        foreach (var monitor in wanted)
        {
            if (_bars.TryGetValue(monitor.DeviceName, out var bar))
            {
                bar.SetMonitor(monitor.Handle);
                bar.Relayout();
            }
            else
            {
                _bars[monitor.DeviceName] = new TaskbarWindow(
                    monitor.Handle,
                    _services.GetRequiredService<TaskbarViewModel>(),
                    _settings,
                    _ui,
                    _logger);
            }
        }
    }

    private void OnShellRestarted()
    {
        _logger.LogInformation("Explorer restarted; registering bars again");
        foreach (var bar in _bars.Values) bar.Reregister();
        ScheduleBarSync();
    }

    private void OnResumed()
    {
        _logger.LogInformation("Resumed from sleep");
        ScheduleBarSync();
    }

    /// <summary>Signing out or shutting down: give the space back synchronously, Windows may not wait.</summary>
    private void OnSessionEnding()
    {
        foreach (var bar in _bars.Values) bar.Dispose();
        _bars.Clear();
        _settings.FlushAsync().GetAwaiter().GetResult();
    }

    public void RestartInterface()
    {
        _logger.LogInformation("Restarting interface");
        foreach (var bar in _bars.Values) bar.Dispose();
        _bars.Clear();
        CloseOverlays();
        SyncBars();
    }

    public async Task ExitAsync()
    {
        if (_stopping) return;
        await ((App)Microsoft.UI.Xaml.Application.Current).ShutdownAsync();
    }

    public async Task StopAsync()
    {
        if (_stopping) return;
        _stopping = true;
        _settings.Changed -= OnSettingsChanged;
        foreach (var bar in _bars.Values) bar.Dispose();
        _bars.Clear();
        _hotkeys.UnregisterAll();
        _tray.Dispose();
        CloseOverlays();
        await _settings.FlushAsync();
        _logger.LogInformation("Glyphbar stopped cleanly");
    }
}
