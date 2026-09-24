using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class SetResolutionAction(ResolutionRefreshService service, ILogger logger) : IActionDefinition
{
    public string Id => "set-resolution";
    public LocalizedText Name => Strings.Actions.SetResolution.Name();
    public LocalizedText Description => Strings.Actions.SetResolution.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.SetResolution.Display.Label(), defaultValue: "1"),
        ActionParameter.Choice("resolution",
        [
            new() { Value = "1920x1080", Label = "1920 x 1080 (FHD)" },
            new() { Value = "2560x1440", Label = "2560 x 1440 (2K QHD)" },
            new() { Value = "3840x2160", Label = "3840 x 2160 (4K UHD)" },
            new() { Value = "2560x1080", Label = "2560 x 1080 (Ultrawide FHD)" },
            new() { Value = "3440x1440", Label = "3440 x 1440 (Ultrawide QHD)" },
            new() { Value = "1280x720", Label = "1280 x 720 (HD)" },
            new() { Value = "1600x900", Label = "1600 x 900" },
            new() { Value = "1366x768", Label = "1366 x 768" },
        ], label: Strings.Actions.SetResolution.Resolution.Label(), defaultValue: "1920x1080")
    ];

    public IActionExecutor CreateExecutor() => new Executor(service, logger);

    private sealed class Executor(ResolutionRefreshService service, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            string resStr = context.Parameters.GetValueOrDefault("resolution")?.ToString() ?? "1920x1080";

            var parts = resStr.Split('x', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out int width) || !int.TryParse(parts[1], out int height))
            {
                return Task.FromResult(ActionResult.Failed("INVALID_RESOLUTION", $"Invalid resolution format: '{resStr}'. Expected 'WIDTHxHEIGHT'."));
            }

            bool success = service.SetResolution(display, width, height);
            logger.Information("SetResolution executed: Display={Display}, Resolution={W}x{H}, Success={Success}", display, width, height, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("RESOLUTION_FAILED", $"Requested resolution {width}x{height} is not supported or failed to apply."));
        }
    }
}

public sealed class RestoreResolutionAction(ResolutionRefreshService service, ILogger logger) : IActionDefinition
{
    public string Id => "restore-resolution";
    public LocalizedText Name => Strings.Actions.RestoreResolution.Name();
    public LocalizedText Description => Strings.Actions.RestoreResolution.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.RestoreResolution.Display.Label(), defaultValue: "1")
    ];

    public IActionExecutor CreateExecutor() => new Executor(service, logger);

    private sealed class Executor(ResolutionRefreshService service, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            bool success = service.RestorePreviousSettings(display);
            logger.Information("RestoreResolutionAction executed: Display={Display}, Result={Result}", display, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("RESTORE_FAILED", "No previous resolution saved to restore."));
        }
    }
}
