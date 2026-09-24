using System;
using System.Collections.Generic;
using System.Linq;
using ScreenxDeck.Core.Models;
using ScreenxDeck.Core.Native;
using Serilog;

namespace ScreenxDeck.Services;

public sealed class TopologyService
{
    private readonly ILogger _logger;
    private readonly DisplayManagerService _displayManager;
    private readonly SettingsService _settingsService;
    private CcdApi.DisplayTopology _currentTopology = CcdApi.DisplayTopology.Extend;

    public CcdApi.DisplayTopology CurrentTopology => _currentTopology;

    public TopologyService(ILogger logger, DisplayManagerService displayManager, SettingsService settingsService)
    {
        _logger = logger.ForContext<TopologyService>();
        _displayManager = displayManager;
        _settingsService = settingsService;
    }

    public bool SetDisplayMode(CcdApi.DisplayTopology topology)
    {
        _logger.Information("Setting display mode to {Topology}", topology);
        bool success = CcdApi.ApplyTopology(topology);
        if (success)
        {
            _currentTopology = topology;
            _displayManager.RefreshMonitors();
        }
        else
        {
            _logger.Error("Failed to apply display topology {Topology}", topology);
        }
        return success;
    }

    public bool ToggleDuplicateExtend()
    {
        var target = _currentTopology == CcdApi.DisplayTopology.Clone 
            ? CcdApi.DisplayTopology.Extend 
            : CcdApi.DisplayTopology.Clone;
        return SetDisplayMode(target);
    }

    public bool SetPrimaryMonitor(int targetDisplayIndex)
    {
        var targetMonitor = _displayManager.GetDisplay(targetDisplayIndex);
        if (targetMonitor == null)
        {
            _logger.Warning("SetPrimaryMonitor: Display {Index} not found", targetDisplayIndex);
            return false;
        }

        if (targetMonitor.IsPrimary)
        {
            _logger.Information("Display {Index} is already primary", targetDisplayIndex);
            return true;
        }

        var devModeTarget = new DEVMODE();
        devModeTarget.Init();
        if (!User32.EnumDisplaySettingsExA(targetMonitor.DeviceName, User32.ENUM_CURRENT_SETTINGS, ref devModeTarget, 0))
        {
            _logger.Error("Failed to query target display settings for {Device}", targetMonitor.DeviceName);
            return false;
        }

        // In Windows, primary display coordinates must be (0, 0).
        // Calculate offset (dx, dy) to shift all monitors
        int offsetX = -devModeTarget.dmPositionX;
        int offsetY = -devModeTarget.dmPositionY;

        // Shift other monitors first
        foreach (var mon in _displayManager.ConnectedMonitors)
        {
            if (mon.DisplayIndex == targetDisplayIndex) continue;

            var devModeOther = new DEVMODE();
            devModeOther.Init();
            if (User32.EnumDisplaySettingsExA(mon.DeviceName, User32.ENUM_CURRENT_SETTINGS, ref devModeOther, 0))
            {
                devModeOther.dmPositionX += offsetX;
                devModeOther.dmPositionY += offsetY;
                devModeOther.dmFields |= User32.DM_POSITION;

                User32.ChangeDisplaySettingsExA(mon.DeviceName, ref devModeOther, IntPtr.Zero, User32.CDS_UPDATEREGISTRY | User32.CDS_NORESET, IntPtr.Zero);
            }
        }

        // Set target monitor to (0, 0) and make it primary
        devModeTarget.dmPositionX = 0;
        devModeTarget.dmPositionY = 0;
        devModeTarget.dmFields |= User32.DM_POSITION;

        int result = User32.ChangeDisplaySettingsExA(targetMonitor.DeviceName, ref devModeTarget, IntPtr.Zero, User32.CDS_SET_PRIMARY | User32.CDS_UPDATEREGISTRY | User32.CDS_RESET, IntPtr.Zero);
        if (result == User32.DISP_CHANGE_SUCCESSFUL)
        {
            _logger.Information("Successfully set Display {Index} ({Name}) as primary monitor", targetDisplayIndex, targetMonitor.FriendlyName);
            _displayManager.RefreshMonitors();
            return true;
        }

        _logger.Error("Failed to set primary monitor (Code: {Code})", result);
        return false;
    }

