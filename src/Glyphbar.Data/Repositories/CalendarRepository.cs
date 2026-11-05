using System.Globalization;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Data.Database;
using Microsoft.Data.Sqlite;

namespace Glyphbar.Data.Repositories;

public sealed class CalendarRepository(GlyphbarDatabase db) : ICalendarRepository
{
    // Events are wall-clock times, so they stay put if the user changes time zone.
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss";

    private static string Stamp(DateTime value) => value.ToString(Format, CultureInfo.InvariantCulture);

    public Task<IReadOnlyList<CalendarEvent>> GetRangeAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        db.RunAsync(c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = """
                SELECT Id, Title, Description, StartDateTime, EndDateTime, IsAllDay, CreatedAt
                FROM CalendarEvents WHERE StartDateTime < $to AND EndDateTime >= $from
                ORDER BY StartDateTime
                """;
            cmd.Parameters.AddWithValue("$from", Stamp(from));
            cmd.Parameters.AddWithValue("$to", Stamp(to));
            return Read(cmd);
        }, ct);

    public Task<IReadOnlyList<CalendarEvent>> GetUpcomingAsync(DateTime from, int count, CancellationToken ct = default) =>
        db.RunAsync(c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = """
                SELECT Id, Title, Description, StartDateTime, EndDateTime, IsAllDay, CreatedAt
                FROM CalendarEvents WHERE EndDateTime >= $from
                ORDER BY StartDateTime LIMIT $count
                """;
            cmd.Parameters.AddWithValue("$from", Stamp(from));
            cmd.Parameters.AddWithValue("$count", count);
            return Read(cmd);
        }, ct);

    public Task<long> SaveAsync(CalendarEvent e, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(e.Title)) throw new ArgumentException("An event needs a title.", nameof(e));
        if (e.End < e.Start) throw new ArgumentException("An event cannot end before it starts.", nameof(e));

        return db.RunAsync(c =>
        {
            using var cmd = c.CreateCommand();
            if (e.Id == 0)
            {
                e.CreatedAt = e.CreatedAt == default ? DateTimeOffset.Now : e.CreatedAt;
                cmd.CommandText = """
                    INSERT INTO CalendarEvents (Title, Description, StartDateTime, EndDateTime, IsAllDay, CreatedAt)
                    VALUES ($title, $description, $start, $end, $allDay, $created);
                    SELECT last_insert_rowid();
                    """;
                cmd.Parameters.AddWithValue("$created", e.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
            }
            else
            {
                cmd.CommandText = """
                    UPDATE CalendarEvents SET Title=$title, Description=$description, StartDateTime=$start,
                        EndDateTime=$end, IsAllDay=$allDay WHERE Id=$id;
                    SELECT $id;
                    """;
                cmd.Parameters.AddWithValue("$id", e.Id);
            }
            cmd.Parameters.AddWithValue("$title", e.Title.Trim());
            cmd.Parameters.AddWithValue("$description", (object?)e.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$start", Stamp(e.Start));
            cmd.Parameters.AddWithValue("$end", Stamp(e.End));
            cmd.Parameters.AddWithValue("$allDay", e.IsAllDay ? 1 : 0);
            e.Id = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            return e.Id;
        }, ct);
    }

    public Task DeleteAsync(long id, CancellationToken ct = default) => db.RunAsync(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM CalendarEvents WHERE Id=$id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }, ct);

    private static IReadOnlyList<CalendarEvent> Read(SqliteCommand cmd)
    {
        var list = new List<CalendarEvent>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new CalendarEvent
            {
                Id = r.GetInt64(0),
                Title = r.GetString(1),
                Description = r.IsDBNull(2) ? null : r.GetString(2),
                Start = DateTime.ParseExact(r.GetString(3), Format, CultureInfo.InvariantCulture),
                End = DateTime.ParseExact(r.GetString(4), Format, CultureInfo.InvariantCulture),
                IsAllDay = r.GetInt64(5) != 0,
                CreatedAt = DateTimeOffset.Parse(r.GetString(6), CultureInfo.InvariantCulture),
            });
        }
        return list;
    }
}
