using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Single-shot condition check that returns a structured PASS/FAIL result.
/// For async checks use windows_wait_for first, then assert.
/// </summary>
public class AssertTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public AssertTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_assert";

    public override string Description =>
        "Assert that a UI condition holds right now (single evaluation, no polling). " +
        "Returns 'ASSERT [PASS] ...' or 'ASSERT [FAIL] ...' for easy parsing. " +
        "Use windows_wait_for before asserting on async conditions. " +
        "In windows_batch with stopOnError:true, a failed assert stops the batch — " +
        "useful for building structured test cases.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            condition = new
            {
                type = "string",
                @enum = ConditionEvaluator.ValidConditions,
                description = "Predicate to assert."
            },
            @ref = new
            {
                type = "string",
                description = "Element ref from windows_snapshot."
            },
            handle = new
            {
                type = "string",
                description = "Window handle. Used with 'selector' when ref is not available."
            },
            selector = new
            {
                type = "object",
                description = "Locate element within 'handle' by name/automationId/role.",
                properties = new
                {
                    name         = new { type = "string" },
                    automationId = new { type = "string" },
                    role         = new { type = "string" }
                }
            },
            text = new
            {
                type = "string",
                description = "Comparison value for textEquals / textContains."
            },
            message = new
            {
                type = "string",
                description = "Human-readable assertion label included in the result."
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

        var refId   = GetStringArgument(arguments, "ref");
        var handle  = GetStringArgument(arguments, "handle");
        var text    = GetStringArgument(arguments, "text");
        var message = GetStringArgument(arguments, "message") ?? condition;

        var selector = arguments.HasValue ? Selector.From(arguments.Value) : default;
        var element  = ElementResolver.Resolve(_sessionManager, _elementRegistry, refId, handle, selector);
        var (met, observed) = ConditionEvaluator.Evaluate(element, condition, text);

        var resultText = FormatResult(met, condition, text, observed, message);
        return Task.FromResult(met ? TextResult(resultText) : ErrorResult(resultText));
    }

    internal static string FormatResult(bool met, string condition, string? text, string observed, string message)
    {
        var label  = met ? "PASS" : "FAIL";
        var detail = text != null
            ? $"'{condition}'='{text}', observed: {observed}"
            : $"'{condition}', observed: {observed}";
        return $"ASSERT [{label}] {message}: {detail}";
    }
}
