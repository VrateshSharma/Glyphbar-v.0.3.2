using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.Extensions.Logging;

namespace Glyphbar.App.Services;

/// <summary>The one copy of the widget layout, shared by the dashboard, the panel and Settings.</summary>
public sealed class WidgetLayoutService(IWidgetLayoutRepository repository, ILogger<WidgetLayoutService> logger)
{
    private List<WidgetLayoutItem> _items = [];
    private bool _loaded;

    public IReadOnlyList<WidgetLayoutItem> Items => _items;
    public IEnumerable<WidgetLayoutItem> Enabled => _items.Where(w => w.IsEnabled);
    public event EventHandler? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _items = WidgetLayoutEngine.Normalize(await repository.GetAllAsync()).ToList();
        _loaded = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetEnabled(WidgetKind kind, bool enabled) => Change(kind, w => w.IsEnabled = enabled);

    public void CycleSize(WidgetKind kind) => Change(kind, w => w.Size = WidgetLayoutEngine.NextSize(w.Kind, w.Size));

    public void MoveBefore(WidgetKind moving, WidgetKind target)
    {
        int from = _items.FindIndex(w => w.Kind == moving), to = _items.FindIndex(w => w.Kind == target);
        WidgetLayoutEngine.Move(_items, from, to);
        Commit();
    }

    public void Shift(WidgetKind kind, int delta)
    {
        int from = _items.FindIndex(w => w.Kind == kind);
        WidgetLayoutEngine.Move(_items, from, Math.Clamp(from + delta, 0, _items.Count - 1));
        Commit();
    }

    public void Reset()
    {
        _items = WidgetLayoutEngine.Defaults().ToList();
        Commit();
    }

    private void Change(WidgetKind kind, Action<WidgetLayoutItem> change)
    {
        if (_items.FirstOrDefault(w => w.Kind == kind) is not { } item) return;
        change(item);
        Commit();
    }

    private void Commit()
    {
        for (int i = 0; i < _items.Count; i++) _items[i].SortOrder = i;
        Changed?.Invoke(this, EventArgs.Empty);
        _ = SaveAsync();
    }

    private async Task SaveAsync()
    {
        try { await repository.SaveAllAsync(_items.ToList()); }
        catch (Exception e) { logger.LogError(e, "Could not save the widget layout"); }
    }
}
