using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class SetPrimaryMonitorAction(TopologyService topologyService, ILogger logger) : IActionDefinition
{
    public string Id => "set-primary-monitor";
    public LocalizedText Name => Strings.Actions.SetPrimaryMonitor.Name();
    public LocalizedText Description => Strings.Actions.SetPrimaryMonitor.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.SetPrimaryMonitor.Display.Label(), defaultValue: "1")
    ];

    public IActionExecutor CreateExecutor() => new Executor(topologyService, logger);

    private sealed class Executor(TopologyService topologyService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            bool success = topologyService.SetPrimaryMonitor(display);
            logger.Information("SetPrimaryMonitor executed for Display {Display}, Success={Success}", display, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("PRIMARY_FAILED", $"Failed to set Display {display} as primary."));
        }
    }
}
