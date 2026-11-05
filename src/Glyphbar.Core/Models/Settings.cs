namespace Glyphbar.Core.Models;

public enum TaskbarEdge { Bottom, Top }
public enum TaskbarAlignment { Left, Center, Right }
public enum MultiMonitorMode { PrimaryOnly, AllMonitors }
public enum ClockFormat { System, TwelveHour, TwentyFourHour }
public enum AnimationIntensity { Off, Subtle, Full }
public enum BackdropMode { Opaque, Acrylic }

/// <summary>Root of the JSON settings document. Plain mutable POCOs keep System.Text.Json simple.</summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public AppearanceSettings Appearance { get; set; } = new();
    public TaskbarSettings Taskbar { get; set; } = new();
    public WidgetSettings Widgets { get; set; } = new();
    public SearchSettings Search { get; set; } = new();
    public SystemSettings System { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
}

public sealed class AppearanceSettings
{
    public BackdropMode Backdrop { get; set; } = BackdropMode.Acrylic;
    public AnimationIntensity Animation { get; set; } = AnimationIntensity.Subtle;
    public bool MonochromeAppIcons { get; set; } = true;
    public string FontFamily { get; set; } = "Segoe UI Variable Text";
    public bool DotMatrixHeadings { get; set; } = true;
}

public sealed class TaskbarSettings
{
    public bool Visible { get; set; } = true;
    public TaskbarEdge Edge { get; set; } = TaskbarEdge.Bottom;
    public TaskbarAlignment Alignment { get; set; } = TaskbarAlignment.Center;
    public bool Floating { get; set; } = true;
    public int WidthPercent { get; set; } = 100;
    public int Height { get; set; } = 52;
    public int FloatingMargin { get; set; } = 8;
    public int IconSize { get; set; } = 28;
    public int CornerRadius { get; set; } = 18;
    public double Opacity { get; set; } = 0.92;
    public bool AutoHide { get; set; }
    public bool ShowRunningIndicators { get; set; } = true;
    public bool ShowRunningUnpinned { get; set; } = true;
    public bool ShowClock { get; set; } = true;
    public bool ShowDate { get; set; } = true;
    public bool ShowSystemStatus { get; set; } = true;
}

public sealed class WidgetSettings
{
    public ClockFormat ClockFormat { get; set; } = ClockFormat.System;
    public bool ShowSeconds { get; set; } = true;
    public bool DotMatrixClock { get; set; } = true;
    /// <summary>Windows time-zone id; null follows the system zone.</summary>
    public string? TimeZoneId { get; set; }
    public int BatteryRefreshSeconds { get; set; } = 30;
    public int StorageRefreshSeconds { get; set; } = 60;
    public int SystemInfoRefreshSeconds { get; set; } = 5;
    public bool ResourceMonitoringEnabled { get; set; } = true;
    public BatteryThresholds BatteryThresholds { get; set; } = new();
}

public sealed class BatteryThresholds
{
    public int Moderate { get; set; } = 50;
    public int Low { get; set; } = 20;
    public int Critical { get; set; } = 10;
}

public sealed class SearchSettings
{
    public bool IncludeApplications { get; set; } = true;
    public bool IncludeFiles { get; set; } = true;
    public bool IncludeFolders { get; set; } = true;
    public bool IncludeSettings { get; set; } = true;
    public bool KeepHistory { get; set; } = true;
    public int MaxHistory { get; set; } = 12;
    public List<string> RecentQueries { get; set; } = [];
    /// <summary>Limits file search to these roots; empty means everything Windows has indexed.</summary>
    public List<string> IndexedLocations { get; set; } = [];
    public int LauncherIconSize { get; set; } = 44;
    public int LauncherSpacing { get; set; } = 12;
}

public sealed class SystemSettings
{
    public bool LaunchAtStartup { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public MultiMonitorMode MultiMonitor { get; set; } = MultiMonitorMode.PrimaryOnly;
    public bool ConfirmSignOut { get; set; } = true;
    public bool ConfirmSleep { get; set; }
    /// <summary>Optional delay after confirming; 0 disables the countdown.</summary>
    public int PowerCountdownSeconds { get; set; }
    public bool FirstRunGuideShown { get; set; }
}

public sealed class HotkeySettings
{
    public string Search { get; set; } = "Alt+Space";
    public string Dashboard { get; set; } = "Win+Shift+G";
    public string WidgetPanel { get; set; } = "Win+Shift+W";
    public string PowerMenu { get; set; } = "Win+Shift+P";
}
