using Glyphbar.Core.Models;

namespace Glyphbar.Core.Services;

/// <summary>One dock slot: a pinned app, a running app, or both.</summary>
public sealed record DockSlot(
    string AppId,
    string Name,
    string LaunchTarget,
    string? ExecutablePath,
    string? CustomIconPath,
    bool IsPinned,
    IReadOnlyList<RunningWindow> Windows);

public static class DockComposer
{
    /// <summary>Pinned apps keep their order; unpinned running apps follow in first-seen order.</summary>
    public static IReadOnlyList<DockSlot> Compose(
        IReadOnlyList<PinnedApp> pinned,
        IReadOnlyList<RunningWindow> running,
        IReadOnlyList<AppEntry> catalog,
        bool showUnpinnedRunning)
    {
        var remaining = running.ToList();
        var slots = new List<DockSlot>(pinned.Count);

        foreach (var pin in pinned.OrderBy(p => p.SortOrder))
        {
            var matches = remaining.Where(w => Matches(w, pin.LaunchTarget, pin.ExecutablePath)).ToList();
            remaining.RemoveAll(matches.Contains);
            slots.Add(new DockSlot(pin.AppId, pin.Name, pin.LaunchTarget, pin.ExecutablePath, pin.IconPath, true, matches));
        }

        if (!showUnpinnedRunning) return slots;

        foreach (var group in remaining.GroupBy(GroupKey, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            var app = catalog.FirstOrDefault(a => Matches(first, a.LaunchTarget, a.ExecutablePath));
            string target = app?.LaunchTarget ?? first.ExecutablePath ?? "";
            string name = app?.Name ?? first.Title;
            slots.Add(new DockSlot(app?.Id ?? group.Key, name, target, app?.ExecutablePath ?? first.ExecutablePath, null, false, group.ToList()));
        }
        return slots;
    }

    private static string GroupKey(RunningWindow w) =>
        w.AppUserModelId ?? w.ExecutablePath ?? w.Handle.ToString();

    private static string StripPrefix(string target)
    {
        const string prefix = @"shell:AppsFolder\";
        return target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? target[prefix.Length..] : target;
    }

    public static bool Matches(RunningWindow window, string launchTarget, string? executablePath)
    {
        if (window.AppUserModelId is { Length: > 0 } aumid && StripPrefix(launchTarget).Equals(aumid, StringComparison.OrdinalIgnoreCase))
            return true;
        return window.ExecutablePath is { Length: > 0 } exe
            && executablePath is { Length: > 0 }
            && exe.Equals(executablePath, StringComparison.OrdinalIgnoreCase);
    }
}
