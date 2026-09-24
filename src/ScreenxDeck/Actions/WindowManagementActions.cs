using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using ScreenxDeck.Services;
using Serilog;

namespace ScreenxDeck.Actions;

public sealed class ToggleAlwaysOnTopAction(WindowManagerService windowManager, ILogger logger) : IActionDefinition, IStateProviderActionDefinition
{
    public string Id => "toggle-always-on-top";
    public LocalizedText Name => Strings.Actions.ToggleAlwaysOnTop.Name();
    public LocalizedText Description => Strings.Actions.ToggleAlwaysOnTop.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } = [];

    public IActionExecutor CreateExecutor() => new Executor(windowManager, logger);

    public Task<ActionStateSnapshot?> GetActionStateAsync(IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        bool isTopmost = windowManager.IsForegroundWindowTopmost();

        ActionStateDefinition[] states =
        [
            new("pinned", Strings.Common.Pinned()),
            new("unpinned", Strings.Common.Unpinned())
        ];

        return Task.FromResult<ActionStateSnapshot?>(new ActionStateSnapshot(states, isTopmost ? "pinned" : "unpinned"));
    }

    private sealed class Executor(WindowManagerService windowManager, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            bool success = windowManager.ToggleAlwaysOnTopActiveWindow();
            logger.Information("ToggleAlwaysOnTopAction executed: Result={Success}", success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("PIN_TOGGLE_FAILED", "Failed to toggle Always on Top"));
        }
    }
}

public sealed class UnpinAllWindowsAction(WindowManagerService windowManager, ILogger logger) : IActionDefinition
{
    public string Id => "unpin-all-windows";
    public LocalizedText Name => Strings.Actions.UnpinAllWindows.Name();
    public LocalizedText Description => Strings.Actions.UnpinAllWindows.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } = [];

    public IActionExecutor CreateExecutor() => new Executor(windowManager, logger);

    private sealed class Executor(WindowManagerService windowManager, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            windowManager.UnpinAllWindows();
            logger.Information("UnpinAllWindowsAction executed");
            return Task.FromResult(ActionResult.Success());
        }
    }
}

public sealed class MoveWindowToDisplayAction(WindowManagerService windowManager, ILogger logger) : IActionDefinition
{
    public string Id => "move-window-to-display";
    public LocalizedText Name => Strings.Actions.MoveWindowToDisplay.Name();
    public LocalizedText Description => Strings.Actions.MoveWindowToDisplay.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.MoveWindowToDisplay.Display.Label(), defaultValue: "1")
    ];

    public IActionExecutor CreateExecutor() => new Executor(windowManager, logger);

    private sealed class Executor(WindowManagerService windowManager, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            bool success = windowManager.MoveActiveWindowToDisplay(display);
            logger.Information("MoveWindowToDisplayAction executed: Display={Display}, Result={Success}", display, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("MOVE_WINDOW_FAILED", $"Failed to move window to display {display}"));
        }
    }
}

public sealed class MoveWindowNextPreviousAction(WindowManagerService windowManager, ILogger logger) : IActionDefinition
{
    public string Id => "move-window-next-prev";
    public LocalizedText Name => Strings.Actions.MoveWindowNextPrevious.Name();
    public LocalizedText Description => Strings.Actions.MoveWindowNextPrevious.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("direction",
        [
            new() { Value = "next", Label = "Next Display" },
            new() { Value = "prev", Label = "Previous Display" }
        ],
        label: Strings.Actions.MoveWindowNextPrevious.Direction.Label(),
        defaultValue: "next")
    ];

    public IActionExecutor CreateExecutor() => new Executor(windowManager, logger);

    private sealed class Executor(WindowManagerService windowManager, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            string direction = context.Parameters.GetValueOrDefault("direction")?.ToString() ?? "next";
            bool next = string.Equals(direction, "next", StringComparison.OrdinalIgnoreCase);

            bool success = windowManager.MoveActiveWindowNextPrevious(next);
            logger.Information("MoveWindowNextPreviousAction executed: Next={Next}, Result={Success}", next, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("MOVE_NEXT_PREV_FAILED", "Failed to cycle window"));
        }
    }
}

public sealed class MoveCursorToDisplayAction(WindowManagerService windowManager, ILogger logger) : IActionDefinition
{
    public string Id => "move-cursor-to-display";
    public LocalizedText Name => Strings.Actions.MoveCursorToDisplay.Name();
    public LocalizedText Description => Strings.Actions.MoveCursorToDisplay.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.MoveCursorToDisplay.Display.Label(), defaultValue: "1")
    ];

    public IActionExecutor CreateExecutor() => new Executor(windowManager, logger);

    private sealed class Executor(WindowManagerService windowManager, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            bool success = windowManager.MoveCursorToDisplay(display);
            logger.Information("MoveCursorToDisplayAction executed: Display={Display}, Result={Success}", display, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("MOVE_CURSOR_FAILED", $"Failed to move cursor to display {display}"));
        }
    }
}

public sealed class MaximizeOnDisplayAction(WindowManagerService windowManager, ILogger logger) : IActionDefinition
{
    public string Id => "maximize-on-display";
    public LocalizedText Name => Strings.Actions.MaximizeOnDisplay.Name();
    public LocalizedText Description => Strings.Actions.MaximizeOnDisplay.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } =
    [
        ActionParameter.Choice("display", ActionHelpers.GetSingleDisplayOptions(), label: Strings.Actions.MaximizeOnDisplay.Display.Label(), defaultValue: "1")
    ];

    public IActionExecutor CreateExecutor() => new Executor(windowManager, logger);

    private sealed class Executor(WindowManagerService windowManager, ILogger logger) : IActionExecutor
    {
        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            int display = Convert.ToInt32(context.Parameters.GetValueOrDefault("display")?.ToString() ?? "1", CultureInfo.InvariantCulture);
            bool success = windowManager.MaximizeActiveWindowOnDisplay(display);
            logger.Information("MaximizeOnDisplayAction executed: Display={Display}, Result={Success}", display, success);
            return Task.FromResult(success ? ActionResult.Success() : ActionResult.Failed("MAXIMIZE_FAILED", $"Failed to maximize window on display {display}"));
        }
    }
}
