using Glyphbar.Core.Models;
using Glyphbar.Core.Services;

namespace Glyphbar.Core.Tests;

public class BatteryTests
{
    private static readonly BatteryThresholds Defaults = new();

    [Theory]
    [InlineData(100, BatteryLevel.Normal)]
    [InlineData(51, BatteryLevel.Normal)]
    [InlineData(50, BatteryLevel.Moderate)]
    [InlineData(20, BatteryLevel.Moderate)]
    [InlineData(19, BatteryLevel.Low)]
    [InlineData(10, BatteryLevel.Low)]
    [InlineData(9, BatteryLevel.Critical)]
    [InlineData(0, BatteryLevel.Critical)]
    public void Classify_uses_prd_bands(int percent, BatteryLevel expected) =>
        Assert.Equal(expected, BatteryInterpreter.Classify(percent, Defaults));

    [Fact]
    public void Classify_honours_custom_thresholds() =>
        Assert.Equal(BatteryLevel.Low, BatteryInterpreter.Classify(30, new BatteryThresholds { Moderate = 80, Low = 40, Critical = 5 }));

    [Fact]
    public void Desktop_without_battery_reports_no_battery()
    {
        var s = BatteryInterpreter.Interpret(1, 128, 255, 0, 0xFFFFFFFF);
        Assert.False(s.HasBattery);
        Assert.Null(s.Percent);
        Assert.Equal(true, s.OnAcPower);
    }

    [Fact]
    public void Unknown_flag_255_is_not_mistaken_for_no_battery()
    {
        var s = BatteryInterpreter.Interpret(255, 255, 60, 0, 0xFFFFFFFF);
        Assert.True(s.HasBattery);
        Assert.Equal(60, s.Percent);
        Assert.Null(s.OnAcPower);
        Assert.False(s.IsCharging);
    }

    [Fact]
    public void Remaining_time_is_never_invented()
    {
        Assert.Null(BatteryInterpreter.Interpret(0, 1, 80, 0, 0xFFFFFFFF).Remaining);
        Assert.Null(BatteryInterpreter.Interpret(1, 8, 80, 0, 3600).Remaining);
        Assert.Equal(TimeSpan.FromHours(1), BatteryInterpreter.Interpret(0, 1, 80, 0, 3600).Remaining);
    }

    [Fact]
    public void Charging_and_saver_flags_are_read()
    {
        var s = BatteryInterpreter.Interpret(1, 8, 45, 1, 0xFFFFFFFF);
        Assert.True(s.IsCharging);
        Assert.True(s.BatterySaverOn);
    }
}
