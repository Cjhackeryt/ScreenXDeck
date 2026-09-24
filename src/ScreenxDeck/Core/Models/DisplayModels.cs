using System;
using System.Collections.Generic;

namespace ScreenxDeck.Core.Models;

public readonly record struct DisplayResolution(int Width, int Height)
{
    public override string ToString() => $"{Width}x{Height}";
}

public sealed class MonitorProfileEntry
{
    public int DisplayIndex { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public uint Orientation { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public bool IsPrimary { get; set; }
    public int Brightness { get; set; } = 100;
    public uint InputSource { get; set; }
}

public sealed class DisplayProfile
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DisplayMode { get; set; } = "Extend";
    public List<MonitorProfileEntry> Monitors { get; set; } = [];
}

public sealed class MonitorArrangementEntry
{
    public int DisplayIndex { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class DisplayArrangement
{
    public string Name { get; set; } = string.Empty;
    public List<MonitorArrangementEntry> Displays { get; set; } = [];
}

public sealed class PluginSettings
{
    public int DefaultMonitorIndex { get; set; } = 1; // 1 to 4, or 0 for All
    public int BrightnessStep { get; set; } = 10;
    public List<int> BrightnessPresets { get; set; } = [0, 25, 50, 75, 100];
    public List<int> RefreshRatePresets { get; set; } = [60, 120, 144, 165, 240];
    public List<string> ResolutionPresets { get; set; } = ["1920x1080", "2560x1440", "3840x2160"];
    public List<DisplayProfile> SavedProfiles { get; set; } = [];
    public List<DisplayArrangement> SavedArrangements { get; set; } = [];
    public List<string> AutoPinApplications { get; set; } = [];
    public bool EnableDebugLogging { get; set; } = false;
    public string ActiveProfileName { get; set; } = "Default";
}
