using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Glyphbar.Data.Configuration;
using Glyphbar.Data.Database;
using Glyphbar.Data.Migrations;
using Glyphbar.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Glyphbar.Data.Tests;

public sealed class TempData : IDisposable
{
    public TempData()
    {
        Root = Path.Combine(Path.GetTempPath(), "glyphbar-tests", Guid.NewGuid().ToString("N"));
        Paths = new AppPaths(Root);
        Database = new GlyphbarDatabase(Paths, NullLogger<GlyphbarDatabase>.Instance);
    }

    public string Root { get; }
    public AppPaths Paths { get; }
    public GlyphbarDatabase Database { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Root, true); }
        catch (IOException) { /* a leftover temp folder doesn't matter */ }
    }
}

public class CalendarRepositoryTests
{
    private static CalendarEvent Event(string title, DateTime start, int hours = 1) =>
        new() { Title = title, Start = start, End = start.AddHours(hours) };

    [Fact]
    public async Task Create_edit_delete_round_trip()
    {
        using var t = new TempData();
        var repo = new CalendarRepository(t.Database);
        var day = new DateTime(2026, 11, 5, 9, 0, 0);

        var e = Event("Standup", day);
        e.Description = "Notes";
        long id = await repo.SaveAsync(e);
        Assert.True(id > 0);

        var read = (await repo.GetRangeAsync(day.Date, day.Date.AddDays(1))).Single();
        Assert.Equal("Standup", read.Title);
        Assert.Equal("Notes", read.Description);
        Assert.Equal(day, read.Start);

        read.Title = "Renamed";
        await repo.SaveAsync(read);
        Assert.Equal("Renamed", (await repo.GetRangeAsync(day.Date, day.Date.AddDays(1))).Single().Title);

        await repo.DeleteAsync(id);
        Assert.Empty(await repo.GetRangeAsync(day.Date, day.Date.AddDays(1)));
    }

    [Fact]
    public async Task Range_includes_events_overlapping_its_edges()
    {
        using var t = new TempData();
        var repo = new CalendarRepository(t.Database);
        await repo.SaveAsync(Event("Overnight", new DateTime(2026, 11, 4, 22, 0, 0), 4));
        await repo.SaveAsync(Event("Next day", new DateTime(2026, 11, 6, 9, 0, 0)));
        var events = await repo.GetRangeAsync(new DateTime(2026, 11, 5), new DateTime(2026, 11, 6));
        Assert.Equal("Overnight", Assert.Single(events).Title);
    }

    [Fact]
    public async Task Upcoming_is_ordered_limited_and_skips_finished_events()
    {
        using var t = new TempData();
        var repo = new CalendarRepository(t.Database);
        var now = new DateTime(2026, 11, 5, 12, 0, 0);
        await repo.SaveAsync(Event("Past", now.AddDays(-1)));
        await repo.SaveAsync(Event("Later", now.AddDays(2)));
        await repo.SaveAsync(Event("Soon", now.AddHours(1)));
        await repo.SaveAsync(Event("Far", now.AddDays(9)));
        var upcoming = await repo.GetUpcomingAsync(now, 2);
        Assert.Equal(["Soon", "Later"], upcoming.Select(e => e.Title));
    }

    [Fact]
    public async Task Invalid_events_are_rejected()
    {
        using var t = new TempData();
        var repo = new CalendarRepository(t.Database);
        var day = new DateTime(2026, 11, 5, 9, 0, 0);
        await Assert.ThrowsAsync<ArgumentException>(() => repo.SaveAsync(Event("  ", day)));
        await Assert.ThrowsAsync<ArgumentException>(() => repo.SaveAsync(new CalendarEvent { Title = "x", Start = day, End = day.AddHours(-1) }));
    }

    [Fact]
    public async Task Title_with_sql_metacharacters_is_stored_literally()
    {
        using var t = new TempData();
        var repo = new CalendarRepository(t.Database);
        var day = new DateTime(2026, 11, 5, 9, 0, 0);
        const string nasty = "x'); DROP TABLE CalendarEvents;--";
        await repo.SaveAsync(Event(nasty, day));
        Assert.Equal(nasty, (await repo.GetRangeAsync(day.Date, day.Date.AddDays(1))).Single().Title);
    }

    [Fact]
    public async Task Events_survive_reopening_the_database()
    {
        using var t = new TempData();
        var day = new DateTime(2026, 11, 5, 9, 0, 0);
        await new CalendarRepository(t.Database).SaveAsync(Event("Persist", day));
        var reopened = new GlyphbarDatabase(t.Paths, NullLogger<GlyphbarDatabase>.Instance);
        Assert.Single(await new CalendarRepository(reopened).GetRangeAsync(day.Date, day.Date.AddDays(1)));
    }
}

public class ApplicationRepositoryTests
{
    private static PinnedApp Pin(string id) => new() { AppId = id, Name = id, LaunchTarget = id };

