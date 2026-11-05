using Glyphbar.Core.Models;

namespace Glyphbar.Core.Services;

/// <summary>Turns raw SYSTEM_POWER_STATUS fields into a snapshot without inventing anything.</summary>
public static class BatteryInterpreter
{
    private const byte Unknown = 255;
    private const byte FlagCharging = 8;
    private const byte FlagNoBattery = 128;
    private const uint UnknownLifetime = 0xFFFFFFFF;

    public static BatterySnapshot Interpret(byte acLineStatus, byte batteryFlag, byte lifePercent, byte systemStatusFlag, uint lifeTimeSeconds)
    {
        bool? onAc = acLineStatus switch { 0 => false, 1 => true, _ => null };
        bool saver = systemStatusFlag == 1;

        // 255 alone means "unknown", and that value also has the 128 bit set.
        bool noBattery = batteryFlag != Unknown && (batteryFlag & FlagNoBattery) != 0;
        if (noBattery)
            return new BatterySnapshot(false, null, false, onAc, saver, null);

        bool charging = batteryFlag != Unknown && (batteryFlag & FlagCharging) != 0;
        int? percent = lifePercent <= 100 ? lifePercent : null;

        // Windows has no estimate while on AC, and reports -1 when it cannot compute one.
        TimeSpan? remaining = lifeTimeSeconds == UnknownLifetime || onAc == true
            ? null
            : TimeSpan.FromSeconds(lifeTimeSeconds);

        return new BatterySnapshot(batteryFlag != Unknown || percent is not null, percent, charging, onAc, saver, remaining);
    }

    /// <summary>Above Moderate: normal. Moderate..Low: moderate. Low..Critical: low. Below Critical: critical.</summary>
    public static BatteryLevel Classify(int percent, BatteryThresholds thresholds)
    {
        if (percent > thresholds.Moderate) return BatteryLevel.Normal;
        if (percent >= thresholds.Low) return BatteryLevel.Moderate;
        if (percent >= thresholds.Critical) return BatteryLevel.Low;
        return BatteryLevel.Critical;
    }
}
