using System;
using System.Collections.Generic;
using MacroDeck.Localization;
using MacroDeck.Sdk.Variables;

namespace ScreenxDeck.Integration;

public static class ScreenxDeckVariables
{
    public const string Brightness = "screenxdeck_brightness";
    public const string BrightnessMode = "screenxdeck_brightness_mode";
    public const string Resolution = "screenxdeck_resolution";
    public const string RefreshRate = "screenxdeck_refreshrate";
    public const string Orientation = "screenxdeck_orientation";
    public const string PrimaryName = "screenxdeck_primary_name";
    public const string DisplayCount = "screenxdeck_display_count";
    public const string PrimaryDisplay = "screenxdeck_primary_display";
    public const string ActiveDisplayMode = "screenxdeck_active_display_mode";
    public const string ActiveWindowTopmost = "screenxdeck_active_window_topmost";
    public const string ActiveWindowTitle = "screenxdeck_active_window_title";
    public const string ActiveProfile = "screenxdeck_active_profile";

    private static string ToLocalId(string name) => name.Replace('_', '-').ToLowerInvariant();

    public static IReadOnlyList<VariableDefinition> CreateDefinitions()
    {
        var list = new List<VariableDefinition>
        {
            // Default / Primary Display
            VariableDefinition.Eager(Brightness, VariableType.Numeric, 0, TimeSpan.FromMilliseconds(500)) with
            {
                Id = ToLocalId(Brightness),
                Name = Brightness,
                DisplayName = (LocalizedText)"Primary Display Brightness",
                Description = (LocalizedText)"Brightness percentage (0-100%) of primary display. Bind to Slider Widget!",
                Unit = "%",
                SemanticKind = VariableSemanticKinds.Percentage,
                Write = new VariableWriteCapability { CommitOnRelease = false }
            },
            VariableDefinition.Eager(BrightnessMode, VariableType.Text, refreshInterval: TimeSpan.FromMilliseconds(1000)) with
            {
                Id = ToLocalId(BrightnessMode),
                Name = BrightnessMode,
                DisplayName = (LocalizedText)"Brightness Mode",
                Description = (LocalizedText)"Active brightness control method: DDC/CI (Hardware) or Gamma (Software)"
            },
            VariableDefinition.Eager(Resolution, VariableType.Text, refreshInterval: TimeSpan.FromMilliseconds(1000)) with
            {
                Id = ToLocalId(Resolution),
                Name = Resolution,
                DisplayName = (LocalizedText)"Primary Resolution",
                Description = (LocalizedText)"Resolution of primary display (e.g. 2560x1440)"
            },
            VariableDefinition.Eager(RefreshRate, VariableType.Numeric, 0, TimeSpan.FromMilliseconds(1000)) with
            {
                Id = ToLocalId(RefreshRate),
                Name = RefreshRate,
                DisplayName = (LocalizedText)"Primary Refresh Rate",
                Description = (LocalizedText)"Refresh rate of primary display in Hz",
                Unit = "Hz"
            },
            VariableDefinition.Eager(Orientation, VariableType.Text, refreshInterval: TimeSpan.FromMilliseconds(1000)) with
            {
                Id = ToLocalId(Orientation),
                Name = Orientation,
                DisplayName = (LocalizedText)"Primary Orientation",
                Description = (LocalizedText)"Display orientation (Landscape, Portrait, etc.)"
            },
            VariableDefinition.Eager(PrimaryName, VariableType.Text, refreshInterval: TimeSpan.FromMilliseconds(2000)) with
            {
                Id = ToLocalId(PrimaryName),
                Name = PrimaryName,
                DisplayName = (LocalizedText)"Primary Display Name",
                Description = (LocalizedText)"Friendly name of the primary monitor"
            },
            VariableDefinition.Eager(DisplayCount, VariableType.Numeric, 0, TimeSpan.FromMilliseconds(2000)) with
            {
                Id = ToLocalId(DisplayCount),
                Name = DisplayCount,
                DisplayName = (LocalizedText)"Connected Displays Count",
                Description = (LocalizedText)"Total count of connected displays"
            },
            VariableDefinition.Eager(PrimaryDisplay, VariableType.Numeric, 0, TimeSpan.FromMilliseconds(2000)) with
            {
                Id = ToLocalId(PrimaryDisplay),
                Name = PrimaryDisplay,
                DisplayName = (LocalizedText)"Primary Display Index",
                Description = (LocalizedText)"1-based display index of the primary monitor"
            },
            VariableDefinition.Eager(ActiveDisplayMode, VariableType.Text, refreshInterval: TimeSpan.FromMilliseconds(1000)) with
            {
                Id = ToLocalId(ActiveDisplayMode),
                Name = ActiveDisplayMode,
                DisplayName = (LocalizedText)"Display Mode (Topology)",
                Description = (LocalizedText)"Current topology mode: Extend, Duplicate, InternalOnly, ExternalOnly",
                Write = new VariableWriteCapability { CommitOnRelease = true }
            },
            VariableDefinition.Eager(ActiveWindowTopmost, VariableType.Boolean, refreshInterval: TimeSpan.FromMilliseconds(500)) with
            {
                Id = ToLocalId(ActiveWindowTopmost),
                Name = ActiveWindowTopmost,
                DisplayName = (LocalizedText)"Active Window Always on Top",
                Description = (LocalizedText)"True if active foreground window is pinned on top. Writable to toggle!",
                Write = new VariableWriteCapability { CommitOnRelease = false }
            },
            VariableDefinition.Eager(ActiveWindowTitle, VariableType.Text, refreshInterval: TimeSpan.FromMilliseconds(500)) with
            {
                Id = ToLocalId(ActiveWindowTitle),
                Name = ActiveWindowTitle,
                DisplayName = (LocalizedText)"Active Window Title",
                Description = (LocalizedText)"Title of current foreground window"
            },
            VariableDefinition.Eager(ActiveProfile, VariableType.Text, refreshInterval: TimeSpan.FromMilliseconds(1000)) with
            {
                Id = ToLocalId(ActiveProfile),
                Name = ActiveProfile,
                DisplayName = (LocalizedText)"Active Display Profile",
                Description = (LocalizedText)"Name of active display profile. Writable to apply profile!",
                Write = new VariableWriteCapability { CommitOnRelease = true }
            }
        };

        // Per-display 1 through 4 (connection, brightness, resolution, and refresh rate)
        for (int i = 1; i <= 4; i++)
        {
            string connected = $"screenxdeck_display{i}_connected";
            list.Add(VariableDefinition.Eager(connected, VariableType.Boolean, refreshInterval: TimeSpan.FromMilliseconds(2000)) with
            {
                Id = ToLocalId(connected),
                Name = connected,
                DisplayName = (LocalizedText)$"Monitor {i} Connected",
                Description = (LocalizedText)$"True when monitor {i} is connected"
            });

            string bright = $"screenxdeck_display{i}_brightness";
            list.Add(VariableDefinition.Eager(bright, VariableType.Numeric, 0, TimeSpan.FromMilliseconds(500)) with
            {
                Id = ToLocalId(bright),
                Name = bright,
                DisplayName = (LocalizedText)$"Display {i} Brightness",
                Description = (LocalizedText)$"Brightness percentage (0-100%) of Display {i}. Bind to Slider Widget!",
                Unit = "%",
                SemanticKind = VariableSemanticKinds.Percentage,
                Write = new VariableWriteCapability { CommitOnRelease = false }
            });

            string res = $"screenxdeck_display{i}_resolution";
            list.Add(VariableDefinition.Eager(res, VariableType.Text, refreshInterval: TimeSpan.FromMilliseconds(1000)) with
            {
                Id = ToLocalId(res),
                Name = res,
                DisplayName = (LocalizedText)$"Display {i} Resolution",
                Description = (LocalizedText)$"Current resolution of Display {i}, or Not connected when unavailable"
            });

            string rate = $"screenxdeck_display{i}_refreshrate";
            list.Add(VariableDefinition.Eager(rate, VariableType.Numeric, 0, TimeSpan.FromMilliseconds(1000)) with
            {
                Id = ToLocalId(rate),
                Name = rate,
                DisplayName = (LocalizedText)$"Display {i} Refresh Rate",
                Description = (LocalizedText)$"Refresh rate of Display {i} in Hz",
                Unit = "Hz"
            });
        }

        return list;
    }
}
