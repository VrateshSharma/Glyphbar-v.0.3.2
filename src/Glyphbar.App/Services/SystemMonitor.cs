using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Glyphbar.App.Services;

/// <summary>
/// Single background loop for all polled data, rather than a thread per widget. Storage and system info
/// are only read while something visible wants them; battery is mostly event-driven.
/// </summary>
public sealed class SystemMonitor(
    IBatteryService battery,
    IStorageService storage,
    ISystemInfoService systemInfo,
    ISettingsStore settings,
    ILogger<SystemMonitor> logger) : BackgroundService
{
    private readonly SemaphoreSlim _wake = new(0);
    private DateTimeOffset _nextBattery, _nextStorage, _nextSystem;
    private int _storageInterest, _systemInterest;

    public BatterySnapshot Battery { get; private set; } = BatterySnapshot.Unavailable;
    public IReadOnlyList<DriveSnapshot> Drives { get; private set; } = [];
    public SystemInfoSnapshot? SystemInfo { get; private set; }

    public event EventHandler? BatteryChanged;
    public event EventHandler? DrivesChanged;
    public event EventHandler? SystemInfoChanged;

    public IDisposable WatchStorage()
    {
        Interlocked.Increment(ref _storageInterest);
        RefreshStorageNow();
        return new Lease(() => Interlocked.Decrement(ref _storageInterest));
    }

    public IDisposable WatchSystemInfo()
    {
        Interlocked.Increment(ref _systemInterest);
        _nextSystem = default;
        Wake();
        return new Lease(() => Interlocked.Decrement(ref _systemInterest));
    }

    public void RefreshStorageNow()
    {
        _nextStorage = default;
        Wake();
    }

    private void Wake()
    {
        if (_wake.CurrentCount == 0) _wake.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        battery.PowerChanged += OnPowerChanged;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await PollDueAsync(ct);
                var delay = NextDelay();
                try
                {
                    await _wake.WaitAsync(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            battery.PowerChanged -= OnPowerChanged;
        }
    }

    private void OnPowerChanged(object? sender, EventArgs e)
    {
        _nextBattery = default;
        Wake();
    }

    private TimeSpan NextDelay()
    {
        var next = _nextBattery;
        if (Volatile.Read(ref _storageInterest) > 0 && _nextStorage < next) next = _nextStorage;
        if (Volatile.Read(ref _systemInterest) > 0 && _nextSystem < next) next = _nextSystem;
        var delay = next - DateTimeOffset.Now;
        return delay < TimeSpan.FromMilliseconds(50) ? TimeSpan.FromMilliseconds(50) : delay;
    }

    private async Task PollDueAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.Now;
        var w = settings.Current.Widgets;
        try
        {
            if (now >= _nextBattery)
            {
                _nextBattery = now.AddSeconds(w.BatteryRefreshSeconds);
                var snapshot = battery.Read();
                if (snapshot != Battery)
                {
                    Battery = snapshot;
                    BatteryChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            if (Volatile.Read(ref _storageInterest) > 0 && now >= _nextStorage)
            {
                _nextStorage = now.AddSeconds(w.StorageRefreshSeconds);
                Drives = await storage.ReadDrivesAsync(ct);
                DrivesChanged?.Invoke(this, EventArgs.Empty);
            }

            if (Volatile.Read(ref _systemInterest) > 0 && w.ResourceMonitoringEnabled && now >= _nextSystem)
            {
                _nextSystem = now.AddSeconds(w.SystemInfoRefreshSeconds);
                SystemInfo = await systemInfo.ReadAsync(ct);
                SystemInfoChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "System poll failed");
        }
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) release();
        }
    }
}
