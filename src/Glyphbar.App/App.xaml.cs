using Glyphbar.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace Glyphbar.App;

public partial class App : Application
{
    private IHost? _host;
    private ILogger<App>? _logger;
    private ShellCoordinator? _shell;

    public App()
    {
        InitializeComponent();
        // Glyphbar lives in the tray. By default WinUI ends the process when the last window closes, which would
        // make hiding the bar or restarting the interface quit the app.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _logger?.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _host = AppHost.Build(dispatcher);
        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("Glyphbar starting, version {Version}", typeof(App).Assembly.GetName().Version);

        UiDispatcher.ErrorSink = e => _logger.LogError(e, "Unhandled exception in a UI callback");

        AppInstance.GetCurrent().Activated += (_, activation) =>
        {
            // Read the arguments here: the activation object belongs to this thread, not the UI thread.
            CommandLine? args = null;
            UiDispatcher.Guard(() => args = CommandLine.From(activation));
            if (args is not null) dispatcher.TryEnqueue(() => UiDispatcher.Guard(() => _shell?.HandleArguments(args)));
        };

        // async void: anything thrown after the first await would otherwise fail-fast with no log entry.
        try
        {
            await _host.StartAsync();
            _shell = _host.Services.GetRequiredService<ShellCoordinator>();
            await _shell.StartAsync(CommandLine.From(AppInstance.GetCurrent().GetActivatedEventArgs()));
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Startup failed");
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.Exception, "Unhandled UI exception");
        // A broken widget should not take the desktop's taskbar down with it.
        e.Handled = true;
    }

    /// <summary>Releases the reserved screen space and shell registrations, then ends the process.</summary>
    public async Task ShutdownAsync()
    {
        if (_shell is not null) await _shell.StopAsync();
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        Exit();
    }
}
