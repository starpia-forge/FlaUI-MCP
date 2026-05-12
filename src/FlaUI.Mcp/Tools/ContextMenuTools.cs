using System.Text.Json;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Right-click any element and discover the resulting context menu as a snapshot-able popup handle.
/// </summary>
public class ContextMenuTool : ToolBase
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;

    public ContextMenuTool(SessionManager session, ElementRegistry registry)
    {
        _session = session;
        _registry = registry;
    }

    public override string Name => "windows_context_menu";

    public override string Description =>
        "Open a context menu by right-clicking an element (or sending Shift+F10 / VK_APPS) and " +
        "register the resulting menu as a popup handle. " +
        "Use windows_snapshot with the returned handle to list menu items, " +
        "then windows_click to activate one. " +
        "Warning: context menus dismiss on focus loss — snapshot and click immediately " +
        "without calling any other window-enumeration tools in between.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            @ref = new
            {
                type = "string",
                description = "Element ref (e.g. 'w1e5') to right-click. Required unless x/y provided."
            },
            x = new
            {
                type = "integer",
                description = "Absolute screen X coordinate to right-click (use with y instead of ref)."
            },
            y = new
            {
                type = "integer",
                description = "Absolute screen Y coordinate to right-click (use with x instead of ref)."
            },
            method = new
            {
                type = "string",
                @enum = new[] { "right_click", "shift_f10", "vk_apps" },
                description = "How to open the menu. 'right_click' (default): physical right-click. " +
                              "'shift_f10': keyboard shortcut. 'vk_apps': Application key."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Milliseconds to wait for the menu to appear. Default 1500."
            }
        }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var refId   = GetStringArgument(arguments, "ref");
        var xArg    = GetArgument<int?>(arguments, "x");
        var yArg    = GetArgument<int?>(arguments, "y");
        var method  = GetStringArgument(arguments, "method") ?? "right_click";
        var timeout = GetArgument<int?>(arguments, "timeoutMs") ?? 1500;

        if (string.IsNullOrEmpty(refId) && (xArg == null || yArg == null))
            return Task.FromResult(ErrorResult(
                "Provide either 'ref' (element ref) or both 'x' and 'y' (screen coordinates)."));

        try
        {
            var menuBaseline = _session.SnapshotTopLevelMenus();

            // Perform the trigger action
            string targetName;
            if (!string.IsNullOrEmpty(refId))
            {
                var entry = _registry.GetEntry(refId);
                if (entry == null)
                    return Task.FromResult(ErrorResult(
                        $"Element ref '{refId}' not found. Call windows_snapshot to refresh refs."));

                targetName = entry.Name ?? refId;

                ActionExecutor.ExecuteWithRetry(
                    _registry, _session, refId,
                    e => TriggerMenu(e, method, refId),
                    ActionExecutor.DefaultTimeoutMs);
            }
            else
            {
                targetName = $"({xArg},{yArg})";
                var pt = new System.Drawing.Point(xArg!.Value, yArg!.Value);
                TriggerMenuAtPoint(pt, method);
            }

            var menuElement = _session.PollForNewMenu(menuBaseline, timeout);
            if (menuElement == null)
                return Task.FromResult(ErrorResult(
                    $"No context menu appeared within {timeout}ms after '{method}' on {targetName}. " +
                    "The element may not have a context menu, or the menu was dismissed before detection."));

            var menuHandle = _session.RegisterPopup(menuElement);
            return Task.FromResult(TextResult(
                $"Opened context menu on \"{targetName}\" via {method}. Popup registered: {menuHandle}. " +
                $"Use windows_snapshot {{ \"handle\": \"{menuHandle}\" }} to list items, " +
                $"then windows_click to activate one. " +
                $"Dismiss with windows_dismiss_menu {{ \"handle\": \"{menuHandle}\" }} when done."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to open context menu: {ex.Message}"));
        }
    }

    private static string TriggerMenu(FlaUI.Core.AutomationElements.AutomationElement element,
        string method, string refId)
    {
        switch (method)
        {
            case "shift_f10":
                element.Focus();
                Thread.Sleep(50);
                Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
                return $"Shift+F10 on {refId}";

            case "vk_apps":
                element.Focus();
                Thread.Sleep(50);
                Keyboard.Press(VirtualKeyShort.APPS);
                return $"VK_APPS on {refId}";

            default: // right_click
                var pt = element.GetClickablePoint();
                Mouse.Click(pt, MouseButton.Right);
                return $"Right-clicked {refId}";
        }
    }

    private static void TriggerMenuAtPoint(System.Drawing.Point pt, string method)
    {
        switch (method)
        {
            case "shift_f10":
            case "vk_apps":
                Mouse.Position = pt;
                Thread.Sleep(50);
                if (method == "shift_f10")
                    Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);
                else
                    Keyboard.Press(VirtualKeyShort.APPS);
                break;

            default:
                Mouse.Click(pt, MouseButton.Right);
                break;
        }
    }
}

/// <summary>
/// Dismiss a context menu popup and clean up its handle from the registry.
/// </summary>
public class DismissMenuTool : ToolBase
{
    private readonly SessionManager _session;

    public DismissMenuTool(SessionManager session)
    {
        _session = session;
    }

    public override string Name => "windows_dismiss_menu";

    public override string Description =>
        "Dismiss an open context menu by sending Escape and remove its popup handle from the registry. " +
        "Call this after you have finished with a context menu (clicked an item or want to cancel). " +
        "The popup handle becomes invalid after this call.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            handle = new
            {
                type = "string",
                description = "Popup handle (e.g. 'm1') returned by windows_context_menu or windows_tray_invoke."
            }
        },
        required = new[] { "handle" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var handle = GetStringArgument(arguments, "handle");
        if (string.IsNullOrEmpty(handle))
            return Task.FromResult(ErrorResult("Missing required argument: handle"));

        if (_session.GetPopup(handle) == null)
            return Task.FromResult(ErrorResult($"No popup registered for handle: {handle}"));

        try
        {
            Keyboard.Press(VirtualKeyShort.ESC);
        }
        catch { /* best-effort — menu may already be gone */ }

        _session.ClearPopup(handle);
        return Task.FromResult(TextResult($"Dismissed popup '{handle}' and removed it from the registry."));
    }
}
