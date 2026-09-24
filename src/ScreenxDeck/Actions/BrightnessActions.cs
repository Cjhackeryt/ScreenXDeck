using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public static class ActionHelpers
{
    public static List<ActionParameterOption> GetDisplayOptions() =>
    [
        new() { Value = "0", Label = Strings.Common.AllDisplays() },
        new() { Value = "1", Label = Strings.Common.Display1() },
        new() { Value = "2", Label = Strings.Common.Display2() },
        new() { Value = "3", Label = Strings.Common.Display3() },
        new() { Value = "4", Label = Strings.Common.Display4() },
    ];

    public static List<ActionParameterOption> GetSingleDisplayOptions() =>
    [
        new() { Value = "1", Label = Strings.Common.Display1() },
        new() { Value = "2", Label = Strings.Common.Display2() },
        new() { Value = "3", Label = Strings.Common.Display3() },
        new() { Value = "4", Label = Strings.Common.Display4() },
    ];
}

public sealed class SetBrightnessAction(BrightnessService brightnessService, ILogger logger) : IActionDefinition
{
    public string Id => "set-brightness";
    public LocalizedText Name => Strings.Actions.SetBrightness.Name();
    public LocalizedText Description => Strings.Actions.SetBrightness.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetDisplayOptions(), label: Strings.Actions.SetBrightness.Display.Label(), defaultValue: "0"),
        ActionParameter.Slider("brightness", 0, 100, label: Strings.Actions.SetBrightness.Brightness.Label(), defaultValue: 100)
    ];

    public IActionExecutor CreateExecutor() => new Executor(brightnessService, logger);

    private sealed class Executor(BrightnessService brightnessService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "0", CultureInfo.InvariantCulture);
            int brightness = Convert.ToInt32(context.Parameters.GetValueOrDefault("brightness")?.ToString() ?? "100", CultureInfo.InvariantCulture);

            bool success = brightnessService.SetBrightness(display, brightness);
            logger.Information("SetBrightnessAction executed: Display={Display}, Brightness={Brightness}%, Result={Result}", display, brightness, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("BRIGHTNESS_SET_FAILED", "Failed to set brightness"));
        }
    }
}

public sealed class AdjustBrightnessAction(BrightnessService brightnessService, ILogger logger) : IActionDefinition
{
    public string Id => "adjust-brightness";
    public LocalizedText Name => Strings.Actions.AdjustBrightness.Name();
    public LocalizedText Description => Strings.Actions.AdjustBrightness.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetDisplayOptions(), label: Strings.Actions.AdjustBrightness.Display.Label(), defaultValue: "0"),
        ActionParameter.Number("delta", label: Strings.Actions.AdjustBrightness.Delta.Label(), defaultValue: 10, min: -100, max: 100)
    ];

    public IActionExecutor CreateExecutor() => new Executor(brightnessService, logger);

    private sealed class Executor(BrightnessService brightnessService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "0", CultureInfo.InvariantCulture);
            int delta = Convert.ToInt32(context.Parameters.GetValueOrDefault("delta")?.ToString() ?? "10", CultureInfo.InvariantCulture);

            bool success = brightnessService.AdjustBrightness(display, delta);
            logger.Information("AdjustBrightnessAction executed: Display={Display}, Delta={Delta}%, Result={Result}", display, delta, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("BRIGHTNESS_ADJUST_FAILED", "Failed to adjust brightness"));
        }
    }
}

public sealed class BrightnessPresetAction(BrightnessService brightnessService, ILogger logger) : IActionDefinition
{
    public string Id => "brightness-preset";
    public LocalizedText Name => Strings.Actions.BrightnessPreset.Name();
    public LocalizedText Description => Strings.Actions.BrightnessPreset.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetDisplayOptions(), label: Strings.Actions.BrightnessPreset.Display.Label(), defaultValue: "0"),
        ActionParameter.Choice("preset",
        [
            new() { Value = "0", Label = "0%" },
            new() { Value = "25", Label = "25%" },
            new() { Value = "50", Label = "50%" },
            new() { Value = "75", Label = "75%" },
            new() { Value = "100", Label = "100%" }
        ], label: Strings.Actions.BrightnessPreset.Preset.Label(), defaultValue: "50")
    ];

    public IActionExecutor CreateExecutor() => new Executor(brightnessService, logger);

    private sealed class Executor(BrightnessService brightnessService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "0", CultureInfo.InvariantCulture);
            int target = Convert.ToInt32(context.Parameters.GetValueOrDefault("preset")?.ToString() ?? "50", CultureInfo.InvariantCulture);

            bool success = brightnessService.SetBrightness(display, target);
            logger.Information("BrightnessPresetAction executed: Display={Display}, Preset={Preset}%, Result={Result}", display, target, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("BRIGHTNESS_PRESET_FAILED", "Failed to set brightness preset"));
        }
    }
}
