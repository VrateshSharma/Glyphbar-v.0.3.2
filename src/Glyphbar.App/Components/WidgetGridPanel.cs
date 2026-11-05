using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Glyphbar.App.Components;

/// <summary>Lays out widgets by span, packed by <see cref="WidgetLayoutEngine"/>.</summary>
public sealed class WidgetGridPanel : Panel
{
    private const double MinCellWidth = 150, CellHeight = 172, Spacing = 12;

    private IReadOnlyList<WidgetCell> _cells = [];
    private int _columns = 2;

    public int MaxColumns { get; set; } = 6;

    private IReadOnlyList<WidgetSize> Sizes() =>
        Children.OfType<WidgetHost>().Select(h => h.WidgetSize).ToList();

    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsInfinity(available.Width) ? MaxColumns * (MinCellWidth + Spacing) : available.Width;
        _columns = Math.Clamp((int)((width + Spacing) / (MinCellWidth + Spacing)), 2, MaxColumns);
        _cells = WidgetLayoutEngine.Pack(Sizes(), _columns);
        double cell = (width - (_columns - 1) * Spacing) / _columns;

        int rows = 0;
        for (int i = 0; i < Children.Count && i < _cells.Count; i++)
        {
            var c = _cells[i];
            Children[i].Measure(new Size(Span(cell, c.ColumnSpan), Span(CellHeight, c.RowSpan)));
            rows = Math.Max(rows, c.Row + c.RowSpan);
        }
        return new Size(width, rows == 0 ? 0 : Span(CellHeight, rows));
    }

    protected override Size ArrangeOverride(Size final)
    {
        double cell = (final.Width - (_columns - 1) * Spacing) / _columns;
        for (int i = 0; i < Children.Count && i < _cells.Count; i++)
        {
            var c = _cells[i];
            Children[i].Arrange(new Rect(
                c.Column * (cell + Spacing),
                c.Row * (CellHeight + Spacing),
                Span(cell, c.ColumnSpan),
                Span(CellHeight, c.RowSpan)));
        }
        return final;
    }

    private static double Span(double size, int count) => size * count + Spacing * (count - 1);
}
