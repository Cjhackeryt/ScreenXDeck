using System;
using System.Runtime.InteropServices;

namespace ScreenxDeck.Core.Native;

public static class Dxva2
{
    public const byte VCP_INPUT_SOURCE = 0x60;
    public const byte VCP_POWER_MODE = 0xD6;

    public const uint POWER_MODE_ON = 0x01;
    public const uint POWER_MODE_STANDBY = 0x02;
    public const uint POWER_MODE_SUSPEND = 0x03;
    public const uint POWER_MODE_OFF = 0x04;

    // Common VCP 0x60 Input Select Values
    public const uint INPUT_VGA_1 = 0x01;
    public const uint INPUT_DVI_1 = 0x03;
    public const uint INPUT_HDMI_1 = 0x11;
    public const uint INPUT_HDMI_2 = 0x12;
    public const uint INPUT_DP_1 = 0x0F;
    public const uint INPUT_DP_2 = 0x10;
    public const uint INPUT_USB_C = 0x1B;

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor,
        uint dwPhysicalMonitorArraySize,
        [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool DestroyPhysicalMonitors(
        uint dwPhysicalMonitorArraySize,
        [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetMonitorBrightness(
        IntPtr hMonitor,
        out uint pdwMinimumBrightness,
        out uint pdwCurrentBrightness,
        out uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool SetMonitorBrightness(
        IntPtr hMonitor,
        uint dwNewBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetVCPFeatureAndVCPFeatureReply(
        IntPtr hMonitor,
        byte bVCPCode,
        out uint pvct,
        out uint pdwCurrentValue,
        out uint pdwMaximumValue);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool SetVCPFeature(
        IntPtr hMonitor,
        byte bVCPCode,
        uint dwNewValue);
}
