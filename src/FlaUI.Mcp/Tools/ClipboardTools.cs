using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

public class GetClipboardTool : ToolBase
{
    public override string Name => "windows_get_clipboard";

    public override string Description =>
        "Read the current text content of the system clipboard (CF_UNICODETEXT). " +
        "Returns the text directly so the agent can inspect or process it without keyboard shortcuts. " +
        "Returns an explicit message when the clipboard is empty or contains non-text data.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new { }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        try
        {
            var text = ClipboardAccessor.ReadText();
            return Task.FromResult(TextResult(
                string.IsNullOrEmpty(text)
                    ? "Clipboard is empty or contains no text"
                    : text));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to read clipboard: {ex.Message}"));
        }
    }
}

public class SetClipboardTool : ToolBase
{
    public override string Name => "windows_set_clipboard";

    public override string Description =>
        "Write text to the system clipboard. " +
        "Passes the text to any subsequent Paste operation (Ctrl+V / windows_keys). " +
        "An empty string clears the clipboard.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            text = new
            {
                type = "string",
                description = "Text to place on the clipboard. Pass an empty string to clear the clipboard."
            }
        },
        required = new[] { "text" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var text = GetStringArgument(arguments, "text");
        if (text is null)
            return Task.FromResult(ErrorResult("Missing required argument: text"));

        try
        {
            ClipboardAccessor.WriteText(text);
            return Task.FromResult(TextResult(
                string.IsNullOrEmpty(text)
                    ? "Clipboard cleared"
                    : $"Clipboard set ({text.Length} characters)"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to write clipboard: {ex.Message}"));
        }
    }
}
