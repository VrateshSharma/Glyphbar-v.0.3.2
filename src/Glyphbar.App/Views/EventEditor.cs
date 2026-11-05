using Glyphbar.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glyphbar.App.Views;

public enum EventEditAction { None, Save, Delete }

public sealed record EventEditResult(EventEditAction Action, CalendarEvent? Event);

/// <summary>Create or edit dialog for a local calendar event.</summary>
public static class EventEditor
{
    public static async Task<EventEditResult> ShowAsync(XamlRoot root, CalendarEvent? existing, DateOnly date)
    {
        var start = existing?.Start ?? date.ToDateTime(new TimeOnly(9, 0));
        var end = existing?.End ?? start.AddHours(1);

        var title = new TextBox { Header = "Title", Text = existing?.Title ?? "", MaxLength = 200 };
        var description = new TextBox { Header = "Description", Text = existing?.Description ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 90 };
        var allDay = new ToggleSwitch { Header = "All day", IsOn = existing?.IsAllDay ?? false };
        var startDate = new CalendarDatePicker { Header = "Starts", Date = start, HorizontalAlignment = HorizontalAlignment.Stretch };
        var startTime = new TimePicker { Time = start.TimeOfDay, MinuteIncrement = 5 };
        var endDate = new CalendarDatePicker { Header = "Ends", Date = end, HorizontalAlignment = HorizontalAlignment.Stretch };
        var endTime = new TimePicker { Time = end.TimeOfDay, MinuteIncrement = 5 };
        var error = new TextBlock { Style = (Style)Application.Current.Resources["GbCaption"], TextWrapping = TextWrapping.Wrap };

        void SyncAllDay() => startTime.Visibility = endTime.Visibility = allDay.IsOn ? Visibility.Collapsed : Visibility.Visible;
        allDay.Toggled += (_, _) => SyncAllDay();
        SyncAllDay();

        var form = new StackPanel { Spacing = 10, MinWidth = 300 };
        foreach (var child in new UIElement[] { title, description, allDay, startDate, startTime, endDate, endTime, error })
            form.Children.Add(child);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = existing is null ? "New event" : "Edit event",
            Content = new ScrollViewer { Content = form, MaxHeight = 420 },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            SecondaryButtonText = existing is null ? "" : "Delete",
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = ElementTheme.Dark,
        };

        CalendarEvent? result = null;
        dialog.PrimaryButtonClick += (_, e) =>
        {
            var s = Compose(startDate, allDay.IsOn ? TimeSpan.Zero : startTime.Time);
            var f = Compose(endDate, allDay.IsOn ? new TimeSpan(23, 59, 59) : endTime.Time);
            if (string.IsNullOrWhiteSpace(title.Text)) error.Text = "Add a title.";
            else if (s is null || f is null) error.Text = "Choose start and end dates.";
            else if (f < s) error.Text = "The event ends before it starts.";
            else
            {
                result = new CalendarEvent
                {
                    Id = existing?.Id ?? 0,
                    Title = title.Text.Trim(),
                    Description = string.IsNullOrWhiteSpace(description.Text) ? null : description.Text,
                    Start = s.Value,
                    End = f.Value,
                    IsAllDay = allDay.IsOn,
                    CreatedAt = existing?.CreatedAt ?? default,
                };
                return;
            }
            e.Cancel = true;
        };

        var choice = await dialog.ShowAsync();
        return choice switch
        {
            ContentDialogResult.Primary when result is not null => new EventEditResult(EventEditAction.Save, result),
            ContentDialogResult.Secondary => new EventEditResult(EventEditAction.Delete, existing),
            _ => new EventEditResult(EventEditAction.None, null),
        };
    }

    private static DateTime? Compose(CalendarDatePicker picker, TimeSpan time) =>
        picker.Date is { } d ? d.Date + time : null;
}
