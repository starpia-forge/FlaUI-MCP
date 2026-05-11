using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Attach to an already-running process and register its windows
/// </summary>
public class AttachTool : ToolBase
{
    private readonly SessionManager _session;

    public AttachTool(SessionManager session) => _session = session;

    public override string Name => "windows_attach";

    public override string Description =>
        "Attach to an already-running Windows process by PID or executable name. " +
        "Returns window handles for every UIA-visible window the process owns — " +
        "including hidden/untitled windows typical of tray-resident apps. " +
        "Provide exactly one of 'pid' or 'processName'.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            pid = new
            {
                type = "integer",
                description = "Process ID. Mutually exclusive with processName."
            },
            processName = new
            {
                type = "string",
                description = "Executable name, with or without .exe (e.g. 'Discord', 'notepad'). Case-insensitive. Mutually exclusive with pid."
            }
        }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var pid = GetArgument<int?>(arguments, "pid");
        var name = GetStringArgument(arguments, "processName");

        if (pid.HasValue && !string.IsNullOrEmpty(name))
            return Task.FromResult(ErrorResult("Provide exactly one of 'pid' or 'processName', not both."));
        if (!pid.HasValue && string.IsNullOrEmpty(name))
            return Task.FromResult(ErrorResult("Provide either 'pid' or 'processName'."));

        try
        {
            var attached = _session.AttachByProcess(pid, name);
            var label = name ?? GetProcessLabel(pid!.Value);
            var lines = attached.Select(a =>
                $"- {a.Handle}: \"{a.Title}\" [{(a.IsVisible ? "visible" : "hidden")}]");
            return Task.FromResult(TextResult(
                $"Attached to {label} (pid={attached[0].OwnerPid}). Found {attached.Count} window(s):\n" +
                string.Join("\n", lines)));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(ex.Message));
        }
    }

    private static string GetProcessLabel(int pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return p.ProcessName;
        }
        catch { return $"pid={pid}"; }
    }
}