    [Fact]
    public async Task Pin_reorder_unpin()
    {
        using var t = new TempData();
        var repo = new ApplicationRepository(t.Database);
        IPinnedAppsRepository pins = repo;
        await repo.PinAsync(Pin("a"));
        await repo.PinAsync(Pin("b"));
        await repo.PinAsync(Pin("c"));
        Assert.Equal(["a", "b", "c"], (await pins.GetAllAsync()).Select(p => p.AppId));

        await repo.ReorderAsync(["c", "a", "b"]);
        Assert.Equal(["c", "a", "b"], (await pins.GetAllAsync()).Select(p => p.AppId));

        await repo.UnpinAsync("a");
        Assert.Equal(["c", "b"], (await pins.GetAllAsync()).Select(p => p.AppId));
    }

    [Fact]
    public async Task Pinning_twice_does_not_duplicate()
    {
        using var t = new TempData();
        var repo = new ApplicationRepository(t.Database);
        await repo.PinAsync(Pin("a"));
        await repo.PinAsync(Pin("A"));
        Assert.Single(await ((IPinnedAppsRepository)repo).GetAllAsync());
    }

    [Fact]
    public async Task Launch_history_counts_and_orders()
    {
        using var t = new TempData();
        var repo = new ApplicationRepository(t.Database);
        await repo.RecordLaunchAsync("a");
        await Task.Delay(15);
        await repo.RecordLaunchAsync("b");
        await Task.Delay(15);
        await repo.RecordLaunchAsync("a");
        var recent = await repo.GetRecentAsync(10);
        Assert.Equal("a", recent[0].AppId);
        Assert.Equal(2, recent[0].LaunchCount);
    }

    [Fact]
    public async Task History_does_not_disturb_a_pin()
    {
        using var t = new TempData();
        var repo = new ApplicationRepository(t.Database);
        await repo.PinAsync(Pin("a"));
        await repo.RecordLaunchAsync("a");
        Assert.Equal("a", (await ((IPinnedAppsRepository)repo).GetAllAsync()).Single().Name);
    }

    [Fact]
    public async Task User_added_apps_round_trip()
    {
        using var t = new TempData();
        var repo = new ApplicationRepository(t.Database);
        IUserAppsRepository user = repo;
        await repo.AddAsync(new AppEntry(@"C:\x.exe", "X", @"C:\x.exe", @"C:\x.exe", null, true));
        Assert.True((await user.GetAllAsync()).Single().IsUserAdded);
        await repo.RemoveAsync(@"C:\x.exe");
        Assert.Empty(await user.GetAllAsync());
    }

    [Fact]
    public async Task Custom_icon_is_stored_and_cleared()
    {
        using var t = new TempData();
        var repo = new ApplicationRepository(t.Database);
        await repo.PinAsync(Pin("a"));
        await repo.SetIconAsync("a", @"C:\i.png");
        Assert.Equal(@"C:\i.png", (await ((IPinnedAppsRepository)repo).GetAllAsync()).Single().IconPath);
        await repo.SetIconAsync("a", null);
        Assert.Null((await ((IPinnedAppsRepository)repo).GetAllAsync()).Single().IconPath);
    }
}

public class WidgetLayoutRepositoryTests
{
    [Fact]
    public async Task Layout_round_trips_with_order_size_and_state()
    {
        using var t = new TempData();
        var repo = new WidgetLayoutRepository(t.Database);
        Assert.Empty(await repo.GetAllAsync());

        var layout = WidgetLayoutEngine.Defaults().Reverse().ToList();
        layout[0].IsEnabled = true;
        layout[1].Size = WidgetSize.Large;
        await repo.SaveAllAsync(layout);

        var read = await repo.GetAllAsync();
        Assert.Equal(layout.Select(w => w.Kind), read.Select(w => w.Kind));
        Assert.True(read[0].IsEnabled);
        Assert.Equal(WidgetSize.Large, read[1].Size);

        await repo.SaveAllAsync(read);
        Assert.Equal(layout.Count, (await repo.GetAllAsync()).Count);
    }
}

public class MigrationTests
{
    [Fact]
    public async Task Fresh_database_reaches_the_latest_version()
    {
        using var t = new TempData();
        await new WidgetLayoutRepository(t.Database).GetAllAsync();
        Assert.Equal(Migrator.Steps.Count, t.Database.SchemaVersion);
    }

