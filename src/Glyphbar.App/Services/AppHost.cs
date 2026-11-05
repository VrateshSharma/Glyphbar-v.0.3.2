using Glyphbar.App.ViewModels;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Services;
using Glyphbar.Data.Configuration;
using Glyphbar.Data.Database;
using Glyphbar.Data.Repositories;
using Glyphbar.Platform.AppBar;
using Glyphbar.Platform.Applications;
using Glyphbar.Platform.Battery;
using Glyphbar.Platform.Hotkeys;
using Glyphbar.Platform.Media;
using Glyphbar.Platform.Power;
using Glyphbar.Platform.Search;
using Glyphbar.Platform.Startup;
using Glyphbar.Platform.Storage;
using Glyphbar.Platform.SystemInfo;
using Glyphbar.Platform.SystemTray;
using Glyphbar.Platform.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Glyphbar.App.Services;

/// <summary>Composition root: the only place that knows concrete platform types.</summary>
public static class AppHost
{
    public static IHost Build(DispatcherQueue dispatcher)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        var paths = new AppPaths();
        var s = builder.Services;

        s.AddLogging(l => l.SetMinimumLevel(LogLevel.Information).AddProvider(new FileLoggerProvider(paths)));
        s.AddSingleton(paths);
        s.AddSingleton(new UiDispatcher(dispatcher));

        // Data
        s.AddSingleton<ISettingsStore, JsonSettingsStore>();
        s.AddSingleton<GlyphbarDatabase>();
        s.AddSingleton<ICalendarRepository, CalendarRepository>();
        s.AddSingleton<IWidgetLayoutRepository, WidgetLayoutRepository>();
        s.AddSingleton<ApplicationRepository>();
        s.AddSingleton<IPinnedAppsRepository>(p => p.GetRequiredService<ApplicationRepository>());
        s.AddSingleton<IRecentAppsRepository>(p => p.GetRequiredService<ApplicationRepository>());
        s.AddSingleton<IUserAppsRepository>(p => p.GetRequiredService<ApplicationRepository>());

        // Platform
        s.AddSingleton<IClock, SystemClock>();
        s.AddSingleton<ShellEventWindow>();
        s.AddSingleton<ShellWorker>();
        s.AddSingleton<IAppCatalog, ShellAppCatalog>();
        s.AddSingleton<IIconProvider, ShellIconProvider>();
        s.AddSingleton<IAppLauncher, ShellLauncher>();
        s.AddSingleton<IRunningWindows, RunningWindowTracker>();
        s.AddSingleton<IBatteryService, BatteryService>();
        s.AddSingleton<IStorageService, StorageService>();
        s.AddSingleton<ISystemInfoService, SystemInfoService>();
        s.AddSingleton<IMediaService, MediaService>();
        s.AddSingleton<IFileSearchProvider, WindowsSearchProvider>();
        if (DryRunPowerExecutor.IsEnabled) s.AddSingleton<IPowerExecutor, DryRunPowerExecutor>();
        else s.AddSingleton<IPowerExecutor, PowerExecutor>();
        s.AddSingleton<IStartupManager, StartupManager>();
        s.AddSingleton<IHotkeyService, HotkeyService>();
        s.AddSingleton<TrayIcon>();

        // Application
        s.AddSingleton<PowerMenuController>();
        s.AddSingleton<ClockService>();
        s.AddSingleton<IconLoader>();
        s.AddSingleton<PinService>();
        s.AddSingleton<WidgetLayoutService>();
        s.AddSingleton<SystemMonitor>();
        s.AddHostedService(p => p.GetRequiredService<SystemMonitor>());
        s.AddSingleton<ShellCoordinator>();
        s.AddTransient<TaskbarViewModel>();
        s.AddSingleton<DashboardViewModel>();
        s.AddSingleton<SearchViewModel>();
        s.AddSingleton<LauncherViewModel>();
        s.AddSingleton<PowerMenuViewModel>();
        s.AddTransient<SettingsViewModel>();
        s.AddTransient<ClockWidgetViewModel>();
        s.AddTransient<DateWidgetViewModel>();
        s.AddTransient<CalendarWidgetViewModel>();
        s.AddTransient<BatteryWidgetViewModel>();
        s.AddTransient<StorageWidgetViewModel>();
        s.AddTransient<MusicWidgetViewModel>();
        s.AddTransient<SystemInfoWidgetViewModel>();

        return builder.Build();
    }
}
