using Glyphbar.Core.Models;

namespace Glyphbar.Core.Services;

public readonly record struct WidgetCell(int Column, int Row, int ColumnSpan, int RowSpan);

public static class WidgetLayoutEngine
{
    public static (int Columns, int Rows) SpanOf(WidgetSize size) => size switch
    {
        WidgetSize.Small => (1, 1),
        WidgetSize.Medium => (2, 1),
        _ => (2, 2),
    };

    public static IReadOnlyList<WidgetSize> AllowedSizes(WidgetKind kind) => kind switch
    {
        WidgetKind.Calendar => [WidgetSize.Medium, WidgetSize.Large],
        WidgetKind.Storage => [WidgetSize.Medium, WidgetSize.Large],
        WidgetKind.Music => [WidgetSize.Small, WidgetSize.Medium, WidgetSize.Large],
        WidgetKind.Clock => [WidgetSize.Small, WidgetSize.Medium, WidgetSize.Large],
        _ => [WidgetSize.Small, WidgetSize.Medium],
    };

    public static WidgetSize NextSize(WidgetKind kind, WidgetSize current)
    {
        var allowed = AllowedSizes(kind);
        int index = -1;
        for (int i = 0; i < allowed.Count; i++)
            if (allowed[i] == current) index = i;
        return allowed[(index + 1) % allowed.Count];
    }

    /// <summary>First-fit packing, in order. Spans wider than the grid are clamped.</summary>
    public static IReadOnlyList<WidgetCell> Pack(IReadOnlyList<WidgetSize> sizes, int columns)
    {
        if (columns < 1) throw new ArgumentOutOfRangeException(nameof(columns));
        var occupied = new List<bool[]>();
        var result = new WidgetCell[sizes.Count];

        bool Fits(int row, int col, int w, int h)
        {
            if (col + w > columns) return false;
            for (int r = row; r < row + h; r++)
            {
                if (r >= occupied.Count) continue;
                for (int c = col; c < col + w; c++)
                    if (occupied[r][c]) return false;
            }
            return true;
        }

        for (int i = 0; i < sizes.Count; i++)
        {
            var (w, h) = SpanOf(sizes[i]);
            w = Math.Min(w, columns);
            for (int row = 0, placed = 0; placed == 0; row++)
            {
                for (int col = 0; col + w <= columns; col++)
                {
                    if (!Fits(row, col, w, h)) continue;
                    while (occupied.Count < row + h) occupied.Add(new bool[columns]);
                    for (int r = row; r < row + h; r++)
                        for (int c = col; c < col + w; c++)
                            occupied[r][c] = true;
                    result[i] = new WidgetCell(col, row, w, h);
                    placed = 1;
                    break;
                }
            }
        }
        return result;
    }

    public static IReadOnlyList<WidgetLayoutItem> Defaults() =>
    [
        new() { Kind = WidgetKind.Clock, Size = WidgetSize.Medium, SortOrder = 0 },
        new() { Kind = WidgetKind.Date, Size = WidgetSize.Small, SortOrder = 1 },
        new() { Kind = WidgetKind.Battery, Size = WidgetSize.Small, SortOrder = 2 },
        new() { Kind = WidgetKind.Calendar, Size = WidgetSize.Large, SortOrder = 3 },
        new() { Kind = WidgetKind.Music, Size = WidgetSize.Medium, SortOrder = 4 },
        new() { Kind = WidgetKind.Storage, Size = WidgetSize.Medium, SortOrder = 5 },
        new() { Kind = WidgetKind.SystemInfo, Size = WidgetSize.Medium, SortOrder = 6, IsEnabled = false },
    ];

    /// <summary>Adds widget kinds missing from a saved layout (new in a later version).</summary>
    public static IReadOnlyList<WidgetLayoutItem> Normalize(IReadOnlyList<WidgetLayoutItem> saved)
    {
        if (saved.Count == 0) return Defaults();
        var list = saved.OrderBy(w => w.SortOrder).ToList();
        foreach (var missing in Defaults().Where(d => list.All(w => w.Kind != d.Kind)))
        {
            missing.SortOrder = list.Count;
            missing.IsEnabled = false;
            list.Add(missing);
        }
        return list;
    }

    public static void Move<T>(IList<T> items, int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= items.Count || to >= items.Count) return;
        var item = items[from];
        items.RemoveAt(from);
        items.Insert(to, item);
    }
}
