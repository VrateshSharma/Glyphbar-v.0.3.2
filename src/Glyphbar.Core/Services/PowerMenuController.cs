using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;

namespace Glyphbar.Core.Services;

public enum PowerMenuState { Idle, Confirming, CountingDown, Executing }

/// <summary>
/// The only route from the power menu to the executor. Shutdown and restart always need an
/// explicit confirmation; no setting can turn that off.
/// </summary>
public sealed class PowerMenuController(IPowerExecutor executor, ISettingsStore settings)
{
    private CancellationTokenSource? _countdown;

    public PowerMenuState State { get; private set; }
    public PowerAction? Pending { get; private set; }
    public int SecondsRemaining { get; private set; }
    public string? LastError { get; private set; }
    public event EventHandler? StateChanged;

    /// <summary>Delay used by the countdown; tests replace it.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    public bool RequiresConfirmation(PowerAction action) => action switch
    {
        PowerAction.Shutdown or PowerAction.Restart => true,
        PowerAction.SignOut => settings.Current.System.ConfirmSignOut,
        PowerAction.Sleep => settings.Current.System.ConfirmSleep,
        _ => false,
    };

    public static bool UsesCountdown(PowerAction action) =>
        action is PowerAction.Shutdown or PowerAction.Restart or PowerAction.SignOut;

    public Task SelectAsync(PowerAction action)
    {
        if (State != PowerMenuState.Idle) return Task.CompletedTask;
        Pending = action;
        LastError = null;
        if (RequiresConfirmation(action))
        {
            Set(PowerMenuState.Confirming);
            return Task.CompletedTask;
        }
        return RunAsync(action);
    }

    public Task ConfirmAsync()
    {
        if (State != PowerMenuState.Confirming || Pending is not { } action) return Task.CompletedTask;
        return RunAsync(action);
    }

    public void Cancel()
    {
        if (State is PowerMenuState.Executing) return;
        _countdown?.Cancel();
        Pending = null;
        SecondsRemaining = 0;
        Set(PowerMenuState.Idle);
    }

    private async Task RunAsync(PowerAction action)
    {
        int seconds = UsesCountdown(action) ? Math.Clamp(settings.Current.System.PowerCountdownSeconds, 0, 60) : 0;
        if (seconds > 0)
        {
            using var cts = new CancellationTokenSource();
            _countdown = cts;
            try
            {
                for (SecondsRemaining = seconds; SecondsRemaining > 0; SecondsRemaining--)
                {
                    Set(PowerMenuState.CountingDown);
                    await Delay(TimeSpan.FromSeconds(1), cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            finally
            {
                _countdown = null;
            }
        }

        Set(PowerMenuState.Executing);
        var result = await executor.ExecuteAsync(action);
        LastError = result.Success ? null : result.Error;
        Pending = null;
        Set(PowerMenuState.Idle);
    }

    private void Set(PowerMenuState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
