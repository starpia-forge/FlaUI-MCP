using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

public class GetValueTool : ToolBase
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;

    public GetValueTool(SessionManager session, ElementRegistry registry)
    {
        _session = session;
        _registry = registry;
    }

    public override string Name => "windows_get_value";

    public override string Description =>
        "Read the current value of an element using UIA patterns (Value, RangeValue, Toggle, SelectionItem). " +
        "Returns the highest-priority supported pattern's value. " +
        "Use instead of windows_get_text when you need slider position, checkbox state, or combo box selection.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            @ref = new
            {
                type = "string",
                description = "Element ref from windows_snapshot (e.g., 'w1e5')."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Operation timeout in milliseconds. Default 5000."
            }
        },
        required = new[] { "ref" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var refId = GetStringArgument(arguments, "ref");
        if (string.IsNullOrEmpty(refId))
            return Task.FromResult(ErrorResult("Missing required argument: ref"));

        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            var report = ActionExecutor.ExecuteWithRetry(
                _registry, _session, refId,
                e =>
                {
                    var result = ValueAccessor.Read(e);
                    if (result is null)
                        throw new NotSupportedException(
                            $"Element '{refId}' does not support Value, RangeValue, Toggle, or SelectionItem patterns. " +
                            "Use windows_inspect to see which patterns are available.");
                    return ValueAccessor.Format(result.Value);
                },
                timeoutMs);
            return Task.FromResult(TextResult(report));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(
                $"Failed to get value of '{refId}': {ex.Message}. " +
                "Call windows_snapshot to refresh element refs."));
        }
    }
}

public class SetValueTool : ToolBase
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;

    public SetValueTool(SessionManager session, ElementRegistry registry)
    {
        _session = session;
        _registry = registry;
    }

    public override string Name => "windows_set_value";

    public override string Description =>
        "Set the value of an element via UIA patterns. " +
        "string → Value pattern (text input), or Selection container's child Name match (combo box / list box); " +
        "number → RangeValue pattern (slider); " +
        "boolean → Toggle pattern (checkbox, cycling to the target On/Off state).";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            @ref = new
            {
                type = "string",
                description = "Element ref from windows_snapshot (e.g., 'w1e5')."
            },
            value = new
            {
                description = "Target value. " +
                    "Use a string for text inputs or to select an item by name in a list/combo box. " +
                    "Use a number for sliders (RangeValue). " +
                    "Use true/false for checkboxes (Toggle)."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Operation timeout in milliseconds. Default 5000."
            }
        },
        required = new[] { "ref", "value" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var refId = GetStringArgument(arguments, "ref");
        if (string.IsNullOrEmpty(refId))
            return Task.FromResult(ErrorResult("Missing required argument: ref"));

        if (!arguments.HasValue || !arguments.Value.TryGetProperty("value", out var valueElement))
            return Task.FromResult(ErrorResult("Missing required argument: value"));

        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            string msg;
            switch (valueElement.ValueKind)
            {
                case JsonValueKind.String:
                {
                    var str = valueElement.GetString() ?? "";
                    msg = ActionExecutor.ExecuteWithRetry(
                        _registry, _session, refId,
                        e => ValueAccessor.SetString(e, refId, str),
                        timeoutMs);
                    break;
                }
                case JsonValueKind.Number:
                {
                    var num = valueElement.GetDouble();
                    msg = ActionExecutor.ExecuteWithRetry(
                        _registry, _session, refId,
                        e => ValueAccessor.SetNumber(e, refId, num),
                        timeoutMs);
                    break;
                }
                case JsonValueKind.True:
                case JsonValueKind.False:
                {
                    var flag = valueElement.ValueKind == JsonValueKind.True;
                    msg = ActionExecutor.ExecuteWithRetry(
                        _registry, _session, refId,
                        e => ValueAccessor.SetBool(e, refId, flag),
                        timeoutMs);
                    break;
                }
                default:
                    return Task.FromResult(ErrorResult(
                        "Argument 'value' must be a string, number, or boolean"));
            }
            return Task.FromResult(TextResult(msg));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(
                $"Failed to set value of '{refId}': {ex.Message}. " +
                "Call windows_snapshot to refresh element refs."));
        }
    }
}
