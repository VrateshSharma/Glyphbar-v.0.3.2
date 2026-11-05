using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glyphbar.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Glyphbar.App.ViewModels;

public sealed partial class DashboardViewModel(IServiceProvider services) : ObservableObject
{
    private ShellCoordinator Shell => services.GetRequiredService<ShellCoordinator>();

    [ObservableProperty] public partial bool IsEditing { get; set; }

    [RelayCommand] private void OpenLauncher() => Shell.ToggleLauncher();
    [RelayCommand] private void OpenSettings() => Shell.ShowSettings();
    [RelayCommand] private void OpenPower() => Shell.TogglePowerMenu();
}
