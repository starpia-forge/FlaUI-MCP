using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Click an element by ref
/// </summary>
public class ClickTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public ClickTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_click";

    public override string Description =>
        "Click an element by its ref (from windows_snapshot). Prefers Invoke pattern for reliability, " +
        "falls back to mouse click if needed. Retries automatically on transient UIA errors.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            @ref = new
            {
                type = "string",
                description = "Element ref from windows_snapshot (e.g., 'w1e5')"
            },
            button = new
            {
                type = "string",
                @enum = new[] { "left", "right", "middle" },
                description = "Mouse button to click (default: left)"
            },
            doubleClick = new
            {
                type = "boolean",
                description = "Whether to double-click (default: false)"
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Operation timeout in milliseconds (default: 5000)"
            }
        },
        required = new[] { "ref" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var refId = GetStringArgument(arguments, "ref");
        if (string.IsNullOrEmpty(refId))
            return Task.FromResult(ErrorResult("Missing required argument: ref"));

        var button = GetStringArgument(arguments, "button") ?? "left";
        var doubleClick = GetBoolArgument(arguments, "doubleClick", false);
        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            var msg = ActionExecutor.ExecuteWithRetry(
                _elementRegistry, _sessionManager, refId,
                e => ClickStrategy.Click(e, refId, button, doubleClick),
                timeoutMs);
            return Task.FromResult(TextResult(msg));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to click {refId}: {ex.Message}"));
        }
    }
}
