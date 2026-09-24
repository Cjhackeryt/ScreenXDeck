using System;
using System.Linq;
using ScreenxDeck.Core.Models;
using ScreenxDeck.Core.Native;
using Serilog;

namespace ScreenxDeck.Services;

public sealed class ProfileService
{
    private readonly ILogger _logger;
    private readonly DisplayManagerService _displayManager;
    private readonly BrightnessService _brightnessService;
    private readonly ResolutionRefreshService _resolutionRefreshService;
    private readonly TopologyService _topologyService;
    private readonly MonitorControlService _monitorControlService;
    private readonly SettingsService _settingsService;

    public ProfileService(
        ILogger logger,
        DisplayManagerService displayManager,
        BrightnessService brightnessService,
        ResolutionRefreshService resolutionRefreshService,
        TopologyService topologyService,
        MonitorControlService monitorControlService,
        SettingsService settingsService)
    {
        _logger = logger.ForContext<ProfileService>();
        _displayManager = displayManager;
        _brightnessService = brightnessService;
        _resolutionRefreshService = resolutionRefreshService;
        _topologyService = topologyService;
        _monitorControlService = monitorControlService;
        _settingsService = settingsService;
    }

    public bool SaveCurrentAsProfile(string profileName, string description = "")
    {
        var profile = new DisplayProfile
        {
            Name = profileName,
            Description = description,
            DisplayMode = _topologyService.CurrentTopology.ToString()
        };

        foreach (var mon in _displayManager.ConnectedMonitors)
        {
            var devMode = new DEVMODE();
            devMode.Init();
            int posX = 0, posY = 0;
            if (User32.EnumDisplaySettingsExA(mon.DeviceName, User32.ENUM_CURRENT_SETTINGS, ref devMode, 0))
            {
                posX = devMode.dmPositionX;
                posY = devMode.dmPositionY;
            }

            profile.Monitors.Add(new MonitorProfileEntry
            {
                DisplayIndex = mon.DisplayIndex,
                DeviceName = mon.DeviceName,
                FriendlyName = mon.FriendlyName,
                Width = mon.CurrentWidth,
                Height = mon.CurrentHeight,
                RefreshRate = mon.CurrentRefreshRate,
                Orientation = mon.CurrentOrientation,
                PositionX = posX,
                PositionY = posY,
                IsPrimary = mon.IsPrimary,
                Brightness = _brightnessService.GetBrightness(mon.DisplayIndex)
            });
        }

        _settingsService.Update(s =>
        {
            s.SavedProfiles.RemoveAll(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));
            s.SavedProfiles.Add(profile);
            s.ActiveProfileName = profileName;
        });

        _logger.Information("Saved current display state as profile '{Name}'", profileName);
        return true;
    }

    public bool ApplyProfile(string profileName)
    {
        var profile = _settingsService.Current.SavedProfiles.FirstOrDefault(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (profile == null)
        {
            _logger.Warning("ApplyProfile: Profile '{Name}' not found", profileName);
            return false;
        }

        _logger.Information("Applying display profile '{Name}'", profileName);

        // 1. Set display topology if specified
        if (Enum.TryParse<CcdApi.DisplayTopology>(profile.DisplayMode, true, out var topology))
        {
            _topologyService.SetDisplayMode(topology);
        }

        // 2. Apply monitor settings (resolution, refresh rate, orientation, brightness)
        foreach (var entry in profile.Monitors)
        {
            if (entry.Width > 0 && entry.Height > 0)
            {
                _resolutionRefreshService.SetResolution(entry.DisplayIndex, entry.Width, entry.Height);
            }

            if (entry.RefreshRate > 0)
            {
                _resolutionRefreshService.SetRefreshRate(entry.DisplayIndex, entry.RefreshRate);
            }

            _topologyService.SetOrientation(entry.DisplayIndex, entry.Orientation);
            _brightnessService.SetBrightness(entry.DisplayIndex, entry.Brightness);
        }

        // 3. Set Primary monitor
        var primaryEntry = profile.Monitors.FirstOrDefault(m => m.IsPrimary);
        if (primaryEntry != null)
        {
            _topologyService.SetPrimaryMonitor(primaryEntry.DisplayIndex);
        }

        _settingsService.Update(s => s.ActiveProfileName = profileName);
        _displayManager.RefreshMonitors();
        return true;
    }

    public bool ToggleProfiles(string profileA, string profileB)
    {
        string current = _settingsService.Current.ActiveProfileName;
        string target = string.Equals(current, profileA, StringComparison.OrdinalIgnoreCase) ? profileB : profileA;
        return ApplyProfile(target);
    }
}
