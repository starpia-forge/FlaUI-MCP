using System.Text.Json;
using PlaywrightWindows.Mcp.Core;

namespace PlaywrightWindows.Mcp.Tools;

/// <summary>
/// Type text into an element
/// </summary>
public class TypeTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public TypeTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_type";

    public override string Description =>
        "Type text into an element. The element will be focused first. " +
        "Use this for typing without clearing existing content. Use windows_fill to replace content.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            @ref = new
            {
                type = "string",
                description = "Element ref from windows_snapshot (e.g., 'w1e5'). If omitted, types to currently focused element."
            },
            text = new
            {
                type = "string",
                description = "Text to type"
            },
            submit = new
            {
                type = "boolean",
                description = "Press Enter after typing (default: false)"
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Operation timeout in milliseconds (default: 5000)"
            }
        },
        required = new[] { "text" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var text = GetStringArgument(arguments, "text");
        if (text == null)
            return Task.FromResult(ErrorResult("Missing required argument: text"));

        var refId = GetStringArgument(arguments, "ref");
        var submit = GetBoolArgument(arguments, "submit", false);
        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            if (!string.IsNullOrEmpty(refId))
            {
                var msg = ActionExecutor.ExecuteWithRetry(
                    _elementRegistry, _sessionManager, refId,
                    e => TypeStrategy.Type(e, refId, text, submit),
                    timeoutMs);
                return Task.FromResult(TextResult(msg));
            }

            return Task.FromResult(TextResult(TypeStrategy.TypeToFocused(text, submit)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to type: {ex.Message}"));
        }
    }
}

/// <summary>
/// Fill (clear and type) an element
/// </summary>
public class FillTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public FillTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_fill";

    public override string Description =>
        "Clear and fill a text field with a new value. Prefers Value pattern for reliability, " +
        "falls back to Ctrl+A + Type. Retries automatically on transient UIA errors.";

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
            value = new
            {
                type = "string",
                description = "Value to fill"
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Operation timeout in milliseconds (default: 5000)"
            }
        },
        required = new[] { "ref", "value" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var refId = GetStringArgument(arguments, "ref");
        var value = GetStringArgument(arguments, "value");

        if (string.IsNullOrEmpty(refId))
            return Task.FromResult(ErrorResult("Missing required argument: ref"));
        if (value == null)
            return Task.FromResult(ErrorResult("Missing required argument: value"));

        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            var msg = ActionExecutor.ExecuteWithRetry(
                _elementRegistry, _sessionManager, refId,
                e => FillStrategy.Fill(e, refId, value),
                timeoutMs);
            return Task.FromResult(TextResult(msg));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to fill {refId}: {ex.Message}"));
        }
    }
}
