using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glyphbar.App.Services;
using Glyphbar.App.Views;
using Glyphbar.Core.Helpers;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;

namespace Glyphbar.App.ViewModels;

public sealed partial class ClockWidgetViewModel(ClockService clock, ISettingsStore settings) : ObservableObject
{
    private IDisposable? _seconds;
    private WidgetSize _size = WidgetSize.Medium;

    [ObservableProperty] public partial string Time { get; set; } = "";
    [ObservableProperty] public partial string Meridiem { get; set; } = "";
    [ObservableProperty] public partial string DayOfWeek { get; set; } = "";
    [ObservableProperty] public partial string FullDate { get; set; } = "";
    [ObservableProperty] public partial double DotSize { get; set; } = 5;
    [ObservableProperty] public partial double DotGap { get; set; } = 1.8;
    [ObservableProperty] public partial double PlainSize { get; set; } = 44;
    [ObservableProperty] public partial bool UseDots { get; set; } = true;
    [ObservableProperty] public partial bool UsePlain { get; set; }

    public void SetSize(WidgetSize size)
    {
        _size = size;
        Update();
    }

    public void Activate()
    {
        clock.Tick += OnTick;
        settings.Changed += OnTick;
        if (settings.Current.Widgets.ShowSeconds) _seconds = clock.RequestSeconds();
        Update();
    }

    public void Deactivate()
    {
        clock.Tick -= OnTick;
        settings.Changed -= OnTick;
        _seconds?.Dispose();
        _seconds = null;
    }

    private void OnTick(object? sender, EventArgs e) => Update();

    private void Update()
    {
        var w = settings.Current.Widgets;
        var now = ClockFormatter.ToZone(clock.Now, w.TimeZoneId).DateTime;
        var culture = CultureInfo.CurrentCulture;
        bool seconds = w.ShowSeconds && _size != WidgetSize.Small;
        var text = ClockFormatter.Format(now, w.ClockFormat, seconds, culture);
        Time = text.Time;
        Meridiem = text.Meridiem ?? "";
        DayOfWeek = now.ToString("dddd", culture);
        FullDate = now.ToString("D", culture);
        UseDots = w.DotMatrixClock;
        UsePlain = !w.DotMatrixClock;
        (DotSize, DotGap, PlainSize) = _size switch
        {
            WidgetSize.Small => (3.2, 1.2, 30d),
            WidgetSize.Large => (seconds ? 6.4 : 8.5, 2.2, 60d),
            _ => (seconds ? 5.2 : 6.5, 1.8, 46d),
        };
    }
}

public sealed partial class DateWidgetViewModel(ClockService clock, ISettingsStore settings) : ObservableObject
{
    [ObservableProperty] public partial string Weekday { get; set; } = "";
    [ObservableProperty] public partial string Day { get; set; } = "";
    [ObservableProperty] public partial string Month { get; set; } = "";
    [ObservableProperty] public partial string Year { get; set; } = "";

    private bool _expanded;

    public void SetSize(WidgetSize size)
    {
        _expanded = size != WidgetSize.Small;
        Update();
    }

    public void Activate()
    {
        clock.Tick += OnTick;
        Update();
    }

    public void Deactivate() => clock.Tick -= OnTick;

    private void OnTick(object? sender, EventArgs e) => Update();

