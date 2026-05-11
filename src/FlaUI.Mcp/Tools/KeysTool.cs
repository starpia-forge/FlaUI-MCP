using System.Text.Json;
using FlaUI.Core.Input;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Send keyboard shortcuts or key sequences to the focused element or a target ref.
/// Supports chords ("Ctrl+S"), single keys ("F5"), and space-separated sequences ("Ctrl+A Delete").
/// </summary>
public class KeysTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public KeysTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_keys";

    public override string Description =>
        "Send keyboard keys or shortcuts to the focused element or a target ref. " +
        "Chord syntax: 'Ctrl+S', 'Ctrl+Shift+N'. Single keys: 'Tab', 'Enter', 'F5'. " +
        "Sequences (space-separated): 'Ctrl+A Delete'. " +
        "Supported modifiers: Ctrl, Shift, Alt, Win. " +
        "Keys: a-z, 0-9, F1-F12, Tab, Enter, Escape, Space, Backspace, Delete, Home, End, PageUp, PageDown, Up, Down, Left, Right.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            keys = new
            {
                type = "string",
                description = "Key chord ('Ctrl+S'), single key ('Tab'), or space-separated sequence ('Ctrl+A Delete')."
            },
            @ref = new
            {
                type = "string",
                description = "Optional element ref to focus before sending keys."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Element focus timeout in milliseconds (default: 5000)."
            }
        },
        required = new[] { "keys" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var keys = GetStringArgument(arguments, "keys");
        if (string.IsNullOrEmpty(keys))
            return Task.FromResult(ErrorResult("Missing required argument: keys"));

        var refId     = GetStringArgument(arguments, "ref");
        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        try
        {
            if (!string.IsNullOrEmpty(refId))
            {
                ActionExecutor.ExecuteWithRetry(
                    _elementRegistry, _sessionManager, refId,
                    e => { e.Focus(); Thread.Sleep(50); return true; },
                    timeoutMs);
            }

            SendKeys(keys);
            return Task.FromResult(TextResult($"Sent keys: {keys}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to send keys '{keys}': {ex.Message}"));
        }
    }

    internal static void SendKeys(string keys)
    {
        foreach (var (modifiers, mainKey) in KeyMap.ParseSequence(keys))
        {
            if (modifiers.Length == 0)
            {
                Keyboard.Press(mainKey);
            }
            else
            {
                var allKeys = new FlaUI.Core.WindowsAPI.VirtualKeyShort[modifiers.Length + 1];
                modifiers.CopyTo(allKeys, 0);
                allKeys[modifiers.Length] = mainKey;
                Keyboard.TypeSimultaneously(allKeys);
            }
        }
    }
}
