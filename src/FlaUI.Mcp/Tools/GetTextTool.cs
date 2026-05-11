using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Get text content of an element
/// </summary>
public class GetTextTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public GetTextTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_get_text";

    public override string Description =>
        "Get the text content of an element. Returns the Value pattern content for text inputs, " +
        "or the element Name property as fallback.";

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

        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            var text = ActionExecutor.ExecuteWithRetry(
                _elementRegistry, _sessionManager, refId,
                e =>
                {
                    string? result = null;
                    if (e.Patterns.Value.IsSupported)
                        result = e.Patterns.Value.Pattern.Value.ValueOrDefault;
                    if (string.IsNullOrEmpty(result))
                        result = e.Properties.Name.ValueOrDefault;
                    if (string.IsNullOrEmpty(result) && e.Patterns.Text.IsSupported)
                        result = e.Patterns.Text.Pattern.DocumentRange.GetText(-1);
                    return result ?? "";
                },
                timeoutMs);
            return Task.FromResult(TextResult(text));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to get text from {refId}: {ex.Message}"));
        }
    }
}
