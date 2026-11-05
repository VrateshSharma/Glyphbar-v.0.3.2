using System.Globalization;
using Glyphbar.Core.Models;

namespace Glyphbar.Core.Services;

public readonly record struct ClockText(string Time, string? Meridiem);

public static class ClockFormatter
{
    /// <summary>Windows regional settings decide 12h/24h when the format is System.</summary>
    public static bool Uses24Hour(ClockFormat format, CultureInfo culture) => format switch
    {
        ClockFormat.TwentyFourHour => true,
        ClockFormat.TwelveHour => false,
        _ => culture.DateTimeFormat.ShortTimePattern.Contains('H'),
    };

    public static ClockText Format(DateTime time, ClockFormat format, bool seconds, CultureInfo culture)
    {
        bool h24 = Uses24Hour(format, culture);
        string pattern = (h24 ? "HH:mm" : "h:mm") + (seconds ? ":ss" : "");
        string text = time.ToString(pattern, CultureInfo.InvariantCulture);
        if (h24) return new ClockText(text, null);
        // Some cultures define no designators, so fall back to AM/PM.
        string meridiem = time.Hour < 12 ? culture.DateTimeFormat.AMDesignator : culture.DateTimeFormat.PMDesignator;
        if (string.IsNullOrEmpty(meridiem)) meridiem = time.Hour < 12 ? "AM" : "PM";
        return new ClockText(text, meridiem);
    }

    public static DateTimeOffset ToZone(DateTimeOffset now, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return now.ToLocalTime();
        try
        {
            return TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return now.ToLocalTime();
        }
    }
}
