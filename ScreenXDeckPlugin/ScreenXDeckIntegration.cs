using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Variables;
using MacroDeck.Sdk.Ui;
using MacroDeck.Sdk.Widgets;
using Microsoft.Extensions.Logging;
using ScreenControl;
using ScreenControl.Actions;
using ScreenControl.Monitors;
using ScreenControl.Windows;
using ScreenControl.Widgets;
using ScreenXDeckPlugin.Actions;
using System.Text.Json;
using System.Management;
using System.Windows.Forms;

namespace ScreenXDeckPlugin;

public sealed class ScreenXDeckIntegration : IPluginIntegration, IVariableProvider, IWidgetTypeProvider, IUiProvider
{
    private const string BrightnessVariable = "screenxdeck_brightness";
    private const string DisplayModeVariable = "screenxdeck_display_mode";
    private const string ActiveMonitorVariable = "screenxdeck_active_monitor";
    private const string DisplayConnectedVariable = "screenxdeck_display_connected";
    private const int MaximumMonitorVariables = 4;
    private static readonly TimeSpan VariableRefreshInterval = TimeSpan.FromMilliseconds(500);
    public IReadOnlyList<IActionDefinition> Actions { get; }
    public IReadOnlyList<VariableDefinition> Variables { get; }
    public IReadOnlyList<VariableDefinition> DeclaredVariables => Variables;
    public bool VariablesDependOnConfiguration => false;

    private readonly MonitorBrightnessService _brightness;
    private readonly DisplayModeService _display;
    private readonly RefreshRateService _refreshRates;
    private readonly IMonitorService _screenControlMonitors;
    private readonly IWindowService _screenControlWindows;
    private readonly BrightnessWidget _screenControlWidget;
    private readonly ILogger<ScreenXDeckIntegration> _logger;
    private string _displayMode = "extend";
    private int _lastBrightness = 50;

