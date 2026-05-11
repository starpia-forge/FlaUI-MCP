using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Take accessibility snapshot of a window - THE KEY TOOL FOR AGENTS
/// </summary>
public class SnapshotTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;
    private readonly SnapshotBuilder _snapshotBuilder;

    public SnapshotTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
        _snapshotBuilder = new SnapshotBuilder(elementRegistry);
    }

    public override string Name => "windows_snapshot";

    public override string Description =>
        "Capture accessibility snapshot of a window or popup menu. Returns a structured tree with " +
        "element refs that can be used with windows_click, windows_type, etc. " +
        "This is the primary tool for understanding window contents — use it before interacting with elements. " +
        "Also accepts popup handles (m1, m2, …) returned by windows_context_menu or windows_tray_invoke.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            handle = new
            {
                type = "string",
                description = "Window handle (w1, w2, …) or popup/menu handle (m1, m2, …) from windows_launch, windows_list_windows, windows_context_menu, or windows_tray_invoke. If omitted, uses the most recently focused window."
            }
        }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var handle = GetStringArgument(arguments, "handle");

        try
        {
            // Popup handle (m*) — bypass window resolver and look up directly in popup registry.
            if (!string.IsNullOrEmpty(handle) && handle.StartsWith("m", StringComparison.OrdinalIgnoreCase))
            {
                var popup = _sessionManager.GetPopup(handle);
                if (popup == null)
                    return Task.FromResult(ErrorResult(
                        $"Popup '{handle}' not found. The menu may have been dismissed. " +
                        "Re-open the context menu and use the new handle."));

                var snapshot = _snapshotBuilder.BuildSnapshot(handle, popup);
                return Task.FromResult(TextResult(snapshot));
            }

            var resolution = WindowResolver.ResolveOrFocused(_sessionManager, handle, registerFocused: true);

            if (resolution.Failure != WindowResolutionFailure.None)
            {
                var error = resolution.Failure == WindowResolutionFailure.HandleNotFound
                    ? $"Window not found: {handle}"
                    : "No window specified and no focused window found. Use windows_list_windows to see available windows.";
                return Task.FromResult(ErrorResult(error));
            }

            var snapshot2 = _snapshotBuilder.BuildSnapshot(resolution.Handle!, resolution.Window!);
            return Task.FromResult(TextResult(snapshot2));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to capture snapshot: {ex.Message}"));
        }
    }
}
