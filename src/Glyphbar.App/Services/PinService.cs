using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.Extensions.Logging;

namespace Glyphbar.App.Services;

/// <summary>Pinned apps and launching, shared by the dock, launcher and search.</summary>
public sealed class PinService(
    IPinnedAppsRepository pins,
    IRecentAppsRepository recent,
    IAppLauncher launcher,
    IAppCatalog catalog,
    ISettingsStore settings,
    ILogger<PinService> logger)
{
    private static readonly string[] DefaultPinNames = ["File Explorer", "Settings"];

    public IReadOnlyList<PinnedApp> Pins { get; private set; } = [];
    public event EventHandler? Changed;

    public async Task LoadAsync()
    {
        Pins = await pins.GetAllAsync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gives a brand-new install something to show, resolved from the catalog, not from paths.</summary>
    public async Task SeedDefaultsAsync()
    {
        if (settings.Current.System.FirstRunGuideShown || Pins.Count > 0) return;
        foreach (string name in DefaultPinNames)
        {
            var app = catalog.Apps.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (app is not null) await pins.PinAsync(ToPin(app));
        }
        await LoadAsync();
    }

    public bool IsPinned(string appId) => Pins.Any(p => p.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase));

    private static PinnedApp ToPin(AppEntry app) => new()
    {
        AppId = app.Id,
        Name = app.Name,
        LaunchTarget = app.LaunchTarget,
        ExecutablePath = app.ExecutablePath,
    };

    public async Task PinAsync(AppEntry app)
    {
        await pins.PinAsync(ToPin(app));
        await LoadAsync();
    }

    public async Task PinAsync(string appId, string name, string target, string? exe)
    {
        await pins.PinAsync(new PinnedApp { AppId = appId, Name = name, LaunchTarget = target, ExecutablePath = exe });
        await LoadAsync();
    }

    public async Task UnpinAsync(string appId)
    {
        await pins.UnpinAsync(appId);
        await LoadAsync();
    }

    public async Task ReorderAsync(IReadOnlyList<string> appIds)
    {
        await pins.ReorderAsync(appIds);
        await LoadAsync();
    }

    public async Task SetIconAsync(string appId, string? path)
    {
        await pins.SetIconAsync(appId, path);
        await LoadAsync();
    }

    public async Task<OperationResult> LaunchAsync(string appId, string target, bool admin = false)
    {
        var result = await launcher.LaunchAsync(target, admin);
        if (result.Success)
        {
            try { await recent.RecordLaunchAsync(appId); }
            catch (Exception e) { logger.LogWarning(e, "Could not record launch history"); }
        }
        else
        {
            logger.LogWarning("Launch of {App} failed: {Error}", appId, result.Error);
        }
        return result;
    }
}