    public ScreenXDeckIntegration(
        ILogger<ScreenXDeckIntegration> logger,
        IMonitorService screenControlMonitors,
        IWindowService screenControlWindows,
        Serilog.ILogger serilogLogger)
    {
        _logger = logger;
        _screenControlMonitors = screenControlMonitors;
        _screenControlWindows = screenControlWindows;
        _screenControlWidget = new BrightnessWidget(screenControlMonitors, serilogLogger);
        var display = new DisplayModeService(logger);
        _display = display;
        _brightness = new MonitorBrightnessService(logger, screenControlMonitors);
        _refreshRates = new RefreshRateService(logger);
        var variables = new List<VariableDefinition>
        {
            VariableDefinition.Eager(BrightnessVariable, VariableType.Numeric, 0, VariableRefreshInterval) with
            {
                Unit = "%",
                SemanticKind = VariableSemanticKinds.Percentage,
                Write = new VariableWriteCapability { CommitOnRelease = false }
            },
            VariableDefinition.Eager(DisplayModeVariable, VariableType.Text, refreshInterval: VariableRefreshInterval) with
            {
                Write = new VariableWriteCapability { CommitOnRelease = true }
            },
            VariableDefinition.Eager(ActiveMonitorVariable, VariableType.Text, refreshInterval: VariableRefreshInterval)
            ,
            VariableDefinition.Eager(DisplayConnectedVariable, VariableType.Boolean, refreshInterval: VariableRefreshInterval)
        };
        for (var index = 0; index < MaximumMonitorVariables; index++)
        {
            variables.Add(VariableDefinition.Eager(
                $"screenxdeck_monitor_{index + 1}_brightness",
                VariableType.Numeric,
                0,
                VariableRefreshInterval) with
            {
                Unit = "%",
                SemanticKind = VariableSemanticKinds.Percentage,
                Write = new VariableWriteCapability { CommitOnRelease = false }
            });
            variables.Add(VariableDefinition.Eager(
                $"screenxdeck_monitor_{index + 1}_name",
                VariableType.Text,
                refreshInterval: VariableRefreshInterval));
            variables.Add(VariableDefinition.Eager(
                $"screenxdeck_monitor_{index + 1}_connected",
                VariableType.Boolean,
                refreshInterval: VariableRefreshInterval));
            variables.Add(VariableDefinition.Eager(
                $"screenxdeck_monitor_{index + 1}_refresh_rate",
                VariableType.Numeric,
                0,
                VariableRefreshInterval) with
            {
                Unit = "Hz"
            });
        }
        for (var index = 1; index <= MaximumMonitorVariables; index++)
            variables.Add(DisplayVariables.MonitorBrightness(index));
        Variables = variables;
        Actions =
        [
            new DisplayModeAction("pc-screen-only", "PC Screen Only", "Show content on the main screen only.", "internal", display),
            new DisplayModeAction("duplicate", "Duplicate", "Mirror the main screen to all displays.", "clone", display),
            new DisplayModeAction("extend", "Extend", "Extend the desktop across multiple displays.", "extend", display),
            new DisplayModeAction("second-screen-only", "Second Screen Only", "Show content on the second screen only.", "external", display),
            new BrightnessStepAction("brightness-up", "Brightness Up", "Increase monitor brightness.", 10, _brightness),
            new BrightnessStepAction("brightness-down", "Brightness Down", "Decrease monitor brightness.", -10, _brightness),
            new SetBrightnessAction(_brightness)
            ,
            new RefreshRateStepAction("refresh-rate-up", "Refresh Rate Up", "Increase monitor refresh rate.", 1, _refreshRates),
            new RefreshRateStepAction("refresh-rate-down", "Refresh Rate Down", "Decrease monitor refresh rate.", -1, _refreshRates),
            new SetRefreshRateAction(_refreshRates),
            new SetMonitorBrightnessAction(_screenControlMonitors),
            new AdjustMonitorBrightnessAction(_screenControlMonitors),
            new SetMonitorInputAction(_screenControlMonitors),
            new CycleMonitorInputAction(_screenControlMonitors),
            new SetMonitorPowerAction(_screenControlMonitors),
            new FocusWindowAction(_screenControlWindows),
            new MinimizeWindowAction(_screenControlWindows),
            new MaximizeWindowAction(_screenControlWindows),
            new RestoreWindowAction(_screenControlWindows),
            new CloseWindowAction(_screenControlWindows),
            new ToggleAlwaysOnTopAction(_screenControlWindows),
            new SnapWindowAction(_screenControlWindows),
            new NextDesktopAction(_screenControlWindows),
            new PreviousDesktopAction(_screenControlWindows)
        ];
    }

    public Task InitializeAsync(IIntegrationContext context)
    {
        _logger.LogInformation(
            "ScreenXDeck variable provider initialized with {VariableCount} variables.",
            Variables.Count);
        return Task.CompletedTask;
    }

    public Task InitializeAsync(IWidgetTypeProviderContext context, CancellationToken cancellationToken) =>
        _screenControlWidget.InitializeAsync(context, cancellationToken);

    public IReadOnlyList<WidgetTypeDescriptor> GetWidgetTypes() =>
        _screenControlWidget.GetWidgetTypes();

    public IReadOnlyList<UiSurfaceDeclaration> Surfaces => _screenControlWidget.Surfaces;

    public Task<IUiSession?> CreateSessionAsync(
        UiSessionRequest request,
        CancellationToken cancellationToken) =>
        _screenControlWidget.CreateSessionAsync(request, cancellationToken);

    public Task ShutdownAsync() => Task.CompletedTask;

