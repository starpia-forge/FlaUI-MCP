using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Scroll within an element — uses ScrollPattern for precision, falls back to mouse wheel.
/// </summary>
public class ScrollTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public ScrollTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_scroll";

    public override string Description =>
        "Scroll within an element. Prefers UIA ScrollPattern for precision; falls back to mouse wheel. " +
        "Horizontal scrolling is pattern-only (no mouse wheel fallback).";

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
            direction = new
            {
                type = "string",
                @enum = new[] { "up", "down", "left", "right" },
                description = "Scroll direction."
            },
            amount = new
            {
                type = "integer",
                description = "Number of scroll steps (default: 3)."
            },
            usePattern = new
            {
                type = "boolean",
                description = "Prefer ScrollPattern over mouse wheel (default: true)."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Element resolution timeout in milliseconds (default: 5000)."
            }
        },
        required = new[] { "ref", "direction" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var refId = GetStringArgument(arguments, "ref");
        var direction = GetStringArgument(arguments, "direction");

        if (string.IsNullOrEmpty(refId))
            return Task.FromResult(ErrorResult("Missing required argument: ref"));
        if (string.IsNullOrEmpty(direction))
            return Task.FromResult(ErrorResult("Missing required argument: direction"));
        if (direction is not ("up" or "down" or "left" or "right"))
            return Task.FromResult(ErrorResult("direction must be one of: up, down, left, right"));

        var amount     = GetArgument<int?>(arguments, "amount") ?? 3;
        var usePattern = GetBoolArgument(arguments, "usePattern", true);
        var timeoutMs  = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            var msg = ActionExecutor.ExecuteWithRetry(
                _elementRegistry, _sessionManager, refId,
                e => ScrollStrategy.Scroll(e, refId, direction, amount, usePattern),
                timeoutMs);
            return Task.FromResult(TextResult(msg));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to scroll {refId}: {ex.Message}"));
        }
    }
}
