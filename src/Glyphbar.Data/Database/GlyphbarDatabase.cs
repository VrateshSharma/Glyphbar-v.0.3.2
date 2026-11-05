using Glyphbar.Data.Configuration;
using Glyphbar.Data.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Data.Database;

/// <summary>Opens SQLite connections and applies migrations once, before first use.</summary>
public sealed class GlyphbarDatabase(AppPaths paths, ILogger<GlyphbarDatabase> logger)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = paths.Database,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Private,
    }.ToString();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    public int SchemaVersion { get; private set; }

    /// <summary>Runs work on a pool thread: SQLite's async API is synchronous underneath.</summary>
    public Task<T> RunAsync<T>(Func<SqliteConnection, T> work, CancellationToken ct = default) => Task.Run(async () =>
    {
        await EnsureReadyAsync(ct).ConfigureAwait(false);
        using var connection = Open();
        return work(connection);
    }, ct);

    public Task RunAsync(Action<SqliteConnection> work, CancellationToken ct = default) =>
        RunAsync(c => { work(c); return 0; }, ct);

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=3000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (_ready) return;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_ready) return;
            using var connection = Open();
            // Rollback journal, not WAL. WAL maps a shared-memory file, and Windows Error Reporting's snapshot of a
            // crashed process can keep that mapping alive, which then fails every later open with "disk I/O error".
            // One process and a few writes gain nothing from WAL. This also converts databases made by older builds.
            try
            {
                using var journal = connection.CreateCommand();
                journal.CommandText = "PRAGMA journal_mode=DELETE;";
                journal.ExecuteNonQuery();
            }
            catch (SqliteException e)
            {
                logger.LogWarning(e, "Could not set the journal mode");
            }
            SchemaVersion = Migrator.Apply(connection, paths.Database, logger);
            _ready = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
