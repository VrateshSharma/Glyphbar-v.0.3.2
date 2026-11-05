using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Storage;

public sealed class StorageService(ILogger<StorageService> logger) : IStorageService
{
    // A disconnected network share can block for a long time, so each drive gets a deadline.
    private static readonly TimeSpan DriveTimeout = TimeSpan.FromSeconds(3);

    public async Task<IReadOnlyList<DriveSnapshot>> ReadDrivesAsync(CancellationToken ct = default)
    {
        var names = await Task.Run(() =>
        {
            try { return DriveInfo.GetDrives().Select(d => d.Name).ToArray(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(e, "Could not list drives");
                return [];
            }
        }, ct).ConfigureAwait(false);

        string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var reads = names.Select(n => ReadWithDeadlineAsync(n, systemRoot, ct));
        var results = await Task.WhenAll(reads).ConfigureAwait(false);
        return results.OrderByDescending(d => d.IsSystem).ThenBy(d => d.Letter, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<DriveSnapshot> ReadWithDeadlineAsync(string name, string systemRoot, CancellationToken ct)
    {
        var read = Task.Run(() => Read(name, systemRoot), ct);
        var winner = await Task.WhenAny(read, Task.Delay(DriveTimeout, ct)).ConfigureAwait(false);
        return winner == read
            ? await read.ConfigureAwait(false)
            : Placeholder(name, systemRoot, DriveState.NotReady);
    }

    private static DriveSnapshot Placeholder(string name, string systemRoot, DriveState state) =>
        new(name.TrimEnd('\\'), "", name.Equals(systemRoot, StringComparison.OrdinalIgnoreCase), "Unknown", state, 0, 0);

    private static DriveSnapshot Read(string name, string systemRoot)
    {
        try
        {
            var drive = new DriveInfo(name);
            if (!drive.IsReady)
                return Placeholder(name, systemRoot, DriveState.NotReady) with { DriveType = drive.DriveType.ToString() };

            return new DriveSnapshot(
                name.TrimEnd('\\'),
                drive.VolumeLabel,
                name.Equals(systemRoot, StringComparison.OrdinalIgnoreCase),
                drive.DriveType.ToString(),
                DriveState.Ready,
                drive.TotalSize,
                drive.TotalFreeSpace);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Placeholder(name, systemRoot, DriveState.Inaccessible);
        }
    }
}
