# ScreenxDeck (Macro Deck 3 Plugin)

[![Macro Deck Version](https://img.shields.io/badge/Macro%20Deck-v3.0.0--beta.13+-blue.svg)](https://macro-deck.app)
[![Target Runtime](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-0078D6.svg)](https://microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**Package ID:** `com.cjhackeryt.screenxdeck`  
**Version:** `1.0.0`

**ScreenxDeck** is a high-performance Windows display and window management plugin engineered specifically for **Macro Deck 3** on **.NET 10**. It transforms your Macro Deck stream deck into a command center for multi-monitor setups, hardware brightness control, resolution and refresh rate switching, display profiles, and window management.

## Summary

ScreenxDeck gives Macro Deck 3 users fast, programmable control over Windows displays and windows from a single button surface. It supports multi-monitor brightness, resolution, refresh rate, topology, orientation, profile, power, input, and window-management workflows.

## Tags

`Macro Deck` `Windows` `multi-monitor` `display control` `brightness` `refresh rate` `resolution` `window management` `.NET 10`

---

## Key Highlights

- **Hardware & Software Brightness:** True **DDC/CI hardware brightness** via `dxva2.dll` with an automatic, seamless **GDI Gamma Ramp fallback** for laptops and non-DDC panels (never errors out).
- **Safe Mode Changes:** Refresh rate and resolution changes are always pre-tested using Windows `CDS_TEST` before applying, preventing black-screen or unsupported mode crashes.
- **Dynamic Button States:** Real-time visual feedback on Macro Deck buttons (e.g. Always-on-Top "Pinned" vs "Unpinned", Display Mode "Extend" vs "Duplicate").
- **Reactive Variables:** Exposes real-time variables (`screenxdeck_display1_brightness`, `screenxdeck_active_display_mode`, `screenxdeck_active_window_topmost`, etc.) to bind directly to button labels and conditional flows.
- **Zero-Crash Architecture:** All Win32 interop calls (`user32.dll`, `gdi32.dll`, `dxva2.dll`) are wrapped in safe exception boundaries with comprehensive Serilog logging.

---

## Features & Action Catalog

### 1. Monitor & Display Detection
- Automatically enumerates up to **4 connected displays**.
- Resolves friendly names (e.g. *"Dell U2720Q"*, *"LG UltraGear"*) via `EnumDisplayDevices`.
- Detects the Primary monitor and displays exact screen coordinates and work areas.
- Dynamic monitor polling updates state when displays are connected, disconnected, or rearranged.

### 2. Brightness Control
| Action ID | Name | Description |
|---|---|---|
| `set-brightness` | **Set Brightness** | Sets exact brightness percentage (0–100%) for Display 1–4 or All Displays. |
| `adjust-brightness` | **Adjust Brightness (Step)** | Increases or decreases brightness by a configurable delta (e.g. `+10` or `-10`). |
| `brightness-preset` | **Brightness Preset** | Quick 1-click presets: 25%, 50%, 75%, and 100%. |

### 3. Refresh Rate Control
| Action ID | Name | Description |
|---|---|---|
| `set-refresh-rate` | **Set Refresh Rate** | Sets monitor refresh rate (60Hz, 75Hz, 120Hz, 144Hz, 165Hz, 240Hz, 360Hz). |
| `restore-refresh-rate` | **Restore Previous Refresh Rate** | Safely rolls back to the previous refresh rate setting. |

### 4. Resolution Control
| Action ID | Name | Description |
|---|---|---|
| `set-resolution` | **Set Resolution** | Changes resolution to standard (1080p, 1440p, 4K, 720p) or ultrawide presets. |
| `restore-resolution` | **Restore Previous Resolution** | Rolls back to the previously active resolution. |

### 5. Windows Display Modes & Topologies
| Action ID | Name | Description |
|---|---|---|
| `set-display-mode` | **Windows Display Mode** | Switches to PC Screen Only, Duplicate, Extend, or Second Screen Only. Includes dynamic active state feedback. |
| `toggle-duplicate-extend` | **Toggle Duplicate / Extend** | Toggles between Duplicate (Clone) and Extend modes with reactive button state ("Extend" / "Duplicate"). |

### 6. Primary Monitor Selection
| Action ID | Name | Description |
|---|---|---|
| `set-primary-monitor` | **Set Primary Monitor** | Sets Display 1, 2, 3, or 4 as the Windows primary display, automatically recalculating coordinate offsets for all remaining displays. |

### 7. Orientation & Arrangement
| Action ID | Name | Description |
|---|---|---|
| `set-orientation` | **Set Monitor Orientation** | Rotates monitor to Landscape, Portrait (90°), Landscape Flipped (180°), or Portrait Flipped (270°). |
| `save-arrangement` | **Save Monitor Arrangement** | Captures multi-monitor coordinates under a custom layout name. |
| `restore-arrangement` | **Restore Monitor Arrangement** | Restores saved multi-monitor coordinates with zero desktop disruption. |

### 8. Monitor Power & Input Switching (DDC/CI)
| Action ID | Name | Description |
|---|---|---|
| `monitor-power` | **Monitor Power** | Puts monitor(s) into standby/off via `SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER)` or wakes them up using synthetic mouse events. |
| `switch-input-source` | **Switch Monitor Input Source** | Switches input ports (HDMI 1/2, DisplayPort 1/2, USB-C, DVI, VGA) via DDC/CI VCP opcode `0x60`. |
| `toggle-input-source` | **Toggle Input Sources** | Quick toggle between two selected input sources (e.g. HDMI 1 and DisplayPort 1). |

### 9. Multi-Monitor Display Profiles
| Action ID | Name | Description |
|---|---|---|
| `apply-profile` | **Apply Display Profile** | Restores complete snapshot (topologies, resolutions, refresh rates, orientations, and brightness) for *Gaming*, *Work*, *Movie*, or *Custom*. |
| `toggle-profiles` | **Toggle Display Profiles** | Toggles back and forth between two saved profiles (e.g. *Work* <-> *Gaming*). |

### 10. Quick Toggles
| Action ID | Name | Description |
|---|---|---|
| `toggle-brightness` | **Toggle Brightness Levels** | Toggles between two configured brightness levels (e.g. 20% and 80%). |
| `toggle-refresh-rate` | **Toggle Refresh Rates** | Toggles between two refresh rates (e.g. 60Hz and 144Hz). |
| `toggle-resolution` | **Toggle Resolutions** | Toggles between two resolutions (e.g. 1080p and 1440p). |

### 11. Window Management & Always on Top
| Action ID | Name | Description |
|---|---|---|
| `toggle-always-on-top` | **Toggle Always on Top** | Pins or unpins the currently active window on top of all others. Implements dynamic button state: `"Pinned"` / `"Unpinned"`. |
| `unpin-all-windows` | **Unpin All Windows** | Removes Always on Top from all pinned windows with one click. |
| `move-window-to-display` | **Move Window to Display** | Moves active window centered to Display 1, 2, 3, or 4. |
| `move-window-next-prev` | **Move Window to Next/Prev Display** | Cycles active window to the next or previous monitor in sequence. |
| `move-cursor-to-display` | **Move Cursor to Display** | Instantly warps mouse cursor to the center of Display 1, 2, 3, or 4. |
| `maximize-on-display` | **Maximize Window on Display** | Moves active window to target display and maximizes it. |

---

## Reactive Variables Reference

ScreenxDeck synchronizes 28 high-frequency variables with the Macro Deck host. This keeps simultaneous remote polls below the host dispatcher limit, so values remain available instead of flickering. In addition to reading values in labels (`{{ vars.screenxdeck_brightness }}%`), **variables are bidirectional**: you can bind Macro Deck's built-in **Slider Widget** directly to `screenxdeck_brightness` or `screenxdeck_display{N}_brightness` to control brightness in real time.

### Default / Primary Display (Ideal for Slider Widgets)
| Variable Name | Type | Description |
|---|---|---|
| `screenxdeck_brightness` | Numeric | Primary display brightness % (0–100). **Bind to Macro Deck Slider Widget for native control!** |
| `screenxdeck_brightness_mode` | Text | Active brightness control method: `"DDC/CI"` (Hardware) or `"Gamma"` (Software). |
| `screenxdeck_primary_name` | Text | Friendly monitor name for the primary display. |
| `screenxdeck_resolution` | Text | Primary display resolution (e.g. `"1920x1080"`). |
| `screenxdeck_refreshrate` | Numeric | Primary display refresh rate in Hz (e.g. `60`, `144`). |
| `screenxdeck_orientation` | Text | Primary display orientation: `"Landscape"`, `"Portrait"`, etc. |

### Global Desktop & Window State
| Variable Name | Type | Description |
|---|---|---|
| `screenxdeck_display_count` | Numeric | Number of currently connected monitors (1–4). |
| `screenxdeck_primary_display` | Numeric | Index of the current primary monitor (1–4). |
| `screenxdeck_active_display_mode` | Text | Current topology: `"Extend"`, `"Duplicate"`, `"InternalOnly"`, `"ExternalOnly"`. |
| `screenxdeck_active_window_topmost` | Boolean | `true` if currently active window is pinned Always-on-Top (Writable to toggle!). |
| `screenxdeck_active_window_title` | Text | Title of the currently focused foreground window. |
| `screenxdeck_active_profile` | Text | Name of the active display profile (Writable to apply!). |

### Per-Monitor Variables (N = 1 to 4)
| Variable Name | Type | Description |
|---|---|---|
| `screenxdeck_display{N}_connected` | Boolean | `true` when monitor N is connected, otherwise `false`. |
| `screenxdeck_display{N}_brightness` | Numeric | Current brightness % for display N (0–100). Bidirectional with Slider widget. |
| `screenxdeck_display{N}_resolution` | Text | Resolution for display N, or `"Not connected"` when the monitor is unavailable. |
| `screenxdeck_display{N}_refreshrate` | Numeric | Refresh rate in Hz for display N (e.g. `144`). |

---

## Installation

### From Pre-built Package
1. Get `com.cjhackeryt.screenxdeck-1.0.0.macrodeckplugin` from the `dist/` directory or GitHub Releases.
2. Open **Macro Deck 3**.
3. Go to **Plugins** -> **Install from file**.
4. Select `dist/com.cjhackeryt.screenxdeck-1.0.0.macrodeckplugin`.
5. Restart Macro Deck if prompted.

### From Source
Requires the .NET 10 SDK and the Macro Deck CLI:
```powershell
# 1. Clone repository
git clone https://github.com/Cjhackeryt/screenxdeck.git
cd screenxdeck

# 2. Install the latest Macro Deck CLI preview
dotnet tool install --global MacroDeck.Plugin.Cli --prerelease

# 3. Build and run unit tests
dotnet test

# 4. Build, package, and inspect the plugin archive
.\package.ps1
```
The packaged archive will be generated at:
`dist/com.cjhackeryt.screenxdeck-1.0.0.macrodeckplugin`

---

## Configuration & Persistence

Settings are automatically saved in:
`%APPDATA%\MacroDeck\config\com.cjhackeryt.screenxdeck\settings.json`

Example configuration:
```json
{
  "activeProfileName": "Work",
  "autoPinApplications": [
    "notepad",
    "CalculatorApp",
    "obs64"
  ],
  "savedProfiles": [
    {
      "name": "Gaming",
      "displayMode": "Extend",
      "monitors": [
        {
          "displayIndex": 1,
          "width": 2560,
          "height": 1440,
          "refreshRate": 144,
          "brightness": 85,
          "isPrimary": true
        }
      ]
    }
  ]
}
```

---

## Architecture

```
ScreenxDeck/
├── package.ps1                   # Automated build & packaging script
├── ScreenxDeck.slnx              # .NET 10 Solution
├── src/ScreenxDeck/
│   ├── manifest.json             # Macro Deck 3 extension manifest
│   ├── Assets/icon.svg           # High-resolution vector icon
│   ├── Program.cs                # Entry point & DI builder
│   ├── Core/
│   │   ├── Models/               # Display, Monitor, Profile, Settings models
│   │   └── Native/               # User32, Gdi32, Dxva2, CcdApi Win32 P/Invoke
│   ├── Services/                 # DisplayManager, Brightness, Resolution, Topology,
│   │                             # MonitorControl, Profile, WindowManager, Settings
│   ├── Actions/                  # 27 Macro Deck Action Definitions & Executors
│   ├── Integration/              # ScreenxDeckIntegration (Actions & Variable sync)
│   └── Localization/             # Strings.resx (Strongly typed source-generated)
└── tests/ScreenxDeck.Tests/      # xUnit automated test suite (.NET 10)
```

---

## AI Disclosure

AI assistance was used to implement the variable stability and monitor-state updates. ScreenxDeck does not use AI at runtime and does not transmit user data to AI services.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
