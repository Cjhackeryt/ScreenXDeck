using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenControl;

namespace ScreenXDeckPlugin.Actions;

public sealed class DisplayModeAction(
    string id,
    LocalizedText name,
    LocalizedText description,
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
                return ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.Cancelled());
            }
            catch (Exception ex)
            {
                return ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.DisplayModeFailed(ex.Message));
            }
        }
    }
}

public sealed class BrightnessStepAction(
    string id,
    LocalizedText name,
    LocalizedText description,
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
                return ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.Cancelled());
            }
            catch (Exception ex)
            {
                return ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.BrightnessFailed(ex.Message));
            }
        }
    }
}

public sealed class SetBrightnessAction(MonitorBrightnessService brightness) : IActionDefinition
{
    public string Id => "set-brightness";
    public LocalizedText Name => Strings.Actions.SetBrightness.Name();
    public LocalizedText Description => Strings.Actions.SetBrightness.Description();
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Slider(
            name: "brightness",
            min: 0,
            max: 100,
            label: Strings.Actions.SetBrightness.Brightness.Label(),
            description: Strings.Actions.SetBrightness.Brightness.Description(),
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
                return ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.Cancelled());
            }
            catch (Exception ex)
            {
                return ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.BrightnessFailed(ex.Message));
            }
        }
    }
}

