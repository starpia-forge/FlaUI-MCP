using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Enumerate system-tray (notification-area) icons
/// </summary>
public class TrayListTool : ToolBase
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;

    public TrayListTool(SessionManager session, ElementRegistry registry)
    {
        _session = session;
        _registry = registry;
    }

    public override string Name => "windows_tray_list";

    public override string Description =>
        "List icons in the Windows system tray (notification area). " +
        "Returns refs usable with windows_tray_invoke. " +
        "Each call clears previous tray refs and re-registers fresh ones. " +
        "Side-effect: when includeOverflow=true and the overflow popup is hidden, " +
        "the chevron is briefly clicked then dismissed with Escape. " +
        "Limitation: Windows 11 native taskbar (22H2+) is not supported.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            includeOverflow = new
            {
                type = "boolean",
                description = "Include icons hidden behind the overflow chevron. Default true."
            },
            includeSystem = new
            {
                type = "boolean",
                description = "Include system icons (e.g. Volume, Wi-Fi, Battery). Default false."
            }
        }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var includeOverflow = GetBoolArgument(arguments, "includeOverflow", true);
        var includeSystem = GetBoolArgument(arguments, "includeSystem", false);

        try
        {
            var icons = TrayWalker.Enumerate(_session.Automation, includeOverflow, includeSystem);

            // Clear stale tray refs so each call returns a clean ref set
            _registry.ClearWindow("tray");

            if (icons.Count == 0)
            {
                var hint = !TrayWalker.HasShellTrayWnd(_session.Automation)
                    ? "Shell_TrayWnd not found — this usually indicates a Windows 11 native taskbar (22H2+) or a non-Explorer shell, which is not yet supported."
                    : "No tray icons found in the requested categories. Try includeSystem=true or includeOverflow=true.";
                return Task.FromResult(TextResult(hint));
            }

            var lines = icons.Select(icon =>
            {
                var refId = _registry.Register("tray", icon.Element);
                var pid = icon.OwnerPid.HasValue ? $"pid={icon.OwnerPid}" : "pid=unknown";
                var src = icon.Source.ToString().ToLowerInvariant();
                var displayName = string.IsNullOrEmpty(icon.Name) ? "(unnamed)" : icon.Name;
                return $"- {refId}: \"{displayName}\" [{src}, {pid}]";
            }).ToList();

            return Task.FromResult(TextResult(
                $"Found {icons.Count} tray icon(s):\n" +
                string.Join("\n", lines) +
                "\n\nNote: pid often reflects the icon-hosting process (explorer.exe), not the owning app. Use the icon name to identify the owner."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to list tray icons: {ex.Message}"));
        }
    }
}

/// <summary>
/// Click a system-tray icon
/// </summary>
public class TrayInvokeTool : ToolBase
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;

    public TrayInvokeTool(SessionManager session, ElementRegistry registry)
    {
        _session = session;
        _registry = registry;
    }

    public override string Name => "windows_tray_invoke";

    public override string Description =>
        "Click a system-tray icon by ref (obtained from windows_tray_list). " +
        "Left-click typically surfaces the owner app's window; " +
        "any newly-appeared window is auto-registered and its handle returned. " +
        "Right-click opens the icon's context menu (no auto-registration).";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            @ref = new
            {
                type = "string",
                description = "Tray icon ref from windows_tray_list (e.g. 'traye1')."
            },
            button = new
            {
                type = "string",
                @enum = new[] { "left", "right", "middle" },
                description = "Mouse button to use. Default 'left'."
            },
            doubleClick = new
            {
                type = "boolean",
                description = "Perform a double-click. Default false."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Milliseconds to wait for a new window to appear after left-click. Default 1500."
            }
        },
        required = new[] { "ref" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var refId = GetStringArgument(arguments, "ref");
        if (string.IsNullOrEmpty(refId))
            return Task.FromResult(ErrorResult("Missing required argument: ref"));

        var entry = _registry.GetEntry(refId);
        if (entry == null)
            return Task.FromResult(ErrorResult(
                $"Tray ref '{refId}' not found in registry. Re-call windows_tray_list to refresh refs."));

        var button = GetStringArgument(arguments, "button") ?? "left";
        var doubleClick = GetBoolArgument(arguments, "doubleClick", false);
        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? 1500;

        try
        {
            // Snapshot existing top-level windows before the click (for left-click discovery)
            var baseline = button == "left" ? _session.SnapshotTopLevelHwnds() : null;

            ActionExecutor.ExecuteWithRetry(
                _registry, _session, refId,
                e => ClickStrategy.Click(e, refId, button, doubleClick),
                ActionExecutor.DefaultTimeoutMs);

            var iconName = string.IsNullOrEmpty(entry.Name) ? refId : entry.Name;

            if (button != "left" || baseline == null)
            {
                var verb = doubleClick ? "Double-clicked" : button == "right" ? "Right-clicked" : "Clicked";
                var extra = button == "right"
                    ? " Context menu may now be open — use windows_snapshot or windows_keys to interact."
                    : string.Empty;
                return Task.FromResult(TextResult($"{verb} tray icon \"{iconName}\".{extra}"));
            }

            // Discover newly-appeared window (left-click only)
            var newWnd = _session.PollForNewWindow(baseline, timeoutMs);
            if (newWnd != null)
            {
                var handle = _session.RegisterWindow(newWnd);
                var title = SafeAccess.Get(() => newWnd.Title ?? string.Empty, string.Empty);
                return Task.FromResult(TextResult(
                    $"Invoked tray icon \"{iconName}\" (left). New window registered: {handle} \"{title}\""));
            }

            return Task.FromResult(TextResult(
                $"Invoked tray icon \"{iconName}\" (left). No new window detected within {timeoutMs}ms. " +
                "The owner may have toggled an existing window — use windows_list_windows to find it."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to invoke tray icon '{refId}': {ex.Message}"));
        }
    }
}
