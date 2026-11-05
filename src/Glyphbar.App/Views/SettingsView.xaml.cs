using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glyphbar.App.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ThemeApplier.Track(this, viewModel.Store);
        Loaded += (sender, args) => _ = ViewModel.LoadAsync();
    }

    public SettingsViewModel ViewModel { get; }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Reset all settings?",
            Content = "Appearance, taskbar and widget options go back to their defaults. Calendar events and pinned apps are kept.",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = ElementTheme.Dark,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.ResetAll();
    }
}
