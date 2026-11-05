namespace Glyphbar.Core.Models;

public enum BatteryLevel { Normal, Moderate, Low, Critical }

/// <summary>Battery state as reported by Windows. Null members mean Windows did not report them.</summary>
public sealed record BatterySnapshot(
    bool HasBattery,
    int? Percent,
    bool IsCharging,
    bool? OnAcPower,
    bool BatterySaverOn,
    TimeSpan? Remaining)
{
    public static BatterySnapshot Unavailable { get; } = new(false, null, false, null, false, null);
}

public enum DriveState { Ready, NotReady, Inaccessible }

public sealed record DriveSnapshot(
    string Letter,
    string Label,
    bool IsSystem,
    string DriveType,
    DriveState State,
    long TotalBytes,
    long FreeBytes)
{
    public long UsedBytes => TotalBytes - FreeBytes;
    public double PercentUsed => TotalBytes <= 0 ? 0 : UsedBytes * 100d / TotalBytes;
}

public enum MediaPlayback { Unknown, Closed, Stopped, Paused, Playing, Changing }

public sealed record MediaSessionInfo(
    string SessionId,
    string SourceName,
    string? Title,
    string? Artist,
    string? Album,
    byte[]? Artwork,
    MediaPlayback Playback,
    bool CanPlay,
    bool CanPause,
    bool CanNext,
    bool CanPrevious);

public sealed record SystemInfoSnapshot(
    double? CpuPercent,
    long? MemoryUsedBytes,
    long? MemoryTotalBytes,
    string? GpuName,
    bool? NetworkConnected,
    string? NetworkName);

public enum PowerAction { Shutdown, Restart, Sleep, Lock, SignOut }

public sealed record OperationResult(bool Success, string? Error = null)
{
    public static OperationResult Ok { get; } = new(true);
    public static OperationResult Fail(string error) => new(false, error);
}
