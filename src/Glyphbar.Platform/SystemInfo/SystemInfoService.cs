using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Glyphbar.Platform.SystemInfo;

public sealed class SystemInfoService(ILogger<SystemInfoService> logger) : ISystemInfoService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX status);
    [DllImport("kernel32", SetLastError = true)] private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    private long _lastIdle, _lastTotal;
    private string? _gpuName;
    private bool _gpuRead;

    public Task<SystemInfoSnapshot> ReadAsync(CancellationToken ct = default) => Task.Run(() =>
    {
        var memory = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        bool hasMemory = GlobalMemoryStatusEx(ref memory);
        var (connected, name) = ReadNetwork();
        return new SystemInfoSnapshot(
            ReadCpu(),
            hasMemory ? (long)(memory.ullTotalPhys - memory.ullAvailPhys) : null,
            hasMemory ? (long)memory.ullTotalPhys : null,
            ReadGpuName(),
            connected,
            name);
    }, ct);

    /// <summary>CPU use between two calls. The first call has no baseline, so it reports nothing.</summary>
    private double? ReadCpu()
    {
        if (!GetSystemTimes(out long idle, out long kernel, out long user)) return null;
        long total = kernel + user;   // kernel time includes idle time
        long deltaTotal = total - _lastTotal, deltaIdle = idle - _lastIdle;
        bool hasBaseline = _lastTotal != 0;
        _lastTotal = total;
        _lastIdle = idle;
        if (!hasBaseline || deltaTotal <= 0) return null;
        return Math.Clamp(100d * (deltaTotal - deltaIdle) / deltaTotal, 0, 100);
    }

    private string? ReadGpuName()
    {
        if (_gpuRead) return _gpuName;
        _gpuRead = true;
        const string displayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(displayClass);
            var names = new List<string>();
            foreach (string sub in root?.GetSubKeyNames() ?? [])
            {
                if (!int.TryParse(sub, out _)) continue;
                try
                {
                    using var key = root!.OpenSubKey(sub);
                    if (key?.GetValue("DriverDesc") is string desc && !names.Contains(desc)) names.Add(desc);
                }
                catch (System.Security.SecurityException) { /* some adapters' keys are protected */ }
            }
            _gpuName = names.Count > 0 ? string.Join(" + ", names) : null;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            logger.LogDebug(e, "GPU name unavailable");
        }
        return _gpuName;
    }

    private static (bool?, string?) ReadNetwork()
    {
        try
        {
            var up = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n =>
                n.OperationalStatus == OperationalStatus.Up
                && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                && n.GetIPProperties().GatewayAddresses.Count > 0);
            return (up is not null, up?.Name);
        }
        catch (NetworkInformationException)
        {
            return (null, null);
        }
    }
}
