using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Launch a Windows application
/// </summary>
public class LaunchTool : ToolBase
{
    private readonly SessionManager _sessionManager;

    public LaunchTool(SessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public override string Name => "windows_launch";

    public override string Description => 
        "Launch a Windows application. Returns a window handle for use with other tools.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            app = new
            {
                type = "string",
                description = "Path to executable or UWP app ID (e.g., 'calc.exe', 'notepad.exe', 'C:\\\\Program Files\\\\MyApp\\\\app.exe')"
            },
            args = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Optional command line arguments"
            }
        },
        required = new[] { "app" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var app = GetStringArgument(arguments, "app");
        if (string.IsNullOrEmpty(app))
        {
            return Task.FromResult(ErrorResult("Missing required argument: app"));
        }

        var args = GetArgument<string[]>(arguments, "args");

        try
        {
            var (handle, window) = _sessionManager.LaunchApp(app, args);
            return Task.FromResult(TextResult($"Launched {app}\nWindow handle: {handle}\nTitle: {window.Title}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to launch {app}: {ex.Message}"));
        }
    }
}
