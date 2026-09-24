using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using ScreenxDeck.Actions;
using ScreenxDeck.Core.Models;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Integration;

public sealed class ScreenxDeckIntegration : IPluginIntegration, IVariableProvider, IDisposable
{
    private const string CatalogMigrationMarker = "catalog-variables-v2.marker";
    private static readonly string[] LegacyVariableSuffixes =
    [
        "name",
        "device",
        "brightness_method"
    ];

    private readonly ILogger _logger;
    private readonly DisplayManagerService _displayManager;
    private readonly BrightnessService _brightnessService;
    private readonly ResolutionRefreshService _resolutionRefreshService;
    private readonly TopologyService _topologyService;
    private readonly MonitorControlService _monitorControlService;
    private readonly ProfileService _profileService;
    private readonly WindowManagerService _windowManager;
    private readonly SettingsService _settingsService;

    private readonly List<IActionDefinition> _actions = [];
    private readonly IReadOnlyList<VariableDefinition> _variables = ScreenxDeckVariables.CreateDefinitions();

    public IReadOnlyList<IActionDefinition> Actions => _actions;
    public IReadOnlyList<VariableDefinition> Variables => _variables;
    public IReadOnlyList<VariableDefinition> DeclaredVariables => _variables;
    public bool VariablesDependOnConfiguration => false;

    public ScreenxDeckIntegration(
        ILogger logger,
        DisplayManagerService displayManager,
        BrightnessService brightnessService,
        ResolutionRefreshService resolutionRefreshService,
        TopologyService topologyService,
        MonitorControlService monitorControlService,
        ProfileService profileService,
        WindowManagerService windowManager,
        SettingsService settingsService)
    {
        _logger = logger.ForContext<ScreenxDeckIntegration>();
        _displayManager = displayManager;
        _brightnessService = brightnessService;
        _resolutionRefreshService = resolutionRefreshService;
        _topologyService = topologyService;
        _monitorControlService = monitorControlService;
        _profileService = profileService;
        _windowManager = windowManager;
        _settingsService = settingsService;

        RegisterActions();
    }

    private void RegisterActions()
    {
        // 1. Brightness
        _actions.Add(new SetBrightnessAction(_brightnessService, _logger));
        _actions.Add(new AdjustBrightnessAction(_brightnessService, _logger));
        _actions.Add(new BrightnessPresetAction(_brightnessService, _logger));

        // 2. Refresh Rate
        _actions.Add(new SetRefreshRateAction(_resolutionRefreshService, _logger));
        _actions.Add(new RestoreRefreshRateAction(_resolutionRefreshService, _logger));

        // 3. Resolution
        _actions.Add(new SetResolutionAction(_resolutionRefreshService, _logger));
        _actions.Add(new RestoreResolutionAction(_resolutionRefreshService, _logger));

        // 4. Windows Display Mode & Topologies
        _actions.Add(new SetDisplayModeAction(_topologyService, _logger));
        _actions.Add(new ToggleDuplicateExtendAction(_topologyService, _logger));

        // 5. Primary Monitor
        _actions.Add(new SetPrimaryMonitorAction(_topologyService, _logger));

        // 6. Orientation & Arrangement
        _actions.Add(new SetOrientationAction(_topologyService, _logger));
        _actions.Add(new SaveArrangementAction(_topologyService, _logger));
        _actions.Add(new RestoreArrangementAction(_topologyService, _logger));

        // 7. Monitor Control & DDC/CI
        _actions.Add(new MonitorPowerAction(_monitorControlService, _logger));
        _actions.Add(new SwitchInputSourceAction(_monitorControlService, _logger));
        _actions.Add(new ToggleInputSourceAction(_monitorControlService, _logger));

        // 8. Display Profiles
        _actions.Add(new ApplyProfileAction(_profileService, _logger));
        _actions.Add(new ToggleProfilesAction(_profileService, _logger));

        // 9. Quick Toggles
        _actions.Add(new ToggleBrightnessAction(_brightnessService, _logger));
        _actions.Add(new ToggleRefreshRateAction(_resolutionRefreshService, _logger));
        _actions.Add(new ToggleResolutionAction(_resolutionRefreshService, _logger));

        // 10. Window Management & Always on Top
        _actions.Add(new ToggleAlwaysOnTopAction(_windowManager, _logger));
        _actions.Add(new UnpinAllWindowsAction(_windowManager, _logger));
        _actions.Add(new MoveWindowToDisplayAction(_windowManager, _logger));
        _actions.Add(new MoveWindowNextPreviousAction(_windowManager, _logger));
        _actions.Add(new MoveCursorToDisplayAction(_windowManager, _logger));
        _actions.Add(new MaximizeOnDisplayAction(_windowManager, _logger));

        _logger.Information("Registered {Count} ScreenxDeck actions successfully", _actions.Count);
    }

