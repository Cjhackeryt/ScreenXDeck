using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Core.Models;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class ToggleBrightnessAction(BrightnessService brightnessService, ILogger logger) : IActionDefinition
{
    public string Id => "toggle-brightness";
    public LocalizedText Name => Strings.Actions.ToggleBrightness.Name();
    public LocalizedText Description => Strings.Actions.ToggleBrightness.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetDisplayOptions(), label: Strings.Actions.ToggleBrightness.Display.Label(), defaultValue: "0"),
        ActionParameter.Slider("levelA", 0, 100, label: Strings.Actions.ToggleBrightness.LevelA.Label(), defaultValue: 20),
        ActionParameter.Slider("levelB", 0, 100, label: Strings.Actions.ToggleBrightness.LevelB.Label(), defaultValue: 80)
    ];

    public IActionExecutor CreateExecutor() => new Executor(brightnessService, logger);

    private sealed class Executor(BrightnessService brightnessService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "0", CultureInfo.InvariantCulture);
            int levelA = Convert.ToInt32(context.Parameters.GetValueOrDefault("levelA")?.ToString() ?? "20", CultureInfo.InvariantCulture);
            int levelB = Convert.ToInt32(context.Parameters.GetValueOrDefault("levelB")?.ToString() ?? "80", CultureInfo.InvariantCulture);

            bool success = brightnessService.ToggleBrightness(display, levelA, levelB);
            logger.Information("ToggleBrightness executed: Display={Display}, LevelA={A}%, LevelB={B}%, Result={Result}", display, levelA, levelB, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("TOGGLE_BRIGHTNESS_FAILED", "Failed to toggle brightness"));
        }
    }
}

public sealed class ToggleRefreshRateAction(ResolutionRefreshService resService, ILogger logger) : IActionDefinition
{
    public string Id => "toggle-refresh-rate";
    public LocalizedText Name => Strings.Actions.ToggleRefreshRate.Name();
    public LocalizedText Description => Strings.Actions.ToggleRefreshRate.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.ToggleRefreshRate.Display.Label(), defaultValue: "1"),
        ActionParameter.Choice("rateA",
        [
            new() { Value = "60", Label = "60 Hz" },
            new() { Value = "75", Label = "75 Hz" },
            new() { Value = "120", Label = "120 Hz" },
            new() { Value = "144", Label = "144 Hz" },
            new() { Value = "165", Label = "165 Hz" },
            new() { Value = "240", Label = "240 Hz" }
        ],
        label: Strings.Actions.ToggleRefreshRate.RateA.Label(),
        defaultValue: "60"),

        ActionParameter.Choice("rateB",
        [
            new() { Value = "144", Label = "144 Hz" },
            new() { Value = "60", Label = "60 Hz" },
            new() { Value = "120", Label = "120 Hz" },
            new() { Value = "165", Label = "165 Hz" },
            new() { Value = "240", Label = "240 Hz" }
        ],
        label: Strings.Actions.ToggleRefreshRate.RateB.Label(),
        defaultValue: "144")
    ];

    public IActionExecutor CreateExecutor() => new Executor(resService, logger);

    private sealed class Executor(ResolutionRefreshService resService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            int rateA = Convert.ToInt32(context.Parameters.GetValueOrDefault("rateA")?.ToString() ?? "60", CultureInfo.InvariantCulture);
            int rateB = Convert.ToInt32(context.Parameters.GetValueOrDefault("rateB")?.ToString() ?? "144", CultureInfo.InvariantCulture);

            bool success = resService.ToggleRefreshRate(display, rateA, rateB);
            logger.Information("ToggleRefreshRate executed: Display={Display}, RateA={RateA}Hz, RateB={RateB}Hz, Result={Result}", display, rateA, rateB, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("TOGGLE_RATE_FAILED", "Failed to toggle refresh rate"));
        }
    }
}

public sealed class ToggleResolutionAction(ResolutionRefreshService resService, ILogger logger) : IActionDefinition
{
    public string Id => "toggle-resolution";
    public LocalizedText Name => Strings.Actions.ToggleResolution.Name();
    public LocalizedText Description => Strings.Actions.ToggleResolution.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.ToggleResolution.Display.Label(), defaultValue: "1"),
        ActionParameter.Choice("resA",
        [
            new() { Value = "1920x1080", Label = "1920 x 1080 (FHD)" },
            new() { Value = "2560x1440", Label = "2560 x 1440 (2K QHD)" },
            new() { Value = "3840x2160", Label = "3840 x 2160 (4K UHD)" },
            new() { Value = "1366x768", Label = "1366 x 768 (HD)" },
            new() { Value = "1280x720", Label = "1280 x 720 (720p)" }
        ],
        label: Strings.Actions.ToggleResolution.ResA.Label(),
        defaultValue: "1920x1080"),

        ActionParameter.Choice("resB",
        [
            new() { Value = "2560x1440", Label = "2560 x 1440 (2K QHD)" },
            new() { Value = "1920x1080", Label = "1920 x 1080 (FHD)" },
            new() { Value = "3840x2160", Label = "3840 x 2160 (4K UHD)" },
            new() { Value = "1366x768", Label = "1366 x 768 (HD)" },
            new() { Value = "1280x720", Label = "1280 x 720 (720p)" }
        ],
        label: Strings.Actions.ToggleResolution.ResB.Label(),
        defaultValue: "2560x1440")
    ];

    public IActionExecutor CreateExecutor() => new Executor(resService, logger);

    private sealed class Executor(ResolutionRefreshService resService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            string resAStr = context.Parameters.GetValueOrDefault("resA")?.ToString() ?? "1920x1080";
            string resBStr = context.Parameters.GetValueOrDefault("resB")?.ToString() ?? "2560x1440";

            var partsA = resAStr.Split('x');
            var partsB = resBStr.Split('x');
            if (partsA.Length == 2 && partsB.Length == 2 &&
                int.TryParse(partsA[0], out int wA) && int.TryParse(partsA[1], out int hA) &&
                int.TryParse(partsB[0], out int wB) && int.TryParse(partsB[1], out int hB))
            {
                var resA = new DisplayResolution(wA, hA);
                var resB = new DisplayResolution(wB, hB);
                bool success = resService.ToggleResolution(display, resA, resB);
                logger.Information("ToggleResolution executed: Display={Display}, ResA={A}, ResB={B}, Result={Result}", display, resAStr, resBStr, success);
                return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("TOGGLE_RES_FAILED", "Failed to toggle resolution"));
            }

            return Task.FromResult(ActionResult.Failed("INVALID_RES", "Invalid resolution parameters"));
        }
    }
}
