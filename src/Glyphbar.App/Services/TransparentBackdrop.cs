using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Glyphbar.App.Services;

/// <summary>
/// Makes a window's backdrop fully transparent, so XAML's alpha reaches the desktop. This is what lets the
/// floating dock have anti-aliased rounded corners and an adjustable opacity that leaves the icons solid.
/// </summary>
public sealed class TransparentBackdrop : SystemBackdrop
{
    private static Windows.UI.Composition.Compositor? _compositor;
    private static Windows.System.DispatcherQueueController? _queue;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        // The brush must come from the system compositor, which needs a system DispatcherQueue on this thread.
        if (_compositor is null)
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() is null)
                _queue = CreateSystemQueue();
            _compositor = new Windows.UI.Composition.Compositor();
        }
        target.SystemBackdrop = _compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        target.SystemBackdrop = null;
        base.OnTargetDisconnected(target);
    }

    private static Windows.System.DispatcherQueueController CreateSystemQueue()
    {
        var options = new DispatcherQueueOptions
        {
            dwSize = System.Runtime.InteropServices.Marshal.SizeOf<DispatcherQueueOptions>(),
            threadType = 2,       // DQTYPE_THREAD_CURRENT
            apartmentType = 2,    // DQTAT_COM_STA
        };
        Marshal(CreateDispatcherQueueController(options, out nint controller));
        return WinRT.MarshalInspectable<Windows.System.DispatcherQueueController>.FromAbi(controller);
    }

    private static void Marshal(int hr) => System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hr);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize, threadType, apartmentType;
    }

    [System.Runtime.InteropServices.DllImport("CoreMessaging")]
    private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out nint controller);
}
