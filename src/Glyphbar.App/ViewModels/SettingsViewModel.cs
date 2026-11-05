using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glyphbar.App.Services;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Data.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Glyphbar.Platform.Interop;

namespace Glyphbar.App.ViewModels;

public sealed partial class WidgetRowViewModel(WidgetLayoutItem item, WidgetLayoutService layout, string name) : ObservableObject
{
    public string Name { get; } = name;
    public string SizeLabel => item.Size.ToString();
    public string EnabledLabel => $"Show {Name} widget";
    public string SizeButtonLabel => $"{Name} widget size";

    public bool IsEnabled
    {
        get => item.IsEnabled;
        set
        {
            if (value != item.IsEnabled) layout.SetEnabled(item.Kind, value);
        }
    }

    [RelayCommand]
    private void CycleSize()
    {
        layout.CycleSize(item.Kind);
        OnPropertyChanged(nameof(SizeLabel));
    }

    [RelayCommand] private void MoveUp() => layout.Shift(item.Kind, -1);
    [RelayCommand] private void MoveDown() => layout.Shift(item.Kind, 1);
}

public sealed partial class HotkeyRowViewModel(HotkeyAction action, string label, string gesture, IServiceProvider services) : ObservableObject
{
    public string Label { get; } = label;

    [ObservableProperty] public partial string Gesture { get; set; } = gesture;
    [ObservableProperty] public partial string Status { get; set; } = "";

    public void ShowStatus(HotkeyRegistration? registration) => Status = registration switch
    {
        HotkeyRegistration.Registered => "Active",
        HotkeyRegistration.Conflict => "Already in use by Glyphbar or another app. Choose a different shortcut.",
        HotkeyRegistration.Reserved => "Reserved by Windows. Choose a different shortcut.",
        HotkeyRegistration.Invalid => "Not a valid shortcut. Use a form like Alt+Space or Win+Shift+G.",
        _ => "",
    };

    [RelayCommand]
    private void Apply() => ShowStatus(services.GetRequiredService<ShellCoordinator>().ApplyHotkey(action, Gesture.Trim()));
}

