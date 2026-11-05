using Glyphbar.Core.Helpers;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;

namespace Glyphbar.Core.Tests;

public class TaskbarAndDockTests
{
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);

    [Theory]
    [InlineData(96u, 52, 52)]
    [InlineData(144u, 52, 78)]
    [InlineData(192u, 52, 104)]
    public void Scale_converts_dips_to_pixels(uint dpi, int dip, int expected) =>
        Assert.Equal(expected, TaskbarGeometry.Scale(dip, dpi));

    [Fact]
    public void Floating_bar_reserves_its_margins_and_is_inset()
    {
        var s = new TaskbarSettings { Height = 52, FloatingMargin = 8, Floating = true };
        int thickness = TaskbarGeometry.ReservedThickness(s, 96);
        Assert.Equal(68, thickness);
        var strip = TaskbarGeometry.RequestedStrip(Monitor, TaskbarEdge.Bottom, thickness);
        Assert.Equal(new PixelRect(0, 1012, 1920, 1080), strip);
        Assert.Equal(new PixelRect(8, 1020, 1912, 1072), TaskbarGeometry.WindowRect(strip, s, 96));
    }

    [Fact]
    public void Docked_bar_fills_the_strip()
    {
        var s = new TaskbarSettings { Height = 48, Floating = false };
        var strip = TaskbarGeometry.RequestedStrip(Monitor, TaskbarEdge.Top, TaskbarGeometry.ReservedThickness(s, 96));
        Assert.Equal(new PixelRect(0, 0, 1920, 48), TaskbarGeometry.WindowRect(strip, s, 96));
    }

    [Theory]
    [InlineData(TaskbarAlignment.Left, 0, 960)]
    [InlineData(TaskbarAlignment.Center, 480, 1440)]
    [InlineData(TaskbarAlignment.Right, 960, 1920)]
    public void Partial_width_bar_aligns(TaskbarAlignment alignment, int left, int right)
    {
        var s = new TaskbarSettings { Floating = false, WidthPercent = 50, Alignment = alignment };
        var r = TaskbarGeometry.WindowRect(new PixelRect(0, 1000, 1920, 1080), s, 96);
        Assert.Equal((left, right), (r.Left, r.Right));
    }

    [Fact]
    public void Shell_adjusted_rectangle_keeps_the_bar_above_the_windows_taskbar()
    {
        // ABM_QUERYPOS moved the bottom up by 48px for a visible Windows taskbar.
        var fitted = TaskbarGeometry.FitStrip(new PixelRect(0, 1012, 1920, 1032), TaskbarEdge.Bottom, 68);
        Assert.Equal(new PixelRect(0, 964, 1920, 1032), fitted);
    }

    [Fact]
    public void Works_on_a_secondary_monitor_with_negative_coordinates()
    {
        var left = new PixelRect(-1920, 0, 0, 1080);
        var s = new TaskbarSettings { Floating = false, Height = 40 };
        var r = TaskbarGeometry.WindowRect(TaskbarGeometry.RequestedStrip(left, TaskbarEdge.Bottom, 40), s, 96);
        Assert.Equal(new PixelRect(-1920, 1040, 0, 1080), r);
    }

    [Theory]
    [InlineData(TaskbarEdge.Bottom)]
    [InlineData(TaskbarEdge.Top)]
    public void Hidden_bar_leaves_a_sliver_for_the_pointer(TaskbarEdge edge)
    {
        var window = new PixelRect(8, 1020, 1912, 1072);
        var hidden = TaskbarGeometry.HiddenRect(window, Monitor, edge, 2);
        Assert.Equal(window.Height, hidden.Height);
        if (edge == TaskbarEdge.Bottom) Assert.Equal(1078, hidden.Top);
        else Assert.Equal(2, hidden.Bottom);
    }

    private static RunningWindow Win(nint h, string? exe, string? aumid = null, string title = "t") =>
        new(h, title, exe, aumid, false, false);

    private static PinnedApp Pin(string id, string target, string? exe, int order = 0) =>
        new() { AppId = id, Name = id, LaunchTarget = target, ExecutablePath = exe, SortOrder = order };

    [Fact]
    public void Running_windows_attach_to_their_pin_by_path_case_insensitively()
    {
        var slots = DockComposer.Compose([Pin("a", "x", @"C:\A\a.exe")], [Win(1, @"c:\a\A.EXE")], [], true);
        Assert.Single(slots);
        Assert.True(slots[0].IsPinned);
        Assert.Single(slots[0].Windows);
    }

    [Fact]
    public void Running_windows_attach_by_aumid_exactly()
    {
        var pinned = Pin("calc", @"shell:AppsFolder\Microsoft.Calc!App", null);
        Assert.Single(DockComposer.Compose([pinned], [Win(1, null, "Microsoft.Calc!App")], [], true));
        // "App" is a suffix of the pin's id, but it is a different application.
        Assert.Equal(2, DockComposer.Compose([pinned], [Win(1, null, "App")], [], true).Count);
    }

    [Fact]
    public void Pins_keep_order_and_unpinned_running_apps_follow()
    {
        var slots = DockComposer.Compose([Pin("b", "b", null, 1), Pin("a", "a", null, 0)], [Win(1, @"C:\z.exe")], [], true);
        Assert.Equal(["a", "b"], slots.Take(2).Select(s => s.AppId));
        Assert.False(slots[2].IsPinned);
    }

    [Fact]
    public void Windows_of_one_unpinned_app_group_together()
    {
        var slots = DockComposer.Compose([], [Win(1, @"C:\z.exe"), Win(2, @"C:\Z.EXE")], [], true);
        Assert.Single(slots);
        Assert.Equal(2, slots[0].Windows.Count);
    }

    [Fact]
    public void Unpinned_running_apps_can_be_hidden()
    {
        Assert.Empty(DockComposer.Compose([], [Win(1, @"C:\z.exe")], [], false));
    }

    [Fact]
    public void Sanitizer_clamps_hostile_values()
    {
        var s = new AppSettings();
        s.Taskbar.Height = 5000;
        s.Taskbar.Opacity = double.NaN;
        s.Taskbar.CornerRadius = -3;
        s.Widgets.BatteryThresholds = new BatteryThresholds { Critical = 90, Low = 10, Moderate = 5 };
        SettingsSanitizer.Sanitize(s);
        Assert.Equal(96, s.Taskbar.Height);
        Assert.Equal(0.92, s.Taskbar.Opacity);
        Assert.Equal(0, s.Taskbar.CornerRadius);
        var b = s.Widgets.BatteryThresholds;
        Assert.True(b.Critical < b.Low && b.Low < b.Moderate);
    }

    [Fact]
    public void Desktop_widgets_default_to_on_and_fully_transparent()
    {
        var w = new WidgetSettings();
        Assert.True(w.ShowOnDesktop);
        Assert.Equal(0, w.DesktopBackgroundPercent);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(40, 40)]
    [InlineData(500, 100)]
    public void Desktop_background_is_clamped(int input, int expected)
    {
        var s = new AppSettings();
        s.Widgets.DesktopBackgroundPercent = input;
        Assert.Equal(expected, SettingsSanitizer.Sanitize(s).Widgets.DesktopBackgroundPercent);
    }

    [Fact]
    public void Dot_font_covers_everything_the_clock_can_show()
    {
        foreach (char c in "0123456789:%AMPGLYPHBARSETTINGSPOWER ")
            Assert.True(DotMatrixFont.Supports(c), $"Missing glyph {c}");
    }

    [Fact]
    public void Dot_layout_is_stable_and_within_bounds()
    {
        var (dots, columns) = DotMatrixFont.Layout("12:34");
        Assert.True(columns > 0);
        Assert.All(dots, d =>
        {
            Assert.InRange(d.X, 0, columns);
            Assert.InRange(d.Y, 0, DotMatrixFont.Height - 1);
        });
        // A colon has four dots, whichever narrow columns it uses.
        Assert.Equal(4, DotMatrixFont.Layout(":").Dots.Count);
    }

    [Fact]
    public void Monochrome_keeps_alpha_and_lifts_black()
    {
        byte[] px = [0, 0, 0, 128];
        IconImage.ToMonochrome(px);
        Assert.Equal(128, px[3]);
        Assert.True(px[0] >= 90 && px[0] == px[1] && px[1] == px[2]);
    }

    [Fact]
    public void Icons_with_no_alpha_become_opaque()
    {
        byte[] px = [10, 20, 30, 0, 40, 50, 60, 0];
        IconImage.FixMissingAlpha(px);
        Assert.Equal(255, px[3]);
        Assert.Equal(255, px[7]);
    }

    [Fact]
    public void Unpremultiply_restores_colour()
    {
        byte[] px = [64, 64, 64, 128];
        IconImage.Unpremultiply(px);
        Assert.InRange(px[0], 126, 129);
    }

    [Fact]
    public void Png_has_valid_signature_and_chunks()
    {
        var png = IconImage.EncodePng(new byte[2 * 2 * 4], 2, 2);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        Assert.Equal("IHDR"u8.ToArray(), png[12..16]);
        Assert.Equal("IEND"u8.ToArray(), png[^8..^4]);
    }
}
