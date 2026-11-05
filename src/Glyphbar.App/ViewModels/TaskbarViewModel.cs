using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glyphbar.App.Services;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.NetworkInformation;
using Windows.UI;

namespace Glyphbar.App.ViewModels;

public sealed partial class TaskbarViewModel : ObservableObject, IDisposable
{
    private const int IconRequestSize = 64;

    private readonly ISettingsStore _settings;
    private readonly IRunningWindows _running;
    private readonly IAppCatalog _catalog;
    private readonly IAppLauncher _launcher;
    private readonly PinService _pins;
    private readonly IconLoader _icons;
    private readonly ClockService _clock;
    private readonly SystemMonitor _monitor;
    private readonly UiDispatcher _ui;
    private readonly IServiceProvider _services;

    public TaskbarViewModel(
        ISettingsStore settings, IRunningWindows running, IAppCatalog catalog, IAppLauncher launcher,
        PinService pins, IconLoader icons, ClockService clock, SystemMonitor monitor, UiDispatcher ui, IServiceProvider services)
    {
        _settings = settings;
        _running = running;
        _catalog = catalog;
        _launcher = launcher;
        _pins = pins;
        _icons = icons;
        _clock = clock;
        _monitor = monitor;
        _ui = ui;
        _services = services;

        _settings.Changed += OnSettingsChanged;
        _running.Changed += OnDockInputsChanged;
        _catalog.Changed += OnDockInputsChanged;
        _pins.Changed += OnDockInputsChanged;
        _clock.Tick += OnClockTick;
        _monitor.BatteryChanged += OnBatteryChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;

        ApplySettings();
        UpdateClock();
        UpdateBattery();
        UpdateNetwork();
        RebuildDock();
    }

    private ShellCoordinator Shell => _services.GetRequiredService<ShellCoordinator>();

    public ObservableCollection<DockItemViewModel> DockItems { get; } = [];

    /// <summary>
    /// Asked of the menu itself. A Closed event can't be relied on, because unpinning removes the very item the
    /// menu is anchored to, and a stale flag would stop auto-hide from ever hiding the bar again.
    /// </summary>
    public bool IsMenuOpen => _openMenu is { IsOpen: true };
    public void TrackMenu(MenuFlyout menu) => _openMenu = menu;

    private MenuFlyout? _openMenu;

    [ObservableProperty] public partial Brush? BarBrush { get; set; }
    [ObservableProperty] public partial CornerRadius BarCorner { get; set; }
    [ObservableProperty] public partial HorizontalAlignment DockAlignment { get; set; }
    [ObservableProperty] public partial double ButtonSize { get; set; }
    [ObservableProperty] public partial Visibility ClockVisibility { get; set; }
    [ObservableProperty] public partial Visibility DateVisibility { get; set; }
    [ObservableProperty] public partial Visibility StatusVisibility { get; set; }
    [ObservableProperty] public partial string TimeText { get; set; } = "";
    [ObservableProperty] public partial string DateText { get; set; } = "";
    [ObservableProperty] public partial string BatteryText { get; set; } = "";
    [ObservableProperty] public partial string BatteryTooltip { get; set; } = "";
    [ObservableProperty] public partial double BatteryPercent { get; set; }
    [ObservableProperty] public partial bool BatteryCharging { get; set; }
    [ObservableProperty] public partial bool BatteryWarning { get; set; }
    [ObservableProperty] public partial Visibility BatteryVisibility { get; set; } = Visibility.Collapsed;
    [ObservableProperty] public partial string NetworkGlyph { get; set; } = "";

