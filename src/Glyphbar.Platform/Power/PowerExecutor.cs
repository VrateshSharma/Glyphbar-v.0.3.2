using System.ComponentModel;
using System.Runtime.InteropServices;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Power;

/// <summary>
/// Executes power actions through documented APIs, never through a shell command.
/// Shutdown and restart need SeShutdownPrivilege, which every interactive user holds but which is
/// disabled by default, so it is enabled on our own token; no elevation is requested.
/// </summary>
public sealed class PowerExecutor(ILogger<PowerExecutor> logger) : IPowerExecutor
{
    private const uint EWX_LOGOFF = 0, EWX_REBOOT = 2, EWX_POWEROFF = 8;
    // SHTDN_REASON_MAJOR_OTHER | SHTDN_REASON_MINOR_OTHER | SHTDN_REASON_FLAG_PLANNED
    private const uint ReasonPlanned = 0x80000000;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x20, TOKEN_QUERY = 0x8, SE_PRIVILEGE_ENABLED = 2;

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public long Luid;
        public uint Attributes;
    }

    [DllImport("user32", SetLastError = true)] private static extern bool ExitWindowsEx(uint flags, uint reason);
    [DllImport("user32", SetLastError = true)] private static extern bool LockWorkStation();
    [DllImport("powrprof", SetLastError = true)] private static extern bool SetSuspendState(bool hibernate, bool force, bool wakeupEventsDisabled);
    [DllImport("kernel32")] private static extern nint GetCurrentProcess();
    [DllImport("advapi32", SetLastError = true)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool LookupPrivilegeValue(string? system, string name, out long luid);
    [DllImport("advapi32", SetLastError = true)] private static extern bool AdjustTokenPrivileges(nint token, bool disableAll, ref TOKEN_PRIVILEGES state, uint length, nint prev, nint returned);
    [DllImport("kernel32", SetLastError = true)] private static extern bool CloseHandle(nint handle);

    public Task<OperationResult> ExecuteAsync(PowerAction action) => Task.Run(() =>
    {
        logger.LogInformation("Executing power action {Action}", action);
        bool ok = action switch
        {
            PowerAction.Lock => LockWorkStation(),
            PowerAction.Sleep => SetSuspendState(false, false, false),
            PowerAction.SignOut => ExitWindowsEx(EWX_LOGOFF, ReasonPlanned),
            PowerAction.Restart => EnableShutdownPrivilege() && ExitWindowsEx(EWX_REBOOT, ReasonPlanned),
            PowerAction.Shutdown => EnableShutdownPrivilege() && ExitWindowsEx(EWX_POWEROFF, ReasonPlanned),
            _ => false,
        };
        if (ok) return OperationResult.Ok;

        int error = Marshal.GetLastWin32Error();
        string message = new Win32Exception(error).Message;
        logger.LogWarning("Power action {Action} failed: {Error} {Message}", action, error, message);
        return OperationResult.Fail(message);
    });

    /// <summary>Public so integration tests can check it without shutting anything down.</summary>
    public static bool EnableShutdownPrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out nint token))
            return false;
        try
        {
            if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out long luid)) return false;
            var state = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
            // AdjustTokenPrivileges can return true without assigning; the last error is the real answer.
            return AdjustTokenPrivileges(token, false, ref state, 0, 0, 0) && Marshal.GetLastWin32Error() == 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }
}
