using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Glyphbar.App;

public static class Program
{
    private const string InstanceKey = "Glyphbar.Main";

    [System.Runtime.InteropServices.DllImport("user32")]
    private static extern bool AllowSetForegroundWindow(int processId);

    /// <summary>
    /// Custom entry point so a second launch can hand its arguments to the running instance
    /// and exit before any window exists.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var main = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!main.IsCurrent)
        {
            // We were just launched, so we hold foreground rights; pass them on so an overlay can take focus.
            AllowSetForegroundWindow(-1);
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            using var done = new ManualResetEventSlim();
            // Redirecting must not block the STA thread directly.
            _ = Task.Run(async () =>
            {
                await main.RedirectActivationToAsync(activation);
                done.Set();
            });
            done.Wait(TimeSpan.FromSeconds(5));
            return 0;
        }

        Application.Start(callback =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
        return 0;
    }
}
