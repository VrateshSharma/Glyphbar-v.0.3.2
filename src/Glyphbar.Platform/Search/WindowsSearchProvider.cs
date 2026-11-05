using System.Data.OleDb;
using System.Text;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Search;

/// <summary>
/// File search through the Windows Search index (the Search.CollatorDSO OLE DB provider). It only reads
/// the index, so nothing walks the disk, and the index already honours the user's indexed locations.
/// </summary>
public sealed class WindowsSearchProvider(ILogger<WindowsSearchProvider> logger) : IFileSearchProvider
{
    private const string ConnectionString = "Provider=Search.CollatorDSO;Extended Properties='Application=Windows';";

    public Task<FileSearchOutcome> SearchAsync(string query, bool files, bool folders, IReadOnlyList<string> scopes, int max, CancellationToken ct)
    {
        string? sql = BuildSql(query, files, folders, scopes, max);
        if (sql is null) return Task.FromResult(new FileSearchOutcome([], true));

        return Task.Run(() =>
        {
            var results = new List<SearchResult>();
            try
            {
                using var connection = new OleDbConnection(ConnectionString);
                connection.Open();
                ct.ThrowIfCancellationRequested();
                using var command = new OleDbCommand(sql, connection) { CommandTimeout = 5 };
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    if (reader[0] is not string path || path.Length == 0) continue;
                    bool isFolder = reader[2] is string type && type.Equals("Directory", StringComparison.OrdinalIgnoreCase);
                    string name = reader[1] as string ?? Path.GetFileName(path);
                    results.Add(new SearchResult(isFolder ? SearchCategory.Folder : SearchCategory.File, name, Path.GetDirectoryName(path), path, 0));
                }
                return new FileSearchOutcome(results, true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e) when (e is OleDbException or InvalidOperationException or PlatformNotSupportedException)
            {
                logger.LogWarning(e, "Windows Search is unavailable");
                return new FileSearchOutcome([], false);
            }
        }, ct);
    }

    /// <summary>Returns null when the query has nothing searchable in it.</summary>
    public static string? BuildSql(string query, bool files, bool folders, IReadOnlyList<string> scopes, int max)
    {
        if (!files && !folders) return null;

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Clean).Where(t => t.Length > 0).ToList();
        if (terms.Count == 0) return null;

        var sql = new StringBuilder();
        sql.Append($"SELECT TOP {Math.Clamp(max, 1, 200)} System.ItemPathDisplay, System.ItemNameDisplay, System.ItemType FROM SystemIndex WHERE ");

        // CONTAINS with a trailing wildcard uses the index, which LIKE '%x%' cannot.
        sql.Append(string.Join(" AND ", terms.Select(t => $"CONTAINS(System.FileName, '\"{t}*\"')")));

        if (files != folders)
            sql.Append(folders ? " AND System.ItemType = 'Directory'" : " AND System.ItemType <> 'Directory'");

        var cleanScopes = scopes.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        sql.Append(cleanScopes.Count == 0
            ? " AND SCOPE='file:'"
            : " AND (" + string.Join(" OR ", cleanScopes.Select(s => $"SCOPE='file:{s.Replace("'", "''").Replace('\\', '/')}'")) + ")");

        sql.Append(" ORDER BY System.Search.Rank DESC, System.DateModified DESC");
        return sql.ToString();
    }

    /// <summary>
    /// User text ends up inside a quoted CONTAINS literal, which can't take parameters here. An allow-list of
    /// characters means nothing the user types can end the literal or change the query.
    /// </summary>
    public static string Clean(string term)
    {
        var sb = new StringBuilder(term.Length);
        foreach (char c in term)
            if (char.IsLetterOrDigit(c) || c is '.' or '-' or '_')
                sb.Append(c);
        return sb.ToString();
    }
}
