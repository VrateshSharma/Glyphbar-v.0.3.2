using Glyphbar.Core.Models;

namespace Glyphbar.Core.Services;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>Physical-pixel layout of a bar on a monitor. Pure maths, so DPI edge cases are unit-tested.</summary>
public static class TaskbarGeometry
{
    public static int Scale(int dip, uint dpi) => (int)Math.Round(dip * dpi / 96d, MidpointRounding.AwayFromZero);

    /// <summary>The strip reserved through ABM_SETPOS; floating bars reserve their margins too.</summary>
    public static int ReservedThickness(TaskbarSettings s, uint dpi) =>
        Scale(s.Height + (s.Floating ? s.FloatingMargin * 2 : 0), dpi);

    public static PixelRect RequestedStrip(PixelRect monitor, TaskbarEdge edge, int thickness) => edge == TaskbarEdge.Bottom
        ? monitor with { Top = monitor.Bottom - thickness }
        : monitor with { Bottom = monitor.Top + thickness };

    /// <summary>Trims a shell-adjusted rectangle back to the bar's thickness, keeping the correct edge.</summary>
    public static PixelRect FitStrip(PixelRect adjusted, TaskbarEdge edge, int thickness) => edge == TaskbarEdge.Bottom
        ? adjusted with { Top = adjusted.Bottom - thickness }
        : adjusted with { Bottom = adjusted.Top + thickness };

    /// <summary>The window's rectangle inside the reserved strip.</summary>
    public static PixelRect WindowRect(PixelRect strip, TaskbarSettings s, uint dpi)
    {
        int margin = s.Floating ? Scale(s.FloatingMargin, dpi) : 0;
        int available = strip.Width - margin * 2;
        int width = Math.Clamp((int)Math.Round(available * Math.Clamp(s.WidthPercent, 20, 100) / 100d), 1, Math.Max(1, available));
        int left = s.Alignment switch
        {
            TaskbarAlignment.Left => strip.Left + margin,
            TaskbarAlignment.Right => strip.Right - margin - width,
            _ => strip.Left + (strip.Width - width) / 2,
        };
        return new PixelRect(left, strip.Top + margin, left + width, strip.Bottom - margin);
    }

    /// <summary>Where an auto-hidden bar parks, leaving a thin sliver on the screen edge for the pointer.</summary>
    public static PixelRect HiddenRect(PixelRect window, PixelRect monitor, TaskbarEdge edge, int sliver)
    {
        if (edge == TaskbarEdge.Bottom)
        {
            int top = monitor.Bottom - sliver;
            return window with { Top = top, Bottom = top + window.Height };
        }
        int bottom = monitor.Top + sliver;
        return window with { Top = bottom - window.Height, Bottom = bottom };
    }
}
