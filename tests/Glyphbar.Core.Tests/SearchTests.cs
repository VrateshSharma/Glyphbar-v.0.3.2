using Glyphbar.Core.Models;
using Glyphbar.Core.Services;

namespace Glyphbar.Core.Tests;

public class SearchTests
{
    private static AppEntry App(string name) => new(name, name, name, null, null);

    [Fact]
    public void Exact_beats_prefix_beats_word_beats_substring()
    {
        double exact = SearchRanker.Score("Code", "code");
        double prefix = SearchRanker.Score("Codec Tool", "code");
        double word = SearchRanker.Score("Visual Code", "code");
        double inner = SearchRanker.Score("Barcode", "code");
        Assert.True(exact > prefix && prefix > word && word > inner && inner > 0);
    }

    [Fact]
    public void Acronym_matches()
    {
        Assert.True(SearchRanker.Score("Visual Studio Code", "vsc") >= 60);
    }

    [Fact]
    public void Fuzzy_subsequence_needs_three_characters()
    {
        Assert.True(SearchRanker.Score("Calculator", "clc") > 0);
        Assert.Equal(0, SearchRanker.Score("Calculator", "cz"));
    }

    [Fact]
    public void No_match_and_blank_query_score_zero()
    {
        Assert.Equal(0, SearchRanker.Score("Calculator", "xyz"));
        Assert.Equal(0, SearchRanker.Score("Calculator", "   "));
    }

    [Fact]
    public void Launch_history_breaks_ties()
    {
        var recent = new Dictionary<string, RecentApp> { ["Notes B"] = new("Notes B", DateTimeOffset.Now, 5) };
        var ranked = SearchRanker.RankApps([App("Notes A"), App("Notes B")], "notes", recent, 5);
        Assert.Equal("Notes B", ranked[0].Title);
    }

    [Fact]
    public void Results_respect_the_limit()
    {
        var apps = Enumerable.Range(0, 20).Select(i => App($"App {i}"));
        Assert.Equal(5, SearchRanker.RankApps(apps, "app", null, 5).Count);
    }

    [Fact]
    public void Settings_match_by_keyword()
    {
        Assert.Contains(SearchRanker.RankSettings("wallpaper", 3), r => r.Target == "ms-settings:personalization");
    }

    [Fact]
    public void Recent_queries_dedupe_and_cap()
    {
        var history = SearchRanker.PushRecent(["alpha", "beta"], "ALPHA", 2);
        Assert.Equal(["ALPHA", "beta"], history);
        Assert.Equal(["x", "alpha"], SearchRanker.PushRecent(["alpha", "beta"], " x ", 2));
    }

    [Fact]
    public void Only_catalogued_settings_uris_are_allowed()
    {
        Assert.True(SettingsCatalog.IsAllowed("ms-settings:display"));
        Assert.False(SettingsCatalog.IsAllowed("ms-settings:display;calc.exe"));
        Assert.False(SettingsCatalog.IsAllowed("cmd.exe"));
    }
}
