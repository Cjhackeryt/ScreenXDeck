using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ScreenxDeck.Core.Models;
using ScreenxDeck.Core.Native;
using Serilog;

namespace ScreenxDeck.Services;

public sealed class DisplayManagerService : IDisposable
{
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private List<MonitorInfoModel> _monitors = [];
    private readonly Dictionary<IntPtr, PHYSICAL_MONITOR[]> _physicalMonitorsByHMon = new();

    public event Action? DisplaysChanged;

    public IReadOnlyList<MonitorInfoModel> ConnectedMonitors
    {
        get
        {
            lock (_lock)
            {
                return _monitors.ToList();
            }
        }
    }

    public MonitorInfoModel? PrimaryMonitor
    {
        get
        {
            lock (_lock)
            {
                return _monitors.FirstOrDefault(m => m.IsPrimary) ?? _monitors.FirstOrDefault();
            }
        }
    }

    public DisplayManagerService(ILogger logger)
    {
        _logger = logger.ForContext<DisplayManagerService>();
        RefreshMonitors();
    }

    public void RefreshMonitors()
    {
        lock (_lock)
        {
            try
            {
                CleanPhysicalMonitors();

                var detected = new List<MonitorInfoModel>();
                int displayIndex = 1;

                User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT rcClip, IntPtr dwData) =>
                {
                    if (displayIndex > 4)
                    {
                        // Supports up to 4 displays
                        return true;
                    }

                    var mi = new MONITORINFOEX();
                    mi.Init();

                    if (User32.GetMonitorInfo(hMonitor, ref mi))
                    {
                        bool isPrimary = (mi.dwFlags & User32.MONITORINFOF_PRIMARY) != 0;
                        string deviceName = mi.szDevice ?? $"DISPLAY{displayIndex}";

                        string friendlyName = GetFriendlyMonitorName(deviceName, displayIndex);

                        var devMode = new DEVMODE();
                        devMode.Init();
                        int currentWidth = 1920;
                        int currentHeight = 1080;
                        int refreshRate = 60;
                        uint orientation = User32.DMDO_DEFAULT;

                        if (User32.EnumDisplaySettingsExA(deviceName, User32.ENUM_CURRENT_SETTINGS, ref devMode, 0))
                        {
                            currentWidth = (int)devMode.dmPelsWidth;
                            currentHeight = (int)devMode.dmPelsHeight;
                            refreshRate = (int)devMode.dmDisplayFrequency;
                            orientation = devMode.dmDisplayOrientation;
                        }
                        else
                        {
                            currentWidth = mi.rcMonitor.Width;
                            currentHeight = mi.rcMonitor.Height;
                        }

                        // Check DDC/CI hardware support
                        bool supportsDdc = false;
                        IntPtr hPhysical = IntPtr.Zero;

                        try
                        {
                            if (Dxva2.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) && count > 0)
                            {
                                var phys = new PHYSICAL_MONITOR[count];
                                if (Dxva2.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, phys))
                                {
                                    _physicalMonitorsByHMon[hMonitor] = phys;
                                    hPhysical = phys[0].hPhysicalMonitor;

                                    if (Dxva2.GetMonitorBrightness(hPhysical, out _, out _, out _))
                                    {
                                        supportsDdc = true;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug(ex, "DDC/CI query not supported for monitor {Device}", deviceName);
                        }

                        var model = new MonitorInfoModel
                        {
                            DisplayIndex = displayIndex,
                            DeviceName = deviceName,
                            FriendlyName = friendlyName,
                            HMonitor = hMonitor,
                            IsPrimary = isPrimary,
                            Bounds = mi.rcMonitor,
                            WorkArea = mi.rcWork,
                            CurrentWidth = currentWidth,
                            CurrentHeight = currentHeight,
                            CurrentRefreshRate = refreshRate,
                            CurrentOrientation = orientation,
                            SupportsDdcCi = supportsDdc,
                            DdcPhysicalMonitor = hPhysical,
                            CurrentBrightness = 100 // Default, updated by BrightnessService
                        };

                        detected.Add(model);
                        displayIndex++;
                    }

                    return true;
                }, IntPtr.Zero);

                // Sort so primary monitor or Display 1 comes first logically if needed
                detected.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));
                _monitors = detected;

                _logger.Information("Detected {Count} active displays: {Displays}",
                    _monitors.Count,
                    string.Join(", ", _monitors.Select(m => $"[Display {m.DisplayIndex}: {m.FriendlyName} ({m.ResolutionString}@{m.RefreshRateString}) Primary={m.IsPrimary} DDC={m.SupportsDdcCi}]")));

                DisplaysChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred during monitor enumeration");
            }
        }
    }

    public MonitorInfoModel? GetDisplay(int displayIndex)
    {
        lock (_lock)
        {
            if (displayIndex >= 1 && displayIndex <= 4)
            {
                var mon = _monitors.FirstOrDefault(m => m.DisplayIndex == displayIndex);
                if (mon != null) return mon;
            }
            return PrimaryMonitor;
        }
    }

    private string GetFriendlyMonitorName(string deviceName, int displayIndex)
    {
        try
        {
            var dd = new DISPLAY_DEVICE();
            dd.Init();

            // First call to get adapter/monitor associated with device
            if (User32.EnumDisplayDevices(deviceName, 0, ref dd, 0))
            {
                if (!string.IsNullOrWhiteSpace(dd.DeviceString))
                {
                    // Check if monitor device is attached to this adapter
                    var monDev = new DISPLAY_DEVICE();
                    monDev.Init();
                    if (User32.EnumDisplayDevices(deviceName, 0, ref monDev, 1) && !string.IsNullOrWhiteSpace(monDev.DeviceString))
                    {
                        return CleanMonitorString(monDev.DeviceString);
                    }

                    return CleanMonitorString(dd.DeviceString);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to resolve friendly name for {Device}", deviceName);
        }

        return $"Display {displayIndex}";
    }

    private static string CleanMonitorString(string raw)
    {
        string trimmed = raw.Trim();
        if (string.Equals(trimmed, "Generic PnP Monitor", StringComparison.OrdinalIgnoreCase))
        {
            return "Generic Display";
        }
        return trimmed;
    }

    private void CleanPhysicalMonitors()
    {
        foreach (var kvp in _physicalMonitorsByHMon)
        {
            try
            {
                Dxva2.DestroyPhysicalMonitors((uint)kvp.Value.Length, kvp.Value);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        _physicalMonitorsByHMon.Clear();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            CleanPhysicalMonitors();
            _monitors.Clear();
        }
    }
}
