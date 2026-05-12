using System.Text.Json;
using FlaUI.Core.Definitions;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

public class WindowStateTool : ToolBase
{
    private static readonly string[] ValidActions = ["maximize", "minimize", "restore", "move", "resize"];

    private readonly SessionManager _session;

    public WindowStateTool(SessionManager session)
    {
        _session = session;
    }

    public override string Name => "windows_window_state";

    public override string Description =>
        "Control a window's visual state or geometry. " +
        "Use action='maximize'|'minimize'|'restore' to change visibility state via WindowPattern. " +
        "Use action='move' with x/y to reposition, or action='resize' with width/height to resize via TransformPattern. " +
        "Omit handle to target the currently focused window (a new handle is registered and returned).";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            handle = new
            {
                type = "string",
                description = "Window handle (e.g. 'w1'). Omit to target the currently focused window."
            },
            action = new
            {
                type = "string",
                @enum = ValidActions,
                description = "Action to perform: maximize, minimize, restore, move, or resize."
            },
            x = new
            {
                type = "integer",
                description = "Left coordinate in screen pixels. Required when action='move'."
            },
            y = new
            {
                type = "integer",
                description = "Top coordinate in screen pixels. Required when action='move'."
            },
            width = new
            {
                type = "integer",
                description = "Width in pixels. Required when action='resize'."
            },
            height = new
            {
                type = "integer",
                description = "Height in pixels. Required when action='resize'."
            }
        },
        required = new[] { "action" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var action = GetStringArgument(arguments, "action");
        if (string.IsNullOrEmpty(action))
            return Task.FromResult(ErrorResult(
                $"Missing required argument: action. Valid values: {string.Join(", ", ValidActions)}."));

        if (!ValidActions.Contains(action))
            return Task.FromResult(ErrorResult(
                $"Invalid action '{action}'. Valid values: {string.Join(", ", ValidActions)}."));

        if (action == "move")
        {
            if (!arguments.HasValue || !arguments.Value.TryGetProperty("x", out _))
                return Task.FromResult(ErrorResult("Missing required argument: x (required when action='move')."));
            if (!arguments.Value.TryGetProperty("y", out _))
                return Task.FromResult(ErrorResult("Missing required argument: y (required when action='move')."));
        }

        if (action == "resize")
        {
            if (!arguments.HasValue || !arguments.Value.TryGetProperty("width", out _))
                return Task.FromResult(ErrorResult("Missing required argument: width (required when action='resize')."));
            if (!arguments.Value.TryGetProperty("height", out _))
                return Task.FromResult(ErrorResult("Missing required argument: height (required when action='resize')."));
        }

        var handle = GetStringArgument(arguments, "handle");

        try
        {
            var resolution = WindowResolver.ResolveOrFocused(_session, handle, registerFocused: true);

            if (resolution.Failure == WindowResolutionFailure.HandleNotFound)
                return Task.FromResult(ErrorResult($"Window not found: {handle}"));
            if (resolution.Failure == WindowResolutionFailure.NoFocusedElement)
                return Task.FromResult(ErrorResult("No focused window found. Provide a 'handle' argument."));
            if (resolution.Failure == WindowResolutionFailure.NoWindowAncestor)
                return Task.FromResult(ErrorResult("Could not find a window for the focused element."));

            var window = resolution.Window!;
            var effectiveHandle = resolution.Handle ?? handle!;
            var autoRegistered = string.IsNullOrEmpty(handle) ? $" (auto-registered from focused)" : "";

            string msg = action switch
            {
                "maximize" => WindowStateController.SetVisualState(window, effectiveHandle, WindowVisualState.Maximized),
                "minimize" => WindowStateController.SetVisualState(window, effectiveHandle, WindowVisualState.Minimized),
                "restore"  => WindowStateController.SetVisualState(window, effectiveHandle, WindowVisualState.Normal),
                "move"     => WindowStateController.Move(window, effectiveHandle,
                                  GetArgument<int?>(arguments, "x") ?? 0,
                                  GetArgument<int?>(arguments, "y") ?? 0),
                "resize"   => WindowStateController.Resize(window, effectiveHandle,
                                  GetArgument<int?>(arguments, "width") ?? 0,
                                  GetArgument<int?>(arguments, "height") ?? 0),
                _ => throw new InvalidOperationException($"Unhandled action: {action}")
            };

            return Task.FromResult(TextResult(msg + autoRegistered));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to {action} window: {ex.Message}"));
        }
    }
}