    public async Task InitializeAsync(IIntegrationContext context)
    {
        _logger.Information("ScreenxDeck variable provider initialized with {Count} variables", _variables.Count);
        _displayManager.RefreshMonitors();
        await RemoveLegacyEagerVariablesAsync(context.Variables);
    }

    private async Task RemoveLegacyEagerVariablesAsync(IVariableApi hostVariables)
    {
        string? dataDirectory = Environment.GetEnvironmentVariable("MACRO_DECK_PLUGIN_DATA_DIRECTORY");
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            _logger.Warning("MACRO_DECK_PLUGIN_DATA_DIRECTORY is unavailable; legacy variable migration was deferred");
            return;
        }

        string markerPath = Path.Combine(dataDirectory, CatalogMigrationMarker);
        if (File.Exists(markerPath))
        {
            return;
        }

        try
        {
            var legacyNames = Enumerable.Range(1, 4)
                .SelectMany(display => LegacyVariableSuffixes.Select(suffix => $"screenxdeck_display{display}_{suffix}"))
                .ToHashSet(StringComparer.Ordinal);
            var legacyIds = legacyNames
                .Select(name => name.Replace('_', '-'))
                .ToHashSet(StringComparer.Ordinal);

            int removedCount = 0;
            foreach (VariableHandle handle in await hostVariables.GetAllAsync())
            {
                bool isLegacyEagerVariable =
                    (handle.Name != null && legacyNames.Contains(handle.Name)) ||
                    (handle.DefinitionId != null && legacyIds.Contains(handle.DefinitionId));
                if (!isLegacyEagerVariable)
                {
                    continue;
                }

                await hostVariables.DeleteAsync(handle.Id);
                removedCount++;
            }

            Directory.CreateDirectory(dataDirectory);
            await File.WriteAllTextAsync(markerPath, "1");
            _logger.Information("Removed {Count} legacy eager ScreenxDeck variables", removedCount);
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "ScreenxDeck legacy variable migration failed and will be retried");
        }
    }

    public Task ShutdownAsync()
    {
        _logger.Information("ScreenxDeck integration shutting down...");
        return Task.CompletedTask;
    }

    private static string NormalizeVariableName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var val = name.Trim().Replace('-', '_').ToLowerInvariant();
        int lastSeparator = val.LastIndexOfAny(['.', ':', '/']);
        if (lastSeparator >= 0)
        {
            val = val[(lastSeparator + 1)..];
        }
        return val;
    }

    private static string FormatOrientation(uint orientation) => orientation switch
    {
        1 => "Portrait",
        2 => "Landscape (Flipped)",
        3 => "Portrait (Flipped)",
        _ => "Landscape"
    };

    public ValueTask<VariableReading> ReadAsync(string name, CancellationToken cancellationToken = default)
    {
        string normName = NormalizeVariableName(name);
        var monitors = _displayManager.ConnectedMonitors;
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        int primaryIdx = primary?.DisplayIndex ?? 1;

        if (normName.Equals(ScreenxDeckVariables.Brightness, StringComparison.OrdinalIgnoreCase) ||
            (normName.EndsWith("_brightness", StringComparison.OrdinalIgnoreCase) && !normName.Contains("display")))
        {
            int val = primary != null ? _brightnessService.GetBrightness(primaryIdx) : 100;
            return ValueTask.FromResult(VariableReading.Of(val, 0, 100, 1));
        }

        if (normName.Equals(ScreenxDeckVariables.BrightnessMode, StringComparison.OrdinalIgnoreCase))
        {
            string mode = primary != null ? _brightnessService.GetBrightnessMethod(primaryIdx) : "DDC/CI";
            return ValueTask.FromResult(VariableReading.Of(mode));
        }

        if (normName.Equals(ScreenxDeckVariables.Resolution, StringComparison.OrdinalIgnoreCase))
        {
            string res = primary != null ? $"{primary.CurrentWidth}x{primary.CurrentHeight}" : "1920x1080";
            return ValueTask.FromResult(VariableReading.Of(res));
        }

        if (normName.Equals(ScreenxDeckVariables.RefreshRate, StringComparison.OrdinalIgnoreCase))
        {
            int rate = (primary != null && primary.CurrentRefreshRate > 0) ? primary.CurrentRefreshRate : 60;
            return ValueTask.FromResult(VariableReading.Of(rate));
        }

        if (normName.Equals(ScreenxDeckVariables.Orientation, StringComparison.OrdinalIgnoreCase))
        {
            string ori = primary != null ? FormatOrientation(primary.CurrentOrientation) : "Landscape";
            return ValueTask.FromResult(VariableReading.Of(ori));
        }

        if (normName.Equals(ScreenxDeckVariables.PrimaryName, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(VariableReading.Of(primary?.FriendlyName ?? "Primary Display"));
        }

        if (normName.Equals(ScreenxDeckVariables.DisplayCount, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(VariableReading.Of(monitors.Count > 0 ? monitors.Count : 1));
        }

        if (normName.Equals(ScreenxDeckVariables.PrimaryDisplay, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(VariableReading.Of(primaryIdx));
        }

        if (normName.Equals(ScreenxDeckVariables.ActiveDisplayMode, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(VariableReading.Of(_topologyService.CurrentTopology.ToString()));
        }

        if (normName.Equals(ScreenxDeckVariables.ActiveWindowTopmost, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(VariableReading.Of(_windowManager.IsForegroundWindowTopmost()));
        }

        if (normName.Equals(ScreenxDeckVariables.ActiveWindowTitle, StringComparison.OrdinalIgnoreCase))
        {
            var (title, _, _) = _windowManager.GetActiveWindowInfo();
            return ValueTask.FromResult(VariableReading.Of(!string.IsNullOrWhiteSpace(title) ? title : "Desktop"));
        }

        if (normName.Equals(ScreenxDeckVariables.ActiveProfile, StringComparison.OrdinalIgnoreCase))
        {
            string prof = _settingsService.Current.ActiveProfileName;
            return ValueTask.FromResult(VariableReading.Of(!string.IsNullOrWhiteSpace(prof) ? prof : "Default"));
        }

        // Per-display checking 1 through 4 (connection, brightness, resolution, and refresh rate)
        for (int i = 1; i <= 4; i++)
        {
            var mon = monitors.FirstOrDefault(m => m.DisplayIndex == i);

            if (normName.Equals($"screenxdeck_display{i}_connected", StringComparison.OrdinalIgnoreCase))
                return ValueTask.FromResult(VariableReading.Of(mon != null));

            if (normName.Equals($"screenxdeck_display{i}_brightness", StringComparison.OrdinalIgnoreCase))
            {
                int b = mon != null ? _brightnessService.GetBrightness(i) : 100;
                return ValueTask.FromResult(VariableReading.Of(b, 0, 100, 1));
            }

            if (normName.Equals($"screenxdeck_display{i}_resolution", StringComparison.OrdinalIgnoreCase))
                return ValueTask.FromResult(VariableReading.Of(mon != null ? $"{mon.CurrentWidth}x{mon.CurrentHeight}" : "Not connected"));

            if (normName.Equals($"screenxdeck_display{i}_refreshrate", StringComparison.OrdinalIgnoreCase))
            {
                int rate = (mon != null && mon.CurrentRefreshRate > 0) ? mon.CurrentRefreshRate : 60;
                return ValueTask.FromResult(VariableReading.Of(rate));
            }
        }

        // Safe fallback for any other reading so it never shows "Unavailable" in Macro Deck!
        return ValueTask.FromResult(VariableReading.Of(""));
    }

    public ValueTask<VariableWriteResult> SetValueAsync(string name, object? value, CancellationToken cancellationToken = default)
    {
        string normName = NormalizeVariableName(name);
        _logger.Information("SetValueAsync invoked for variable '{Name}' (normalized '{Norm}') with value '{Val}'", name, normName, value);

        var monitors = _displayManager.ConnectedMonitors;
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        int primaryIdx = primary?.DisplayIndex ?? 1;

        if (normName.Equals(ScreenxDeckVariables.Brightness, StringComparison.OrdinalIgnoreCase))
        {
            if (TryConvertToNumber(value, out int val) && val >= 0 && val <= 100)
            {
                _brightnessService.SetBrightness(primaryIdx, val);
                return ValueTask.FromResult(VariableWriteResult.Applied());
            }
            return ValueTask.FromResult(VariableWriteResult.InvalidValue((MacroDeck.Localization.LocalizedText)"Brightness must be 0-100"));
        }

        for (int i = 1; i <= 4; i++)
        {
            if (normName.Equals($"screenxdeck_display{i}_brightness", StringComparison.OrdinalIgnoreCase))
            {
                if (TryConvertToNumber(value, out int val) && val >= 0 && val <= 100)
                {
                    _brightnessService.SetBrightness(i, val);
                    return ValueTask.FromResult(VariableWriteResult.Applied());
                }
                return ValueTask.FromResult(VariableWriteResult.InvalidValue((MacroDeck.Localization.LocalizedText)"Brightness must be 0-100"));
            }
        }

        if (normName.Equals(ScreenxDeckVariables.ActiveWindowTopmost, StringComparison.OrdinalIgnoreCase))
        {
            _windowManager.ToggleAlwaysOnTopActiveWindow();
            return ValueTask.FromResult(VariableWriteResult.Applied());
        }

        if (normName.Equals(ScreenxDeckVariables.ActiveDisplayMode, StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<ScreenxDeck.Core.Native.CcdApi.DisplayTopology>(value?.ToString(), true, out var topology))
            {
                _topologyService.SetDisplayMode(topology);
                return ValueTask.FromResult(VariableWriteResult.Applied());
            }
            return ValueTask.FromResult(VariableWriteResult.InvalidValue((MacroDeck.Localization.LocalizedText)"Invalid topology mode"));
        }

        if (normName.Equals(ScreenxDeckVariables.ActiveProfile, StringComparison.OrdinalIgnoreCase))
        {
            if (value is string profile && !string.IsNullOrWhiteSpace(profile))
            {
                _profileService.ApplyProfile(profile);
                return ValueTask.FromResult(VariableWriteResult.Applied());
            }
            return ValueTask.FromResult(VariableWriteResult.InvalidValue((MacroDeck.Localization.LocalizedText)"Profile name cannot be empty"));
        }

        return ValueTask.FromResult(VariableWriteResult.NotWritable((MacroDeck.Localization.LocalizedText)"Variable is read-only"));
    }

    private static bool TryConvertToNumber(object? value, out int result)
    {
        result = 0;
        if (value is null) return false;
        try
        {
            result = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _displayManager.Dispose();
        _windowManager.Dispose();
    }
}
