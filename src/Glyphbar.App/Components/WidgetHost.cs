using Glyphbar.App.Components.Widgets;
using Glyphbar.App.Services;
using Glyphbar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace Glyphbar.App.Components;

/// <summary>The card around a widget, plus its edit controls and drag-to-reorder.</summary>
public sealed class WidgetHost : UserControl
{
    private const string DragFormat = "GlyphbarWidget";

    private readonly WidgetLayoutService _layout;
    private readonly StackPanel _editBar;
    private readonly Border _card;

    public WidgetHost(WidgetLayoutItem item, IWidgetView view, WidgetLayoutService layout)
    {
        Item = item;
        View = view;
        _layout = layout;

        _card = new Border
        {
            Style = (Style)Application.Current.Resources["GbCard"],
            Child = (UIElement)view,
        };

        _editBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(6),
            Spacing = 2,
            Visibility = Visibility.Collapsed,
        };
        _editBar.Children.Add(EditButton("", "Change size", () => _layout.CycleSize(Item.Kind)));
        _editBar.Children.Add(EditButton("", "Hide widget", () => _layout.SetEnabled(Item.Kind, false)));

        var grid = new Grid();
        grid.Children.Add(_card);
        grid.Children.Add(_editBar);
        Content = grid;

        AllowDrop = true;
        DragStarting += (_, e) => e.Data.SetData(DragFormat, Item.Kind.ToString());
        DragOver += (_, e) => e.AcceptedOperation = DataPackageOperation.Move;
        Drop += OnDrop;
    }

    public WidgetLayoutItem Item { get; }
    public IWidgetView View { get; }
    public WidgetSize WidgetSize => Item.Size;

    private static Button EditButton(string glyph, string tip, Action action)
    {
        var button = new Button
        {
            Style = (Style)Application.Current.Resources["GbIconButton"],
            Content = new FontIcon { Glyph = glyph, FontSize = 13 },
            Background = (Brush)Application.Current.Resources["GbElevatedBrush"],
        };
        ToolTipService.SetToolTip(button, tip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tip);
        button.Click += (_, _) => action();
        return button;
    }

    public void SetEditing(bool editing)
    {
        CanDrag = editing;
        _editBar.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        _card.BorderBrush = (Brush)Application.Current.Resources[editing ? "GbTextMutedBrush" : "GbBorderBrush"];
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(DragFormat)) return;
        if (await e.DataView.GetDataAsync(DragFormat) is string text && Enum.TryParse<WidgetKind>(text, out var kind) && kind != Item.Kind)
            _layout.MoveBefore(kind, Item.Kind);
    }
}
