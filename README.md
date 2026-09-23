# ScreenXDeck

Macro Deck 3 plugin for Windows display modes, monitor control, window management,
virtual desktops, and brightness.

## Actions

- PC Screen Only
- Duplicate
- Extend
- Second Screen Only
- Brightness Up
- Brightness Down
- Set Brightness
- Set Monitor Brightness
- Adjust Monitor Brightness
- Set Monitor Input
- Next Monitor Input
- Set Monitor Power
- Focus, Minimize, Maximize, Restore, and Close Window
- Toggle Always on Top
- Snap Window
- Next Desktop
- Previous Desktop

The plugin also includes the MacroTools monitor-brightness widget and its dynamic
monitor brightness variables. These features are part of the ScreenXDeck plugin;
MacroTools is not shipped or registered as a separate plugin.

ScreenXDeck is the only plugin project in this repository. Its source includes the
monitor, window, widget, virtual-desktop, and display-control features that were
previously maintained in the separate ScreenControl source.

## Macro Deck brightness variable

The plugin exposes a writable numeric percentage variable named `screenxdeck_brightness`.
Add a Macro Deck slider, bind it to this variable, and enable variable writing. The slider
reads the current brightness and writes a value from 0 to 100 when moved. Changes are
applied to every monitor that supports WMI or DDC/CI.

Additional variables:

- `screenxdeck_display_mode` — writable text value: `internal`, `clone`, `extend`, or `external`.
- `screenxdeck_active_monitor` — live text showing the primary monitor device,
  resolution, and number of connected displays.
- `screenxdeck_monitor_1_name` through `screenxdeck_monitor_4_name` — friendly
  monitor model names read from Windows EDID, such as `DELL U2412M`.
- `screenxdeck_display_connected` — `true` when Windows reports at least one display.
- `screenxdeck_monitor_1_connected` through `screenxdeck_monitor_4_connected` —
  boolean connection status for each Windows display slot.
- `screenxdeck_monitor_1_refresh_rate` through `screenxdeck_monitor_4_refresh_rate` —
  current refresh rate in Hz for each connected monitor.

Refresh-rate actions use Windows display APIs and only apply rates available at the
monitor's current resolution: **Refresh Rate Up**, **Refresh Rate Down**, and
**Set Refresh Rate**.
- `screenxdeck_monitor_1_brightness` through `screenxdeck_monitor_4_brightness` —
  individual writable DDC/CI monitor brightness variables. Variables remain visible
  when a monitor is temporarily disconnected; unsupported or disconnected monitors
  report `Unavailable` and never reuse another monitor's value.

The display-mode variable now queries the live Windows display topology, so it
updates when the mode is changed outside Macro Deck. The slider binding's
`Step` field belongs to Macro Deck's UI, not the plugin variable definition;
set it to the desired resolution in the slider binding.

The plugin targets Macro Deck SDK `3.0.0-beta.12` and .NET 10. It uses Windows `DisplaySwitch.exe` for display modes and a two-layer brightness strategy:

- WMI `WmiMonitorBrightness` for laptop/internal panels.
- Windows DDC/CI (`dxva2.dll`) for external HDMI/DisplayPort monitors.

For external monitors, enable **DDC/CI** in the monitor's on-screen menu. Some docks, KVMs, adapters, TVs, and monitors do not pass DDC/CI even when the setting is enabled. The plugin applies a brightness change to every monitor that accepts either WMI or DDC/CI control.
For monitors that expose neither hardware control, ScreenXDeck falls back to the Windows GPU
gamma ramp. Gamma brightness changes the rendered desktop rather than the monitor backlight and
may reduce color range while active.

## Build and package

From the repository root:

```powershell
dotnet restore ScreenXDeckPlugin\ScreenXDeckPlugin.csproj
dotnet build ScreenXDeckPlugin\ScreenXDeckPlugin.csproj -c Release
$build = "ScreenXDeckPlugin\bin\Release\net10.0-windows"
$staging = "dist\staging"
New-Item -ItemType Directory -Force "$staging\runtimes\win-x64" | Out-Null
Copy-Item "$build\*" "$staging\runtimes\win-x64" -Recurse -Force
Remove-Item "$staging\runtimes\win-x64\manifest.json" -Force -ErrorAction SilentlyContinue
Remove-Item "$staging\runtimes\win-x64\Assets" -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "ScreenXDeckPlugin\manifest.json" $staging -Force
Copy-Item "ScreenXDeckPlugin\Assets" $staging -Recurse -Force
macrodeck-plugin validate --directory $staging
macrodeck-plugin pack --source $staging --output dist\com.screenxdeck.display.macroDeckPlugin --force
```
