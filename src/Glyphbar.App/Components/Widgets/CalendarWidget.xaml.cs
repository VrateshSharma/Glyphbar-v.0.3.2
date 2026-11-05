using System.ComponentModel;
using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Glyphbar.App.Views;
using Glyphbar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Glyphbar.App.Components.Widgets;

public sealed partial class CalendarWidget : UserControl, IWidgetView
{
    private const double DayCellHeight = 30;

    public CalendarWidget(CalendarWidgetViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        for (int c = 0; c < 7; c++)
        {
            DayGrid.ColumnDefinitions.Add(new ColumnDefinition());
            WeekdayGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }
        for (int r = 0; r < 6; r++) DayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DayCellHeight) });
        ViewModel.DaysChanged += RenderDays;
        ViewModel.MonthChanged += () => Motion.Enter(MonthPanel, 6, 160);
    }

    public CalendarWidgetViewModel ViewModel { get; }
    public void Activate() => ViewModel.Activate();
    public void Deactivate() => ViewModel.Deactivate();
    public void ApplySize(WidgetSize size) => ViewModel.SetSize(size);

    /// <summary>The month is a fixed 6x7 grid, so cells are built here rather than through virtualisation.</summary>
    private void RenderDays()
    {
        WeekdayGrid.Children.Clear();
        for (int c = 0; c < ViewModel.Weekdays.Count; c++)
        {
            var label = new TextBlock
            {
                Text = ViewModel.Weekdays[c],
                Style = (Style)Application.Current.Resources["GbLabel"],
                TextAlignment = TextAlignment.Center,
            };
            Grid.SetColumn(label, c);
            WeekdayGrid.Children.Add(label);
        }

        DayGrid.Children.Clear();
        var text = (Brush)Application.Current.Resources["GbTextBrush"];
        var muted = (Brush)Application.Current.Resources["GbTextMutedBrush"];
        var inverse = (Brush)Application.Current.Resources["GbBackgroundBrush"];

        for (int i = 0; i < ViewModel.Days.Count; i++)
        {
            var day = ViewModel.Days[i];
            var cell = new Grid { Width = 28, Height = 28 };
            cell.Children.Add(new Ellipse
            {
                Fill = day.IsToday ? text : null,
                Stroke = day.IsSelected && !day.IsToday ? text : null,
                StrokeThickness = 1,
            });
            cell.Children.Add(new TextBlock
            {
                Text = day.Label,
                FontSize = 12,
                Foreground = day.IsToday ? inverse : day.InMonth ? text : muted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            if (day.HasEvents)
            {
                cell.Children.Add(new Ellipse
                {
                    Width = 4,
                    Height = 4,
                    Fill = day.IsToday ? inverse : text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 2),
                });
            }

            var button = new Button
            {
                Style = (Style)Application.Current.Resources["GbIconButton"],
                Content = cell,
                MinWidth = 0,
                MinHeight = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            AutomationName(button, day);
            var date = day.Date;
            button.Click += (_, _) => ViewModel.Select(date);
            Grid.SetRow(button, i / 7);
            Grid.SetColumn(button, i % 7);
            DayGrid.Children.Add(button);
        }
    }

    private static void AutomationName(Button button, CalendarDayViewModel day) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, day.Date.ToLongDateString());

    private async void OnAddClick(object sender, RoutedEventArgs e) =>
        await ViewModel.EditAsync(await EventEditor.ShowAsync(XamlRoot, null, ViewModel.SelectedDate));

    private async void OnEventClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CalendarEventViewModel item)
            await ViewModel.EditAsync(await EventEditor.ShowAsync(XamlRoot, item.Event, ViewModel.SelectedDate));
    }
}