    public bool SetOrientation(int displayIndex, uint orientation)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null) return false;

        var devMode = new DEVMODE();
        devMode.Init();
        if (!User32.EnumDisplaySettingsExA(monitor.DeviceName, User32.ENUM_CURRENT_SETTINGS, ref devMode, 0))
        {
            return false;
        }

        bool wasPortrait = devMode.dmDisplayOrientation == User32.DMDO_90 || devMode.dmDisplayOrientation == User32.DMDO_270;
        bool isTargetPortrait = orientation == User32.DMDO_90 || orientation == User32.DMDO_270;

        // If switching between portrait and landscape, swap width and height
        if (wasPortrait != isTargetPortrait)
        {
            (devMode.dmPelsWidth, devMode.dmPelsHeight) = (devMode.dmPelsHeight, devMode.dmPelsWidth);
        }

        devMode.dmDisplayOrientation = orientation;
        devMode.dmFields |= User32.DM_DISPLAYORIENTATION | User32.DM_PELSWIDTH | User32.DM_PELSHEIGHT;

        int result = User32.ChangeDisplaySettingsExA(monitor.DeviceName, ref devMode, IntPtr.Zero, User32.CDS_UPDATEREGISTRY | User32.CDS_RESET, IntPtr.Zero);
        if (result == User32.DISP_CHANGE_SUCCESSFUL)
        {
            _logger.Information("Changed orientation for {Device} to {Orientation}", monitor.FriendlyName, orientation);
            _displayManager.RefreshMonitors();
            return true;
        }

        return false;
    }

    public bool SaveCurrentArrangement(string name)
    {
        var arrangement = new DisplayArrangement { Name = name };
        foreach (var monitor in _displayManager.ConnectedMonitors)
        {
            var devMode = new DEVMODE();
            devMode.Init();
            if (User32.EnumDisplaySettingsExA(monitor.DeviceName, User32.ENUM_CURRENT_SETTINGS, ref devMode, 0))
            {
                arrangement.Displays.Add(new MonitorArrangementEntry
                {
                    DisplayIndex = monitor.DisplayIndex,
                    DeviceName = monitor.DeviceName,
                    X = devMode.dmPositionX,
                    Y = devMode.dmPositionY,
                    Width = (int)devMode.dmPelsWidth,
                    Height = (int)devMode.dmPelsHeight
                });
            }
        }

        _settingsService.Update(s =>
        {
            s.SavedArrangements.RemoveAll(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            s.SavedArrangements.Add(arrangement);
        });

        _logger.Information("Saved monitor arrangement '{Name}' with {Count} displays", name, arrangement.Displays.Count);
        return true;
    }

    public bool RestoreArrangement(string name)
    {
        var arrangement = _settingsService.Current.SavedArrangements.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        if (arrangement == null || arrangement.Displays.Count == 0)
        {
            _logger.Warning("Arrangement '{Name}' not found", name);
            return false;
        }

        foreach (var entry in arrangement.Displays)
        {
            var devMode = new DEVMODE();
            devMode.Init();
            if (User32.EnumDisplaySettingsExA(entry.DeviceName, User32.ENUM_CURRENT_SETTINGS, ref devMode, 0))
            {
                devMode.dmPositionX = entry.X;
                devMode.dmPositionY = entry.Y;
                devMode.dmFields |= User32.DM_POSITION;

                User32.ChangeDisplaySettingsExA(entry.DeviceName, ref devMode, IntPtr.Zero, User32.CDS_UPDATEREGISTRY | User32.CDS_NORESET, IntPtr.Zero);
            }
        }

        // Apply reset on primary
        var primary = _displayManager.PrimaryMonitor;
        if (primary != null)
        {
            var devMode = new DEVMODE();
            devMode.Init();
            if (User32.EnumDisplaySettingsExA(primary.DeviceName, User32.ENUM_CURRENT_SETTINGS, ref devMode, 0))
            {
                User32.ChangeDisplaySettingsExA(primary.DeviceName, ref devMode, IntPtr.Zero, User32.CDS_UPDATEREGISTRY | User32.CDS_RESET, IntPtr.Zero);
            }
        }

        _logger.Information("Restored monitor arrangement '{Name}'", name);
        _displayManager.RefreshMonitors();
        return true;
    }
}
