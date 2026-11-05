using Glyphbar.Core.Models;

namespace Glyphbar.Core.Interfaces;

public interface IClock
{
    DateTimeOffset Now { get; }
}

public interface ISettingsStore
{
    AppSettings Current { get; }
    /// <summary>Raised on the caller's thread after <see cref="Update"/>.</summary>
    event EventHandler? Changed;
    /// <summary>Applies a change, notifies listeners and schedules a debounced, atomic save.</summary>
    void Update(Action<AppSettings> change);
    Task FlushAsync();
    void ResetToDefaults();
}

public interface ICalendarRepository
{
    Task<IReadOnlyList<CalendarEvent>> GetRangeAsync(DateTime fromInclusive, DateTime toExclusive, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarEvent>> GetUpcomingAsync(DateTime from, int count, CancellationToken ct = default);
    Task<long> SaveAsync(CalendarEvent calendarEvent, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public interface IPinnedAppsRepository
{
    Task<IReadOnlyList<PinnedApp>> GetAllAsync(CancellationToken ct = default);
    Task PinAsync(PinnedApp app, CancellationToken ct = default);
    Task UnpinAsync(string appId, CancellationToken ct = default);
    Task ReorderAsync(IReadOnlyList<string> appIdsInOrder, CancellationToken ct = default);
    Task SetIconAsync(string appId, string? iconPath, CancellationToken ct = default);
}

public interface IWidgetLayoutRepository
{
    Task<IReadOnlyList<WidgetLayoutItem>> GetAllAsync(CancellationToken ct = default);
    Task SaveAllAsync(IReadOnlyList<WidgetLayoutItem> items, CancellationToken ct = default);
}

public interface IRecentAppsRepository
{
    Task RecordLaunchAsync(string appId, CancellationToken ct = default);
    Task<IReadOnlyList<RecentApp>> GetRecentAsync(int count, CancellationToken ct = default);
}

public interface IUserAppsRepository
{
    Task<IReadOnlyList<AppEntry>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(AppEntry app, CancellationToken ct = default);
    Task RemoveAsync(string appId, CancellationToken ct = default);
}

public interface IAppCatalog
{
    IReadOnlyList<AppEntry> Apps { get; }
    event EventHandler? Changed;
    Task RefreshAsync(CancellationToken ct = default);
    AppEntry? Find(string appId);
    Task<AppEntry?> AddExecutableAsync(string path, CancellationToken ct = default);
    Task RemoveUserAppAsync(string appId, CancellationToken ct = default);
}

public interface IIconProvider
{
    /// <summary>Returns PNG bytes, or null when the shell has no icon for the target.</summary>
    Task<byte[]?> GetIconPngAsync(string launchTarget, int size, bool monochrome, CancellationToken ct = default);
}

public interface IAppLauncher
{
    Task<OperationResult> LaunchAsync(string target, bool asAdministrator = false);
    /// <summary>Opens a file, folder or allow-listed ms-settings URI through the Windows Shell.</summary>
    Task<OperationResult> OpenAsync(string target);
    Task<OperationResult> RevealInExplorerAsync(string path);
}

public interface IRunningWindows
{
    IReadOnlyList<RunningWindow> Windows { get; }
    event EventHandler? Changed;
    void Start();
    void Activate(nint handle);
    void Minimize(nint handle);
    void Close(nint handle);
}

public interface IBatteryService
{
    BatterySnapshot Read();
    /// <summary>Fires on a background thread when Windows reports a power change.</summary>
    event EventHandler? PowerChanged;
}

public interface IStorageService
{
    Task<IReadOnlyList<DriveSnapshot>> ReadDrivesAsync(CancellationToken ct = default);
}

public interface ISystemInfoService
{
    Task<SystemInfoSnapshot> ReadAsync(CancellationToken ct = default);
}

public interface IMediaService
{
    IReadOnlyList<MediaSessionInfo> Sessions { get; }
    string? CurrentSessionId { get; }
    event EventHandler? Changed;
    Task StartAsync();
    Task PlayPauseAsync(string sessionId);
    Task NextAsync(string sessionId);
    Task PreviousAsync(string sessionId);
}

public interface IFileSearchProvider
{
    Task<FileSearchOutcome> SearchAsync(string query, bool files, bool folders, IReadOnlyList<string> scopes, int max, CancellationToken ct);
}

public interface IPowerExecutor
{
    Task<OperationResult> ExecuteAsync(PowerAction action);
}

public interface IStartupManager
{
    Task<bool> IsEnabledAsync();
    /// <summary>Returns the resulting state; Windows or policy can refuse enabling.</summary>
    Task<bool> SetEnabledAsync(bool enabled);
}

public enum HotkeyAction { Search, Dashboard, WidgetPanel, PowerMenu }

public enum HotkeyRegistration { Registered, Conflict, Invalid, Reserved }

public interface IHotkeyService
{
    event EventHandler<HotkeyAction>? Pressed;
    IReadOnlyDictionary<HotkeyAction, HotkeyRegistration> Status { get; }
    HotkeyRegistration Register(HotkeyAction action, string gesture);
    void UnregisterAll();
}
