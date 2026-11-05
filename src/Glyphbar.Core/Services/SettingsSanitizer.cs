using Glyphbar.Core.Models;

namespace Glyphbar.Core.Services;

/// <summary>Clamps values from a hand-edited or older settings file so they cannot break the bar.</summary>
public static class SettingsSanitizer
{
    public static AppSettings Sanitize(AppSettings s)
    {
        s.Appearance ??= new();
        s.Taskbar ??= new();
        s.Widgets ??= new();
        s.Search ??= new();
        s.System ??= new();
        s.Hotkeys ??= new();
        s.Widgets.BatteryThresholds ??= new();
        s.Search.RecentQueries ??= [];
        s.Search.IndexedLocations ??= [];

        var t = s.Taskbar;
        t.Height = Math.Clamp(t.Height, 32, 96);
        t.IconSize = Math.Clamp(t.IconSize, 16, Math.Max(16, t.Height - 8));
        t.CornerRadius = Math.Clamp(t.CornerRadius, 0, t.Height / 2);
        t.FloatingMargin = Math.Clamp(t.FloatingMargin, 0, 32);
        t.WidthPercent = Math.Clamp(t.WidthPercent, 20, 100);
        t.Opacity = double.IsNaN(t.Opacity) ? 0.92 : Math.Clamp(t.Opacity, 0.3, 1);

        var w = s.Widgets;
        w.BatteryRefreshSeconds = Math.Clamp(w.BatteryRefreshSeconds, 10, 300);
        w.StorageRefreshSeconds = Math.Clamp(w.StorageRefreshSeconds, 30, 600);
        w.SystemInfoRefreshSeconds = Math.Clamp(w.SystemInfoRefreshSeconds, 2, 60);

        w.DesktopBackgroundPercent = Math.Clamp(w.DesktopBackgroundPercent, 0, 100);

        var b = w.BatteryThresholds;
        b.Critical = Math.Clamp(b.Critical, 1, 98);
        b.Low = Math.Clamp(b.Low, b.Critical + 1, 99);
        b.Moderate = Math.Clamp(b.Moderate, b.Low + 1, 100);

        s.Search.MaxHistory = Math.Clamp(s.Search.MaxHistory, 0, 50);
        s.Search.LauncherIconSize = Math.Clamp(s.Search.LauncherIconSize, 24, 96);
        s.Search.LauncherSpacing = Math.Clamp(s.Search.LauncherSpacing, 0, 48);
        s.System.PowerCountdownSeconds = Math.Clamp(s.System.PowerCountdownSeconds, 0, 60);
        return s;
    }
}
