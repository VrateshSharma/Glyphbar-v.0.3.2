using Glyphbar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;

namespace Glyphbar.App.Components;

/// <summary>Draws text as dots from <see cref="DotMatrixFont"/>, as vector geometry that stays sharp at any DPI.</summary>
public sealed class DotMatrixText : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(DotMatrixText), new PropertyMetadata("", (d, _) => ((DotMatrixText)d).Rebuild()));
    public static readonly DependencyProperty DotSizeProperty = DependencyProperty.Register(
        nameof(DotSize), typeof(double), typeof(DotMatrixText), new PropertyMetadata(4d, (d, _) => ((DotMatrixText)d).Rebuild()));
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(DotMatrixText), new PropertyMetadata(1.5d, (d, _) => ((DotMatrixText)d).Rebuild()));
    public static readonly DependencyProperty DotBrushProperty = DependencyProperty.Register(
        nameof(DotBrush), typeof(Brush), typeof(DotMatrixText), new PropertyMetadata(null, (d, e) => ((DotMatrixText)d)._path.Fill = (Brush)e.NewValue));

    private readonly ShapePath _path = new();

    public DotMatrixText()
    {
        _path.Fill = (Brush)Application.Current.Resources["GbTextBrush"];
        Content = _path;
        IsTabStop = false;
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double DotSize { get => (double)GetValue(DotSizeProperty); set => SetValue(DotSizeProperty, value); }
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public Brush? DotBrush { get => (Brush?)GetValue(DotBrushProperty); set => SetValue(DotBrushProperty, value); }

    private void Rebuild()
    {
        var (dots, columns) = DotMatrixFont.Layout(Text ?? "");
        double pitch = DotSize + Gap, radius = DotSize / 2;
        var group = new GeometryGroup();
        foreach (var (x, y) in dots)
        {
            group.Children.Add(new EllipseGeometry
            {
                Center = new Point(x * pitch + radius, y * pitch + radius),
                RadiusX = radius,
                RadiusY = radius,
            });
        }
        _path.Data = group;
        _path.Width = Math.Max(0, columns * pitch - Gap);
        _path.Height = DotMatrixFont.Height * pitch - Gap;
    }
}
