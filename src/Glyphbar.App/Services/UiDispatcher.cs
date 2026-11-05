using Microsoft.UI.Dispatching;

namespace Glyphbar.App.Services;

public sealed class UiDispatcher(DispatcherQueue queue)
{
    /// <summary>Receives exceptions caught by <see cref="Guard"/>.</summary>
    public static Action<Exception>? ErrorSink { get; set; }

    public DispatcherQueue Queue => queue;

    public void Post(Action action)
    {
        if (queue.HasThreadAccess) Guard(action);
        else queue.TryEnqueue(() => Guard(action));
    }

    /// <summary>
    /// Wrap every dispatcher and timer callback in this. An exception escaping one never reaches
    /// Application.UnhandledException: it fail-fasts the process inside CoreMessagingXP (0xc000027b), with no log entry.
    /// </summary>
    public static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            ErrorSink?.Invoke(e);
        }
    }
}
