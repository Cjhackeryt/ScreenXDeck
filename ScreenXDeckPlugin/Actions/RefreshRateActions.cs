using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenControl;

namespace ScreenXDeckPlugin.Actions;

internal static class RefreshRateParameters
{
    public static ActionParameter Monitor() =>
        ActionParameter.Slider("monitor", 1, 8, Strings.Params.RefreshRateMonitor.Label(), Strings.Params.RefreshRateMonitor.Description(), 1, 1);
}

public sealed class RefreshRateStepAction(
    string id,
    LocalizedText name,
    LocalizedText description,
    int direction,
    RefreshRateService refreshRates) : IActionDefinition
{
    public string Id => id;
    public LocalizedText Name => name;
    public LocalizedText Description => description;
    public IReadOnlyList<ActionParameter> Parameters { get; } = [RefreshRateParameters.Monitor()];

    public IActionExecutor CreateExecutor() => new Executor(refreshRates, direction);

    private sealed class Executor(RefreshRateService refreshRates, int direction) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            try
            {
                var monitor = ReadMonitor(context) - 1;
                refreshRates.Step(monitor, direction);
                return ActionResult.SucceededTask;
            }
            catch (Exception ex)
            {
                return Task.FromResult(ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.RefreshRateFailed(ex.Message)));
            }
        }
    }

    private static int ReadMonitor(ActionExecutionContext context) =>
        int.TryParse(context.Parameters["monitor"]?.ToString(), out var value) ? value : 1;
}

public sealed class SetRefreshRateAction(RefreshRateService refreshRates) : IActionDefinition
{
    public string Id => "set-refresh-rate";
    public LocalizedText Name => Strings.Actions.SetRefreshRate.Name();
    public LocalizedText Description => Strings.Actions.SetRefreshRate.Description();
    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        RefreshRateParameters.Monitor(),
        ActionParameter.Slider(
            "refreshRate",
            24,
            360,
            Strings.Actions.SetRefreshRate.Rate.Label(),
            Strings.Actions.SetRefreshRate.Rate.Description(),
            1,
            60)
    ];

    public IActionExecutor CreateExecutor() => new Executor(refreshRates);

    private sealed class Executor(RefreshRateService refreshRates) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            try
            {
                var monitor = Read(context, "monitor", 1) - 1;
                var rate = Read(context, "refreshRate", 60);
                refreshRates.Set(monitor, rate);
                return ActionResult.SucceededTask;
            }
            catch (Exception ex)
            {
                return Task.FromResult(ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.RefreshRateFailed(ex.Message)));
            }
        }

        private static int Read(ActionExecutionContext context, string name, int fallback) =>
            context.Parameters.TryGetValue(name, out var value) &&
            int.TryParse(value?.ToString(), out var parsed) ? parsed : fallback;
    }
}

