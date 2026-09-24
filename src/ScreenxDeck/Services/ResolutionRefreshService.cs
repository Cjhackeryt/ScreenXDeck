using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ScreenxDeck.Core.Models;
using ScreenxDeck.Core.Native;
using Serilog;

namespace ScreenxDeck.Services;

public sealed class ResolutionRefreshService
{
    private readonly ILogger _logger;
    private readonly DisplayManagerService _displayManager;
    // Stores previous DEVMODE settings for rollback
    private readonly ConcurrentDictionary<string, DEVMODE> _previousSettings = new(StringComparer.OrdinalIgnoreCase);

    public ResolutionRefreshService(ILogger logger, DisplayManagerService displayManager)
    {
        _logger = logger.ForContext<ResolutionRefreshService>();
        _displayManager = displayManager;
    }

    public List<DisplayResolution> GetSupportedResolutions(int displayIndex)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null) return [];

        var resolutions = new HashSet<DisplayResolution>();
        var devMode = new DEVMODE();
        devMode.Init();

        int modeIndex = 0;
        while (User32.EnumDisplaySettingsExA(monitor.DeviceName, modeIndex, ref devMode, 0))
        {
            if (devMode.dmPelsWidth > 0 && devMode.dmPelsHeight > 0)
            {
                resolutions.Add(new DisplayResolution((int)devMode.dmPelsWidth, (int)devMode.dmPelsHeight));
            }
            modeIndex++;
        }

        return resolutions.OrderByDescending(r => r.Width * r.Height).ThenByDescending(r => r.Width).ToList();
    }

    public List<int> GetSupportedRefreshRates(int displayIndex, int? width = null, int? height = null)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null) return [];

        int targetWidth = width ?? monitor.CurrentWidth;
        int targetHeight = height ?? monitor.CurrentHeight;

        var refreshRates = new HashSet<int>();
        var devMode = new DEVMODE();
        devMode.Init();

        int modeIndex = 0;
        while (User32.EnumDisplaySettingsExA(monitor.DeviceName, modeIndex, ref devMode, 0))
        {
            if (devMode.dmPelsWidth == targetWidth && devMode.dmPelsHeight == targetHeight && devMode.dmDisplayFrequency > 0)
            {
                refreshRates.Add((int)devMode.dmDisplayFrequency);
            }
            modeIndex++;
        }

        return refreshRates.OrderBy(r => r).ToList();
    }

    public bool SetRefreshRate(int displayIndex, int targetHz)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null)
        {
            _logger.Warning("SetRefreshRate: Display {Index} not found", displayIndex);
            return false;
        }

        var devMode = new DEVMODE();
        devMode.Init();
        if (!User32.EnumDisplaySettingsExA(monitor.DeviceName, User32.ENUM_CURRENT_SETTINGS, ref devMode, 0))
        {
            _logger.Error("Failed to query current settings for {Device}", monitor.DeviceName);
            return false;
        }

        // Save current as previous
        _previousSettings[monitor.DeviceName] = devMode;

        devMode.dmDisplayFrequency = (uint)targetHz;
        devMode.dmFields = User32.DM_DISPLAYFREQUENCY | User32.DM_PELSWIDTH | User32.DM_PELSHEIGHT;

        // Test configuration before applying
        int testResult = User32.ChangeDisplaySettingsExA(monitor.DeviceName, ref devMode, IntPtr.Zero, User32.CDS_TEST, IntPtr.Zero);
        if (testResult != User32.DISP_CHANGE_SUCCESSFUL)
        {
            _logger.Warning("Requested refresh rate {Hz}Hz is not supported for {Device} (Test result: {Code})", targetHz, monitor.FriendlyName, testResult);
            return false;
        }

        int applyResult = User32.ChangeDisplaySettingsExA(monitor.DeviceName, ref devMode, IntPtr.Zero, User32.CDS_UPDATEREGISTRY | User32.CDS_RESET, IntPtr.Zero);
        if (applyResult == User32.DISP_CHANGE_SUCCESSFUL)
        {
            _logger.Information("Successfully changed refresh rate to {Hz}Hz for {Device}", targetHz, monitor.FriendlyName);
            _displayManager.RefreshMonitors();
            return true;
        }

        _logger.Error("Failed to apply refresh rate {Hz}Hz for {Device} (Code: {Code})", targetHz, monitor.FriendlyName, applyResult);
        return false;
    }

    public bool SetResolution(int displayIndex, int width, int height)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null)
        {
            _logger.Warning("SetResolution: Display {Index} not found", displayIndex);
            return false;
        }

        var devMode = new DEVMODE();
        devMode.Init();
        if (!User32.EnumDisplaySettingsExA(monitor.DeviceName, User32.ENUM_CURRENT_SETTINGS, ref devMode, 0))
        {
            _logger.Error("Failed to query current settings for {Device}", monitor.DeviceName);
            return false;
        }

        // Save current as previous
        _previousSettings[monitor.DeviceName] = devMode;

        devMode.dmPelsWidth = (uint)width;
        devMode.dmPelsHeight = (uint)height;
        devMode.dmFields = User32.DM_PELSWIDTH | User32.DM_PELSHEIGHT | User32.DM_DISPLAYFREQUENCY;

        // Test mode
        int testResult = User32.ChangeDisplaySettingsExA(monitor.DeviceName, ref devMode, IntPtr.Zero, User32.CDS_TEST, IntPtr.Zero);
        if (testResult != User32.DISP_CHANGE_SUCCESSFUL)
        {
            _logger.Warning("Requested resolution {W}x{H} is not supported for {Device} (Test result: {Code})", width, height, monitor.FriendlyName, testResult);
            return false;
        }

        int applyResult = User32.ChangeDisplaySettingsExA(monitor.DeviceName, ref devMode, IntPtr.Zero, User32.CDS_UPDATEREGISTRY | User32.CDS_RESET, IntPtr.Zero);
        if (applyResult == User32.DISP_CHANGE_SUCCESSFUL)
        {
            _logger.Information("Successfully changed resolution to {W}x{H} for {Device}", width, height, monitor.FriendlyName);
            _displayManager.RefreshMonitors();
            return true;
        }

        _logger.Error("Failed to apply resolution {W}x{H} for {Device} (Code: {Code})", width, height, monitor.FriendlyName, applyResult);
        return false;
    }

    public bool RestorePreviousSettings(int displayIndex)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null) return false;

        if (_previousSettings.TryGetValue(monitor.DeviceName, out var prevDevMode))
        {
            int applyResult = User32.ChangeDisplaySettingsExA(monitor.DeviceName, ref prevDevMode, IntPtr.Zero, User32.CDS_UPDATEREGISTRY | User32.CDS_RESET, IntPtr.Zero);
            if (applyResult == User32.DISP_CHANGE_SUCCESSFUL)
            {
                _logger.Information("Restored previous display settings for {Device}", monitor.FriendlyName);
                _displayManager.RefreshMonitors();
                return true;
            }
        }

        return false;
    }

    public bool ToggleRefreshRate(int displayIndex, int rateA, int rateB)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null) return false;

        int current = monitor.CurrentRefreshRate;
        int target = Math.Abs(current - rateA) < Math.Abs(current - rateB) ? rateB : rateA;
        return SetRefreshRate(displayIndex, target);
    }

    public bool ToggleResolution(int displayIndex, DisplayResolution resA, DisplayResolution resB)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null) return false;

        bool isCloserToA = monitor.CurrentWidth == resA.Width && monitor.CurrentHeight == resA.Height;
        var target = isCloserToA ? resB : resA;
        return SetResolution(displayIndex, target.Width, target.Height);
    }
}
