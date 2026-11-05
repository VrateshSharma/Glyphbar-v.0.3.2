using Glyphbar.App.Components.Widgets;
using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Glyphbar.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glyphbar.App.Components;

/// <summary>Hosts enabled widgets. Widgets only subscribe to data while the board is on screen.</summary>
public sealed class WidgetBoard : UserControl
{
    public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
        nameof(IsEditing), typeof(bool), typeof(WidgetBoard), new PropertyMetadata(false, (d, _) => ((WidgetBoard)d).ApplyEditing()));

    private readonly WidgetGridPanel _panel = new();
    private WidgetLayoutService? _layout;
    private IServiceProvider? _services;
    private bool _active;

    public WidgetBoard()
    {
        Content = new ScrollViewer
        {
            Content = _panel,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    public bool IsEditing { get => (bool)GetValue(IsEditingProperty); set => SetValue(IsEditingProperty, value); }

    public void Initialize(WidgetLayoutService layout, IServiceProvider services, int maxColumns)
    {
        _layout = layout;
        _services = services;
        _panel.MaxColumns = maxColumns;
        _layout.Changed += (_, _) => Rebuild();
        _ = layout.EnsureLoadedAsync();
        Rebuild();
    }

    public void SetActive(bool active)
    {
        if (_active == active) return;
        _active = active;
        foreach (var host in _panel.Children.OfType<WidgetHost>())
        {
            if (active) host.View.Activate();
            else host.View.Deactivate();
        }
    }

    private void Rebuild()
    {
        if (_layout is null || _services is null) return;
        foreach (var host in _panel.Children.OfType<WidgetHost>()) host.View.Deactivate();
        _panel.Children.Clear();

        foreach (var item in _layout.Enabled)
        {
            var view = Create(item.Kind);
            view.ApplySize(item.Size);
            var host = new WidgetHost(item, view, _layout);
            _panel.Children.Add(host);
            if (_active) view.Activate();
        }
        ApplyEditing();
    }

    private void ApplyEditing()
    {
        foreach (var host in _panel.Children.OfType<WidgetHost>()) host.SetEditing(IsEditing);
    }

    private IWidgetView Create(WidgetKind kind)
    {
        var s = _services!;
        return kind switch
        {
            WidgetKind.Clock => new ClockWidget(s.GetRequiredService<ClockWidgetViewModel>()),
            WidgetKind.Date => new DateWidget(s.GetRequiredService<DateWidgetViewModel>()),
            WidgetKind.Calendar => new CalendarWidget(s.GetRequiredService<CalendarWidgetViewModel>()),
            WidgetKind.Battery => new BatteryWidget(s.GetRequiredService<BatteryWidgetViewModel>()),
            WidgetKind.Storage => new StorageWidget(s.GetRequiredService<StorageWidgetViewModel>()),
            WidgetKind.Music => new MusicWidget(s.GetRequiredService<MusicWidgetViewModel>()),
            _ => new SystemInfoWidget(s.GetRequiredService<SystemInfoWidgetViewModel>()),
        };
    }
}
