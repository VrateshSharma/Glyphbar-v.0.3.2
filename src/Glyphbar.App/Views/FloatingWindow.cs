using Glyphbar.App.Services;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Platform.Interop;
using Glyphbar.Platform.Windowing;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;
using WinRT.Interop;

namespace Glyphbar.App.Views;

public enum FloatingPlacement { Center, UpperCenter, BottomRight }

/// <summary>Implemented by views that need to know when their overlay opens or closes.</summary>
public interface IFloatingContent
{
    void OnShown();
    void OnHidden();
}

/// <summary>
/// A borderless, always-on-top overlay that closes when it loses focus or on Escape.
/// It hides instead of closing, so reopening is instant.
/// </summary>
public sealed class FloatingWindow : Window
{
    /// <summary>Set while a system dialog is open, which would otherwise count as losing focus.</summary>
    public static bool SuppressLightDismiss { get; set; } = Environment.GetEnvironmentVariable("GLYPHBAR_KEEP_OVERLAYS") == "1";

    private readonly UIElement _content;
    private readonly Border _root;
    private readonly nint _hwnd;
    private readonly ISettingsStore _settings;
    private bool _destroyed;
    private bool _hiding;

    public FloatingWindow(UIElement content, ISettingsStore settings, string title)
    {
        _content = content;
        _settings = settings;
        Title = title;

        _root = new Border
        {
            Child = content,
            CornerRadius = new CornerRadius(24),
            BorderBrush = (Brush)Application.Current.Resources["GbBorderBrush"],
            BorderThickness = new Thickness(1),
        };
        _root.KeyDown += OnKeyDown;
        Content = _root;
        SystemBackdrop = new TransparentBackdrop();
        ApplyMaterial();

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (sender, e) =>
        {
            if (_destroyed) return;
            e.Cancel = true;
            _ = HideAsync();
        };

        _hwnd = WindowNative.GetWindowHandle(this);
        WindowStyler.MakeToolWindow(_hwnd);
        WindowStyler.ClearFrame(_hwnd);
        WindowStyler.MakeFrameless(_hwnd);
        Activated += OnActivated;
    }

    public bool IsOpen { get; private set; }

    private void ApplyMaterial()
    {
        // Overlays keep the transparent backdrop so their rounded corners stay true at any radius; acrylic would
        // need a rectangular window. "Acrylic" here means a slightly see-through panel. The dashboard uses real acrylic.
        bool translucent = _settings.Current.Appearance.Backdrop == BackdropMode.Acrylic;
        _root.Background = new SolidColorBrush(translucent ? Color.FromArgb(0xF2, 0x0A, 0x0A, 0x0A) : Color.FromArgb(0xFF, 0x0C, 0x0C, 0x0C));
    }

    public void ShowAt(int widthDip, int heightDip, FloatingPlacement placement)
    {
        ApplyMaterial();
        Native.GetCursorPos(out var cursor);
        var monitor = MonitorInfo.ForPoint(cursor);
        if (monitor is null) return;

        double scale = monitor.Dpi / 96d;
        var work = monitor.WorkArea;
        int width = Math.Min((int)(widthDip * scale), work.Width - 16);
        int height = Math.Min((int)(heightDip * scale), work.Height - 16);
        int margin = (int)(12 * scale);
        var (x, y) = placement switch
        {
            FloatingPlacement.UpperCenter => (work.Left + (work.Width - width) / 2, work.Top + (int)(work.Height * 0.16)),
            FloatingPlacement.BottomRight => (work.Right - width - margin, work.Bottom - height - margin),
            _ => (work.Left + (work.Width - width) / 2, work.Top + (work.Height - height) / 2),
        };

        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
        IsOpen = true;
        AppWindow.Show(true);
        Activate();
        Native.SetForegroundWindow(_hwnd);
        Motion.Enter(_root);
        (_content as IFloatingContent)?.OnShown();
    }

    public async Task HideAsync()
    {
        if (!IsOpen || _hiding) return;
        _hiding = true;
        await Motion.ExitAsync(_root);
        AppWindow.Hide();
        IsOpen = false;
        _hiding = false;
        (_content as IFloatingContent)?.OnHidden();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated && IsOpen && !SuppressLightDismiss)
            _ = HideAsync();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape) return;
        e.Handled = true;
        _ = HideAsync();
    }

    public void Destroy()
    {
        _destroyed = true;
        (_content as IFloatingContent)?.OnHidden();
        Close();
    }
}
