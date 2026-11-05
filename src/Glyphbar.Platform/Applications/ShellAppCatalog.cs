using System.Runtime.InteropServices;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Platform.Interop;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Applications;

/// <summary>
/// Lists applications from the shell's AppsFolder, the same virtual folder Start uses. It already merges
/// Start Menu shortcuts with registered packaged apps, so nothing needs to scan the disk.
/// </summary>
public sealed class ShellAppCatalog(ShellWorker worker, IUserAppsRepository userApps, ILogger<ShellAppCatalog> logger) : IAppCatalog
{
    public const string AppsFolderPrefix = @"shell:AppsFolder\";

    private static readonly string[] NonApplicationExtensions = [".url", ".htm", ".html", ".chm", ".txt", ".pdf", ".md", ".hlp"];

    private volatile IReadOnlyList<AppEntry> _apps = [];

    public IReadOnlyList<AppEntry> Apps => _apps;
    public event EventHandler? Changed;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var shellApps = await worker.RunAsync(Enumerate, ct).ConfigureAwait(false);
        // Installed apps come from the shell, so they must still list if the database is unavailable.
        IReadOnlyList<AppEntry> added = [];
        try
        {
            added = await userApps.GetAllAsync(ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "User-added apps unavailable");
        }


        _apps = shellApps.Concat(added)
            .DistinctBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        logger.LogInformation("Application catalog: {Count} apps", _apps.Count);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public AppEntry? Find(string appId) =>
        _apps.FirstOrDefault(a => a.Id.Equals(appId, StringComparison.OrdinalIgnoreCase));

    public async Task<AppEntry?> AddExecutableAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return null;
        var entry = new AppEntry(path, Path.GetFileNameWithoutExtension(path), path, path, null, true);
        await userApps.AddAsync(entry, ct).ConfigureAwait(false);
        await RefreshAsync(ct).ConfigureAwait(false);
        return entry;
    }

    public async Task RemoveUserAppAsync(string appId, CancellationToken ct = default)
    {
        await userApps.RemoveAsync(appId, ct).ConfigureAwait(false);
        await RefreshAsync(ct).ConfigureAwait(false);
    }

    private List<AppEntry> Enumerate()
    {
        var result = new List<AppEntry>();
        try
        {
            var folderId = Shell.FOLDERID_AppsFolder;
            var itemId = Shell.IID_IShellItem;
            Marshal.ThrowExceptionForHR(Shell.SHGetKnownFolderItem(ref folderId, 0, 0, ref itemId, out var folder));

            var bhid = Shell.BHID_EnumItems;
            var enumId = Shell.IID_IEnumShellItems;
            Marshal.ThrowExceptionForHR(folder.BindToHandler(0, ref bhid, ref enumId, out object enumerator));

            var items = (IEnumShellItems)enumerator;
            while (items.Next(1, out var item, out uint fetched) == 0 && fetched == 1)
            {
                try
                {
                    if (Describe(item) is { } app) result.Add(app);
                }
                catch (Exception e) when (e is COMException or InvalidCastException)
                {
                    logger.LogDebug(e, "Skipped an unreadable AppsFolder item");
                }
                finally
                {
                    Marshal.ReleaseComObject(item);
                }
            }
            Marshal.ReleaseComObject(items);
            Marshal.ReleaseComObject(folder);
        }
        catch (Exception e) when (e is COMException or InvalidCastException)
        {
            logger.LogError(e, "Could not enumerate AppsFolder");
        }
        return result;
    }

    private static AppEntry? Describe(IShellItem item)
    {
        if (item.GetDisplayName(Shell.SIGDN_NORMALDISPLAY, out nint namePtr) != 0) return null;
        string? name = Shell.TakeString(namePtr);
        if (item.GetDisplayName(Shell.SIGDN_DESKTOPABSOLUTEPARSING, out nint parsingPtr) != 0) return null;
        string? parsing = Shell.TakeString(parsingPtr);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(parsing)) return null;

        string? exe = null;
        if (item is IShellItem2 item2)
        {
            var key = Shell.PKEY_Link_TargetParsingPath;
            if (item2.GetString(ref key, out nint targetPtr) == 0)
                exe = Shell.TakeString(targetPtr);
        }

        if (!IsApplication(name, parsing, exe)) return null;
        bool isExe = exe?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true;
        return new AppEntry(parsing, name, AppsFolderPrefix + parsing, isExe ? exe : null, null);
    }

    /// <summary>AppsFolder also lists documents and uninstallers that happened to be in Start Menu folders.</summary>
    public static bool IsApplication(string name, string parsingName, string? target)
    {
        if (name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase)) return false;
        string probe = target ?? parsingName;
        return !NonApplicationExtensions.Any(x => probe.EndsWith(x, StringComparison.OrdinalIgnoreCase));
    }
}
