using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Drag from one element to another (or to absolute coordinates) via mouse press + interpolated move + release.
/// </summary>
public class DragTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public DragTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_drag";

    public override string Description =>
        "Drag from one element to another using mouse press + interpolated move + release. " +
        "Provide 'toRef' to drop on a target element, or 'toX'+'toY' for absolute screen coordinates. " +
        "Increase durationMs for apps that require smooth drag movement.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            fromRef = new
            {
                type = "string",
                description = "Source element ref from windows_snapshot."
            },
            toRef = new
            {
                type = "string",
                description = "Target element ref. Alternative to toX/toY."
            },
            toX = new
            {
                type = "integer",
                description = "Target absolute screen X coordinate. Alternative to toRef."
            },
            toY = new
            {
                type = "integer",
                description = "Target absolute screen Y coordinate. Use with toX."
            },
            durationMs = new
            {
                type = "integer",
                description = "Drag movement duration in milliseconds (default: 300). Higher = smoother."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Per-element resolution timeout in milliseconds (default: 5000)."
            }
        },
        required = new[] { "fromRef" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var fromRef = GetStringArgument(arguments, "fromRef");
        if (string.IsNullOrEmpty(fromRef))
            return Task.FromResult(ErrorResult("Missing required argument: fromRef"));

        var toRef      = GetStringArgument(arguments, "toRef");
        var toX        = GetArgument<int?>(arguments, "toX");
        var toY        = GetArgument<int?>(arguments, "toY");
        var durationMs = GetArgument<int?>(arguments, "durationMs") ?? 300;
        var timeoutMs  = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        if (string.IsNullOrEmpty(toRef) && (toX == null || toY == null))
            return Task.FromResult(ErrorResult("Provide either 'toRef' or both 'toX' and 'toY'."));

        try
        {
            System.Drawing.Point? toPoint = null;
            string toName = toRef ?? $"({toX}, {toY})";

            if (!string.IsNullOrEmpty(toRef))
            {
                ActionExecutor.ExecuteWithRetry(
                    _elementRegistry, _sessionManager, toRef,
                    e =>
                    {
                        toPoint = e.GetClickablePoint();
                        toName = e.Properties.Name.ValueOrDefault ?? toRef;
                        return true;
                    },
                    timeoutMs);
            }
            else
            {
                toPoint = new System.Drawing.Point(toX!.Value, toY!.Value);
            }

            var msg = ActionExecutor.ExecuteWithRetry(
                _elementRegistry, _sessionManager, fromRef,
                e => DragStrategy.Drag(e, fromRef, toPoint!.Value, toName, durationMs),
                timeoutMs);

            return Task.FromResult(TextResult(msg));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to drag {fromRef}: {ex.Message}"));
        }
    }
}
