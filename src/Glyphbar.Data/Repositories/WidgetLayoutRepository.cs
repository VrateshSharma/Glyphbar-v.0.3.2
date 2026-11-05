using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Glyphbar.Data.Database;

namespace Glyphbar.Data.Repositories;

public sealed class WidgetLayoutRepository(GlyphbarDatabase db) : IWidgetLayoutRepository
{
    public Task<IReadOnlyList<WidgetLayoutItem>> GetAllAsync(CancellationToken ct = default) => db.RunAsync<IReadOnlyList<WidgetLayoutItem>>(c =>
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id, WidgetType, IsEnabled, PositionX, PositionY, SortOrder, Size FROM Widgets ORDER BY SortOrder";
        var list = new List<WidgetLayoutItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            // A row from a newer version with an unknown widget type is skipped, not fatal.
            if (!Enum.TryParse<WidgetKind>(r.GetString(1), out var kind)) continue;
            list.Add(new WidgetLayoutItem
            {
                Id = r.GetInt64(0),
                Kind = kind,
                IsEnabled = r.GetInt64(2) != 0,
                PositionX = r.GetInt32(3),
                PositionY = r.GetInt32(4),
                SortOrder = r.GetInt32(5),
                Size = Enum.TryParse<WidgetSize>(r.GetString(6), out var size) ? size : WidgetSize.Medium,
            });
        }
        return list;
    }, ct);

    public Task SaveAllAsync(IReadOnlyList<WidgetLayoutItem> items, CancellationToken ct = default) => db.RunAsync(c =>
    {
        using var tx = c.BeginTransaction();
        for (int i = 0; i < items.Count; i++)
        {
            var w = items[i];
            var (width, height) = WidgetLayoutEngine.SpanOf(w.Size);
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO Widgets (WidgetType, IsEnabled, Width, Height, PositionX, PositionY, SortOrder, Size)
                VALUES ($type, $enabled, $w, $h, $x, $y, $order, $size)
                ON CONFLICT(WidgetType) DO UPDATE SET IsEnabled=$enabled, Width=$w, Height=$h,
                    PositionX=$x, PositionY=$y, SortOrder=$order, Size=$size
                """;
            cmd.Parameters.AddWithValue("$type", w.Kind.ToString());
            cmd.Parameters.AddWithValue("$enabled", w.IsEnabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$w", width);
            cmd.Parameters.AddWithValue("$h", height);
            cmd.Parameters.AddWithValue("$x", w.PositionX);
            cmd.Parameters.AddWithValue("$y", w.PositionY);
            cmd.Parameters.AddWithValue("$order", i);
            cmd.Parameters.AddWithValue("$size", w.Size.ToString());
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }, ct);
}
