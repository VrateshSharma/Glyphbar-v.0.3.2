using System.Globalization;
using Glyphbar.Core.Helpers;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;

namespace Glyphbar.Core.Tests;

public class CalendarAndClockTests
{
    [Fact]
    public void Month_grid_is_always_six_full_weeks()
    {
        for (int m = 1; m <= 12; m++)
            Assert.Equal(42, MonthGrid.Build(2026, m, DayOfWeek.Monday).Count);
    }

    [Fact]
    public void Month_grid_starts_on_requested_weekday_and_contains_the_month()
    {
        var cells = MonthGrid.Build(2026, 11, DayOfWeek.Monday);
        Assert.Equal(DayOfWeek.Monday, cells[0].Date.DayOfWeek);
        Assert.Equal(30, cells.Count(c => c.InMonth));
        Assert.Contains(cells, c => c.Date == new DateOnly(2026, 11, 1) && c.InMonth);
    }

    [Fact]
    public void Month_grid_handles_leap_february()
    {
        Assert.Equal(29, MonthGrid.Build(2028, 2, DayOfWeek.Sunday).Count(c => c.InMonth));
        Assert.Equal(28, MonthGrid.Build(2027, 2, DayOfWeek.Sunday).Count(c => c.InMonth));
    }

    [Fact]
    public void Month_grid_crosses_year_boundary()
    {
        var cells = MonthGrid.Build(2027, 1, DayOfWeek.Sunday);
        Assert.Contains(cells, c => c.Date.Year == 2026 && !c.InMonth);
    }

    [Fact]
    public void Weekday_labels_rotate_with_first_day()
    {
        var labels = MonthGrid.WeekdayLabels(DayOfWeek.Monday, CultureInfo.InvariantCulture);
        Assert.Equal("Mon", labels[0]);
        Assert.Equal("Sun", labels[6]);
    }

    [Fact]
    public void Clock_follows_culture_when_set_to_system()
    {
        Assert.True(ClockFormatter.Uses24Hour(ClockFormat.System, new CultureInfo("de-DE")));
        Assert.False(ClockFormatter.Uses24Hour(ClockFormat.System, new CultureInfo("en-US")));
    }

    [Theory]
    [InlineData(ClockFormat.TwentyFourHour, false, "14:05", null)]
    [InlineData(ClockFormat.TwentyFourHour, true, "14:05:09", null)]
    [InlineData(ClockFormat.TwelveHour, false, "2:05", "PM")]
    public void Clock_formats(ClockFormat format, bool seconds, string time, string? meridiem)
    {
        var text = ClockFormatter.Format(new DateTime(2026, 11, 5, 14, 5, 9), format, seconds, new CultureInfo("en-US"));
        Assert.Equal(time, text.Time);
        Assert.Equal(meridiem, text.Meridiem);
    }

    [Fact]
    public void Midnight_in_twelve_hour_time_is_twelve_am()
    {
        var text = ClockFormatter.Format(new DateTime(2026, 11, 5, 0, 30, 0), ClockFormat.TwelveHour, false, new CultureInfo("en-US"));
        Assert.Equal("12:30", text.Time);
        Assert.Equal("AM", text.Meridiem);
    }

    [Fact]
    public void Unknown_time_zone_falls_back_to_local_instead_of_throwing()
    {
        var now = DateTimeOffset.Now;
        Assert.Equal(now.ToLocalTime(), ClockFormatter.ToZone(now, "Not/AZone"));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(500107862016, "466 GB")]
    public void Byte_sizes_format_readably(long bytes, string expected) =>
        Assert.Equal(expected, ByteSize.Format(bytes, CultureInfo.InvariantCulture));
}
