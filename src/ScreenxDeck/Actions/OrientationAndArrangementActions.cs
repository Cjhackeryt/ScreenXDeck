using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class SetOrientationAction(TopologyService topologyService, ILogger logger) : IActionDefinition
{
    public string Id => "set-orientation";
    public LocalizedText Name => Strings.Actions.SetOrientation.Name();
    public LocalizedText Description => Strings.Actions.SetOrientation.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.SetOrientation.Display.Label(), defaultValue: "1"),
        ActionParameter.Choice("orientation",
        [
            new() { Value = "0", Label = "Landscape (Standard)" },
            new() { Value = "1", Label = "Portrait (90°)" },
            new() { Value = "2", Label = "Landscape Flipped (180°)" },
            new() { Value = "3", Label = "Portrait Flipped (270°)" },
        ], label: Strings.Actions.SetOrientation.Orientation.Label(), defaultValue: "0")
    ];

    public IActionExecutor CreateExecutor() => new Executor(topologyService, logger);

    private sealed class Executor(TopologyService topologyService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            uint orientation = Convert.ToUInt32(context.Parameters.GetValueOrDefault("orientation")?.ToString() ?? "0", CultureInfo.InvariantCulture);

            bool success = topologyService.SetOrientation(display, orientation);
            logger.Information("SetOrientation executed: Display={Display}, Orientation={Orientation}, Success={Success}", display, orientation, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("ORIENTATION_FAILED", "Failed to rotate monitor orientation."));
        }
    }
}

public sealed class SaveArrangementAction(TopologyService topologyService, ILogger logger) : IActionDefinition
{
    public string Id => "save-arrangement";
    public LocalizedText Name => Strings.Actions.SaveArrangement.Name();
    public LocalizedText Description => Strings.Actions.SaveArrangement.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text("name", label: Strings.Actions.SaveArrangement.ArrangementName.Label(), defaultValue: "Default Arrangement", required: true)
    ];

    public IActionExecutor CreateExecutor() => new Executor(topologyService, logger);

    private sealed class Executor(TopologyService topologyService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            string name = context.Parameters.GetValueOrDefault("name")?.ToString() ?? "Default Arrangement";
            bool success = topologyService.SaveCurrentArrangement(name);
            logger.Information("SaveArrangement executed: Name={Name}, Success={Success}", name, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("SAVE_ARRANGEMENT_FAILED", "Failed to save monitor arrangement."));
        }
    }
}

public sealed class RestoreArrangementAction(TopologyService topologyService, ILogger logger) : IActionDefinition
{
    public string Id => "restore-arrangement";
    public LocalizedText Name => Strings.Actions.RestoreArrangement.Name();
    public LocalizedText Description => Strings.Actions.RestoreArrangement.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Text("name", label: Strings.Actions.RestoreArrangement.ArrangementName.Label(), defaultValue: "Default Arrangement", required: true)
    ];

    public IActionExecutor CreateExecutor() => new Executor(topologyService, logger);

    private sealed class Executor(TopologyService topologyService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            string name = context.Parameters.GetValueOrDefault("name")?.ToString() ?? "Default Arrangement";
            bool success = topologyService.RestoreArrangement(name);
            logger.Information("RestoreArrangement executed: Name={Name}, Success={Success}", name, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("RESTORE_ARRANGEMENT_FAILED", $"Failed to restore arrangement '{name}'."));
        }
    }
}