    public async ValueTask<VariableReading> ReadAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var variableName = NormalizeVariableName(name);
        _logger.LogDebug("Reading ScreenXDeck variable '{RequestedName}' as '{VariableName}'.", name, variableName);
        if (!variableName.Equals(BrightnessVariable, StringComparison.OrdinalIgnoreCase))
        {
            if (DisplayVariables.TryParseMonitorBrightnessId(variableName, out var screenControlMonitorIndex))
                return ReadScreenControlMonitorBrightness(screenControlMonitorIndex);

            var screenControlReading = ReadScreenControlVariable(variableName);
            if (screenControlReading is not null)
                return screenControlReading;

            if (variableName.Equals(DisplayModeVariable, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _displayMode = _display.GetCurrentMode();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Unable to query the current Windows display topology.");
                }

                return VariableReading.Of(_displayMode);
            }

            if (variableName.Equals(ActiveMonitorVariable, StringComparison.OrdinalIgnoreCase))
                return VariableReading.Of(GetActiveMonitorDescription());

            if (variableName.Equals(DisplayConnectedVariable, StringComparison.OrdinalIgnoreCase))
                return VariableReading.Of(Screen.AllScreens.Length > 0);

            if (TryGetMonitorNameIndex(variableName, out var monitorNameIndex))
                return VariableReading.Of(GetMonitorName(monitorNameIndex));

            if (TryGetMonitorConnectedIndex(variableName, out var monitorConnectedIndex))
                return VariableReading.Of(monitorConnectedIndex < Screen.AllScreens.Length);

            if (TryGetRefreshRateIndex(variableName, out var refreshRateIndex))
            {
                var rate = _refreshRates.GetCurrent(refreshRateIndex);
                return rate is int value ? VariableReading.Of(value) : VariableReading.Unavailable;
            }

            if (TryGetMonitorIndex(variableName, out var monitorIndex))
            {
                try
                {
                    var monitorBrightness = await _brightness.GetDdcBrightnessAsync(monitorIndex, cancellationToken);
                    return monitorBrightness is int
                        ? VariableReading.Of(monitorBrightness.Value)
                        : VariableReading.Unavailable;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Unable to read monitor brightness variable {VariableName}.", variableName);
                    return VariableReading.Unavailable;
                }
            }

            return VariableReading.Unavailable;
        }

