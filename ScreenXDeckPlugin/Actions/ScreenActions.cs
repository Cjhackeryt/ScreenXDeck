using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;

namespace ScreenXDeckPlugin.Actions;

public sealed class DisplayModeAction(
    string id,
    string name,
    string description,
    string mode,
    DisplayModeService display) : IActionDefinition
{
    public string Id => id;
    public LocalizedText Name => name;
    public LocalizedText Description => description;
    public IReadOnlyList<ActionParameter> Parameters { get; } = [];

    public IActionExecutor CreateExecutor() => new Executor(display, mode);

    private sealed class Executor(DisplayModeService display, string mode) : IActionExecutor
    {
        public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            try
            {
                await display.SetModeAsync(mode, context.CancellationToken);
                return ActionResult.Success();
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                return ActionResult.Failed("cancelled", "The display mode change was cancelled.");
            }
            catch (Exception ex)
            {
                return ActionResult.Failed("display_mode_failed", ex.Message);
            }
        }
    }
}

public sealed class BrightnessStepAction(
    string id,
    string name,
    string description,
    int delta,
    MonitorBrightnessService brightness) : IActionDefinition
{
    public string Id => id;
    public LocalizedText Name => name;
    public LocalizedText Description => description;
    public IReadOnlyList<ActionParameter> Parameters { get; } = [];

    public IActionExecutor CreateExecutor() => new Executor(brightness, delta);

    private sealed class Executor(MonitorBrightnessService brightness, int delta) : IActionExecutor
    {
        public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            try
            {
                await brightness.AdjustAsync(delta, context.CancellationToken);
                return ActionResult.Success();
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                return ActionResult.Failed("cancelled", "The brightness change was cancelled.");
            }
            catch (Exception ex)
            {
                return ActionResult.Failed("brightness_failed", ex.Message);
            }
        }
    }
}

public sealed class SetBrightnessAction(MonitorBrightnessService brightness) : IActionDefinition
{
    public string Id => "set-brightness";
    public LocalizedText Name => "Set Brightness";
    public LocalizedText Description => "Set monitor brightness to a specific level.";
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Slider(
            name: "brightness",
            min: 0,
            max: 100,
            label: "Brightness (%)",
            description: "Brightness level from 0 to 100.",
            step: 1,
            defaultValue: 50)
    ];

    public IActionExecutor CreateExecutor() => new Executor(brightness);

    private sealed class Executor(MonitorBrightnessService brightness) : IActionExecutor
    {
        public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            try
            {
                var value = context.Parameters.TryGetValue("brightness", out var raw) &&
                    int.TryParse(raw?.ToString(), out var parsed) ? parsed : 50;
                await brightness.SetAsync(Math.Clamp(value, 0, 100), context.CancellationToken);
                return ActionResult.Success();
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                return ActionResult.Failed("cancelled", "The brightness change was cancelled.");
            }
            catch (Exception ex)
            {
                return ActionResult.Failed("brightness_failed", ex.Message);
            }
        }
    }
}
