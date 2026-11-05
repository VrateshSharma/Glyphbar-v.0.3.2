using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Glyphbar.App.Services;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Glyphbar.App.ViewModels;

public sealed partial class PowerActionViewModel(PowerAction action, string title, string glyph, string description, Func<PowerAction, Task> select)
{
    public string Title { get; } = title;
    public string Glyph { get; } = glyph;
    public string Description { get; } = description;

    [RelayCommand] private Task Select() => select(action);
}

public sealed partial class PowerMenuViewModel : ObservableObject
{
    private readonly PowerMenuController _controller;
    private readonly UiDispatcher _ui;
    private readonly IServiceProvider _services;
    private bool _executed;

    public PowerMenuViewModel(PowerMenuController controller, ISettingsStore settings, UiDispatcher ui, IServiceProvider services)
    {
        _controller = controller;
        _ui = ui;
        _services = services;
        Settings = settings;
        Actions =
        [
            new(PowerAction.Lock, "Lock", "", "Lock the screen. Everything keeps running.", Select),
            new(PowerAction.Sleep, "Sleep", "", "Low-power state. Wakes where you left off.", Select),
            new(PowerAction.SignOut, "Sign out", "", "Closes your apps and returns to the sign-in screen.", Select),
            new(PowerAction.Restart, "Restart", "", "Closes all apps and restarts Windows.", Select),
            new(PowerAction.Shutdown, "Shut down", "", "Closes all apps and turns the PC off.", Select),
        ];
        _controller.StateChanged += (_, _) => _ui.Post(Sync);
    }

    public ISettingsStore Settings { get; }
    public IReadOnlyList<PowerActionViewModel> Actions { get; }
    public event Action? ConfirmationShown;

    [ObservableProperty] public partial bool ShowActions { get; set; } = true;
    [ObservableProperty] public partial bool ShowConfirm { get; set; }
    [ObservableProperty] public partial bool ShowConfirmButton { get; set; }
    [ObservableProperty] public partial string ConfirmTitle { get; set; } = "";
    [ObservableProperty] public partial string ConfirmText { get; set; } = "";
    [ObservableProperty] public partial string ConfirmButton { get; set; } = "";
    [ObservableProperty] public partial string Error { get; set; } = "";

    private Task Select(PowerAction action) => _controller.SelectAsync(action);

    [RelayCommand] private Task Confirm() => _controller.ConfirmAsync();
    [RelayCommand] private void Cancel() => _controller.Cancel();

    public void Reset()
    {
        _controller.Cancel();
        Error = "";
        Sync();
    }

    private void Sync()
    {
        var state = _controller.State;
        bool wasConfirming = ShowConfirm;
        ShowConfirm = state is PowerMenuState.Confirming or PowerMenuState.CountingDown;
        ShowActions = !ShowConfirm;
        ShowConfirmButton = state == PowerMenuState.Confirming;

        if (_controller.Pending is { } action)
        {
            var (title, text, button) = Describe(action);
            ConfirmTitle = title;
            ConfirmText = state == PowerMenuState.CountingDown ? $"Continuing in {_controller.SecondsRemaining} seconds." : text;
            ConfirmButton = button;
        }

        if (state == PowerMenuState.Executing) _executed = true;
        if (state == PowerMenuState.Idle)
        {
            Error = _controller.LastError ?? "";
            // Close only after an action really ran. Opening the menu also passes through Idle.
            if (_executed && _controller.LastError is null) _services.GetRequiredService<ShellCoordinator>().HideOverlays();
            _executed = false;
        }
        if (ShowConfirm && !wasConfirming) ConfirmationShown?.Invoke();
    }

    private static (string, string, string) Describe(PowerAction action) => action switch
    {
        PowerAction.Shutdown => ("Shut down this PC?", "Unsaved work in open apps may be lost.", "Shut down"),
        PowerAction.Restart => ("Restart this PC?", "Unsaved work in open apps may be lost.", "Restart"),
        PowerAction.SignOut => ("Sign out?", "Your apps will close. Unsaved work may be lost.", "Sign out"),
        PowerAction.Sleep => ("Go to sleep?", "Your session stays open.", "Sleep"),
        _ => ("Lock?", "", "Lock"),
    };
}
