using System;
using ScreenxDeck.Core.Native;

namespace ScreenxDeck.Core.Models;

public sealed class MonitorInfoModel
{
    public int DisplayIndex { get; set; } // 1 to 4
    public string DeviceName { get; set; } = string.Empty; // e.g. \\.\DISPLAY1
    public string FriendlyName { get; set; } = string.Empty; // e.g. Dell U2720Q
    public IntPtr HMonitor { get; set; }
    public bool IsPrimary { get; set; }
    public RECT Bounds { get; set; }
    public RECT WorkArea { get; set; }

    public int CurrentWidth { get; set; }
    public int CurrentHeight { get; set; }
    public int CurrentRefreshRate { get; set; }
    public uint CurrentOrientation { get; set; }

    public bool SupportsDdcCi { get; set; }
    public IntPtr DdcPhysicalMonitor { get; set; }
    public int CurrentBrightness { get; set; } = 100;
    public string ActiveInputSource { get; set; } = "Unknown";

    public string ResolutionString => $"{CurrentWidth}x{CurrentHeight}";
    public string RefreshRateString => $"{CurrentRefreshRate}Hz";
    public string DisplaySummary => $"{FriendlyName} ({ResolutionString} @ {RefreshRateString})";
}