    private void Update()
    {
        var now = ClockFormatter.ToZone(clock.Now, settings.Current.Widgets.TimeZoneId).DateTime;
        var c = CultureInfo.CurrentCulture;
        Weekday = now.ToString(_expanded ? "dddd" : "ddd", c).ToUpper(c);
        Day = now.Day.ToString(CultureInfo.InvariantCulture);
        Month = now.ToString(_expanded ? "MMMM" : "MMM", c);
        Year = now.Year.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed partial class BatteryWidgetViewModel(SystemMonitor monitor, ISettingsStore settings, UiDispatcher ui) : ObservableObject
{
    [ObservableProperty] public partial bool HasBattery { get; set; }
    [ObservableProperty] public partial bool NoBattery { get; set; }
    [ObservableProperty] public partial double Percent { get; set; }
    [ObservableProperty] public partial string PercentText { get; set; } = "";
    [ObservableProperty] public partial bool Charging { get; set; }
    [ObservableProperty] public partial bool Warning { get; set; }
    [ObservableProperty] public partial string Detail { get; set; } = "";
    [ObservableProperty] public partial string LevelLabel { get; set; } = "";

    public void Activate()
    {
        monitor.BatteryChanged += OnChanged;
        Update();
    }

    public void Deactivate() => monitor.BatteryChanged -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => ui.Post(Update);

    private void Update()
    {
        var b = monitor.Battery;
        HasBattery = b.HasBattery && b.Percent is not null;
        NoBattery = !HasBattery;
        if (!HasBattery)
        {
            Detail = b.OnAcPower == true ? "On AC power" : "";
            LevelLabel = "";
            return;
        }

        int percent = b.Percent!.Value;
        var level = BatteryInterpreter.Classify(percent, settings.Current.Widgets.BatteryThresholds);
        Percent = percent;
        PercentText = $"{percent}%";
        Charging = b.IsCharging;
        Warning = !b.IsCharging && level is BatteryLevel.Low or BatteryLevel.Critical;
        LevelLabel = b.IsCharging ? "" : level switch
        {
            BatteryLevel.Low => "LOW",
            BatteryLevel.Critical => "CRITICAL",
            _ => "",
        };
        Detail = string.Join(" · ", new[]
        {
            b.IsCharging ? "Charging" : b.OnAcPower == true ? "Plugged in" : b.OnAcPower == false ? "On battery" : null,
            b.Remaining is { } r ? BatteryFormat.Remaining(r) : null,
            b.BatterySaverOn ? "Saver on" : null,
        }.Where(p => p is not null));
    }
}

public sealed record DriveViewModel(DriveSnapshot Drive)
{
    public string Glyph => Drive.IsSystem ? "" : Drive.DriveType == "Removable" ? "" : Drive.DriveType == "Network" ? "" : "";
    public string Heading => $"{Drive.Letter} {(Drive.IsSystem ? "SYSTEM" : Drive.Label.ToUpperInvariant())}".Trim();
    public bool IsReady => Drive.State == DriveState.Ready;
    public double Percent => Drive.PercentUsed;
    public string PercentText => IsReady ? $"{Drive.PercentUsed:0}%" : "";
    public string Detail => Drive.State switch
    {
        DriveState.Ready => $"{ByteSize.Format(Drive.TotalBytes)} total · {ByteSize.Format(Drive.UsedBytes)} used · {ByteSize.Format(Drive.FreeBytes)} free",
        DriveState.Inaccessible => "Not accessible",
        _ => "Not ready",
    };
}

public sealed partial class StorageWidgetViewModel(SystemMonitor monitor, UiDispatcher ui) : ObservableObject
{
    private IDisposable? _watch;

    public ObservableCollection<DriveViewModel> Drives { get; } = [];

    public void Activate()
    {
        monitor.DrivesChanged += OnChanged;
        _watch = monitor.WatchStorage();
        Update();
    }

    public void Deactivate()
    {
        monitor.DrivesChanged -= OnChanged;
        _watch?.Dispose();
        _watch = null;
    }

    [RelayCommand] private void Refresh() => monitor.RefreshStorageNow();

    private void OnChanged(object? sender, EventArgs e) => ui.Post(Update);

    private void Update()
    {
        Drives.Clear();
        foreach (var d in monitor.Drives) Drives.Add(new DriveViewModel(d));
    }
}

public sealed partial class MusicWidgetViewModel(IMediaService media, UiDispatcher ui) : ObservableObject
{
    private string? _pinnedSessionId;
    private string? _artworkKey;
    private bool _updatingSelector;
    private MediaSessionInfo? _session;
    private WidgetSize _size = WidgetSize.Medium;

    public ObservableCollection<string> SessionNames { get; } = [];

    [ObservableProperty] public partial bool HasSession { get; set; }
    [ObservableProperty] public partial bool IsIdle { get; set; } = true;
    [ObservableProperty] public partial bool ShowSelector { get; set; }
    [ObservableProperty] public partial bool ShowArtSlot { get; set; } = true;
    [ObservableProperty] public partial double ArtSize { get; set; } = 72;
    [ObservableProperty] public partial int SelectedSessionIndex { get; set; } = -1;
    [ObservableProperty] public partial string SourceLabel { get; set; } = "MUSIC";
    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string Artist { get; set; } = "";
    [ObservableProperty] public partial string Album { get; set; } = "";
    [ObservableProperty] public partial ImageSource? Artwork { get; set; }
    [ObservableProperty] public partial string PlayGlyph { get; set; } = "";
    [ObservableProperty] public partial bool CanPlayPause { get; set; }
    [ObservableProperty] public partial bool CanNext { get; set; }
    [ObservableProperty] public partial bool CanPrevious { get; set; }

    public void SetSize(WidgetSize size)
    {
        _size = size;
        ShowArtSlot = size != WidgetSize.Small;
        ArtSize = size == WidgetSize.Large ? 150 : 72;
    }

    public void Activate()
    {
        media.Changed += OnChanged;
        Update();
    }

    public void Deactivate() => media.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => ui.Post(Update);

    partial void OnSelectedSessionIndexChanged(int value)
    {
        if (_updatingSelector || value < 0 || value >= media.Sessions.Count) return;
        _pinnedSessionId = media.Sessions[value].SessionId;
        Update();
    }

    private void Update()
    {
        var sessions = media.Sessions;
        _session = sessions.FirstOrDefault(s => s.SessionId == _pinnedSessionId)
            ?? sessions.FirstOrDefault(s => s.SessionId == media.CurrentSessionId)
            ?? sessions.FirstOrDefault();

        _updatingSelector = true;
        SessionNames.Clear();
        foreach (var s in sessions) SessionNames.Add(s.SourceName);
        SelectedSessionIndex = _session is null ? -1 : sessions.ToList().IndexOf(_session);
        _updatingSelector = false;
        ShowSelector = sessions.Count > 1;

        HasSession = _session is not null;
        IsIdle = !HasSession;
        if (_session is not { } m)
        {
            SourceLabel = "MUSIC";
            Artwork = null;
            _artworkKey = null;
            return;
        }

        SourceLabel = m.SourceName.ToUpperInvariant();
        Title = m.Title ?? "Unknown title";
        Artist = m.Artist ?? "";
        Album = m.Album ?? "";
        bool playing = m.Playback == MediaPlayback.Playing;
        PlayGlyph = playing ? "" : "";
        CanPlayPause = playing ? m.CanPause : m.CanPlay;
        CanNext = m.CanNext;
        CanPrevious = m.CanPrevious;
        _ = LoadArtworkAsync(m);
    }

    private async Task LoadArtworkAsync(MediaSessionInfo m)
    {
        string key = $"{m.SessionId}|{m.Title}|{m.Album}|{m.Artwork?.Length}";
        if (key == _artworkKey) return;
        _artworkKey = key;
        try { Artwork = m.Artwork is null ? null : await IconLoader.FromBytesAsync(m.Artwork); }
        catch (Exception) { Artwork = null; }   // a corrupt thumbnail shows the placeholder
    }

    [RelayCommand] private Task PlayPause() => _session is { } s ? media.PlayPauseAsync(s.SessionId) : Task.CompletedTask;
    [RelayCommand] private Task Next() => _session is { } s ? media.NextAsync(s.SessionId) : Task.CompletedTask;
    [RelayCommand] private Task Previous() => _session is { } s ? media.PreviousAsync(s.SessionId) : Task.CompletedTask;
}

public sealed partial class SystemInfoWidgetViewModel(SystemMonitor monitor, ISettingsStore settings, UiDispatcher ui) : ObservableObject
{
    private IDisposable? _watch;

    [ObservableProperty] public partial string Cpu { get; set; } = "CPU";
    [ObservableProperty] public partial double CpuPercent { get; set; }
    [ObservableProperty] public partial string Memory { get; set; } = "Memory";
    [ObservableProperty] public partial double MemoryPercent { get; set; }
    [ObservableProperty] public partial string Gpu { get; set; } = "";
    [ObservableProperty] public partial string Network { get; set; } = "";
    [ObservableProperty] public partial bool MonitoringOn { get; set; }
    [ObservableProperty] public partial bool MonitoringOff { get; set; }

    public void Activate()
    {
        monitor.SystemInfoChanged += OnChanged;
        _watch = monitor.WatchSystemInfo();
        Update();
    }

    public void Deactivate()
    {
        monitor.SystemInfoChanged -= OnChanged;
        _watch?.Dispose();
        _watch = null;
    }

    private void OnChanged(object? sender, EventArgs e) => ui.Post(Update);

    private void Update()
    {
        MonitoringOn = settings.Current.Widgets.ResourceMonitoringEnabled;
        MonitoringOff = !MonitoringOn;
        if (monitor.SystemInfo is not { } s) return;
        CpuPercent = s.CpuPercent ?? 0;
        Cpu = s.CpuPercent is { } c ? $"CPU {c:0}%" : "CPU";
        if (s.MemoryUsedBytes is { } used && s.MemoryTotalBytes is { } total && total > 0)
        {
            MemoryPercent = 100d * used / total;
            Memory = $"Memory {ByteSize.Format(used)} of {ByteSize.Format(total)}";
        }
        Gpu = s.GpuName is null ? "GPU not reported" : $"GPU {s.GpuName}";
        Network = s.NetworkConnected switch
        {
            true => $"Connected · {s.NetworkName}",
            false => "No network connection",
            _ => "Network status unavailable",
        };
    }
}

public sealed class CalendarDayViewModel
{
    public required DateOnly Date { get; init; }
    public required string Label { get; init; }
    public bool InMonth { get; init; }
    public bool IsToday { get; init; }
    public bool IsSelected { get; init; }
    public bool HasEvents { get; init; }
}

public sealed record CalendarEventViewModel(CalendarEvent Event, string When)
{
    public string Title => Event.Title;
    public override string ToString() => $"{Title}, {When}";
}

public sealed partial class CalendarWidgetViewModel(ICalendarRepository repository, ClockService clock, ILogger<CalendarWidgetViewModel> logger) : ObservableObject
{
    private const int UpcomingCount = 5;

    private DateOnly _month;
    private DateOnly _lastToday;
    private List<CalendarEvent> _monthEvents = [];
    private bool _compact;

    public ObservableCollection<string> Weekdays { get; } = [];
    public ObservableCollection<CalendarEventViewModel> VisibleEvents { get; } = [];
    public List<CalendarDayViewModel> Days { get; private set; } = [];
    public DateOnly SelectedDate { get; private set; }
    public event Action? DaysChanged;
    public event Action? MonthChanged;

    [ObservableProperty] public partial string MonthTitle { get; set; } = "";
    [ObservableProperty] public partial string EventsHeading { get; set; } = "";
    [ObservableProperty] public partial string EmptyText { get; set; } = "";
    [ObservableProperty] public partial bool ShowEmpty { get; set; }
    [ObservableProperty] public partial bool ShowGrid { get; set; } = true;

    private DateOnly CurrentDate => DateOnly.FromDateTime(clock.Now.DateTime);

    public void SetSize(WidgetSize size)
    {
        _compact = size != WidgetSize.Large;
        ShowGrid = !_compact;
    }

    public void Activate()
    {
        clock.Tick += OnTick;
        if (SelectedDate == default)
        {
            SelectedDate = CurrentDate;
            _month = new DateOnly(CurrentDate.Year, CurrentDate.Month, 1);
        }
        _lastToday = CurrentDate;
        var c = CultureInfo.CurrentCulture;
        Weekdays.Clear();
        foreach (var d in MonthGrid.WeekdayLabels(c.DateTimeFormat.FirstDayOfWeek, c)) Weekdays.Add(d.ToUpper(c));
        _ = ReloadAsync();
    }

    public void Deactivate() => clock.Tick -= OnTick;

    private void OnTick(object? sender, EventArgs e)
    {
        if (CurrentDate == _lastToday) return;
        _lastToday = CurrentDate;
        _ = ReloadAsync();
    }

    [RelayCommand] private void PreviousMonth() => Navigate(_month.AddMonths(-1));
    [RelayCommand] private void NextMonth() => Navigate(_month.AddMonths(1));
    [RelayCommand] private void PreviousYear() => Navigate(_month.AddYears(-1));
    [RelayCommand] private void NextYear() => Navigate(_month.AddYears(1));

    [RelayCommand]
    private void Today()
    {
        SelectedDate = CurrentDate;
        Navigate(new DateOnly(CurrentDate.Year, CurrentDate.Month, 1));
    }

    private void Navigate(DateOnly month)
    {
        _month = month;
        MonthChanged?.Invoke();
        _ = ReloadAsync();
    }

    public void Select(DateOnly date)
    {
        SelectedDate = date;
        if (date.Month != _month.Month || date.Year != _month.Year) Navigate(new DateOnly(date.Year, date.Month, 1));
        else _ = ReloadAsync();
    }

    public async Task EditAsync(EventEditResult edit)
    {
        try
        {
            switch (edit.Action)
            {
                case EventEditAction.Save:
                    await repository.SaveAsync(edit.Event!);
                    break;
                case EventEditAction.Delete:
                    await repository.DeleteAsync(edit.Event!.Id);
                    break;
                default:
                    return;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not save the calendar event");
        }
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var c = CultureInfo.CurrentCulture;
        var cells = MonthGrid.Build(_month.Year, _month.Month, c.DateTimeFormat.FirstDayOfWeek);
        var from = cells[0].Date.ToDateTime(TimeOnly.MinValue);
        var to = cells[^1].Date.AddDays(1).ToDateTime(TimeOnly.MinValue);

        IReadOnlyList<CalendarEvent> upcoming = [];
        try
        {
            _monthEvents = (await repository.GetRangeAsync(from, to)).ToList();
            if (_compact) upcoming = await repository.GetUpcomingAsync(clock.Now.DateTime, UpcomingCount);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Could not read calendar events");
        }

        MonthTitle = _month.ToString("MMMM yyyy", c);
        Days = cells.Select(cell => new CalendarDayViewModel
        {
            Date = cell.Date,
            Label = cell.Date.Day.ToString(c),
            InMonth = cell.InMonth,
            IsToday = cell.Date == CurrentDate,
            IsSelected = cell.Date == SelectedDate,
            HasEvents = _monthEvents.Any(e => Covers(e, cell.Date)),
        }).ToList();
        DaysChanged?.Invoke();

        var visible = _compact ? upcoming : _monthEvents.Where(e => Covers(e, SelectedDate)).ToList();
        EventsHeading = _compact ? "UPCOMING" : SelectedDate.ToString("dddd d MMMM", c).ToUpper(c);
        VisibleEvents.Clear();
        foreach (var e in visible) VisibleEvents.Add(new CalendarEventViewModel(e, Describe(e, _compact, c)));
        ShowEmpty = VisibleEvents.Count == 0;
        EmptyText = _compact ? "No upcoming events" : "No events";
    }

    private static bool Covers(CalendarEvent e, DateOnly date) =>
        DateOnly.FromDateTime(e.Start) <= date && date <= DateOnly.FromDateTime(e.End);

    private static string Describe(CalendarEvent e, bool withDate, CultureInfo c)
    {
        string time = e.IsAllDay ? "All day" : $"{e.Start.ToString("t", c)} – {e.End.ToString("t", c)}";
        return withDate ? $"{e.Start.ToString("ddd d MMM", c)} · {time}" : time;
    }
}