/// <summary>Every property writes straight through to the store, so changes apply immediately.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IStartupManager _startup;
    private readonly IHotkeyService _hotkeys;
    private readonly WidgetLayoutService _layout;
    private readonly IAppLauncher _launcher;
    private readonly AppPaths _paths;
    private readonly IServiceProvider _services;
    private readonly List<TimeZoneInfo> _zones = TimeZoneInfo.GetSystemTimeZones().ToList();

    public SettingsViewModel(ISettingsStore store, IStartupManager startup, IHotkeyService hotkeys, WidgetLayoutService layout,
        IAppLauncher launcher, AppPaths paths, IServiceProvider services)
    {
        _store = store;
        _startup = startup;
        _hotkeys = hotkeys;
        _layout = layout;
        _launcher = launcher;
        _paths = paths;
        _services = services;
        TimeZones = ["Follow Windows", .. _zones.Select(z => z.DisplayName)];
        _layout.Changed += (_, _) => RebuildWidgets();
        BuildHotkeys();
    }

    private AppSettings S => _store.Current;
    private ShellCoordinator Shell => _services.GetRequiredService<ShellCoordinator>();
    public ISettingsStore Store => _store;

    public ObservableCollection<WidgetRowViewModel> Widgets { get; } = [];
    public ObservableCollection<HotkeyRowViewModel> Hotkeys { get; } = [];
    public IReadOnlyList<string> TimeZones { get; }
    public IReadOnlyList<string> Fonts { get; } = ["Segoe UI Variable Text", "Segoe UI", "Bahnschrift", "Consolas", "Cascadia Mono"];

    [ObservableProperty] public partial int SectionIndex { get; set; }

    public Visibility IsSection(int index, int current) => index == current ? Visibility.Visible : Visibility.Collapsed;

    private bool _applying;

    /// <summary>
    /// The guard stops a feedback loop. Raising PropertyChanged pushes the value into two-way bound controls, and a
    /// control that adjusts it, as a Slider does when snapping to its step, calls the setter again. Unguarded, that
    /// recursed until the stack overflowed.
    /// </summary>
    private void Write(Action<AppSettings> change, [CallerMemberName] string? name = null)
    {
        if (_applying) return;
        _applying = true;
        try
        {
            _store.Update(change);
            OnPropertyChanged(name);
        }
        finally
        {
            _applying = false;
        }
    }

    // Appearance
    public int BackdropIndex { get => (int)S.Appearance.Backdrop; set => Write(s => s.Appearance.Backdrop = (BackdropMode)Math.Max(value, 0)); }
    public int AnimationIndex { get => (int)S.Appearance.Animation; set => Write(s => s.Appearance.Animation = (AnimationIntensity)Math.Max(value, 0)); }
    public string FontFamily { get => S.Appearance.FontFamily; set => Write(s => s.Appearance.FontFamily = value ?? s.Appearance.FontFamily); }
    public bool MonochromeIcons { get => S.Appearance.MonochromeAppIcons; set => Write(s => s.Appearance.MonochromeAppIcons = value); }

    // Taskbar
    public bool TaskbarVisible { get => S.Taskbar.Visible; set => Write(s => s.Taskbar.Visible = value); }
    public int EdgeIndex { get => (int)S.Taskbar.Edge; set => Write(s => s.Taskbar.Edge = (TaskbarEdge)Math.Max(value, 0)); }
    public int AlignmentIndex { get => (int)S.Taskbar.Alignment; set => Write(s => s.Taskbar.Alignment = (TaskbarAlignment)Math.Max(value, 0)); }
    public bool Floating { get => S.Taskbar.Floating; set => Write(s => s.Taskbar.Floating = value); }
    public bool AutoHide { get => S.Taskbar.AutoHide; set => Write(s => s.Taskbar.AutoHide = value); }
    public double WidthPercent { get => S.Taskbar.WidthPercent; set => Write(s => s.Taskbar.WidthPercent = (int)value); }
    public double BarHeight { get => S.Taskbar.Height; set => Write(s => s.Taskbar.Height = (int)value); }
    public double IconSize { get => S.Taskbar.IconSize; set => Write(s => s.Taskbar.IconSize = (int)value); }
    public double CornerRadius { get => S.Taskbar.CornerRadius; set => Write(s => s.Taskbar.CornerRadius = (int)value); }
    public double OpacityPercent { get => Math.Round(S.Taskbar.Opacity * 100); set => Write(s => s.Taskbar.Opacity = value / 100); }
    public bool ShowIndicators { get => S.Taskbar.ShowRunningIndicators; set => Write(s => s.Taskbar.ShowRunningIndicators = value); }
    public bool ShowUnpinned { get => S.Taskbar.ShowRunningUnpinned; set => Write(s => s.Taskbar.ShowRunningUnpinned = value); }
    public bool ShowClock { get => S.Taskbar.ShowClock; set => Write(s => s.Taskbar.ShowClock = value); }
    public bool ShowDate { get => S.Taskbar.ShowDate; set => Write(s => s.Taskbar.ShowDate = value); }
    public bool ShowStatus { get => S.Taskbar.ShowSystemStatus; set => Write(s => s.Taskbar.ShowSystemStatus = value); }

    // Widgets
    public bool ShowOnDesktop { get => S.Widgets.ShowOnDesktop; set => Write(s => s.Widgets.ShowOnDesktop = value); }
    public int DesktopSideIndex { get => (int)S.Widgets.DesktopSide; set => Write(s => s.Widgets.DesktopSide = (DesktopWidgetSide)Math.Max(value, 0)); }
    public double DesktopBackground { get => S.Widgets.DesktopBackgroundPercent; set => Write(s => s.Widgets.DesktopBackgroundPercent = (int)value); }
    public int ClockFormatIndex { get => (int)S.Widgets.ClockFormat; set => Write(s => s.Widgets.ClockFormat = (ClockFormat)Math.Max(value, 0)); }
    public bool ShowSeconds { get => S.Widgets.ShowSeconds; set => Write(s => s.Widgets.ShowSeconds = value); }
    public bool DotMatrixClock { get => S.Widgets.DotMatrixClock; set => Write(s => s.Widgets.DotMatrixClock = value); }
    public bool ResourceMonitoring { get => S.Widgets.ResourceMonitoringEnabled; set => Write(s => s.Widgets.ResourceMonitoringEnabled = value); }
    public double BatteryRefresh { get => S.Widgets.BatteryRefreshSeconds; set => Write(s => s.Widgets.BatteryRefreshSeconds = (int)value); }
    public double StorageRefresh { get => S.Widgets.StorageRefreshSeconds; set => Write(s => s.Widgets.StorageRefreshSeconds = (int)value); }
    public double BatteryLow { get => S.Widgets.BatteryThresholds.Low; set => Write(s => s.Widgets.BatteryThresholds.Low = (int)value); }
    public double BatteryCritical { get => S.Widgets.BatteryThresholds.Critical; set => Write(s => s.Widgets.BatteryThresholds.Critical = (int)value); }

    public int TimeZoneIndex
    {
        get => S.Widgets.TimeZoneId is { } id ? _zones.FindIndex(z => z.Id == id) + 1 : 0;
        set => Write(s => s.Widgets.TimeZoneId = value <= 0 ? null : _zones[value - 1].Id);
    }

    // Search
    public bool SearchApps { get => S.Search.IncludeApplications; set => Write(s => s.Search.IncludeApplications = value); }
    public bool SearchFiles { get => S.Search.IncludeFiles; set => Write(s => s.Search.IncludeFiles = value); }
    public bool SearchFolders { get => S.Search.IncludeFolders; set => Write(s => s.Search.IncludeFolders = value); }
    public bool SearchSettings { get => S.Search.IncludeSettings; set => Write(s => s.Search.IncludeSettings = value); }
    public bool KeepHistory { get => S.Search.KeepHistory; set => Write(s => s.Search.KeepHistory = value); }
    public double LauncherIconSize { get => S.Search.LauncherIconSize; set => Write(s => s.Search.LauncherIconSize = (int)value); }
    public double LauncherSpacing { get => S.Search.LauncherSpacing; set => Write(s => s.Search.LauncherSpacing = (int)value); }

    public string IndexedLocations
    {
        get => string.Join(Environment.NewLine, S.Search.IndexedLocations);
        set => Write(s => s.Search.IndexedLocations = (value ?? "")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList());
    }

    [RelayCommand] private void ClearHistory() => _store.Update(s => s.Search.RecentQueries = []);
    [RelayCommand] private Task OpenWindowsTaskbarSettings() => _launcher.OpenAsync("ms-settings:taskbar");
    [RelayCommand] private Task OpenIndexing() => _launcher.OpenAsync("ms-settings:cortana-windowssearch");

    // System
    public bool Notifications { get => S.System.ShowNotifications; set => Write(s => s.System.ShowNotifications = value); }
    public int MultiMonitorIndex { get => (int)S.System.MultiMonitor; set => Write(s => s.System.MultiMonitor = (MultiMonitorMode)Math.Max(value, 0)); }
    public bool ConfirmSignOut { get => S.System.ConfirmSignOut; set => Write(s => s.System.ConfirmSignOut = value); }
    public bool ConfirmSleep { get => S.System.ConfirmSleep; set => Write(s => s.System.ConfirmSleep = value); }
    public double PowerCountdown { get => S.System.PowerCountdownSeconds; set => Write(s => s.System.PowerCountdownSeconds = (int)value); }

    [ObservableProperty] public partial bool LaunchAtStartup { get; set; }

    private bool _loadingStartup;

    partial void OnLaunchAtStartupChanged(bool value)
    {
        if (_loadingStartup) return;
        _ = ApplyStartupAsync(value);
    }

    private async Task ApplyStartupAsync(bool enabled)
    {
        bool actual = await _startup.SetEnabledAsync(enabled);
        _store.Update(s => s.System.LaunchAtStartup = actual);
        if (actual != enabled) SetStartupQuietly(actual);
    }

    private void SetStartupQuietly(bool value)
    {
        _loadingStartup = true;
        LaunchAtStartup = value;
        _loadingStartup = false;
    }

    public async Task LoadAsync()
    {
        await _layout.EnsureLoadedAsync();
        RebuildWidgets();
        SetStartupQuietly(await _startup.IsEnabledAsync());
    }

    private void RebuildWidgets()
    {
        Widgets.Clear();
        foreach (var w in _layout.Items) Widgets.Add(new WidgetRowViewModel(w, _layout, Label(w.Kind)));
    }

    private static string Label(WidgetKind kind) => kind == WidgetKind.SystemInfo ? "System info" : kind.ToString();

    private void BuildHotkeys()
    {
        Hotkeys.Clear();
        var h = S.Hotkeys;
        foreach (var (action, label, gesture) in new[]
                 {
                     (HotkeyAction.Search, "Open search", h.Search),
                     (HotkeyAction.Dashboard, "Open dashboard", h.Dashboard),
                     (HotkeyAction.WidgetPanel, "Open widget panel", h.WidgetPanel),
                     (HotkeyAction.PowerMenu, "Open power menu", h.PowerMenu),
                 })
        {
            var row = new HotkeyRowViewModel(action, label, gesture, _services);
            row.ShowStatus(_hotkeys.Status.TryGetValue(action, out var status) ? status : null);
            if (Shell.ActiveFallback(action) is { } fallback)
                row.Status = $"{gesture} is used by another app, so {fallback} is active instead.";
            Hotkeys.Add(row);
        }
    }

    // About
    public string VersionText => $"Glyphbar {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
    public string BuildText => $"{(Native.IsPackaged() ? "Packaged (MSIX)" : "Unpackaged")} · .NET {Environment.Version} · Data in {_paths.Root}";

    [RelayCommand] private void RestartInterface() => Shell.RestartInterface();
    [RelayCommand] private Task OpenLogs() => _launcher.OpenAsync(_paths.Logs);

    public void ResetAll()
    {
        _store.ResetToDefaults();
        _layout.Reset();
        Shell.RegisterHotkeys();
        BuildHotkeys();

        // Refreshing every binding must not write each control's value back.
        _applying = true;
        try { OnPropertyChanged(string.Empty); }
        finally { _applying = false; }
    }
}
