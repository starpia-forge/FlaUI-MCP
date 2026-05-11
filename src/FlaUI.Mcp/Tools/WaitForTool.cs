using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Poll until a UI condition is met or the timeout elapses.
/// Replaces ad-hoc snapshot→sleep→snapshot loops for async UI verification.
/// </summary>
public class WaitForTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public WaitForTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_wait_for";

    public override string Description =>
        "Wait until a condition is met on a window element. Polls until the predicate holds " +
        "or timeoutMs elapses. Use instead of fixed 'wait' sleeps for reliable async verification " +
        "(loading spinners, API responses, animations). " +
        "Provide either 'ref' (from a previous snapshot) or 'handle' + 'selector' to locate the element.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            condition = new
            {
                type = "string",
                @enum = ConditionEvaluator.ValidConditions,
                description = "Predicate: visible|hidden|enabled|disabled|exists|missing|textEquals|textContains|checked|unchecked"
            },
            @ref = new
            {
                type = "string",
                description = "Element ref from a previous windows_snapshot (e.g. 'w1e5'). Auto re-resolves if stale."
            },
            handle = new
            {
                type = "string",
                description = "Window handle. Used with 'selector' when ref is not available."
            },
            selector = new
            {
                type = "object",
                description = "Locate element by name/automationId/role within 'handle'.",
                properties = new
                {
                    name         = new { type = "string" },
                    automationId = new { type = "string" },
                    role         = new { type = "string", description = "e.g. 'button', 'textbox'" }
                }
            },
            text = new
            {
                type = "string",
                description = "Comparison value for textEquals / textContains conditions."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Total polling budget in milliseconds (default: 10000)."
            },
            pollMs = new
            {
                type = "integer",
                description = "Interval between polls in milliseconds (default: 100)."
            }
        },
        required = new[] { "condition" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var condition = GetStringArgument(arguments, "condition");
        if (string.IsNullOrEmpty(condition))
            return Task.FromResult(ErrorResult("Missing required argument: condition"));

        if (!ConditionEvaluator.ValidConditions.Contains(condition))
            return Task.FromResult(ErrorResult(
                $"Unknown condition '{condition}'. Valid: {string.Join(", ", ConditionEvaluator.ValidConditions)}"));

        var refId     = GetStringArgument(arguments, "ref");
        var handle    = GetStringArgument(arguments, "handle");
        var text      = GetStringArgument(arguments, "text");
        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? 10000;
        var pollMs    = GetArgument<int?>(arguments, "pollMs") ?? 100;

        var selector = arguments.HasValue ? Selector.From(arguments.Value) : default;

        var needsElement = condition is "visible" or "enabled" or "disabled"
                            or "textEquals" or "textContains" or "checked" or "unchecked";

        if (needsElement && string.IsNullOrEmpty(refId) &&
            string.IsNullOrEmpty(handle) && selector.IsEmpty)
        {
            return Task.FromResult(ErrorResult(
                $"Condition '{condition}' requires an element. Provide 'ref' or 'handle'+'selector'."));
        }

        var started = DateTime.UtcNow;
        string lastObserved = "not yet polled";

        bool met = ActionExecutor.WaitUntil(() =>
        {
            var element = ElementResolver.Resolve(_sessionManager, _elementRegistry, refId, handle, selector);
            var (result, observed) = ConditionEvaluator.Evaluate(element, condition, text);
            lastObserved = observed;
            return result;
        }, timeoutMs, pollMs);

        var elapsed = (int)(DateTime.UtcNow - started).TotalMilliseconds;

        if (met)
            return Task.FromResult(TextResult(
                $"Condition '{condition}' met after {elapsed}ms. Last observed: {lastObserved}"));

        return Task.FromResult(ErrorResult(
            $"Timed out after {timeoutMs}ms waiting for '{condition}'. " +
            $"Last observed: {lastObserved}"));
    }
}