    [Fact]
    public async Task Prd_tables_exist()
    {
        using var t = new TempData();
        var tables = await t.Database.RunAsync(c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            using var r = cmd.ExecuteReader();
            var names = new List<string>();
            while (r.Read()) names.Add(r.GetString(0));
            return names;
        });
        Assert.Superset(new HashSet<string> { "Applications", "Widgets", "CalendarEvents", "Settings" }, tables.ToHashSet());
    }

    [Fact]
    public async Task Database_does_not_use_a_shared_memory_file()
    {
        // A dead process can hold a WAL -shm mapping and stop every later open.
        using var t = new TempData();
        await new WidgetLayoutRepository(t.Database).SaveAllAsync(WidgetLayoutEngine.Defaults());
        Assert.False(File.Exists(t.Paths.Database + "-shm"));
        var mode = await t.Database.RunAsync(c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode";
            return (string)cmd.ExecuteScalar()!;
        });
        Assert.Equal("delete", mode);
    }

    [Fact]
    public async Task A_database_left_in_wal_mode_is_converted()
    {
        using var t = new TempData();
        using (var c = new SqliteConnection($"Data Source={t.Paths.Database}"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        await new WidgetLayoutRepository(t.Database).GetAllAsync();
        var mode = await t.Database.RunAsync(c =>
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode";
            return (string)cmd.ExecuteScalar()!;
        });
        Assert.Equal("delete", mode);
    }

    [Fact]
    public void Applying_twice_is_a_no_op()
    {
        using var t = new TempData();
        using var c = new SqliteConnection($"Data Source={t.Paths.Database}");
        c.Open();
        int first = Migrator.Apply(c, t.Paths.Database, NullLogger.Instance);
        int second = Migrator.Apply(c, t.Paths.Database, NullLogger.Instance);
        Assert.Equal(first, second);
    }

    [Fact]
    public void A_newer_database_is_left_untouched()
    {
        using var t = new TempData();
        using var c = new SqliteConnection($"Data Source={t.Paths.Database}");
        c.Open();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "PRAGMA user_version = 99";
            cmd.ExecuteNonQuery();
        }
        Assert.Equal(99, Migrator.Apply(c, t.Paths.Database, NullLogger.Instance));
    }
}

public class SettingsStoreTests
{
    private static JsonSettingsStore Open(TempData t) =>
        new(t.Paths, NullLogger<JsonSettingsStore>.Instance, TimeSpan.FromMilliseconds(1));

    [Fact]
    public async Task Changes_persist_across_a_restart()
    {
        using var t = new TempData();
        using (var s = Open(t))
        {
            s.Update(x =>
            {
                x.Taskbar.Height = 64;
                x.Taskbar.Alignment = TaskbarAlignment.Left;
                x.Hotkeys.Search = "Win+Alt+Space";
            });
            await s.FlushAsync();
        }
        using var again = Open(t);
        Assert.Equal(64, again.Current.Taskbar.Height);
        Assert.Equal(TaskbarAlignment.Left, again.Current.Taskbar.Alignment);
        Assert.Equal("Win+Alt+Space", again.Current.Hotkeys.Search);
    }

    [Fact]
    public async Task Corrupt_file_falls_back_to_the_backup()
    {
        using var t = new TempData();
        using (var s = Open(t))
        {
            s.Update(x => x.Taskbar.Height = 60);
            await s.FlushAsync();
            s.Update(x => x.Taskbar.Height = 70);
            await s.FlushAsync();
        }
        File.WriteAllText(t.Paths.Settings, "{ not json");
        using var again = Open(t);
        Assert.Equal(60, again.Current.Taskbar.Height);
        Assert.True(File.Exists(t.Paths.Settings + ".corrupt"));
    }

    [Fact]
    public void Corrupt_file_with_no_backup_gives_defaults()
    {
        using var t = new TempData();
        File.WriteAllText(t.Paths.Settings, "garbage");
        using var s = Open(t);
        Assert.Equal(new TaskbarSettings().Height, s.Current.Taskbar.Height);
    }

    [Fact]
    public void Out_of_range_values_in_the_file_are_clamped_on_load()
    {
        using var t = new TempData();
        File.WriteAllText(t.Paths.Settings, """{ "Taskbar": { "Height": 9999, "Opacity": 7 } }""");
        using var s = Open(t);
        Assert.Equal(96, s.Current.Taskbar.Height);
        Assert.Equal(1, s.Current.Taskbar.Opacity);
    }

    [Fact]
    public void Unknown_properties_from_a_future_version_are_tolerated()
    {
        using var t = new TempData();
        File.WriteAllText(t.Paths.Settings, """{ "Brand": "new", "Taskbar": { "Height": 60, "Shiny": true } }""");
        using var s = Open(t);
        Assert.Equal(60, s.Current.Taskbar.Height);
    }

    [Fact]
    public async Task Reset_restores_defaults_and_notifies()
    {
        using var t = new TempData();
        using var s = Open(t);
        s.Update(x => x.Taskbar.Height = 60);
        int raised = 0;
        s.Changed += (_, _) => raised++;
        s.ResetToDefaults();
        await s.FlushAsync();
        Assert.Equal(new TaskbarSettings().Height, s.Current.Taskbar.Height);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task No_temp_file_is_left_behind()
    {
        using var t = new TempData();
        using var s = Open(t);
        s.Update(x => x.Taskbar.Height = 60);
        await s.FlushAsync();
        Assert.False(File.Exists(t.Paths.Settings + ".tmp"));
    }
}
