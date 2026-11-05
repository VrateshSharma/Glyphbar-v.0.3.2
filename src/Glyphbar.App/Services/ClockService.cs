using Glyphbar.Core.Interfaces;
using Microsoft.UI.Dispatching;

namespace Glyphbar.App.Services;

/// <summary>
/// The one clock for the whole UI. It wakes once a minute unless something visible shows seconds,
/// and it aligns to the boundary so displays never lag the real time.
/// </summary>
public sealed class ClockService
{
    private readonly IClock _clock;
    private readonly DispatcherQueueTimer _timer;
    private int _secondsConsumers;

    public ClockService(IClock clock, UiDispatcher ui)
    {
        _clock = clock;
        _timer = ui.Queue.CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) =>
        {
            UiDispatcher.Guard(() => Tick?.Invoke(this, EventArgs.Empty));
            Schedule();
        };
        Schedule();
    }

    public event EventHandler? Tick;
    public DateTimeOffset Now => _clock.Now;

    /// <summary>Asks for per-second ticks. Dispose the result when the display is hidden.</summary>
    public IDisposable RequestSeconds()
    {
        _secondsConsumers++;
        Schedule();
        return new Lease(this);
    }

    private void Schedule()
    {
        var now = _clock.Now;
        double ms = _secondsConsumers > 0
            ? 1000 - now.Millisecond
            : (60 - now.Second) * 1000d - now.Millisecond;
        _timer.Stop();
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, ms + 5));
        _timer.Start();
    }

    private sealed class Lease(ClockService owner) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            owner._secondsConsumers--;
        }
    }
}
