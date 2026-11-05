using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glyphbar.App.Services;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Glyphbar.App.ViewModels;

public sealed partial class LauncherTileViewModel(AppEntry app) : ObservableObject
{
    public AppEntry App { get; } = app;
    public string Name => App.Name;
    public override string ToString() => Name;

    [ObservableProperty] public partial ImageSource? Icon { get; set; }
    [ObservableProperty] public partial double IconSize { get; set; }
    [ObservableProperty] public partial double TileWidth { get; set; }
}

public sealed partial class LauncherViewModel(
    ISettingsStore settings,
    IAppCatalog catalog,
    IRecentAppsRepository recents,
    IAppLauncher launcher,
    PinService pins,
    IconLoader icons,
    IServiceProvider services,
    ILogger<LauncherViewModel> logger) : ObservableObject
{
    private const int RecentCount = 8;
    private static readonly TimeSpan CatalogMaxAge = TimeSpan.FromMinutes(5);

    private DateTimeOffset _catalogRefreshed;
    private List<LauncherTileViewModel> _tiles = [];

    public ISettingsStore Settings => settings;
    private ShellCoordinator Shell => services.GetRequiredService<ShellCoordinator>();

    public ObservableCollection<LauncherTileViewModel> Favourites { get; } = [];
    public ObservableCollection<LauncherTileViewModel> Recent { get; } = [];
    public ObservableCollection<LauncherTileViewModel> All { get; } = [];

    [ObservableProperty] public partial string Filter { get; set; } = "";
    [ObservableProperty] public partial bool ShowFavourites { get; set; }
    [ObservableProperty] public partial bool ShowRecent { get; set; }
    [ObservableProperty] public partial bool ShowNoMatches { get; set; }

    public async Task OpenAsync()
    {
        if (DateTimeOffset.Now - _catalogRefreshed > CatalogMaxAge)
        {
            _catalogRefreshed = DateTimeOffset.Now;
            if (catalog.Apps.Count == 0) await catalog.RefreshAsync();
            else _ = catalog.RefreshAsync();
        }
        await RebuildAsync();
    }

    private LauncherTileViewModel Tile(AppEntry app)
    {
        var s = settings.Current.Search;
        return new LauncherTileViewModel(app) { IconSize = s.LauncherIconSize, TileWidth = s.LauncherIconSize + 44 + s.LauncherSpacing };
    }

    private async Task RebuildAsync()
    {
        _tiles = catalog.Apps.Select(Tile).ToList();
        Favourites.Clear();
        foreach (var pin in pins.Pins)
        {
            var app = catalog.Find(pin.AppId) ?? new AppEntry(pin.AppId, pin.Name, pin.LaunchTarget, pin.ExecutablePath, null);
            Favourites.Add(Tile(app));
        }

        Recent.Clear();
        try
        {
            foreach (var r in await recents.GetRecentAsync(RecentCount))
                if (catalog.Find(r.AppId) is { } app) Recent.Add(Tile(app));
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Recent apps unavailable");
        }
        ApplyFilter();
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        string query = Filter.Trim();
        var matches = query.Length == 0
            ? _tiles
            : _tiles.Select(t => (t, score: SearchRanker.Score(t.Name, query)))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .Select(x => x.t)
                .ToList();

        All.Clear();
        foreach (var t in matches) All.Add(t);
        ShowFavourites = query.Length == 0 && Favourites.Count > 0;
        ShowRecent = query.Length == 0 && Recent.Count > 0;
        ShowNoMatches = All.Count == 0;
    }

    public async Task EnsureIconAsync(LauncherTileViewModel tile)
    {
        if (tile.Icon is null) tile.Icon = await icons.LoadAsync(tile.App.LaunchTarget, 96);
    }

    public async Task LaunchAsync(LauncherTileViewModel tile)
    {
        var result = await pins.LaunchAsync(tile.App.Id, tile.App.LaunchTarget);
        if (result.Success) Shell.HideOverlays();
    }

    public Task SaveFavouriteOrderAsync() => pins.ReorderAsync(Favourites.Select(f => f.App.Id).ToList());

    [RelayCommand]
    private async Task AddApplicationAsync()
    {
        string? path = await Shell.PickFileAsync(".exe");
        if (path is null) return;
        await catalog.AddExecutableAsync(path);
        await RebuildAsync();
    }

    public MenuFlyout BuildContextMenu(LauncherTileViewModel tile)
    {
        var app = tile.App;
        var menu = new MenuFlyout();
        void Add(string text, string glyph, Func<Task> action)
        {
            var item = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph } };
            item.Click += (sender, args) => _ = action();
            menu.Items.Add(item);
        }

        Add("Open", "", () => LaunchAsync(tile));
        if (app.ExecutablePath is { } exe)
            Add("Open as administrator", "", () => pins.LaunchAsync(app.Id, exe, admin: true));
        if (pins.IsPinned(app.Id))
            Add("Unpin", "", async () => { await pins.UnpinAsync(app.Id); await RebuildAsync(); });
        else
            Add("Pin", "", async () => { await pins.PinAsync(app); await RebuildAsync(); });
        if (app.ExecutablePath is { } location)
            Add("Open file location", "", () => launcher.RevealInExplorerAsync(location));
        if (app.IsUserAdded)
            Add("Remove shortcut", "", async () => { await pins.UnpinAsync(app.Id); await catalog.RemoveUserAppAsync(app.Id); await RebuildAsync(); });
        return menu;
    }
}
