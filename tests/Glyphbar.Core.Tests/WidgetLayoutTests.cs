using Glyphbar.Core.Models;
using Glyphbar.Core.Services;

namespace Glyphbar.Core.Tests;

public class WidgetLayoutTests
{
    [Fact]
    public void Packing_never_overlaps()
    {
        var sizes = new[] { WidgetSize.Large, WidgetSize.Small, WidgetSize.Medium, WidgetSize.Small, WidgetSize.Large, WidgetSize.Medium };
        foreach (int columns in new[] { 2, 3, 4, 6 })
        {
            var used = new HashSet<(int, int)>();
            foreach (var c in WidgetLayoutEngine.Pack(sizes, columns))
            {
                Assert.True(c.Column + c.ColumnSpan <= columns);
                for (int y = c.Row; y < c.Row + c.RowSpan; y++)
                    for (int x = c.Column; x < c.Column + c.ColumnSpan; x++)
                        Assert.True(used.Add((x, y)), $"Overlap at {x},{y} with {columns} columns");
            }
        }
    }

    [Fact]
    public void Packing_backfills_gaps()
    {
        var cells = WidgetLayoutEngine.Pack([WidgetSize.Large, WidgetSize.Small, WidgetSize.Small], 3);
        Assert.Equal(new WidgetCell(2, 0, 1, 1), cells[1]);
        Assert.Equal(new WidgetCell(2, 1, 1, 1), cells[2]);
    }

    [Fact]
    public void Wide_widget_is_clamped_to_a_single_column_grid()
    {
        var cell = WidgetLayoutEngine.Pack([WidgetSize.Large], 1)[0];
        Assert.Equal(1, cell.ColumnSpan);
    }

    [Fact]
    public void Next_size_cycles_through_allowed_sizes_only()
    {
        Assert.Equal(WidgetSize.Large, WidgetLayoutEngine.NextSize(WidgetKind.Calendar, WidgetSize.Medium));
        Assert.Equal(WidgetSize.Medium, WidgetLayoutEngine.NextSize(WidgetKind.Calendar, WidgetSize.Large));
        Assert.Equal(WidgetSize.Small, WidgetLayoutEngine.NextSize(WidgetKind.Date, WidgetSize.Medium));
    }

    [Fact]
    public void Defaults_contain_every_required_widget_enabled()
    {
        var d = WidgetLayoutEngine.Defaults();
        foreach (var kind in new[] { WidgetKind.Clock, WidgetKind.Date, WidgetKind.Calendar, WidgetKind.Battery, WidgetKind.Storage, WidgetKind.Music })
            Assert.True(d.Single(w => w.Kind == kind).IsEnabled);
    }

    [Fact]
    public void Normalize_adds_new_widgets_disabled_and_keeps_saved_order()
    {
        var saved = new List<WidgetLayoutItem> { new() { Kind = WidgetKind.Music, SortOrder = 0 }, new() { Kind = WidgetKind.Clock, SortOrder = 1 } };
        var result = WidgetLayoutEngine.Normalize(saved);
        Assert.Equal(WidgetKind.Music, result[0].Kind);
        Assert.Equal(7, result.Count);
        Assert.False(result.Single(w => w.Kind == WidgetKind.Battery).IsEnabled);
    }

    [Fact]
    public void Move_ignores_out_of_range()
    {
        var list = new List<int> { 1, 2, 3 };
        WidgetLayoutEngine.Move(list, 0, 2);
        Assert.Equal([2, 3, 1], list);
        WidgetLayoutEngine.Move(list, -1, 2);
        WidgetLayoutEngine.Move(list, 0, 9);
        Assert.Equal([2, 3, 1], list);
    }
}
