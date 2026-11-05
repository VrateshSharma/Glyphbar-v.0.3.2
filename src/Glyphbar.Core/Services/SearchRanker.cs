using Glyphbar.Core.Models;

namespace Glyphbar.Core.Services;

public static class SearchRanker
{
    /// <summary>Scores a name against a query; 0 means no match.</summary>
    public static double Score(string name, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(name)) return 0;
        query = query.Trim();
        var cmp = StringComparison.OrdinalIgnoreCase;

        if (name.Equals(query, cmp)) return 100;
        if (name.StartsWith(query, cmp)) return 90 - Math.Min(10, (name.Length - query.Length) * 0.25);

        var words = name.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Skip(1).Any(w => w.StartsWith(query, cmp))) return 70;

        if (query.Length >= 2)
        {
            var acronym = string.Concat(words.Select(w => w[0]));
            if (acronym.StartsWith(query, cmp)) return 60;
        }

        int index = name.IndexOf(query, cmp);
        if (index >= 0) return 50 - Math.Min(10, index * 0.5);

        return query.Length >= 3 && IsSubsequence(name, query) ? 20 : 0;
    }

    private static bool IsSubsequence(string text, string query)
    {
        int q = 0;
        foreach (char c in text)
        {
            if (char.ToUpperInvariant(c) == char.ToUpperInvariant(query[q]) && ++q == query.Length)
                return true;
        }
        return false;
    }

    public static IReadOnlyList<SearchResult> RankApps(
        IEnumerable<AppEntry> apps,
        string query,
        IReadOnlyDictionary<string, RecentApp>? recent,
        int max)
    {
        var results = new List<SearchResult>();
        foreach (var app in apps)
        {
            double score = Score(app.Name, query);
            if (score <= 0) continue;
            if (recent is not null && recent.TryGetValue(app.Id, out var r))
                score += Math.Min(8, r.LaunchCount);
            results.Add(new SearchResult(SearchCategory.Application, app.Name, "Application", app.LaunchTarget, score, app));
        }
        return results.OrderByDescending(r => r.Score).ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase).Take(max).ToList();
    }

    public static IReadOnlyList<SearchResult> RankSettings(string query, int max) =>
        SettingsCatalog.Entries
            .Select(e => (e, score: Math.Max(Score(e.Title, query), e.Keywords.Max(k => Score(k, query)) * 0.9)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Take(max)
            .Select(x => new SearchResult(SearchCategory.Setting, x.e.Title, "Windows Settings", x.e.Uri, x.score))
            .ToList();

    public static IReadOnlyList<string> PushRecent(IEnumerable<string> history, string query, int max)
    {
        query = query.Trim();
        if (query.Length == 0) return history.ToList();
        return new[] { query }
            .Concat(history.Where(h => !h.Equals(query, StringComparison.OrdinalIgnoreCase)))
            .Take(Math.Max(0, max))
            .ToList();
    }
}
