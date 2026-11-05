using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Glyphbar.Platform.AppBar;
using Glyphbar.Platform.Interop;
using Glyphbar.Platform.Windowing;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Glyphbar.App.Views;

/// <summary>The HWND-backed bar on one monitor. Screen negotiation lives in <see cref="AppBarController"/>.</summary>
public sealed class TaskbarWindow : Window, IDisposable
{
    private const int HiddenSliverPixels = 2;
    private const double RevealMs = 180, HideMs = 240;
    private static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(600);

    private readonly nint _hwnd;
    private readonly ISettingsStore _settings;
    private readonly UiDispatcher _ui;
    private readonly AppBarController _appBar;
    private readonly TaskbarViewModel _viewModel;
    private readonly TaskbarView _view;
    private readonly DispatcherQueueTimer _autoHideWatch;
    private readonly DispatcherQueueTimer _slide;
    private readonly System.Diagnostics.Stopwatch _slideClock = new();
    private PixelRect _slideTarget;
    private int _slideFromTop;
    private double _slideMs;
    private bool _revealed = true;
    private bool _repositionQueued;
    private bool _disposed;
    private bool _fullscreen;
    private DateTimeOffset _pointerLeft;

    public TaskbarWindow(nint monitor, TaskbarViewModel viewModel, ISettingsStore settings, UiDispatcher ui, ILogger logger)
    {
        _settings = settings;
        _ui = ui;
        _viewModel = viewModel;
        _view = new TaskbarView(viewModel);
        Content = _view;
        ThemeApplier.Track(_view, settings);
        Title = "Glyphbar Taskbar";
        SystemBackdrop = new TransparentBackdrop();

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (_, e) => e.Cancel = !_disposed;   // Alt+F4 must not lose the bar

        _hwnd = WindowNative.GetWindowHandle(this);
        WindowStyler.MakeToolWindow(_hwnd);
        WindowStyler.ClearFrame(_hwnd);
        WindowStyler.MakeFrameless(_hwnd);

        _appBar = new AppBarController(_hwnd, monitor, () => _settings.Current.Taskbar, logger);
        _appBar.PositionInvalidated += QueueReposition;
        _appBar.FullscreenChanged += OnFullscreenChanged;

        _autoHideWatch = ui.Queue.CreateTimer();
        _autoHideWatch.Interval = TimeSpan.FromMilliseconds(200);
        _autoHideWatch.Tick += (_, _) => UiDispatcher.Guard(WatchPointer);

        _slide = ui.Queue.CreateTimer();
        _slide.Interval = TimeSpan.FromMilliseconds(8);
        _slide.Tick += (_, _) => UiDispatcher.Guard(StepSlide);

        AppWindow.Show(false);
        _appBar.Register();
        Relayout();
    }

    public nint Handle => _hwnd;
    public bool IsRegistered => _appBar.IsRegistered;

    public void SetMonitor(nint monitor) => _appBar.SetMonitor(monitor);

    /// <summary>Applies settings and re-negotiates with the shell.</summary>
    public void Relayout()
    {
        if (_disposed) return;
        StopSlide();
        _appBar.Reposition();
        _revealed = true;
        if (_settings.Current.Taskbar.AutoHide)
        {
            _pointerLeft = DateTimeOffset.Now;
            _autoHideWatch.Start();
        }
        else
        {
            _autoHideWatch.Stop();
        }
    }

    public void Reregister()
    {
        if (_disposed) return;
        _appBar.Reregister();
    }

    private void QueueReposition()
    {
        // ABN_POSCHANGED arrives inside the shell's own SendMessage; do the work after it returns.
        if (_repositionQueued) return;
        _repositionQueued = true;
        _ui.Post(() =>
        {
            _repositionQueued = false;
            Relayout();
        });
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;
        // Stay out of the way of games and video, then come back on top afterwards.
        WindowStyler.SetTopmost(_hwnd, !fullscreen);
    }

    /// <summary>
    /// Polls the cursor while auto-hide is on. Pointer events can't be used to reveal the bar: a 2px sliver is too thin
    /// to rely on, and if the Windows taskbar also auto-hides on this edge it receives the pointer first.
    /// </summary>
    private void WatchPointer()
    {
        var s = _settings.Current.Taskbar;
        if (!s.AutoHide)
        {
            _autoHideWatch.Stop();
            return;
        }

        Native.GetCursorPos(out var p);
        var r = _appBar.WindowRect;
        if (!_revealed)
        {
            var m = _appBar.MonitorBounds;
            bool atEdge = s.Edge == TaskbarEdge.Bottom ? p.Y >= m.Bottom - HiddenSliverPixels : p.Y < m.Top + HiddenSliverPixels;
            if (atEdge && p.X >= r.Left && p.X < r.Right)
            {
                _revealed = true;
                _pointerLeft = DateTimeOffset.Now;
                SlideTo(r, RevealMs);
            }
            return;
        }

        bool inside = p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom;
        if (inside || _viewModel.IsMenuOpen)
        {
            _pointerLeft = DateTimeOffset.Now;
            return;
        }
        if (DateTimeOffset.Now - _pointerLeft < HideDelay) return;

        _revealed = false;
        SlideTo(TaskbarGeometry.HiddenRect(r, _appBar.MonitorBounds, s.Edge, HiddenSliverPixels), HideMs);
    }

    /// <summary>
    /// Starts from wherever the bar is now, so a slide that reverses part-way carries on smoothly. Progress comes from
    /// elapsed time, not frames, so a busy moment drops frames instead of slowing the bar down.
    /// </summary>
    private void SlideTo(PixelRect target, double ms)
    {
        if (!Motion.Enabled)
        {
            StopSlide();
            Move(target);
            return;
        }

        Native.GetWindowRect(_hwnd, out var now);
        _slideFromTop = now.Top;
        _slideTarget = target;
        _slideMs = Motion.ScaleMs(ms);
        _slideClock.Restart();
        _appBar.SuppressMoveNotices = true;
        _slide.Start();
    }

    private void StepSlide()
    {
        double t = Math.Min(1, _slideClock.Elapsed.TotalMilliseconds / _slideMs);
        if (t >= 1)
        {
            StopSlide();
            Move(_slideTarget);
            return;
        }

        double eased = 1 - Math.Pow(1 - t, 3);   // ease-out: quick start, gentle landing
        int top = _slideFromTop + (int)Math.Round((_slideTarget.Top - _slideFromTop) * eased);
        Native.SetWindowPos(_hwnd, 0, _slideTarget.Left, top, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    private void StopSlide()
    {
        _slide.Stop();
        _appBar.SuppressMoveNotices = false;
    }

    private void Move(PixelRect r) =>
        Native.SetWindowPos(_hwnd, _fullscreen ? Native.HWND_NOTOPMOST : Native.HWND_TOPMOST, r.Left, r.Top, r.Width, r.Height, Native.SWP_NOACTIVATE);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _autoHideWatch.Stop();
        StopSlide();
        _appBar.Dispose();
        _viewModel.Dispose();
        Close();
    }
}
