using System;
using ScreenxDeck.Core.Native;
using Serilog;

namespace ScreenxDeck.Services;

public sealed class MonitorControlService
{
    private readonly ILogger _logger;
    private readonly DisplayManagerService _displayManager;

    public MonitorControlService(ILogger logger, DisplayManagerService displayManager)
    {
        _logger = logger.ForContext<MonitorControlService>();
        _displayManager = displayManager;
    }

    public bool TurnMonitorOff(int displayIndex)
    {
        if (displayIndex == 0) // All Monitors
        {
            User32.SendMessage(User32.HWND_BROADCAST, User32.WM_SYSCOMMAND, (IntPtr)User32.SC_MONITORPOWER, (IntPtr)User32.MONITOR_OFF);
            _logger.Information("Turned off all monitors via Windows API");
            return true;
        }

        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null) return false;

        if (monitor.SupportsDdcCi && monitor.DdcPhysicalMonitor != IntPtr.Zero)
        {
            try
            {
                if (Dxva2.SetVCPFeature(monitor.DdcPhysicalMonitor, Dxva2.VCP_POWER_MODE, Dxva2.POWER_MODE_OFF))
                {
                    _logger.Information("Turned off monitor {Index} ({Device}) via DDC/CI", displayIndex, monitor.FriendlyName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to power off monitor {Index} via DDC/CI", displayIndex);
            }
        }

        // Fallback to broadcast
        User32.SendMessage(User32.HWND_BROADCAST, User32.WM_SYSCOMMAND, (IntPtr)User32.SC_MONITORPOWER, (IntPtr)User32.MONITOR_OFF);
        return true;
    }

    public bool WakeMonitor(int displayIndex)
    {
        if (displayIndex == 0)
        {
            User32.mouse_event(User32.MOUSEEVENTF_MOVE, 1, 0, 0, UIntPtr.Zero);
            User32.SendMessage(User32.HWND_BROADCAST, User32.WM_SYSCOMMAND, (IntPtr)User32.SC_MONITORPOWER, (IntPtr)User32.MONITOR_ON);
            _logger.Information("Woke monitors");
            return true;
        }

        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor != null && monitor.SupportsDdcCi && monitor.DdcPhysicalMonitor != IntPtr.Zero)
        {
            try
            {
                Dxva2.SetVCPFeature(monitor.DdcPhysicalMonitor, Dxva2.VCP_POWER_MODE, Dxva2.POWER_MODE_ON);
            }
            catch
            {
                // Ignore
            }
        }

        // Synthetic mouse wiggle wakes Windows display subsystem
        User32.mouse_event(User32.MOUSEEVENTF_MOVE, 1, 0, 0, UIntPtr.Zero);
        User32.SendMessage(User32.HWND_BROADCAST, User32.WM_SYSCOMMAND, (IntPtr)User32.SC_MONITORPOWER, (IntPtr)User32.MONITOR_ON);
        return true;
    }

    public bool SwitchInputSource(int displayIndex, uint inputCode)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null)
        {
            _logger.Warning("SwitchInputSource: display {Index} not found", displayIndex);
            return false;
        }

        if (!monitor.SupportsDdcCi || monitor.DdcPhysicalMonitor == IntPtr.Zero)
        {
            _logger.Warning("Monitor {Index} ({Device}) does not support DDC/CI input switching", displayIndex, monitor.FriendlyName);
            return false;
        }

        try
        {
            bool success = Dxva2.SetVCPFeature(monitor.DdcPhysicalMonitor, Dxva2.VCP_INPUT_SOURCE, inputCode);
            if (success)
            {
                _logger.Information("Switched input source for {Device} to 0x{Code:X}", monitor.FriendlyName, inputCode);
                monitor.ActiveInputSource = FormatInputSource(inputCode);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to switch input source for {Device}", monitor.FriendlyName);
        }

        return false;
    }

    public bool ToggleInputSource(int displayIndex, uint codeA, uint codeB)
    {
        var monitor = _displayManager.GetDisplay(displayIndex);
        if (monitor == null || monitor.DdcPhysicalMonitor == IntPtr.Zero) return false;

        uint current = 0;
        try
        {
            if (Dxva2.GetVCPFeatureAndVCPFeatureReply(monitor.DdcPhysicalMonitor, Dxva2.VCP_INPUT_SOURCE, out _, out uint cur, out _))
            {
                current = cur;
            }
        }
        catch
        {
            // Ignore
        }

        uint target = current == codeA ? codeB : codeA;
        return SwitchInputSource(displayIndex, target);
    }

    public static string FormatInputSource(uint code) => code switch
    {
        Dxva2.INPUT_HDMI_1 => "HDMI 1",
        Dxva2.INPUT_HDMI_2 => "HDMI 2",
        Dxva2.INPUT_DP_1 => "DisplayPort 1",
        Dxva2.INPUT_DP_2 => "DisplayPort 2",
        Dxva2.INPUT_USB_C => "USB-C",
        Dxva2.INPUT_VGA_1 => "VGA",
        Dxva2.INPUT_DVI_1 => "DVI",
        _ => $"Input 0x{code:X2}"
    };
}
