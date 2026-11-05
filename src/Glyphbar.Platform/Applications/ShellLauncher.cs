using System.ComponentModel;
using System.Runtime.InteropServices;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Glyphbar.Platform.Interop;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Applications;

/// <summary>
/// Everything opened from the UI goes through here via ShellExecuteEx. Each target must be an AppsFolder
/// item, an existing path, or an allow-listed settings page, so UI text can never become a command line.
/// </summary>
public sealed class ShellLauncher(ILogger<ShellLauncher> logger) : IAppLauncher
{
    private const uint SEE_MASK_FLAG_NO_UI = 0x400, SEE_MASK_NOASYNC = 0x100;
    private const int SW_SHOWNORMAL = 1, ERROR_CANCELLED = 1223;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHELLEXECUTEINFO
    {
        public uint cbSize, fMask;
        public nint hwnd;
        public string? lpVerb, lpFile, lpParameters, lpDirectory;
        public int nShow;
        public nint hInstApp, lpIDList;
        public string? lpClass;
        public nint hkeyClass;
        public uint dwHotKey;
        public nint hIcon, hProcess;
    }

    [DllImport("shell32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO info);

    public Task<OperationResult> LaunchAsync(string target, bool asAdministrator = false)
    {
        if (!IsLaunchable(target))
            return Task.FromResult(OperationResult.Fail("That application is no longer available."));
        // 'runas' is the supported route to UAC; Windows shows consent. We never elevate ourselves.
        return ExecuteAsync(target, asAdministrator ? "runas" : null);
    }

    public Task<OperationResult> OpenAsync(string target)
    {
        bool allowed = target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)
            ? SettingsCatalog.IsAllowed(target)
            : File.Exists(target) || Directory.Exists(target);
        return allowed ? ExecuteAsync(target, null) : Task.FromResult(OperationResult.Fail("That item is no longer available."));
    }

    public static bool IsLaunchable(string target) =>
        target.StartsWith(ShellAppCatalog.AppsFolderPrefix, StringComparison.OrdinalIgnoreCase)
        || File.Exists(target);

    private Task<OperationResult> ExecuteAsync(string file, string? verb)
    {
        var done = new TaskCompletionSource<OperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        // ShellExecuteEx can load shell extensions and wait on UAC, so it gets its own short-lived STA thread.
        var thread = new Thread(() =>
        {
            var info = new SHELLEXECUTEINFO
            {
                cbSize = (uint)Marshal.SizeOf<SHELLEXECUTEINFO>(),
                fMask = SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI,
                lpFile = file,
                lpVerb = verb,
                nShow = SW_SHOWNORMAL,
            };
            if (ShellExecuteEx(ref info))
            {
                done.TrySetResult(OperationResult.Ok);
                return;
            }
            int error = Marshal.GetLastWin32Error();
            if (error == ERROR_CANCELLED)
            {
                done.TrySetResult(OperationResult.Fail("Cancelled."));
                return;
            }
            logger.LogWarning("ShellExecuteEx failed for {File}: {Error}", file, error);
            done.TrySetResult(OperationResult.Fail(new Win32Exception(error).Message));
        })
        { IsBackground = true, Name = "Glyphbar launch" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return done.Task;
    }

    public Task<OperationResult> RevealInExplorerAsync(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return Task.FromResult(OperationResult.Fail("That location no longer exists."));

        return Task.Run(() =>
        {
            if (Shell.SHParseDisplayName(path, 0, out nint pidl, 0, out _) != 0)
                return OperationResult.Fail("Could not open that location.");
            try
            {
                int hr = Shell.SHOpenFolderAndSelectItems(pidl, 0, null, 0);
                return hr == 0 ? OperationResult.Ok : OperationResult.Fail("Could not open that location.");
            }
            finally
            {
                Shell.CoTaskMemFree(pidl);
            }
        });
    }
}
