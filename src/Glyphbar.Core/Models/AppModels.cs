namespace Glyphbar.Core.Models;

/// <summary>
/// An installed application. <see cref="LaunchTarget"/> is the shell parsing name
/// (AppUserModelID or path), which is stable and is what we persist, never a hardcoded path.
/// </summary>
public sealed record AppEntry(
    string Id,
    string Name,
    string LaunchTarget,
    string? ExecutablePath,
    string? ShortcutPath,
    bool IsUserAdded = false)
{
    public bool CanRunAsAdmin => ExecutablePath is not null;
}

public sealed class PinnedApp
{
    public long Id { get; set; }
    public required string AppId { get; set; }
    public required string Name { get; set; }
    public required string LaunchTarget { get; set; }
    public string? ExecutablePath { get; set; }
    public string? IconPath { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A top-level window that belongs in a task switcher.</summary>
public sealed record RunningWindow(
    nint Handle,
    string Title,
    string? ExecutablePath,
    string? AppUserModelId,
    bool IsForeground,
    bool IsMinimized);

public sealed record RecentApp(string AppId, DateTimeOffset LastOpened, int LaunchCount);
