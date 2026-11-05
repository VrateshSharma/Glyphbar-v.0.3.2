using System.Runtime.InteropServices;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.Extensions.Logging;
using Windows.System.Power;

namespace Glyphbar.Platform.Battery;

public sealed class BatteryService : IBatteryService, IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32", SetLastError = true)] private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    private readonly ILogger<BatteryService> _logger;

    public BatteryService(ILogger<BatteryService> logger)
    {
        _logger = logger;
        try
        {
            // Event-driven updates; the periodic poll is only a safety net.
            PowerManager.BatteryStatusChanged += OnChanged;
            PowerManager.PowerSupplyStatusChanged += OnChanged;
            PowerManager.RemainingChargePercentChanged += OnChanged;
            PowerManager.EnergySaverStatusChanged += OnChanged;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Power change events unavailable; relying on polling");
        }
    }

    public event EventHandler? PowerChanged;

    private void OnChanged(object? sender, object e) => PowerChanged?.Invoke(this, EventArgs.Empty);

    public BatterySnapshot Read()
    {
        if (!GetSystemPowerStatus(out var s))
        {
            _logger.LogWarning("GetSystemPowerStatus failed: {Error}", Marshal.GetLastWin32Error());
            return BatterySnapshot.Unavailable;
        }
        return BatteryInterpreter.Interpret(s.ACLineStatus, s.BatteryFlag, s.BatteryLifePercent, s.SystemStatusFlag, s.BatteryLifeTime);
    }

    public void Dispose()
    {
        PowerManager.BatteryStatusChanged -= OnChanged;
        PowerManager.PowerSupplyStatusChanged -= OnChanged;
        PowerManager.RemainingChargePercentChanged -= OnChanged;
        PowerManager.EnergySaverStatusChanged -= OnChanged;
    }
}