    [RelayCommand] private void OpenLauncher() => Shell.ToggleLauncher();
    [RelayCommand] private void OpenSearch() => Shell.ToggleSearch();
    [RelayCommand] private void OpenWidgets() => Shell.ToggleWidgetPanel();
    [RelayCommand] private void OpenPower() => Shell.TogglePowerMenu();

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        ApplySettings();
        UpdateClock();
        UpdateBattery();
        RebuildDock();
    }

    private void ApplySettings()
    {
        var t = _settings.Current.Taskbar;
        byte alpha = (byte)Math.Round(t.Opacity * 255);
        BarBrush = new SolidColorBrush(Color.FromArgb(alpha, 0x08, 0x08, 0x08));
        BarCorner = new CornerRadius(t.Floating ? t.CornerRadius : 0);
        DockAlignment = t.Alignment switch
        {
            TaskbarAlignment.Left => HorizontalAlignment.Left,
            TaskbarAlignment.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center,
        };
        ButtonSize = Math.Min(t.Height - 8, 40);
        ClockVisibility = t.ShowClock ? Visibility.Visible : Visibility.Collapsed;
        DateVisibility = t.ShowDate ? Visibility.Visible : Visibility.Collapsed;
        StatusVisibility = t.ShowSystemStatus ? Visibility.Visible : Visibility.Collapsed;
        foreach (var item in DockItems) ApplyItemSize(item);
    }

    private void ApplyItemSize(DockItemViewModel item)
    {
        var t = _settings.Current.Taskbar;
        item.IconSize = t.IconSize;
        item.SlotSize = Math.Min(t.Height - 6, t.IconSize + 12);
        item.ShowIndicator = t.ShowRunningIndicators;
    }

    private void OnDockInputsChanged(object? sender, EventArgs e) => _ui.Post(RebuildDock);

    /// <summary>Updates the collection in place, so running-state changes don't flicker the icons.</summary>
    private void RebuildDock()
    {
        var slots = DockComposer.Compose(_pins.Pins, _running.Windows, _catalog.Apps, _settings.Current.Taskbar.ShowRunningUnpinned);

        for (int i = DockItems.Count - 1; i >= 0; i--)
            if (slots.All(s => s.AppId != DockItems[i].AppId))
                DockItems.RemoveAt(i);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            int existing = -1;
            for (int j = i; j < DockItems.Count; j++)
                if (DockItems[j].AppId == slot.AppId) { existing = j; break; }

            if (existing < 0)
            {
                var item = new DockItemViewModel(slot);
                ApplyItemSize(item);
                DockItems.Insert(i, item);
                _ = LoadIconAsync(item);
            }
            else
            {
                if (existing != i) DockItems.Move(existing, i);
                var item = DockItems[i];
                bool iconChanged = item.Slot.CustomIconPath != slot.CustomIconPath;
                item.Slot = slot;
                if (iconChanged || item.Icon is null) _ = LoadIconAsync(item);
            }
        }
    }

    private async Task LoadIconAsync(DockItemViewModel item)
    {
        var s = item.Slot;
        item.Icon = await _icons.LoadAsync(s.ExecutablePath is { } exe && !s.LaunchTarget.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ? exe : s.LaunchTarget, IconRequestSize, s.CustomIconPath);
    }

    /// <summary>Click: launch; focus the window; minimise if it's already active; cycle if there are several.</summary>
    public async Task ActivateAsync(DockItemViewModel item)
    {
        var windows = item.Slot.Windows;
        if (windows.Count == 0)
        {
            await _pins.LaunchAsync(item.AppId, item.Slot.LaunchTarget);
            return;
        }

        int active = windows.ToList().FindIndex(w => w.IsForeground);
        if (windows.Count == 1 && active == 0) _running.Minimize(windows[0].Handle);
        else _running.Activate(windows[(active + 1) % windows.Count].Handle);
    }

    public Task SaveOrderAsync() =>
        _pins.ReorderAsync(DockItems.Where(d => d.Slot.IsPinned).Select(d => d.AppId).ToList());

    public MenuFlyout BuildContextMenu(DockItemViewModel item, FrameworkElement host)
    {
        var s = item.Slot;
        var menu = new MenuFlyout();
        void Add(string text, string glyph, Action action) =>
            menu.Items.Add(CreateItem(text, glyph, action));

        Add("Open", "", () => _ = _pins.LaunchAsync(item.AppId, s.LaunchTarget));
        if (s.ExecutablePath is not null)
            Add("Open as administrator", "", () => _ = _pins.LaunchAsync(item.AppId, s.ExecutablePath, admin: true));

        menu.Items.Add(new MenuFlyoutSeparator());
        if (s.IsPinned)
        {
            Add("Unpin", "", () => _ = _pins.UnpinAsync(item.AppId));
            Add("Change icon…", "", () => _ = ChangeIconAsync(item));
            if (s.CustomIconPath is not null)
                Add("Reset icon", "", () => _ = _pins.SetIconAsync(item.AppId, null));
        }
        else if (s.LaunchTarget.Length > 0)
        {
            Add("Pin", "", () => _ = _pins.PinAsync(item.AppId, s.Name, s.LaunchTarget, s.ExecutablePath));
        }

        if (_catalog.Find(item.AppId) is { IsUserAdded: true })
            Add("Remove shortcut", "", () => _ = RemoveShortcutAsync(item));

        if (s.ExecutablePath is not null)
            Add("Open file location", "", () => _ = _launcher.RevealInExplorerAsync(s.ExecutablePath));

        if (s.Windows.Count > 0)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            Add(s.Windows.Count == 1 ? "Close window" : "Close all windows", "", () =>
            {
                foreach (var w in s.Windows) _running.Close(w.Handle);
            });
        }
        return menu;
    }

    private static MenuFlyoutItem CreateItem(string text, string glyph, Action action)
    {
        var item = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph } };
        item.Click += (_, _) => action();
        return item;
    }

    private async Task RemoveShortcutAsync(DockItemViewModel item)
    {
        await _pins.UnpinAsync(item.AppId);
        await _catalog.RemoveUserAppAsync(item.AppId);
    }

    private Task ChangeIconAsync(DockItemViewModel item) => Shell.PickIconAsync(item.AppId);

    private void OnClockTick(object? sender, EventArgs e) => UpdateClock();

    private void UpdateClock()
    {
        var w = _settings.Current.Widgets;
        var now = ClockFormatter.ToZone(_clock.Now, w.TimeZoneId).DateTime;
        var culture = CultureInfo.CurrentCulture;
        var text = ClockFormatter.Format(now, w.ClockFormat, false, culture);
        TimeText = text.Meridiem is null ? text.Time : $"{text.Time} {text.Meridiem}";
        DateText = now.ToString("ddd d MMM", culture);
    }

    private void OnBatteryChanged(object? sender, EventArgs e) => _ui.Post(UpdateBattery);

    private void UpdateBattery()
    {
        var b = _monitor.Battery;
        if (!b.HasBattery || b.Percent is not { } percent)
        {
            BatteryVisibility = Visibility.Collapsed;
            BatteryTooltip = b.OnAcPower == true ? "On AC power" : "Power status";
            return;
        }

        var level = BatteryInterpreter.Classify(percent, _settings.Current.Widgets.BatteryThresholds);
        BatteryVisibility = Visibility.Visible;
        BatteryPercent = percent;
        BatteryCharging = b.IsCharging;
        BatteryWarning = !b.IsCharging && level is BatteryLevel.Low or BatteryLevel.Critical;
        BatteryText = $"{percent}%";
        BatteryTooltip = BatteryFormat.Describe(b);
    }

    private void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e) => _ui.Post(UpdateNetwork);

    private void UpdateNetwork() =>
        NetworkGlyph = NetworkInterface.GetIsNetworkAvailable() ? "" : "";

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _running.Changed -= OnDockInputsChanged;
        _catalog.Changed -= OnDockInputsChanged;
        _pins.Changed -= OnDockInputsChanged;
        _clock.Tick -= OnClockTick;
        _monitor.BatteryChanged -= OnBatteryChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
    }
}
