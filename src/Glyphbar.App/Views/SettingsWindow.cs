using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Glyphbar.Core.Interfaces;
using Glyphbar.Platform.Interop;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace Glyphbar.App.Views;

public sealed class SettingsWindow : Window
{
    private readonly nint _hwnd;

    public SettingsWindow(SettingsViewModel viewModel, ISettingsStore settings)
    {
        Title = "Glyphbar Settings";
        Content = new SettingsView(viewModel);
        _hwnd = WindowNative.GetWindowHandle(this);
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "glyphbar.ico"));
        double scale = Native.GetDpiForWindow(_hwnd) / 96d;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(860 * scale), (int)(720 * scale)));
    }

    public void ShowAndFocus()
    {
        AppWindow.Show(true);
        Activate();
        Native.SetForegroundWindow(_hwnd);
    }
}
