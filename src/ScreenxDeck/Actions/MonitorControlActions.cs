using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Core.Native;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class MonitorPowerAction(MonitorControlService service, ILogger logger) : IActionDefinition
{
    public string Id => "monitor-power";
    public LocalizedText Name => Strings.Actions.MonitorPower.Name();
    public LocalizedText Description => Strings.Actions.MonitorPower.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetDisplayOptions(), label: Strings.Actions.MonitorPower.Display.Label(), defaultValue: "0"),
        ActionParameter.Choice("action",
        [
            new() { Value = "off", Label = "Turn Off / Standby" },
            new() { Value = "wake", Label = "Wake Display" },
        ], label: Strings.Actions.MonitorPower.PowerAction.Label(), defaultValue: "off")
    ];

    public IActionExecutor CreateExecutor() => new Executor(service, logger);

    private sealed class Executor(MonitorControlService service, ILogger logger) : IActionExecutor
    {
        public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "0", CultureInfo.InvariantCulture);
            string action = context.Parameters.GetValueOrDefault("action")?.ToString() ?? "off";

            Task<bool> powerTask = Task.Run(() => action.Equals("wake", StringComparison.OrdinalIgnoreCase)
                ? service.WakeMonitor(display)
                : service.TurnMonitorOff(display));

            bool success;
            try
            {
                success = await powerTask.WaitAsync(TimeSpan.FromMilliseconds(250));
            }
            catch (TimeoutException)
            {
                logger.Warning("MonitorPowerAction timed out: Display={Display}, Action={Action}", display, action);
                return ActionResult.Failed("MONITOR_POWER_TIMEOUT", "Monitor power operation timed out.");
            }
            catch (Exception exception)
            {
                logger.Error(exception, "MonitorPowerAction failed: Display={Display}, Action={Action}", display, action);
                return ActionResult.Failed("MONITOR_POWER_FAILED", "Failed to change monitor power state.");
            }

            logger.Information("MonitorPowerAction executed: Display={Display}, Action={Action}, Success={Success}", display, action, success);
            return success ? ActionResult.Success() : ActionResult.Failed("MONITOR_POWER_FAILED", "Failed to change monitor power state.");
        }
    }
}

public sealed class SwitchInputSourceAction(MonitorControlService service, ILogger logger) : IActionDefinition
{
    public string Id => "switch-input-source";
    public LocalizedText Name => Strings.Actions.SwitchInputSource.Name();
    public LocalizedText Description => Strings.Actions.SwitchInputSource.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.SwitchInputSource.Display.Label(), defaultValue: "1"),
        ActionParameter.Choice("input",
        [
            new() { Value = "17", Label = "HDMI 1" },
            new() { Value = "18", Label = "HDMI 2" },
            new() { Value = "15", Label = "DisplayPort 1" },
            new() { Value = "16", Label = "DisplayPort 2" },
            new() { Value = "1", Label = "D-Sub / VGA" },
            new() { Value = "3", Label = "DVI-1" },
            new() { Value = "27", Label = "USB-C" },
        ], label: Strings.Actions.SwitchInputSource.InputSource.Label(), defaultValue: "17")
    ];

    public IActionExecutor CreateExecutor() => new Executor(service, logger);

    private sealed class Executor(MonitorControlService service, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            uint input = Convert.ToUInt32(context.Parameters.GetValueOrDefault("input")?.ToString() ?? "17", CultureInfo.InvariantCulture);

            bool success = service.SwitchInputSource(display, input);
            logger.Information("SwitchInputSourceAction executed: Display={Display}, Input={Input}, Success={Success}", display, input, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("INPUT_SWITCH_FAILED", "Failed to switch monitor input (ensure DDC/CI is supported and enabled in monitor OSD)."));
        }
    }
}

public sealed class ToggleInputSourceAction(MonitorControlService service, ILogger logger) : IActionDefinition
{
    public string Id => "toggle-input-source";
    public LocalizedText Name => Strings.Actions.ToggleInputSource.Name();
    public LocalizedText Description => Strings.Actions.ToggleInputSource.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.ToggleInputSource.Display.Label(), defaultValue: "1"),
        ActionParameter.Choice("sourceA",
        [
            new() { Value = "17", Label = "HDMI 1" },
            new() { Value = "18", Label = "HDMI 2" },
            new() { Value = "15", Label = "DisplayPort 1" },
            new() { Value = "16", Label = "DisplayPort 2" },
            new() { Value = "27", Label = "USB-C" },
        ], label: Strings.Actions.ToggleInputSource.SourceA.Label(), defaultValue: "17"),
        ActionParameter.Choice("sourceB",
        [
            new() { Value = "15", Label = "DisplayPort 1" },
            new() { Value = "17", Label = "HDMI 1" },
            new() { Value = "18", Label = "HDMI 2" },
            new() { Value = "16", Label = "DisplayPort 2" },
            new() { Value = "27", Label = "USB-C" },
        ], label: Strings.Actions.ToggleInputSource.SourceB.Label(), defaultValue: "15")
    ];

    public IActionExecutor CreateExecutor() => new Executor(service, logger);

    private sealed class Executor(MonitorControlService service, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            uint sourceA = Convert.ToUInt32(context.Parameters.GetValueOrDefault("sourceA")?.ToString() ?? "17", CultureInfo.InvariantCulture);
            uint sourceB = Convert.ToUInt32(context.Parameters.GetValueOrDefault("sourceB")?.ToString() ?? "15", CultureInfo.InvariantCulture);

            bool success = service.ToggleInputSource(display, sourceA, sourceB);
            logger.Information("ToggleInputSourceAction executed: Display={Display}, A={A}, B={B}, Success={Success}", display, sourceA, sourceB, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("TOGGLE_INPUT_FAILED", "Failed to toggle input source"));
        }
    }
}
