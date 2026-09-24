using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Core.Native;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class SetDisplayModeAction(TopologyService topologyService, ILogger logger) : IActionDefinition, IStateProviderActionDefinition
{
    public string Id => "set-display-mode";
    public LocalizedText Name => Strings.Actions.SetDisplayMode.Name();
    public LocalizedText Description => Strings.Actions.SetDisplayMode.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("mode",
        [
            new() { Value = "Extend", Label = "Extend" },
            new() { Value = "Clone", Label = "Duplicate (Clone)" },
            new() { Value = "Internal", Label = "PC Screen Only" },
            new() { Value = "External", Label = "Second Screen Only" },
        ], label: Strings.Actions.SetDisplayMode.Mode.Label(), defaultValue: "Extend")
    ];

    public IActionExecutor CreateExecutor() => new Executor(topologyService, logger);

    public Task<ActionStateSnapshot?> GetActionStateAsync(IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        string configuredMode = parameters.GetValueOrDefault("mode")?.ToString() ?? "Extend";
        bool isActive = string.Equals(topologyService.CurrentTopology.ToString(), configuredMode, StringComparison.OrdinalIgnoreCase);

        ActionStateDefinition[] states =
        [
            new("active", "Active"),
            new("inactive", "Inactive")
        ];

        return Task.FromResult<ActionStateSnapshot?>(new ActionStateSnapshot(states, isActive ? "active" : "inactive"));
    }

    private sealed class Executor(TopologyService topologyService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            string modeStr = context.Parameters.GetValueOrDefault("mode")?.ToString() ?? "Extend";
            if (!Enum.TryParse<CcdApi.DisplayTopology>(modeStr, true, out var topology))
            {
                return Task.FromResult(ActionResult.Failed("UNKNOWN_MODE", $"Unknown display mode: {modeStr}"));
            }

            bool success = topologyService.SetDisplayMode(topology);
            logger.Information("SetDisplayMode executed: Mode={Mode}, Success={Success}", modeStr, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("DISPLAY_MODE_FAILED", "Failed to change Windows display mode."));
        }
    }
}

public sealed class ToggleDuplicateExtendAction(TopologyService topologyService, ILogger logger) : IActionDefinition, IStateProviderActionDefinition
{
    public string Id => "toggle-duplicate-extend";
    public LocalizedText Name => Strings.Actions.ToggleDuplicateExtend.Name();
    public LocalizedText Description => Strings.Actions.ToggleDuplicateExtend.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } = [];

    public IActionExecutor CreateExecutor() => new Executor(topologyService, logger);

    public Task<ActionStateSnapshot?> GetActionStateAsync(IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        bool isExtend = topologyService.CurrentTopology == CcdApi.DisplayTopology.Extend;

        ActionStateDefinition[] states =
        [
            new("extend", "Extend"),
            new("duplicate", "Duplicate")
        ];

        return Task.FromResult<ActionStateSnapshot?>(new ActionStateSnapshot(states, isExtend ? "extend" : "duplicate"));
    }

    private sealed class Executor(TopologyService topologyService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            bool success = topologyService.ToggleDuplicateExtend();
            logger.Information("ToggleDuplicateExtend executed, now: {Mode}", topologyService.CurrentTopology);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("TOGGLE_MODE_FAILED", "Failed to toggle display mode."));
        }
    }
}