        try
        {
            var brightness = await _brightness.GetCurrentAsync(cancellationToken);
            if (brightness is int value)
                _lastBrightness = value;
            return VariableReading.Of(brightness ?? _lastBrightness);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to read combined brightness variable.");
            return VariableReading.Of(_lastBrightness);
        }
    }

    public async ValueTask<VariableWriteResult> SetValueAsync(
        string name,
        object? value,
        CancellationToken cancellationToken = default)
    {
        var variableName = NormalizeVariableName(name);
        _logger.LogDebug("Writing ScreenXDeck variable '{RequestedName}' as '{VariableName}'.", name, variableName);
        if (DisplayVariables.TryParseMonitorBrightnessId(variableName, out var screenControlMonitorIndex))
        {
            var controlBrightness = DisplayParameters.ReadNumberValue(value);
            if (controlBrightness is null || controlBrightness < 0 || controlBrightness > 100)
                return VariableWriteResult.InvalidValue();

            return _screenControlMonitors.TrySetBrightness(screenControlMonitorIndex, (int)controlBrightness)
                ? VariableWriteResult.Applied()
                : VariableWriteResult.Unavailable("Monitor is unavailable.");
        }

        var screenControlWrite = SetScreenControlVariable(variableName, value);
        if (screenControlWrite is not null)
            return screenControlWrite;

        if (variableName.Equals(DisplayModeVariable, StringComparison.OrdinalIgnoreCase))
        {
            var mode = value?.ToString()?.Trim().ToLowerInvariant();
            if (mode is null || !DisplayModeService.IsSupportedMode(mode))
                return VariableWriteResult.InvalidValue();
            try
            {
                await _display.SetModeAsync(mode, cancellationToken);
                _displayMode = mode;
                return VariableWriteResult.Applied();
            }
            catch (Exception ex)
            {
                return VariableWriteResult.Failed(ex.Message);
            }
        }

        if (TryGetMonitorIndex(variableName, out var monitorIndex))
        {
            if (!TryReadNumeric(value, out var monitorBrightness))
                return VariableWriteResult.InvalidValue();
            try
            {
                await _brightness.SetDdcBrightnessAsync(
                    monitorIndex,
                    (int)Math.Round(Math.Clamp(monitorBrightness, 0, 100)),
                    cancellationToken);
                return VariableWriteResult.Applied();
            }
            catch (Exception ex)
            {
                return VariableWriteResult.Failed(ex.Message);
            }
        }

        if (!variableName.Equals(BrightnessVariable, StringComparison.OrdinalIgnoreCase))
            return VariableWriteResult.NotWritable();

        if (!TryReadNumeric(value, out var brightness) ||
            double.IsNaN(brightness) ||
            double.IsInfinity(brightness))
            return VariableWriteResult.InvalidValue();

        try
        {
            await _brightness.SetAsync((int)Math.Round(Math.Clamp(brightness, 0, 100)), cancellationToken);
            return VariableWriteResult.Applied();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write brightness variable.");
            return VariableWriteResult.Failed(ex.Message);
        }
    }

    private VariableReading ReadScreenControlMonitorBrightness(int index)
    {
        var monitor = _screenControlMonitors.GetMonitors().FirstOrDefault(item => item.Index == index);
        return monitor is { SupportsBrightness: true }
            ? VariableReading.Of((double)monitor.BrightnessPercent, 0, 100, 1)
            : VariableReading.Unavailable;
    }

    private VariableReading? ReadScreenControlVariable(string name) => name switch
    {
        "monitor_count" => VariableReading.Of((double)_screenControlMonitors.GetMonitors().Count),
        "primary_brightness" => ReadScreenControlPrimaryBrightness(),
        "primary_input" => ReadScreenControlPrimaryInput(),
        "focused_window_title" => ScreenControlVariableText(_screenControlWindows.GetForeground()?.Title),
        "focused_window_process" => ScreenControlVariableText(_screenControlWindows.GetForeground()?.ProcessName),
        "focused_window_topmost" => ReadScreenControlTopmost(),
        _ => null
    };

    private VariableReading ReadScreenControlPrimaryBrightness()
    {
        var monitor = _screenControlMonitors.GetMonitors().FirstOrDefault(item => item.IsPrimary);
        return monitor is { SupportsBrightness: true }
            ? VariableReading.Of((double)monitor.BrightnessPercent, 0, 100, 1)
            : VariableReading.Unavailable;
    }

    private VariableReading ReadScreenControlPrimaryInput()
    {
        var monitor = _screenControlMonitors.GetMonitors().FirstOrDefault(item => item.IsPrimary);
        var input = monitor is null ? null : _screenControlMonitors.TryGetInput(monitor.Index);
        return input is int value
            ? VariableReading.Of(value switch
            {
                0x11 => "hdmi1",
                0x12 => "hdmi2",
                0x0F => "dp1",
                0x10 => "dp2",
                0x03 => "dvi",
                _ => "unknown"
            })
            : VariableReading.Unavailable;
    }

    private VariableReading ReadScreenControlTopmost()
    {
        var window = _screenControlWindows.GetForeground();
        return window is null
            ? VariableReading.Unavailable
            : VariableReading.Of(_screenControlWindows.IsTopmost(window.Handle));
    }

    private static VariableReading ScreenControlVariableText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? VariableReading.Unavailable : VariableReading.Of(value);

    private VariableWriteResult? SetScreenControlVariable(string name, object? value)
    {
        if (name == "primary_brightness")
        {
            var brightness = DisplayParameters.ReadNumberValue(value);
            var monitor = _screenControlMonitors.GetMonitors().FirstOrDefault(item => item.IsPrimary);
            return brightness is >= 0 and <= 100 && monitor is not null &&
                _screenControlMonitors.TrySetBrightness(monitor.Index, (int)brightness)
                ? VariableWriteResult.Applied()
                : VariableWriteResult.Unavailable("Monitor is unavailable.");
        }

        if (name == "focused_window_topmost")
        {
            var topmost = value switch
            {
                bool boolean => boolean,
                string text when bool.TryParse(text, out var parsed) => parsed,
                _ => (bool?)null
            };
            var window = _screenControlWindows.GetForeground();
            return topmost is bool state && window is not null &&
                _screenControlWindows.SetTopmost(window.Handle, state)
                ? VariableWriteResult.Applied()
                : VariableWriteResult.Unavailable("Window is unavailable.");
        }

        return name is "monitor_count" or "primary_input" or "focused_window_title" or "focused_window_process"
            ? VariableWriteResult.NotWritable()
            : null;
    }

    private static bool TryReadNumeric(object? value, out double number)
    {
        number = 0;
        if (value is JsonElement element)
            return element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out number);

        if (value is IConvertible convertible)
        {
            try
            {
                number = convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch (FormatException) { }
            catch (InvalidCastException) { }
        }

        return false;
    }

    private static string NormalizeVariableName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        value = value.Replace('-', '_').ToLowerInvariant();

        if (value.EndsWith(BrightnessVariable, StringComparison.Ordinal))
            return BrightnessVariable;
        if (value.EndsWith(DisplayModeVariable, StringComparison.Ordinal))
            return DisplayModeVariable;
        if (value.Contains("screenxdeck_monitor_", StringComparison.Ordinal) &&
            value.EndsWith("_brightness", StringComparison.Ordinal))
        {
            var start = value.LastIndexOf("screenxdeck_monitor_", StringComparison.Ordinal);
            return value[start..];
        }

        var separator = value.LastIndexOfAny(['/', ':', '.', '\\']);
        return separator >= 0 ? value[(separator + 1)..] : value;
    }

    private static string GetActiveMonitorDescription()
    {
        var primary = Screen.PrimaryScreen;
        if (primary is null)
            return "No primary monitor detected";

        var bounds = primary.Bounds;
        var name = GetMonitorNames().FirstOrDefault() ?? primary.DeviceName;
        return $"{name} — Primary — {bounds.Width}x{bounds.Height} — {Screen.AllScreens.Length} monitor(s)";
    }

    private static string GetMonitorName(int index) =>
        GetMonitorNames().ElementAtOrDefault(index) ?? "Unavailable";

    private static IReadOnlyList<string> GetMonitorNames()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI",
                "SELECT UserFriendlyName FROM WmiMonitorID WHERE Active = TRUE");
            var names = new List<string>();
            foreach (ManagementObject monitor in searcher.Get())
            {
                if (monitor["UserFriendlyName"] is not ushort[] chars)
                    continue;

                var name = new string(chars.Select(character => (char)character)
                    .TakeWhile(character => character != '\0')
                    .ToArray()).Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }

            return names;
        }
        catch (ManagementException)
        {
            return [];
        }
    }

    private static bool TryGetMonitorNameIndex(string name, out int index)
    {
        const string prefix = "screenxdeck_monitor_";
        const string suffix = "_name";
        index = -1;
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var number = name[prefix.Length..^suffix.Length];
        return int.TryParse(number, out var parsed) && parsed > 0 && parsed <= MaximumMonitorVariables &&
            (index = parsed - 1) >= 0;
    }

    private static bool TryGetMonitorConnectedIndex(string name, out int index)
    {
        const string prefix = "screenxdeck_monitor_";
        const string suffix = "_connected";
        index = -1;
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var number = name[prefix.Length..^suffix.Length];
        return int.TryParse(number, out var parsed) && parsed > 0 && parsed <= MaximumMonitorVariables &&
            (index = parsed - 1) >= 0;
    }

    private static bool TryGetRefreshRateIndex(string name, out int index)
    {
        const string prefix = "screenxdeck_monitor_";
        const string suffix = "_refresh_rate";
        index = -1;
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var number = name[prefix.Length..^suffix.Length];
        return int.TryParse(number, out var parsed) && parsed > 0 && parsed <= MaximumMonitorVariables &&
            (index = parsed - 1) >= 0;
    }

    private static bool TryGetMonitorIndex(string name, out int index)
    {
        const string prefix = "screenxdeck_monitor_";
        const string suffix = "_brightness";
        index = -1;
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return false;

        var number = name[prefix.Length..^suffix.Length];
        return int.TryParse(number, out var parsed) && parsed > 0 && parsed <= MaximumMonitorVariables &&
            (index = parsed - 1) >= 0;
    }
}
