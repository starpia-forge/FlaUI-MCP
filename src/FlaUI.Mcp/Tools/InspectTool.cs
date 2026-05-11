using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Dump all UIA properties and supported patterns for one element ref.
/// </summary>
public class InspectTool : ToolBase
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;

    public InspectTool(SessionManager session, ElementRegistry registry)
    {
        _session = session;
        _registry = registry;
    }

    public override string Name => "windows_inspect";

    public override string Description =>
        "Dump all UIA properties (AutomationId, ClassName, BoundingRect, ProcessId, …) and supported " +
        "patterns (Invoke, Toggle, Value, RangeValue, Grid, …) for one element. " +
        "Use before writing assertions to discover precise selectors, or to understand which " +
        "interaction tool fits the element. Patterns section includes current pattern state values.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            @ref = new
            {
                type = "string",
                description = "Element ref from windows_snapshot (e.g., 'w1e5'). Popup refs (e.g., 'm1e3') are also accepted."
            },
            patterns = new
            {
                type = "boolean",
                description = "Include the Patterns section and the interaction Hint. Default true. Set false for a shorter Identity/Geometry/State-only dump."
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

        var includePatterns = GetBoolArgument(arguments, "patterns", true);
        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            var report = ActionExecutor.ExecuteWithRetry(
                _registry, _session, refId,
                e => InspectionRenderer.Render(e, refId, includePatterns),
                timeoutMs);
            return Task.FromResult(TextResult(report));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(
                $"Failed to inspect '{refId}': {ex.Message}. " +
                "Call windows_snapshot to refresh element refs."));
        }
    }
}
