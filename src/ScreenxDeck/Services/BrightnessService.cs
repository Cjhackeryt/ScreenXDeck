using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ScreenxDeck.Core.Models;
using ScreenxDeck.Core.Native;
using Serilog;

namespace ScreenxDeck.Services;

public sealed class BrightnessService
{
    private readonly ILogger _logger;
    private readonly DisplayManagerService _displayManager;
    private readonly SettingsService _settingsService;
    // Stores software gamma brightness percentage per device name
    private readonly ConcurrentDictionary<string, int> _gammaBrightnessCache = new(StringComparer.OrdinalIgnoreCase);

    public BrightnessService(ILogger logger, DisplayManagerService displayManager, SettingsService settingsService)
    {
        _logger = logger.ForContext<BrightnessService>();
        _displayManager = displayManager;
        _settingsService = settingsService;
    }

    public int GetBrightness(int displayIndex)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null) return 100;

        // Try hardware DDC/CI first if supported
        if (monitor.SupportsDdcCi && monitor.DdcPhysicalMonitor != IntPtr.Zero)
        {
            if (Dxva2.GetMonitorBrightness(monitor.DdcPhysicalMonitor, out uint min, out uint current, out uint max))
            {
                int percent = (int)Math.Round(((double)(current - min) / Math.Max(1, max - min)) * 100.0);
                monitor.CurrentBrightness = percent;
                return percent;
            }
        }

        // Return software gamma cached value or 100%
        if (_gammaBrightnessCache.TryGetValue(monitor.DeviceName, out int gammaBrightness))
        {
            monitor.CurrentBrightness = gammaBrightness;
            return gammaBrightness;
        }

        return monitor.CurrentBrightness;
    }

    public bool SetBrightness(int displayIndex, int targetPercent)
    {
        targetPercent = Math.Clamp(targetPercent, 0, 100);

        if (displayIndex == 0) // All Monitors
        {
            bool anySuccess = false;
            foreach (var monitor in _displayManager.ConnectedMonitors)
            {
                if (ApplyBrightnessToMonitor(monitor, targetPercent))
                {
                    anySuccess = true;
                }
            }
            return anySuccess;
        }
        else
        {
            var monitor = _displayManager.GetDisplay(displayIndex);
            if (monitor == null)
            {
                _logger.Warning("SetBrightness failed: display {Index} not found", displayIndex);
                return false;
            }
            return ApplyBrightnessToMonitor(monitor, targetPercent);
        }
    }

    public bool AdjustBrightness(int displayIndex, int deltaPercent)
    {
        int current = GetBrightness(displayIndex);
        int target = Math.Clamp(current + deltaPercent, 0, 100);
        return SetBrightness(displayIndex, target);
    }

    public bool ToggleBrightness(int displayIndex, int levelA, int levelB)
    {
        int current = GetBrightness(displayIndex);
        // If closer to levelA, toggle to levelB; otherwise levelA
        int diffA = Math.Abs(current - levelA);
        int diffB = Math.Abs(current - levelB);
        int target = diffA <= diffB ? levelB : levelA;
        return SetBrightness(displayIndex, target);
    }

    public string GetBrightnessMethod(int displayIndex)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor != null && monitor.SupportsDdcCi && monitor.DdcPhysicalMonitor != IntPtr.Zero)
        {
            return "DDC/CI";
        }
        return "Gamma";
    }

    private bool ApplyBrightnessToMonitor(MonitorInfoModel monitor, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        bool appliedHardware = false;

        // 1. Attempt DDC/CI Hardware Brightness (for DDC/CI supported monitors)
        if (monitor.SupportsDdcCi && monitor.DdcPhysicalMonitor != IntPtr.Zero)
        {
            try
            {
                if (Dxva2.GetMonitorBrightness(monitor.DdcPhysicalMonitor, out uint min, out _, out uint max))
                {
                    uint targetValue = (uint)Math.Round(min + ((double)percent / 100.0) * (max - min));
                    if (Dxva2.SetMonitorBrightness(monitor.DdcPhysicalMonitor, targetValue))
                    {
                        appliedHardware = true;
                        monitor.CurrentBrightness = percent;
                        _gammaBrightnessCache[monitor.DeviceName] = percent;
                        _logger.Information("Hardware DDC/CI brightness set to {Percent}% for {Device}", percent, monitor.FriendlyName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Hardware DDC/CI brightness failed for {Device}, falling back to gamma ramp", monitor.DeviceName);
            }
        }

        // 2. Fallback to GDI Gamma Ramp adjustment if DDC/CI was not supported or failed
        if (!appliedHardware)
        {
            try
            {
                IntPtr hdc = Gdi32.GetDisplayDC(monitor.DeviceName);
                if (hdc != IntPtr.Zero)
                {
                    var ramp = Gdi32.CreateGammaRampForBrightness(percent);
                    bool rampSet = Gdi32.SetDeviceGammaRamp(hdc, ref ramp);
                    if (!rampSet)
                    {
                        // Conservative fallback if driver enforces lower cutoff
                        ramp = Gdi32.CreateGammaRampWithExponent(Math.Min(4.0, 1.0 + (100 - percent) / 100.0 * 2.5));
                        rampSet = Gdi32.SetDeviceGammaRamp(hdc, ref ramp);
                    }
                    Gdi32.DeleteDC(hdc);

                    if (rampSet)
                    {
                        _gammaBrightnessCache[monitor.DeviceName] = percent;
                        monitor.CurrentBrightness = percent;
                        _logger.Information("Software Gamma brightness set to {Percent}% for {Device}", percent, monitor.FriendlyName);
                        return true;
                    }
                    else
                    {
                        _logger.Warning("SetDeviceGammaRamp rejected gamma ramp for {Device} at {Percent}%", monitor.DeviceName, percent);
                    }
                }
                else
                {
                    _logger.Warning("Could not acquire Display DC for {Device}", monitor.DeviceName);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to apply gamma brightness for {Device}", monitor.DeviceName);
            }
        }

        return appliedHardware;
    }
}
