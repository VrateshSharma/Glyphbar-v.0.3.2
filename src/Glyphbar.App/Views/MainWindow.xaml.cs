using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Platform.Interop;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinRT.Interop;

namespace Glyphbar.App.Views;

/// <summary>The dashboard. Closing it only hides it; Exit lives in the tray menu.</summary>
public sealed partial class MainWindow : Window
{
    private readonly DashboardView _view;
    private readonly ISettingsStore _settings;
    private readonly nint _hwnd;
    private bool _destroyed;

    public MainWindow(DashboardViewModel viewModel, WidgetLayoutService layout, IServiceProvider services, ISettingsStore settings)
    {
        InitializeComponent();
        _settings = settings;
        _hwnd = WindowNative.GetWindowHandle(this);
        _view = new DashboardView(viewModel, layout, services);
        Root.Children.Add(_view);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(_view.DragRegion);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "glyphbar.ico"));
        AppWindow.Resize(new Windows.Graphics.SizeInt32(Scale(1020), Scale(820)));
        ApplyMaterial();

        AppWindow.Closing += (_, e) =>
        {
            if (_destroyed) return;
            e.Cancel = true;
            AppWindow.Hide();
            _view.SetActive(false);
        };
    }

    private int Scale(int dip) => (int)(dip * Native.GetDpiForWindow(_hwnd) / 96d);

    private void ApplyMaterial()
    {
        bool acrylic = _settings.Current.Appearance.Backdrop == BackdropMode.Acrylic;
        SystemBackdrop = acrylic ? new DesktopAcrylicBackdrop() : null;
        Root.Background = new SolidColorBrush(acrylic ? Color.FromArgb(0xD0, 0x08, 0x08, 0x08) : Color.FromArgb(0xFF, 0x08, 0x08, 0x08));
    }

    public void ShowAndFocus()
    {
        ApplyMaterial();
        AppWindow.Show(true);
        Activate();
        Native.SetForegroundWindow(_hwnd);
        _view.SetActive(true);
        Motion.Enter(_view);
    }

    public void Destroy()
    {
        _destroyed = true;
        _view.Detach();
        Close();
    }
}
