using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Move the mouse to an element to trigger hover-only UI such as tooltips and hover menus.
/// </summary>
public class HoverTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public HoverTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_hover";

    public override string Description =>
        "Move the mouse pointer to an element to trigger hover-only UI (tooltips, hover menus). " +
        "Does not click. Take a snapshot after hovering to observe the resulting state.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            @ref = new
            {
                type = "string",
                description = "Element ref from windows_snapshot (e.g. 'w1e5')."
            },
            durationMs = new
            {
                type = "integer",
                description = "How long to linger at the element after moving (default: 200ms)."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Element resolution timeout in milliseconds (default: 5000)."
            }
        },
        required = new[] { "ref" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var refId = GetStringArgument(arguments, "ref");
        if (string.IsNullOrEmpty(refId))
            return Task.FromResult(ErrorResult("Missing required argument: ref"));

        var durationMs = GetArgument<int?>(arguments, "durationMs") ?? 200;
        var timeoutMs  = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            var msg = ActionExecutor.ExecuteWithRetry(
                _elementRegistry, _sessionManager, refId,
                e => HoverStrategy.Hover(e, refId, durationMs),
                timeoutMs);
            return Task.FromResult(TextResult(msg));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to hover {refId}: {ex.Message}"));
        }
    }
}
