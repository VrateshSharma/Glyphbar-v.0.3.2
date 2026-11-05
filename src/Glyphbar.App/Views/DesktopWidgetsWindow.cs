using Glyphbar.App.Components;
using Glyphbar.App.Services;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Platform.Interop;
using Glyphbar.Platform.Windowing;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Glyphbar.App.Views;

/// <summary>
/// Widgets that live on the wallpaper. The window is only as big as its widgets, because even its transparent
/// parts take clicks, and a full-screen one would block the desktop icons.
/// </summary>
public sealed class DesktopWidgetsWindow : Window, IDisposable
{
    private const int WidthDip = 580, MarginDip = 16;

    private readonly nint _hwnd;
    private readonly ISettingsStore _settings;
    private readonly WidgetBoard _board = new();
    private readonly DesktopLayerKeeper _layer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _layerWatch;
    private double _contentHeight = 400;
    private bool _disposed;

    public DesktopWidgetsWindow(WidgetLayoutService layout, IServiceProvider services, ISettingsStore settings)
    {
        _settings = settings;
        Title = "Glyphbar Desktop Widgets";
        Content = _board;
        SystemBackdrop = new TransparentBackdrop();
        ThemeApplier.Track(_board, settings);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (sender, e) => e.Cancel = !_disposed;

        _hwnd = WindowNative.GetWindowHandle(this);
        WindowStyler.MakeToolWindow(_hwnd);
        WindowStyler.ClearFrame(_hwnd);
        WindowStyler.MakeFrameless(_hwnd);

        _board.ContentHeightChanged += height =>
        {
            _contentHeight = height;
            Relayout();
        };
        _board.Initialize(layout, services, maxColumns: 3, desktopMode: true);

        Relayout();
        AppWindow.Show(false);
        _layer = new DesktopLayerKeeper(_hwnd);

        // Show Desktop moves the desktop window forward without a usable event. Checking is one window lookup,
        // and the window only moves when it has fallen behind.
        _layerWatch = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        _layerWatch.Interval = TimeSpan.FromMilliseconds(400);
        _layerWatch.Tick += (_, _) => UiDispatcher.Guard(_layer.Place);
        _layerWatch.Start();

        _board.SetActive(true);
    }

    /// <summary>Anchors to the top corner of the primary monitor's work area, sized to the widgets.</summary>
    public void Relayout()
    {
        if (_disposed) return;
        var monitor = MonitorInfo.GetAll().FirstOrDefault(m => m.IsPrimary);
        if (monitor is null) return;

        double scale = monitor.Dpi / 96d;
        var work = monitor.WorkArea;
        int margin = (int)(MarginDip * scale);
        int width = Math.Min((int)(WidthDip * scale), work.Width - margin * 2);
        int height = Math.Min((int)Math.Ceiling(_contentHeight * scale), work.Height - margin * 2);
        int x = _settings.Current.Widgets.DesktopSide == DesktopWidgetSide.Left ? work.Left + margin : work.Right - width - margin;

        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, work.Top + margin, width, Math.Max(height, 1)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _layerWatch?.Stop();
        _board.Detach();
        _layer?.Dispose();
        Close();
    }
}
