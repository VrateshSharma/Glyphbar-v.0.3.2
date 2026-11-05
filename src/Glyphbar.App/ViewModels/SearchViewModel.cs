using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Glyphbar.App.Services;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;

namespace Glyphbar.App.ViewModels;

public sealed partial class SearchResultViewModel(SearchResult result, string header) : ObservableObject
{
    public SearchResult Result { get; } = result;
    public string Title => Result.Title;
    public override string ToString() => HasSubtitle ? $"{Title}, {Subtitle}" : Title;
    public string Subtitle => Result.Subtitle ?? "";
    public bool HasSubtitle => !string.IsNullOrEmpty(Result.Subtitle);
    public string Header { get; } = header;
    public bool HasHeader => Header.Length > 0;
    public string Glyph => Result.Category switch
    {
        SearchCategory.Setting => "",
        SearchCategory.Folder => "",
        SearchCategory.File => "",
        SearchCategory.Recent => "",
        _ => "",
    };

    [ObservableProperty] public partial ImageSource? Icon { get; set; }
    public bool ShowIcon => Icon is not null;
    public bool ShowGlyph => Icon is null;

    partial void OnIconChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(ShowIcon));
        OnPropertyChanged(nameof(ShowGlyph));
    }
}

public sealed partial class SearchViewModel(
    ISettingsStore settings,
    IAppCatalog catalog,
    IFileSearchProvider files,
    IRecentAppsRepository recents,
    IAppLauncher launcher,
    PinService pins,
    IconLoader icons,
    IServiceProvider services,
    ILogger<SearchViewModel> logger) : ObservableObject
{
    private const int MaxApps = 7, MaxSettings = 4, MaxFiles = 12;
    private static readonly TimeSpan FileDebounce = TimeSpan.FromMilliseconds(180);

    private CancellationTokenSource? _fileSearch;
    private Dictionary<string, RecentApp> _recent = new(StringComparer.OrdinalIgnoreCase);

    public ISettingsStore Settings => settings;
    public ObservableCollection<SearchResultViewModel> Results { get; } = [];

    [ObservableProperty] public partial string Query { get; set; } = "";
    [ObservableProperty] public partial int SelectedIndex { get; set; } = -1;
    [ObservableProperty] public partial string EmptyMessage { get; set; } = "";
    [ObservableProperty] public partial bool ShowEmpty { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";

    public void Open()
    {
        _ = LoadRecentsAsync();
        StatusMessage = "";
        Refresh();
    }

    public void Close()
    {
        _fileSearch?.Cancel();
        Query = "";
    }

    private async Task LoadRecentsAsync()
    {
        try { _recent = (await recents.GetRecentAsync(200)).ToDictionary(r => r.AppId, StringComparer.OrdinalIgnoreCase); }
        catch (Exception e) { logger.LogWarning(e, "Recent apps unavailable"); }
    }

    partial void OnQueryChanged(string value) => Refresh();

    private void Refresh()
    {
        _fileSearch?.Cancel();
        string query = Query.Trim();
        var s = settings.Current.Search;

        if (query.Length == 0)
        {
            var recent = s.KeepHistory ? s.RecentQueries : [];
            Show(recent.Select(q => new SearchResult(SearchCategory.Recent, q, null, q, 0)).ToList());
            ShowEmpty = Results.Count == 0;
            EmptyMessage = "Type to search";
            return;
        }

        // Cached and in memory, so these appear on the same keystroke.
        var local = new List<SearchResult>();
        if (s.IncludeApplications) local.AddRange(SearchRanker.RankApps(catalog.Apps, query, _recent, MaxApps));
        if (s.IncludeSettings) local.AddRange(SearchRanker.RankSettings(query, MaxSettings));
        Show(local);
        ShowEmpty = false;

        if (s.IncludeFiles || s.IncludeFolders)
        {
            _fileSearch = new CancellationTokenSource();
            _ = SearchFilesAsync(query, local, _fileSearch.Token);
        }
        else
        {
            SetEmptyIfNone(query);
        }
    }

    private async Task SearchFilesAsync(string query, List<SearchResult> local, CancellationToken ct)
    {
        try
        {
            await Task.Delay(FileDebounce, ct);
            var s = settings.Current.Search;
            StatusMessage = "Searching files…";
            var outcome = await files.SearchAsync(query, s.IncludeFiles, s.IncludeFolders, s.IndexedLocations, MaxFiles, ct);
            ct.ThrowIfCancellationRequested();

            StatusMessage = outcome.ProviderAvailable ? "" : "Windows Search is unavailable, so file results are off.";
            var folders = outcome.Results.Where(r => r.Category == SearchCategory.Folder);
            var fileResults = outcome.Results.Where(r => r.Category == SearchCategory.File);
            Show([.. local, .. folders, .. fileResults]);
            SetEmptyIfNone(query);
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke replaced this search.
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "File search failed");
            StatusMessage = "File search failed.";
        }
    }

    private void SetEmptyIfNone(string query)
    {
        ShowEmpty = Results.Count == 0;
        EmptyMessage = $"No results for \"{query}\"";
    }

    private void Show(IReadOnlyList<SearchResult> results)
    {
        Results.Clear();
        SearchCategory? last = null;
        foreach (var r in results)
        {
            string header = r.Category != last ? HeaderOf(r.Category) : "";
            last = r.Category;
            var item = new SearchResultViewModel(r, header);
            Results.Add(item);
            if (r.App is { } app) _ = LoadIconAsync(item, app);
        }
        SelectedIndex = Results.Count > 0 ? 0 : -1;
    }

    private async Task LoadIconAsync(SearchResultViewModel item, AppEntry app) =>
        item.Icon = await icons.LoadAsync(app.LaunchTarget, 48);

    private static string HeaderOf(SearchCategory c) => c switch
    {
        SearchCategory.Application => "APPLICATIONS",
        SearchCategory.Setting => "SETTINGS",
        SearchCategory.Folder => "FOLDERS",
        SearchCategory.File => "FILES",
        _ => "RECENT",
    };

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0) return;
        SelectedIndex = (Math.Max(SelectedIndex, 0) + delta + Results.Count) % Results.Count;
    }

    public Task OpenSelectedAsync() =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count ? OpenAsync(Results[SelectedIndex]) : Task.CompletedTask;

    public async Task OpenAsync(SearchResultViewModel item)
    {
        var r = item.Result;
        if (r.Category == SearchCategory.Recent)
        {
            Query = r.Target;
            return;
        }

        RememberQuery();
        var result = r.Category == SearchCategory.Application
            ? await pins.LaunchAsync(r.App!.Id, r.Target)
            : await launcher.OpenAsync(r.Target);

        if (result.Success) services.GetRequiredService<ShellCoordinator>().HideOverlays();
        else StatusMessage = result.Error ?? "Could not open that item.";
    }

    private void RememberQuery()
    {
        if (!settings.Current.Search.KeepHistory) return;
        settings.Update(s => s.Search.RecentQueries = SearchRanker.PushRecent(s.Search.RecentQueries, Query, s.Search.MaxHistory).ToList());
    }
}
