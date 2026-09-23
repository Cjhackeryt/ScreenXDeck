using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ScreenControl.Monitors;

namespace ScreenXDeckPlugin;

public sealed class MonitorBrightnessService
{
    private const byte BrightnessVcpCode = 0x10;
    private readonly ILogger _logger;
    private readonly IMonitorService _monitors;

    public MonitorBrightnessService(ILogger logger, IMonitorService monitors)
    {
        _logger = logger;
        _monitors = monitors;
    }

    public Task AdjustAsync(int delta, CancellationToken cancellationToken) =>
        SetBrightnessAsync(current => current + delta, cancellationToken);

    public Task SetAsync(int brightness, CancellationToken cancellationToken) =>
        SetBrightnessAsync(_ => brightness, cancellationToken);

    public Task<int?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<int?>(ReadCurrentBrightness());
    }

    public int GetDdcMonitorCount()
    {
        try
        {
            return DdcMonitor.Enumerate(_logger).Count(monitor =>
            {
                using (monitor)
                    return monitor.TryGetBrightness(out _, out _);
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to enumerate DDC/CI monitors.");
            return 0;
        }
    }

    public Task<int?> GetDdcBrightnessAsync(int monitorIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var monitor = GetDdcMonitor(monitorIndex);
            int? current = null;
            if (monitor is not null && monitor.TryGetBrightness(out var value, out _))
                current = value;
            return Task.FromResult(current);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to read DDC/CI monitor {MonitorIndex}.", monitorIndex + 1);
            return Task.FromResult<int?>(null);
        }
    }

    public Task SetDdcBrightnessAsync(int monitorIndex, int brightness, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var monitor = GetDdcMonitor(monitorIndex);
        if (monitor is null || !monitor.TrySetBrightness(Math.Clamp(brightness, 0, 100)))
            throw new InvalidOperationException($"DDC/CI monitor {monitorIndex + 1} is unavailable.");
        return Task.CompletedTask;
    }

    private Task SetBrightnessAsync(Func<int, int> calculate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var current = ReadCurrentBrightness();
        var target = Math.Clamp(calculate(current ?? 50), 0, 100);
        var changed = 0;

        changed += SetWmiBrightness(target, cancellationToken);
        changed += SetDdcBrightness(target, cancellationToken);
        changed += SetGammaBrightness(target, cancellationToken);

        if (changed == 0)
        {
            throw new InvalidOperationException(
                "No brightness-capable monitor was found. For external monitors, enable DDC/CI " +
                "in the monitor menu and connect it directly or through a DDC/CI-compatible dock.");
        }

        _logger.LogInformation("Set brightness to {Brightness}% on {MonitorCount} monitor(s).", target, changed);
        return Task.CompletedTask;
    }

    private int? ReadCurrentBrightness()
    {
        try
        {
            using var levels = new ManagementObjectSearcher(
                "root\\WMI",
                "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE").Get();
            var value = levels.Cast<ManagementObject>().FirstOrDefault()?["CurrentBrightness"];
            if (value is not null)
                return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (ManagementException ex)
        {
            _logger.LogDebug(ex, "WMI brightness read is unavailable; trying DDC/CI.");
        }

        try
        {
            foreach (var monitor in DdcMonitor.Enumerate(_logger))
            {
                using (monitor)
                {
                    if (monitor.TryGetBrightness(out var current, out _))
                        return current;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DDC/CI brightness read is unavailable.");
        }

        try
        {
            return _monitors.GetMonitors()
                .FirstOrDefault(monitor => monitor.SoftwareBrightness)
                ?.BrightnessPercent;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Software brightness read is unavailable.");
            return null;
        }
    }

    private int SetWmiBrightness(int target, CancellationToken cancellationToken)
    {
        try
        {
            using var methods = new ManagementObjectSearcher(
                "root\\WMI",
                "SELECT InstanceName FROM WmiMonitorBrightnessMethods WHERE Active = TRUE").Get();
            var writableMonitors = methods.Cast<ManagementObject>().ToArray();
            foreach (var monitor in writableMonitors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                monitor.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)target });
            }

            return writableMonitors.Length;
        }
        catch (ManagementException ex)
        {
            _logger.LogDebug(ex, "WMI brightness control is unavailable; trying DDC/CI.");
            return 0;
        }
    }

    private int SetDdcBrightness(int target, CancellationToken cancellationToken)
    {
        var changed = 0;
        foreach (var monitor in DdcMonitor.Enumerate(_logger))
        {
            using (monitor)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (monitor.TrySetBrightness(target))
                    changed++;
            }
        }

        return changed;
    }

    private int SetGammaBrightness(int target, CancellationToken cancellationToken)
    {
        var changed = 0;
        foreach (var monitor in _monitors.GetMonitors().Where(monitor => monitor.SoftwareBrightness))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_monitors.TrySetBrightness(monitor.Index, target))
                changed++;
        }

        return changed;
    }

    private DdcMonitor? GetDdcMonitor(int monitorIndex)
    {
        if (monitorIndex < 0)
            return null;

        var monitors = DdcMonitor.Enumerate(_logger).ToArray();
        for (var index = 0; index < monitors.Length; index++)
        {
            if (index == monitorIndex)
                return monitors[index];
            monitors[index].Dispose();
        }

        return null;
    }

    private sealed class DdcMonitor : IDisposable
    {
        private readonly IntPtr _handle;
        private readonly ILogger _logger;

        private DdcMonitor(IntPtr handle, ILogger logger)
        {
            _handle = handle;
            _logger = logger;
        }

        public static IEnumerable<DdcMonitor> Enumerate(ILogger logger)
        {
            var monitors = new List<DdcMonitor>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref RECT _, IntPtr _) =>
            {
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0)
                    return true;

                var physical = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, physical))
                    return true;

                monitors.AddRange(physical.Select(item => new DdcMonitor(item.hPhysicalMonitor, logger)));
                return true;
            }, IntPtr.Zero);

            return monitors;
        }

        public bool TryGetBrightness(out int current, out int maximum)
        {
            current = 0;
            maximum = 0;
            uint maximumValue;
            if (!GetVCPFeatureAndVCPFeatureReply(
                    _handle, BrightnessVcpCode, out _, out var value, out maximumValue, out _))
                return false;

            maximum = (int)maximumValue;
            current = maximumValue == 0 ? 0 : (int)Math.Round(value * 100.0 / maximumValue);
            return true;
        }

        public bool TrySetBrightness(int target)
        {
            if (!GetVCPFeatureAndVCPFeatureReply(
                    _handle, BrightnessVcpCode, out _, out _, out var maximum, out _))
                return false;

            var value = (uint)Math.Round(target * maximum / 100.0);
            if (!SetVCPFeature(_handle, BrightnessVcpCode, value))
                return false;

            return true;
        }

        public void Dispose()
        {
            if (!DestroyPhysicalMonitor(_handle))
                _logger.LogDebug("Failed to release a physical monitor handle.");
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr clip,
            MonitorEnumProc callback,
            IntPtr data);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            out uint numberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint physicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] physicalMonitorArray);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetVCPFeatureAndVCPFeatureReply(
            IntPtr hMonitor,
            byte vcpCode,
            out byte vcpType,
            out uint currentValue,
            out uint maximumValue,
            out uint capabilitiesStringLength);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool SetVCPFeature(
            IntPtr hMonitor,
            byte vcpCode,
            uint value);

        private delegate bool MonitorEnumProc(
            IntPtr monitor,
            IntPtr hdc,
            ref RECT rect,
            IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }
    }
}
