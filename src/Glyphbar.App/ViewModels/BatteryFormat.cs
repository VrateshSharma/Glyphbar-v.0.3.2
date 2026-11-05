using Glyphbar.Core.Models;

namespace Glyphbar.App.ViewModels;

public static class BatteryFormat
{
    public static string Describe(BatterySnapshot b)
    {
        var parts = new List<string>();
        if (b.Percent is { } p) parts.Add($"{p}%");
        if (b.IsCharging) parts.Add("Charging");
        else if (b.OnAcPower == true) parts.Add("Plugged in");
        else if (b.OnAcPower == false) parts.Add("On battery");
        if (b.Remaining is { } r) parts.Add(Remaining(r));
        if (b.BatterySaverOn) parts.Add("Battery saver on");
        return string.Join(" · ", parts);
    }

    public static string Remaining(TimeSpan r) =>
        r.TotalHours >= 1 ? $"{(int)r.TotalHours}h {r.Minutes}m left" : $"{r.Minutes}m left";
}
