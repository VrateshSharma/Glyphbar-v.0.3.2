using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Glyphbar.Platform.Applications;
using Glyphbar.Platform.Battery;
using Glyphbar.Platform.Hotkeys;
using Glyphbar.Platform.Media;
using Glyphbar.Platform.Power;
using Glyphbar.Platform.Search;
using Glyphbar.Platform.Storage;
using Glyphbar.Platform.SystemInfo;
using Glyphbar.Platform.Windowing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Glyphbar.Platform.Tests;

/// <summary>These run against the real Windows APIs of the machine executing them. None change system state.</summary>
public class NativeIntegrationTests
{
    private sealed class NoUserApps : IUserAppsRepository
    {
        public Task<IReadOnlyList<AppEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AppEntry>>([]);
        public Task AddAsync(AppEntry app, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string appId, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public void Battery_reading_matches_the_api_contract()
    {
        using var service = new BatteryService(NullLogger<BatteryService>.Instance);
        var s = service.Read();
        if (s.HasBattery && s.Percent is { } p) Assert.InRange(p, 0, 100);
        if (!s.HasBattery) Assert.Null(s.Percent);
    }

    [Fact]
    public async Task Storage_matches_DriveInfo_for_the_system_drive()
    {
        var drives = await new StorageService(NullLogger<StorageService>.Instance).ReadDrivesAsync();
        var system = drives.First();
        Assert.True(system.IsSystem);
        Assert.Equal(DriveState.Ready, system.State);

        var expected = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        Assert.Equal(expected.TotalSize, system.TotalBytes);
        // Free space moves as the machine runs, so compare within 1 GiB.
        Assert.InRange(system.FreeBytes, expected.TotalFreeSpace - (1L << 30), expected.TotalFreeSpace + (1L << 30));
        Assert.InRange(system.PercentUsed, 0, 100);
    }

    [Fact]
    public async Task System_info_reports_memory_and_cpu_after_a_baseline()
    {
        var service = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var first = await service.ReadAsync();
        Assert.Null(first.CpuPercent);
        Assert.True(first.MemoryTotalBytes > 0);
        Assert.True(first.MemoryUsedBytes < first.MemoryTotalBytes);
        await Task.Delay(300);
        var second = await service.ReadAsync();
        Assert.InRange(second.CpuPercent!.Value, 0, 100);
    }

    [Fact]
    public async Task Catalog_finds_real_applications_without_scanning_the_disk()
    {
        using var worker = new ShellWorker();
        var catalog = new ShellAppCatalog(worker, new NoUserApps(), NullLogger<ShellAppCatalog>.Instance);
        await catalog.RefreshAsync();
        Assert.NotEmpty(catalog.Apps);
        Assert.All(catalog.Apps, a => Assert.StartsWith(ShellAppCatalog.AppsFolderPrefix, a.LaunchTarget));
        Assert.Contains(catalog.Apps, a => a.Name == "File Explorer");
        Assert.DoesNotContain(catalog.Apps, a => a.Name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Icons_come_back_as_png_for_win32_and_packaged_apps()
    {
        using var worker = new ShellWorker();
        var catalog = new ShellAppCatalog(worker, new NoUserApps(), NullLogger<ShellAppCatalog>.Instance);
        await catalog.RefreshAsync();
        var icons = new ShellIconProvider(worker, NullLogger<ShellIconProvider>.Instance);

        foreach (var app in catalog.Apps.Where(a => a.Name is "File Explorer" or "Settings"))
        {
            var png = await icons.GetIconPngAsync(app.LaunchTarget, 48, true);
            Assert.NotNull(png);
            Assert.Equal(0x89, png![0]);
        }
    }

    [Fact]
    public async Task Missing_targets_are_refused_without_touching_the_shell()
    {
        var launcher = new ShellLauncher(NullLogger<ShellLauncher>.Instance);
        Assert.False((await launcher.LaunchAsync(@"C:\definitely\not\here.exe")).Success);
        Assert.False((await launcher.OpenAsync(@"C:\definitely\not\here.txt")).Success);
        Assert.False((await launcher.OpenAsync("ms-settings:notarealpage")).Success);
        Assert.False((await launcher.OpenAsync("cmd.exe /c whoami")).Success);
        Assert.False((await launcher.RevealInExplorerAsync(@"C:\definitely\not\here")).Success);
    }

    [Theory]
    [InlineData("calc.exe /c")]
    [InlineData("powershell -enc abc")]
    [InlineData("https://example.com")]
    public void Arbitrary_commands_are_not_launchable(string target) =>
        Assert.False(ShellLauncher.IsLaunchable(target));

    [Fact]
    public void Non_application_shortcuts_are_filtered()
    {
        Assert.False(ShellAppCatalog.IsApplication("Uninstall Foo", "x", null));
        Assert.False(ShellAppCatalog.IsApplication("Readme", "x", @"C:\a\readme.txt"));
        Assert.False(ShellAppCatalog.IsApplication("Website", @"C:\a\site.url", null));
        Assert.True(ShellAppCatalog.IsApplication("Editor", "x", @"C:\a\editor.exe"));
    }

    [Fact]
    public void Shutdown_privilege_can_be_enabled_without_elevation()
    {
        // Only enables the privilege on this test process's token. Nothing is shut down.
        Assert.True(PowerExecutor.EnableShutdownPrivilege());
    }

    [Fact]
    public void Monitors_are_enumerated_with_exactly_one_primary()
    {
        var monitors = MonitorInfo.GetAll();
        Assert.NotEmpty(monitors);
        Assert.Single(monitors, m => m.IsPrimary);
        Assert.All(monitors, m =>
        {
            Assert.True(m.Dpi >= 96);
            Assert.True(m.Bounds.Width > 0 && m.WorkArea.Height > 0);
        });
    }

    [Fact]
    public async Task Media_service_starts_and_reports_a_consistent_snapshot()
    {
        using var media = new MediaService(NullLogger<MediaService>.Instance);
        await media.StartAsync();
        await Task.Delay(800);
        if (media.CurrentSessionId is { } id) Assert.Contains(media.Sessions, s => s.SessionId == id);
    }

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Microsoft.ZuneMusic")]
    [InlineData("", "Media")]
    public void Media_source_names_are_friendly(string aumid, string expected) =>
        Assert.Equal(expected, MediaService.FriendlySource(aumid));

    [Fact]
    public void Search_sql_uses_the_index_and_filters_by_kind()
    {
        string sql = WindowsSearchProvider.BuildSql("budget report", true, false, [], 10)!;
        Assert.Contains("CONTAINS(System.FileName, '\"budget*\"')", sql);
        Assert.Contains("AND CONTAINS(System.FileName, '\"report*\"')", sql);
        Assert.Contains("System.ItemType <> 'Directory'", sql);
        Assert.Contains("TOP 10", sql);
    }

    [Theory]
    [InlineData("x' OR 1=1 --", "x OR 11 --")]
    [InlineData("\"quoted\"", "quoted")]
    [InlineData("a'; DROP TABLE y;--", "a DROP TABLE y--")]
    public void Search_input_cannot_break_out_of_the_literal(string input, string expected)
    {
        string joined = string.Join(' ', input.Split(' ').Select(WindowsSearchProvider.Clean).Where(t => t.Length > 0));
        Assert.Equal(expected, joined);
        Assert.DoesNotContain("'", joined);
        Assert.DoesNotContain("\"", joined);
    }

    [Theory]
    [InlineData("'\"")]
    [InlineData("   ")]
    public void Search_with_nothing_usable_issues_no_query(string input) =>
        Assert.Null(WindowsSearchProvider.BuildSql(input, true, true, [], 10));

    [Fact]
    public void Scope_paths_are_escaped()
    {
        string sql = WindowsSearchProvider.BuildSql("x", true, true, [@"C:\it's here"], 5)!;
        Assert.Contains("SCOPE='file:C:/it''s here'", sql);
    }

    [Fact]
    public async Task Windows_search_answers_a_real_query_or_reports_itself_unavailable()
    {
        var provider = new WindowsSearchProvider(NullLogger<WindowsSearchProvider>.Instance);
        var outcome = await provider.SearchAsync("readme", true, true, [], 5, CancellationToken.None);
        if (outcome.ProviderAvailable)
            Assert.All(outcome.Results, r => Assert.False(string.IsNullOrEmpty(r.Target)));
        else
            Assert.Empty(outcome.Results);
    }

    [Fact]
    public async Task Search_honours_cancellation()
    {
        var provider = new WindowsSearchProvider(NullLogger<WindowsSearchProvider>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.SearchAsync("readme", true, true, [], 5, cts.Token));
    }

    [Theory]
    [InlineData("not a gesture", HotkeyRegistration.Invalid)]
    [InlineData("Win+L", HotkeyRegistration.Reserved)]
    public void Hotkey_validation_never_reaches_the_os_for_bad_gestures(string gesture, HotkeyRegistration expected)
    {
        using var window = new ShellEventWindow(NullLogger<ShellEventWindow>.Instance);
        using var hotkeys = new HotkeyService(window, NullLogger<HotkeyService>.Instance);
        Assert.Equal(expected, hotkeys.Register(HotkeyAction.Search, gesture));
    }

    [Fact]
    public void A_gesture_held_by_another_registration_is_reported_as_a_conflict()
    {
        using var w1 = new ShellEventWindow(NullLogger<ShellEventWindow>.Instance);
        using var h1 = new HotkeyService(w1, NullLogger<HotkeyService>.Instance);
        const string gesture = "Control+Alt+Shift+F11";
        Assert.Equal(HotkeyRegistration.Registered, h1.Register(HotkeyAction.Search, gesture));
        Assert.Equal(HotkeyRegistration.Conflict, h1.Register(HotkeyAction.Dashboard, gesture));
    }
}
