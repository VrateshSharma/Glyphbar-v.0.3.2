namespace Glyphbar.Core.Models;

public enum WidgetKind { Clock, Date, Calendar, Battery, Storage, Music, SystemInfo }

public enum WidgetSize { Small, Medium, Large }

public sealed class WidgetLayoutItem
{
    public long Id { get; set; }
    public WidgetKind Kind { get; set; }
    public bool IsEnabled { get; set; } = true;
    public WidgetSize Size { get; set; } = WidgetSize.Medium;
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int SortOrder { get; set; }
}

public sealed class CalendarEvent
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool IsAllDay { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum SearchCategory { Application, Setting, Folder, File, Recent }

public sealed record SearchResult(
    SearchCategory Category,
    string Title,
    string? Subtitle,
    string Target,
    double Score,
    AppEntry? App = null);

public sealed record FileSearchOutcome(IReadOnlyList<SearchResult> Results, bool ProviderAvailable);
