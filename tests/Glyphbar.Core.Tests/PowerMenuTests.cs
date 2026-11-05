using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;

namespace Glyphbar.Core.Tests;

public class PowerMenuTests
{
    private sealed class FakeExecutor : IPowerExecutor
    {
        public List<PowerAction> Executed { get; } = [];
        public OperationResult Result { get; set; } = OperationResult.Ok;

        public Task<OperationResult> ExecuteAsync(PowerAction action)
        {
            Executed.Add(action);
            return Task.FromResult(Result);
        }
    }

    private static (PowerMenuController, FakeExecutor, TestSettings) Create(Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        var executor = new FakeExecutor();
        var settings = new TestSettings();
        var controller = new PowerMenuController(executor, settings) { Delay = delay ?? ((_, _) => Task.CompletedTask) };
        return (controller, executor, settings);
    }

    [Theory]
    [InlineData(PowerAction.Shutdown)]
    [InlineData(PowerAction.Restart)]
    public async Task Shutdown_and_restart_never_run_without_confirmation(PowerAction action)
    {
        var (c, executor, settings) = Create();
        settings.Current.System.ConfirmSignOut = false;
        settings.Current.System.ConfirmSleep = false;
        await c.SelectAsync(action);
        Assert.Empty(executor.Executed);
        Assert.Equal(PowerMenuState.Confirming, c.State);
        await c.ConfirmAsync();
        Assert.Equal([action], executor.Executed);
        Assert.Equal(PowerMenuState.Idle, c.State);
    }

    [Fact]
    public async Task Cancel_at_confirmation_runs_nothing()
    {
        var (c, executor, _) = Create();
        await c.SelectAsync(PowerAction.Shutdown);
        c.Cancel();
        await c.ConfirmAsync();
        Assert.Empty(executor.Executed);
        Assert.Equal(PowerMenuState.Idle, c.State);
    }

    [Fact]
    public async Task Lock_runs_immediately()
    {
        var (c, executor, _) = Create();
        await c.SelectAsync(PowerAction.Lock);
        Assert.Equal([PowerAction.Lock], executor.Executed);
    }

    [Fact]
    public async Task Sign_out_confirmation_follows_the_setting()
    {
        var (c, executor, settings) = Create();
        await c.SelectAsync(PowerAction.SignOut);
        Assert.Empty(executor.Executed);
        c.Cancel();
        settings.Current.System.ConfirmSignOut = false;
        await c.SelectAsync(PowerAction.SignOut);
        Assert.Equal([PowerAction.SignOut], executor.Executed);
    }

    [Fact]
    public async Task Countdown_runs_only_when_enabled_and_counts_each_second()
    {
        int ticks = 0;
        var (c, executor, settings) = Create((_, _) => { ticks++; return Task.CompletedTask; });
        settings.Current.System.PowerCountdownSeconds = 3;
        await c.SelectAsync(PowerAction.Restart);
        await c.ConfirmAsync();
        Assert.Equal(3, ticks);
        Assert.Equal([PowerAction.Restart], executor.Executed);
    }

    [Fact]
    public async Task Cancelling_during_countdown_prevents_execution()
    {
        var gate = new TaskCompletionSource();
        var (c, executor, settings) = Create(async (_, ct) =>
        {
            gate.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        });
        settings.Current.System.PowerCountdownSeconds = 5;
        await c.SelectAsync(PowerAction.Shutdown);
        var running = c.ConfirmAsync();
        await gate.Task;
        Assert.Equal(PowerMenuState.CountingDown, c.State);
        c.Cancel();
        await running;
        Assert.Empty(executor.Executed);
        Assert.Equal(PowerMenuState.Idle, c.State);
    }

    [Fact]
    public async Task Failure_is_reported_and_returns_to_idle()
    {
        var (c, executor, _) = Create();
        executor.Result = OperationResult.Fail("Denied");
        await c.SelectAsync(PowerAction.Lock);
        Assert.Equal("Denied", c.LastError);
        Assert.Equal(PowerMenuState.Idle, c.State);
    }

    [Fact]
    public async Task A_second_selection_while_busy_is_ignored()
    {
        var (c, executor, _) = Create();
        await c.SelectAsync(PowerAction.Shutdown);
        await c.SelectAsync(PowerAction.Lock);
        Assert.Empty(executor.Executed);
        Assert.Equal(PowerAction.Shutdown, c.Pending);
    }
}
