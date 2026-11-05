using System.Collections.Concurrent;

namespace Glyphbar.Platform.Applications;

/// <summary>
/// One background STA thread for Shell COM calls. Shell objects expect an STA, and funnelling
/// the work through a single thread avoids a thread per icon when the launcher opens.
/// </summary>
public sealed class ShellWorker : IDisposable
{
    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;

    public ShellWorker()
    {
        _thread = new Thread(() =>
        {
            foreach (var work in _queue.GetConsumingEnumerable()) work();
        })
        { IsBackground = true, Name = "Glyphbar shell worker" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> RunAsync<T>(Func<T> work, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            if (ct.IsCancellationRequested)
            {
                tcs.TrySetCanceled(ct);
                return;
            }
            try { tcs.TrySetResult(work()); }
            catch (Exception e) { tcs.TrySetException(e); }
        }, CancellationToken.None);
        return tcs.Task;
    }

    public void Dispose() => _queue.CompleteAdding();
}
