using System;
using System.Runtime.InteropServices;

namespace ScreenxDeck.Core.Native;

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public RECT(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;

    public POINT(int x, int y)
    {
        X = x;
        Y = y;
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct MONITORINFOEX
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szDevice;

    public void Init()
    {
        cbSize = Marshal.SizeOf<MONITORINFOEX>();
        szDevice = string.Empty;
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct DISPLAY_DEVICE
{
    public int cb;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;
    public uint StateFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;

    public void Init()
    {
        cb = Marshal.SizeOf<DISPLAY_DEVICE>();
        DeviceName = string.Empty;
        DeviceString = string.Empty;
        DeviceID = string.Empty;
        DeviceKey = string.Empty;
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct DEVMODE
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmDeviceName;
    public ushort dmSpecVersion;
    public ushort dmDriverVersion;
    public ushort dmSize;
    public ushort dmDriverExtra;
    public uint dmFields;

    public int dmPositionX;
    public int dmPositionY;

    public uint dmDisplayOrientation;
    public uint dmDisplayFixedOutput;

    public short dmColor;
    public short dmDuplex;
    public short dmYResolution;
    public short dmTTOption;
    public short dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string dmFormName;
    public ushort dmLogPixels;
    public uint dmBitsPerPel;
    public uint dmPelsWidth;
    public uint dmPelsHeight;
    public uint dmDisplayFlags;
    public uint dmDisplayFrequency;
    public uint dmICMMethod;
    public uint dmICMIntent;
    public uint dmMediaType;
    public uint dmDitherType;
    public uint dmReserved1;
    public uint dmReserved2;
    public uint dmPanningWidth;
    public uint dmPanningHeight;

    public void Init()
    {
        dmDeviceName = string.Empty;
        dmFormName = string.Empty;
        dmSize = (ushort)Marshal.SizeOf<DEVMODE>();
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct PHYSICAL_MONITOR
{
    public IntPtr hPhysicalMonitor;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string szPhysicalMonitorDescription;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct RAMP
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public ushort[] Red = new ushort[256];
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public ushort[] Green = new ushort[256];
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public ushort[] Blue = new ushort[256];

    public RAMP()
    {
        Red = new ushort[256];
        Green = new ushort[256];
        Blue = new ushort[256];
    }

    public void Init()
    {
        Red = new ushort[256];
        Green = new ushort[256];
        Blue = new ushort[256];
    }
}
