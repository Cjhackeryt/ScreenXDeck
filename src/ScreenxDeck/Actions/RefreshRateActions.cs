using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class SetRefreshRateAction(ResolutionRefreshService service, ILogger logger) : IActionDefinition
{
    public string Id => "set-refresh-rate";
    public LocalizedText Name => Strings.Actions.SetRefreshRate.Name();
    public LocalizedText Description => Strings.Actions.SetRefreshRate.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.SetRefreshRate.Display.Label(), defaultValue: "1"),
        ActionParameter.Choice("hz",
        [
            new() { Value = "60", Label = "60 Hz" },
            new() { Value = "75", Label = "75 Hz" },
            new() { Value = "120", Label = "120 Hz" },
            new() { Value = "144", Label = "144 Hz" },
            new() { Value = "165", Label = "165 Hz" },
            new() { Value = "240", Label = "240 Hz" },
            new() { Value = "360", Label = "360 Hz" },
        ], label: Strings.Actions.SetRefreshRate.Hz.Label(), defaultValue: "60")
    ];

    public IActionExecutor CreateExecutor() => new Executor(service, logger);

    private sealed class Executor(ResolutionRefreshService service, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            int hz = Convert.ToInt32(context.Parameters.GetValueOrDefault("hz")?.ToString() ?? "60", CultureInfo.InvariantCulture);

            bool success = service.SetRefreshRate(display, hz);
            logger.Information("SetRefreshRate executed: Display={Display}, Hz={Hz}, Success={Success}", display, hz, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("REFRESH_RATE_FAILED", $"Requested refresh rate {hz}Hz is not supported or failed to apply."));
        }
    }
}

public sealed class RestoreRefreshRateAction(ResolutionRefreshService service, ILogger logger) : IActionDefinition
{
    public string Id => "restore-refresh-rate";
    public LocalizedText Name => Strings.Actions.RestoreRefreshRate.Name();
    public LocalizedText Description => Strings.Actions.RestoreRefreshRate.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.RestoreRefreshRate.Display.Label(), defaultValue: "1")
    ];

    public IActionExecutor CreateExecutor() => new Executor(service, logger);

    private sealed class Executor(ResolutionRefreshService service, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            bool success = service.RestorePreviousSettings(display);
            logger.Information("RestoreRefreshRateAction executed: Display={Display}, Result={Result}", display, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("RESTORE_FAILED", "No previous refresh rate saved to restore."));
        }
    }
}
