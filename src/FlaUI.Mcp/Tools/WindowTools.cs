using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// List all open windows
/// </summary>
public class ListWindowsTool : ToolBase
{
    private readonly SessionManager _sessionManager;

    public ListWindowsTool(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public override string Name => "windows_list_windows";

    public override string Description => 
        "List all open windows with their handles, titles, and process names. " +
        "Use this to find windows to interact with.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new { }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        try
        {
            var windows = _sessionManager.ListWindows();

            if (windows.Count == 0)
            {
                return Task.FromResult(TextResult("No windows found"));
            }

            var lines = windows.Select(w => 
                $"- {w.handle}: \"{w.title}\" ({w.processName ?? "unknown"})");
            
            return Task.FromResult(TextResult(string.Join("\n", lines)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to list windows: {ex.Message}"));
        }
    }
}

/// <summary>
/// Focus a window
/// </summary>
public class FocusWindowTool : ToolBase
{
    private readonly SessionManager _sessionManager;

    public FocusWindowTool(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public override string Name => "windows_focus";

    public override string Description => 
        "Bring a window to the foreground and give it focus.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            handle = new
            {
                type = "string",
                description = "Window handle from windows_list_windows or windows_launch"
            },
            title = new
            {
                type = "string",
                description = "Window title (alternative to handle). Finds first window containing this text."
            }
        }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var handle = GetStringArgument(arguments, "handle");
        var title = GetStringArgument(arguments, "title");

        try
        {
            if (!string.IsNullOrEmpty(handle))
            {
                _sessionManager.FocusWindow(handle);
                return Task.FromResult(TextResult($"Focused window {handle}"));
            }
            else if (!string.IsNullOrEmpty(title))
            {
                var (windowHandle, window) = _sessionManager.AttachToWindow(title);
                window.Focus();
                return Task.FromResult(TextResult($"Focused window \"{window.Title}\" (handle: {windowHandle})"));
            }
            else
            {
                return Task.FromResult(ErrorResult("Either 'handle' or 'title' is required"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to focus window: {ex.Message}"));
        }
    }
}

/// <summary>
/// Close a window
/// </summary>
public class CloseWindowTool : ToolBase
{
    private readonly SessionManager _sessionManager;

    public CloseWindowTool(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public override string Name => "windows_close";

    public override string Description => 
        "Close a window.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            handle = new
            {
                type = "string",
                description = "Window handle to close"
            }
        },
        required = new[] { "handle" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var handle = GetStringArgument(arguments, "handle");
        if (string.IsNullOrEmpty(handle))
        {
            return Task.FromResult(ErrorResult("Missing required argument: handle"));
        }

        try
        {
            _sessionManager.CloseWindow(handle);
            return Task.FromResult(TextResult($"Closed window {handle}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to close window: {ex.Message}"));
        }
    }
}
