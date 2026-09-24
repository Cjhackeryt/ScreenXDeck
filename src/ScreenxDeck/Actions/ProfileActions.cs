using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class ApplyProfileAction(ProfileService profileService, ILogger logger) : IActionDefinition
{
    public string Id => "apply-profile";
    public LocalizedText Name => Strings.Actions.ApplyProfile.Name();
    public LocalizedText Description => Strings.Actions.ApplyProfile.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("profile",
        [
            new() { Value = "Gaming", Label = "Gaming" },
            new() { Value = "Work", Label = "Work" },
            new() { Value = "Movie", Label = "Movie" },
            new() { Value = "Custom", Label = "Custom" }
        ],
        label: Strings.Actions.ApplyProfile.Profile.Label(),
        defaultValue: "Work")
    ];

    public IActionExecutor CreateExecutor() => new Executor(profileService, logger);

    private sealed class Executor(ProfileService profileService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            string profile = context.Parameters.GetValueOrDefault("profile")?.ToString() ?? "Work";
            bool success = profileService.ApplyProfile(profile);
            logger.Information("ApplyProfileAction executed: Profile={Profile}, Success={Success}", profile, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("APPLY_PROFILE_FAILED", $"Failed to apply profile '{profile}'"));
        }
    }
}

public sealed class ToggleProfilesAction(ProfileService profileService, ILogger logger) : IActionDefinition
{
    public string Id => "toggle-profiles";
    public LocalizedText Name => Strings.Actions.ToggleProfiles.Name();
    public LocalizedText Description => Strings.Actions.ToggleProfiles.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("profileA",
        [
            new() { Value = "Gaming", Label = "Gaming" },
            new() { Value = "Work", Label = "Work" },
            new() { Value = "Movie", Label = "Movie" },
            new() { Value = "Custom", Label = "Custom" }
        ],
        label: Strings.Actions.ToggleProfiles.ProfileA.Label(),
        defaultValue: "Work"),

        ActionParameter.Choice("profileB",
        [
            new() { Value = "Gaming", Label = "Gaming" },
            new() { Value = "Work", Label = "Work" },
            new() { Value = "Movie", Label = "Movie" },
            new() { Value = "Custom", Label = "Custom" }
        ],
        label: Strings.Actions.ToggleProfiles.ProfileB.Label(),
        defaultValue: "Gaming")
    ];

    public IActionExecutor CreateExecutor() => new Executor(profileService, logger);

    private sealed class Executor(ProfileService profileService, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            string profileA = context.Parameters.GetValueOrDefault("profileA")?.ToString() ?? "Work";
            string profileB = context.Parameters.GetValueOrDefault("profileB")?.ToString() ?? "Gaming";

            bool success = profileService.ToggleProfiles(profileA, profileB);
            logger.Information("ToggleProfilesAction executed between {A} and {B}: Success={Success}", profileA, profileB, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("TOGGLE_PROFILES_FAILED", "Failed to toggle profiles"));
        }
    }
}
