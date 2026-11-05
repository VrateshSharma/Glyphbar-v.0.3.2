using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Data.Migrations;

/// <summary>
/// Ordered, append-only schema migrations tracked in PRAGMA user_version. Each runs in a transaction,
/// and an existing database is copied aside first, so a failed upgrade loses nothing.
/// </summary>
public static class Migrator
{
    public static readonly IReadOnlyList<string> Steps =
    [
        // 1: initial schema
        """
        CREATE TABLE Applications (
            Id             INTEGER PRIMARY KEY AUTOINCREMENT,
            AppId          TEXT NOT NULL UNIQUE COLLATE NOCASE,
            Name           TEXT NOT NULL,
            LaunchTarget   TEXT NOT NULL,
            ExecutablePath TEXT,
            ShortcutPath   TEXT,
            IconPath       TEXT,
            IsPinned       INTEGER NOT NULL DEFAULT 0,
            IsUserAdded    INTEGER NOT NULL DEFAULT 0,
            SortOrder      INTEGER NOT NULL DEFAULT 0,
            LaunchCount    INTEGER NOT NULL DEFAULT 0,
            LastOpened     TEXT
        );
        CREATE INDEX IX_Applications_Pinned ON Applications (IsPinned, SortOrder);
        CREATE INDEX IX_Applications_Recent ON Applications (LastOpened);

        CREATE TABLE Widgets (
            Id         INTEGER PRIMARY KEY AUTOINCREMENT,
            WidgetType TEXT NOT NULL UNIQUE,
            IsEnabled  INTEGER NOT NULL DEFAULT 1,
            Width      INTEGER NOT NULL,
            Height     INTEGER NOT NULL,
            PositionX  INTEGER NOT NULL DEFAULT 0,
            PositionY  INTEGER NOT NULL DEFAULT 0,
            SortOrder  INTEGER NOT NULL DEFAULT 0,
            Size       TEXT NOT NULL DEFAULT 'Medium'
        );

        CREATE TABLE CalendarEvents (
            Id            INTEGER PRIMARY KEY AUTOINCREMENT,
            Title         TEXT NOT NULL,
            Description   TEXT,
            StartDateTime TEXT NOT NULL,
            EndDateTime   TEXT NOT NULL,
            IsAllDay      INTEGER NOT NULL DEFAULT 0,
            CreatedAt     TEXT NOT NULL
        );
        CREATE INDEX IX_CalendarEvents_Start ON CalendarEvents (StartDateTime);

        CREATE TABLE Settings (
            Key   TEXT PRIMARY KEY,
            Value TEXT
        );
        """,
    ];

    public static int Apply(SqliteConnection connection, string databasePath, ILogger logger)
    {
        int version = ReadVersion(connection);
        if (version > Steps.Count)
        {
            logger.LogWarning("Database schema {Version} is newer than this build ({Known})", version, Steps.Count);
            return version;
        }

        if (version < Steps.Count && version > 0)
            BackUp(connection, databasePath, version, logger);

        for (int i = version; i < Steps.Count; i++)
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = Steps[i] + $"\nPRAGMA user_version = {i + 1};";
            command.ExecuteNonQuery();
            transaction.Commit();
            logger.LogInformation("Applied database migration {Version}", i + 1);
        }
        return Steps.Count;
    }

    private static int ReadVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void BackUp(SqliteConnection connection, string path, int version, ILogger logger)
    {
        try
        {
            using var backup = new SqliteConnection($"Data Source={path}.v{version}.bak");
            backup.Open();
            connection.BackupDatabase(backup);
        }
        catch (SqliteException e)
        {
            logger.LogWarning(e, "Could not back up the database before migrating");
        }
    }
}
