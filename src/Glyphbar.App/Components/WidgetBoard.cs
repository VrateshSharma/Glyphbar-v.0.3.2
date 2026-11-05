using Glyphbar.App.Components.Widgets;
using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
    private static readonly SolidColorBrush White = new(Microsoft.UI.Colors.White);
    private DateTime _lastBrighten;
    private bool _desktopMode;
    private ISettingsStore? _settings;

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

    /// <summary>Raised when the widgets' total height changes.</summary>
    public event Action<double>? ContentHeightChanged;

    public void Initialize(WidgetLayoutService layout, IServiceProvider services, int maxColumns, bool desktopMode = false)
    {
        _desktopMode = desktopMode;
        _settings = services.GetRequiredService<ISettingsStore>();
        _panel.SizeChanged += (_, e) => ContentHeightChanged?.Invoke(e.NewSize.Height);
        if (desktopMode)
        {
            _settings.Changed += OnSettingsChanged;
            _panel.LayoutUpdated += OnDesktopLayoutUpdated;
        }
        _layout = layout;
        _services = services;
        _panel.MaxColumns = maxColumns;
        _layout.Changed += OnLayoutChanged;
        _ = layout.EnsureLoadedAsync();
        Rebuild();
    }

    private void OnLayoutChanged(object? sender, EventArgs e) => Rebuild();

    private void OnSettingsChanged(object? sender, EventArgs e) => ApplyDesktopFill();

    /// <summary>
    /// Must be called before the window closes. These services outlive the window, and touching a closed window's
    /// XAML tree is a native access violation, which no managed handler can catch.
    /// </summary>
    public void Detach()
    {
        SetActive(false);
        if (_layout is not null) _layout.Changed -= OnLayoutChanged;
        if (_settings is not null) _settings.Changed -= OnSettingsChanged;
        _panel.LayoutUpdated -= OnDesktopLayoutUpdated;
        _layout = null;
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
        ApplyDesktopFill();
    }

    /// <summary>
    /// The palette's greys are chosen for black cards and wash out over a wallpaper. On the desktop they become white,
    /// with opacity keeping the hierarchy. Throttled, because list rows appear after the first layout.
    /// </summary>
    private void OnDesktopLayoutUpdated(object? sender, object e)
    {
        if (DateTime.UtcNow - _lastBrighten < TimeSpan.FromSeconds(1)) return;
        _lastBrighten = DateTime.UtcNow;
        Brighten(_panel);
    }

    private static void Brighten(DependencyObject element)
    {
        switch (element)
        {
            case TextBlock { Foreground: SolidColorBrush brush } text when Lifted(brush) is { } opacity:
                text.Foreground = White;
                text.Opacity = opacity;
                break;
            case FontIcon { Foreground: SolidColorBrush brush } icon when Lifted(brush) is { } opacity:
                icon.Foreground = White;
                icon.Opacity = opacity;
                break;
        }
        for (int i = 0, n = VisualTreeHelper.GetChildrenCount(element); i < n; i++)
            Brighten(VisualTreeHelper.GetChild(element, i));
    }

    private static double? Lifted(SolidColorBrush brush) => brush.Color switch
    {
        { R: 0xA0, G: 0xA0, B: 0xA0 } => 0.92,
        { R: 0x68, G: 0x68, B: 0x68 } => 0.72,
        _ => null,
    };

    private void ApplyDesktopFill()
    {
        if (!_desktopMode || _settings is null) return;
        foreach (var host in _panel.Children.OfType<WidgetHost>()) host.SetDesktopFill(_settings.Current.Widgets.DesktopBackgroundPercent);
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
