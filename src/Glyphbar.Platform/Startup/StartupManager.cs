using Glyphbar.Core.Interfaces;
using Glyphbar.Platform.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Glyphbar.Platform.Startup;

/// <summary>
/// Opt-in launch at sign-in. Packaged builds use the manifest StartupTask, which also shows up in
/// Windows' Startup Apps page. Unpackaged dev builds use the per-user Run key. Only ever called
/// when the user flips the setting.
/// </summary>
public sealed class StartupManager(ILogger<StartupManager> logger) : IStartupManager
{
    public const string TaskId = "GlyphbarStartup";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Glyphbar";

    public async Task<bool> IsEnabledAsync()
    {
        if (Native.IsPackaged())
        {
            var task = await StartupTask.GetAsync(TaskId);
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is not null;
    }

    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        try
        {
            if (Native.IsPackaged())
            {
                var task = await StartupTask.GetAsync(TaskId);
                if (enabled)
                    return await task.RequestEnableAsync() is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
                task.Disable();
                return false;
            }

            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --startup");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            return enabled;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not change launch-at-startup");
            return await IsEnabledAsync();
        }
    }
}
