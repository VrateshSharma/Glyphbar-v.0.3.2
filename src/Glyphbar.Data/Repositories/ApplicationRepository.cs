using System.Globalization;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Data.Database;
using Microsoft.Data.Sqlite;

namespace Glyphbar.Data.Repositories;

/// <summary>One Applications table backs pinned apps, user-added apps and launch history.</summary>
public sealed class ApplicationRepository(GlyphbarDatabase db) : IPinnedAppsRepository, IRecentAppsRepository, IUserAppsRepository
{
    Task<IReadOnlyList<PinnedApp>> IPinnedAppsRepository.GetAllAsync(CancellationToken ct) => db.RunAsync<IReadOnlyList<PinnedApp>>(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT Id, AppId, Name, LaunchTarget, ExecutablePath, IconPath, SortOrder
            FROM Applications WHERE IsPinned=1 ORDER BY SortOrder, Id
            """;
        var list = new List<PinnedApp>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new PinnedApp
            {
                Id = r.GetInt64(0),
                AppId = r.GetString(1),
                Name = r.GetString(2),
                LaunchTarget = r.GetString(3),
                ExecutablePath = r.IsDBNull(4) ? null : r.GetString(4),
                IconPath = r.IsDBNull(5) ? null : r.GetString(5),
                SortOrder = r.GetInt32(6),
            });
        }
        return list;
    }, ct);

    public Task PinAsync(PinnedApp app, CancellationToken ct = default) => db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Applications (AppId, Name, LaunchTarget, ExecutablePath, IconPath, IsPinned, SortOrder)
            VALUES ($appId, $name, $target, $exe, $icon, 1,
                    (SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Applications WHERE IsPinned=1))
            ON CONFLICT(AppId) DO UPDATE SET
                IsPinned=1, Name=excluded.Name, LaunchTarget=excluded.LaunchTarget,
                ExecutablePath=excluded.ExecutablePath, SortOrder=excluded.SortOrder
            """;
        cmd.Parameters.AddWithValue("$appId", app.AppId);
        cmd.Parameters.AddWithValue("$name", app.Name);
        cmd.Parameters.AddWithValue("$target", app.LaunchTarget);
        cmd.Parameters.AddWithValue("$exe", (object?)app.ExecutablePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$icon", (object?)app.IconPath ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }, ct);

    public Task UnpinAsync(string appId, CancellationToken ct = default) => db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE Applications SET IsPinned=0 WHERE AppId=$appId";
        cmd.Parameters.AddWithValue("$appId", appId);
        cmd.ExecuteNonQuery();
    }, ct);

    public Task ReorderAsync(IReadOnlyList<string> ids, CancellationToken ct = default) => db.RunAsync(c =>
    {
        using var tx = c.BeginTransaction();
        for (int i = 0; i < ids.Count; i++)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE Applications SET SortOrder=$order WHERE AppId=$appId";
            cmd.Parameters.AddWithValue("$order", i);
            cmd.Parameters.AddWithValue("$appId", ids[i]);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }, ct);

    public Task SetIconAsync(string appId, string? iconPath, CancellationToken ct = default) => db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE Applications SET IconPath=$icon WHERE AppId=$appId";
        cmd.Parameters.AddWithValue("$icon", (object?)iconPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$appId", appId);
        cmd.ExecuteNonQuery();
    }, ct);

    public Task RecordLaunchAsync(string appId, CancellationToken ct = default) => db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Applications (AppId, Name, LaunchTarget, LaunchCount, LastOpened)
            VALUES ($appId, $appId, $appId, 1, $now)
            ON CONFLICT(AppId) DO UPDATE SET LaunchCount=LaunchCount+1, LastOpened=$now
            """;
        cmd.Parameters.AddWithValue("$appId", appId);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }, ct);

    public Task<IReadOnlyList<RecentApp>> GetRecentAsync(int count, CancellationToken ct = default) => db.RunAsync<IReadOnlyList<RecentApp>>(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT AppId, LastOpened, LaunchCount FROM Applications
            WHERE LastOpened IS NOT NULL ORDER BY LastOpened DESC LIMIT $count
            """;
        cmd.Parameters.AddWithValue("$count", count);
        var list = new List<RecentApp>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new RecentApp(r.GetString(0), DateTimeOffset.Parse(r.GetString(1), CultureInfo.InvariantCulture), r.GetInt32(2)));
        return list;
    }, ct);

    Task<IReadOnlyList<AppEntry>> IUserAppsRepository.GetAllAsync(CancellationToken ct) => db.RunAsync<IReadOnlyList<AppEntry>>(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT AppId, Name, LaunchTarget, ExecutablePath, ShortcutPath FROM Applications WHERE IsUserAdded=1 ORDER BY Name";
        var list = new List<AppEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new AppEntry(r.GetString(0), r.GetString(1), r.GetString(2), Nullable(r, 3), Nullable(r, 4), true));
        return list;
    }, ct);

    public Task AddAsync(AppEntry app, CancellationToken ct = default) => db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Applications (AppId, Name, LaunchTarget, ExecutablePath, ShortcutPath, IsUserAdded)
            VALUES ($appId, $name, $target, $exe, $lnk, 1)
            ON CONFLICT(AppId) DO UPDATE SET IsUserAdded=1, Name=excluded.Name,
                LaunchTarget=excluded.LaunchTarget, ExecutablePath=excluded.ExecutablePath
            """;
        cmd.Parameters.AddWithValue("$appId", app.Id);
        cmd.Parameters.AddWithValue("$name", app.Name);
        cmd.Parameters.AddWithValue("$target", app.LaunchTarget);
        cmd.Parameters.AddWithValue("$exe", (object?)app.ExecutablePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lnk", (object?)app.ShortcutPath ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }, ct);

    public Task RemoveAsync(string appId, CancellationToken ct = default) => db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE Applications SET IsUserAdded=0, IsPinned=0 WHERE AppId=$appId";
        cmd.Parameters.AddWithValue("$appId", appId);
        cmd.ExecuteNonQuery();
    }, ct);

    private static string? Nullable(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}
