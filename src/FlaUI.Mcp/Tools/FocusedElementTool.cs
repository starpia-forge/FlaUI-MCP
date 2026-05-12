using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

public class FocusedElementTool : ToolBase
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;

    public FocusedElementTool(SessionManager session, ElementRegistry registry)
    {
        _session = session;
        _registry = registry;
    }

    public override string Name => "windows_focused_element";

    public override string Description =>
        "Return the element that currently holds keyboard focus, system-wide. " +
        "Auto-registers the owning window if needed and returns a new element ref usable with " +
        "windows_click, windows_inspect, windows_get_value, etc. " +
        "Use after Tab/Shift+Tab navigation or to discover which control received focus after a launch.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new { }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        try
        {
            var focused = _session.Automation.FocusedElement();
            if (focused == null)
                return Task.FromResult(ErrorResult("No element currently has keyboard focus."));

            var current = focused;
            while (current != null)
            {
                if (current.Properties.ControlType.ValueOrDefault == ControlType.Window)
                {
                    var window = current.AsWindow();
                    var handle = _session.RegisterWindow(window);
                    var refId = _registry.Register(handle, focused);

                    var ctrlType = SafeAccess.Get(
                        () => focused.Properties.ControlType.ValueOrDefault,
                        ControlType.Custom);
                    var role = Roles.ToRole(ctrlType);
                    var name = SafeAccess.Get(() => focused.Properties.Name.ValueOrDefault) ?? "";

                    return Task.FromResult(TextResult(
                        $"Focused: {refId} ({role} \"{name}\") in window {handle}"));
                }
                current = current.Parent;
            }

            return Task.FromResult(ErrorResult(
                "Focused element has no owning window (likely desktop or system overlay)."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to get focused element: {ex.Message}"));
        }
    }
}
