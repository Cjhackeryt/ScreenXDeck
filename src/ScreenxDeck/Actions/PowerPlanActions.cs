using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class SetPowerPlanAction(PowerPlanService powerPlanService, ILogger logger) : IActionDefinition
{
    public string Id => "set-power-plan";
    public LocalizedText Name => Strings.Actions.SetPowerPlan.Name();
    public LocalizedText Description => Strings.Actions.SetPowerPlan.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } = CreateParameters(powerPlanService);

    private static IReadOnlyList<ActionParameter> CreateParameters(PowerPlanService powerPlanService)
    {
        IReadOnlyList<PowerPlan> plans = powerPlanService.GetAvailablePowerPlans();
        string? defaultPlan = (plans.FirstOrDefault(plan => plan.IsActive) ?? plans.FirstOrDefault())?.Guid;
        var options = plans
            .Select(plan => new ActionParameterOption { Value = plan.Guid, Label = plan.Name })
            .ToArray();

        return
        [
            ActionParameter.Choice(
                "planGuid",
                options,
                label: Strings.Actions.SetPowerPlan.PlanName.Label(),
                defaultValue: defaultPlan,
                required: true)
        ];
    }

    public IActionExecutor CreateExecutor() => new Executor(powerPlanService, logger);

    private sealed class Executor(PowerPlanService powerPlanService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            string planGuid = context.Parameters.GetValueOrDefault("planGuid")?.ToString() ?? string.Empty;
            bool success = powerPlanService.SetActivePowerPlan(planGuid);
            logger.Information("SetPowerPlan executed: PlanGuid={PlanGuid}, Success={Success}", planGuid, success);
            return Task.FromResult(success
                ? ActionResult.Success()
                : ActionResult.Failed("SET_POWER_PLAN_FAILED", "Failed to activate the requested Windows power plan."));
        }
    }
}
