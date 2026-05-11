using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Execute multiple actions in a single call for better performance
/// </summary>
public class BatchTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;
    private readonly SnapshotBuilder _snapshotBuilder;

    public BatchTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
        _snapshotBuilder = new SnapshotBuilder(elementRegistry);
    }

    public override string Name => "windows_batch";

    public override string Description =>
        "Execute multiple actions in a single call. Much faster than individual calls. " +
        "Supports click, type, fill, wait, and snapshot actions. Returns results for each action.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            actions = new
            {
                type = "array",
                description = "List of actions to execute in order",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        action = new
                        {
                            type = "string",
                            @enum = new[] { "click", "type", "fill", "wait", "snapshot" },
                            description = "Action type"
                        },
                        @ref = new
                        {
                            type = "string",
                            description = "Element ref for click/type/fill actions"
                        },
                        button = new
                        {
                            type = "string",
                            @enum = new[] { "left", "right", "middle" },
                            description = "Mouse button for click (default: left)"
                        },
                        doubleClick = new
                        {
                            type = "boolean",
                            description = "Whether to double-click (default: false)"
                        },
                        text = new
                        {
                            type = "string",
                            description = "Text for type action"
                        },
                        submit = new
                        {
                            type = "boolean",
                            description = "Press Enter after typing (default: false)"
                        },
                        value = new
                        {
                            type = "string",
                            description = "Value for fill action"
                        },
                        ms = new
                        {
                            type = "integer",
                            description = "Milliseconds for wait action (default: 100)"
                        },
                        handle = new
                        {
                            type = "string",
                            description = "Window handle for snapshot action"
                        }
                    },
                    required = new[] { "action" }
                }
            },
            stopOnError = new
            {
                type = "boolean",
                description = "Stop executing if an action fails (default: true)"
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Per-action timeout in milliseconds (default: 5000)"
            }
        },
        required = new[] { "actions" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        if (arguments == null || !arguments.Value.TryGetProperty("actions", out var actionsElement))
            return Task.FromResult(ErrorResult("Missing required argument: actions"));

        var stopOnError = true;
        if (arguments.Value.TryGetProperty("stopOnError", out var stopProp))
            stopOnError = stopProp.GetBoolean();

        var timeoutMs = arguments.Value.TryGetProperty("timeoutMs", out var tmProp)
            ? tmProp.GetInt32()
            : ActionExecutor.DefaultTimeoutMs;

        var results = new List<string>();
        var actions = actionsElement.EnumerateArray().ToList();

        foreach (var (actionObj, index) in actions.Select((a, i) => (a, i)))
        {
            try
            {
                var actionType = actionObj.GetProperty("action").GetString();
                var result = actionType switch
                {
                    "click"    => ExecuteClick(actionObj, timeoutMs),
                    "type"     => ExecuteType(actionObj, timeoutMs),
                    "fill"     => ExecuteFill(actionObj, timeoutMs),
                    "wait"     => ExecuteWait(actionObj),
                    "snapshot" => ExecuteSnapshot(actionObj),
                    _          => $"Unknown action: {actionType}"
                };
                results.Add($"{index + 1}. {actionType}: {result}");
            }
            catch (Exception ex)
            {
                results.Add($"{index + 1}. ERROR: {ex.Message}");
                if (stopOnError)
                {
                    results.Add($"Stopped at action {index + 1} due to error");
                    break;
                }
            }
        }

        return Task.FromResult(TextResult(string.Join("\n", results)));
    }

    private string ExecuteClick(JsonElement action, int timeoutMs)
    {
        var refId = action.TryGetProperty("ref", out var rp) ? rp.GetString() : null;
        if (string.IsNullOrEmpty(refId)) return "Missing ref";

        var button = action.TryGetProperty("button", out var bp) ? bp.GetString() ?? "left" : "left";
        var doubleClick = action.TryGetProperty("doubleClick", out var dp) && dp.GetBoolean();

        return ActionExecutor.ExecuteWithRetry(
            _elementRegistry, _sessionManager, refId,
            e => ClickStrategy.Click(e, refId, button, doubleClick),
            timeoutMs);
    }

    private string ExecuteType(JsonElement action, int timeoutMs)
    {
        var text = action.TryGetProperty("text", out var tp) ? tp.GetString() : null;
        if (string.IsNullOrEmpty(text)) return "Missing text";

        var submit = action.TryGetProperty("submit", out var sp) && sp.GetBoolean();
        var refId = action.TryGetProperty("ref", out var rp) ? rp.GetString() : null;

        if (!string.IsNullOrEmpty(refId))
        {
            return ActionExecutor.ExecuteWithRetry(
                _elementRegistry, _sessionManager, refId,
                e => TypeStrategy.Type(e, refId, text, submit),
                timeoutMs);
        }
        return TypeStrategy.TypeToFocused(text, submit);
    }

    private string ExecuteFill(JsonElement action, int timeoutMs)
    {
        var refId = action.TryGetProperty("ref", out var rp) ? rp.GetString() : null;
        var value = action.TryGetProperty("value", out var vp) ? vp.GetString() : null;

        if (string.IsNullOrEmpty(refId) || value == null) return "Missing ref or value";

        return ActionExecutor.ExecuteWithRetry(
            _elementRegistry, _sessionManager, refId,
            e => FillStrategy.Fill(e, refId, value),
            timeoutMs);
    }

    private static string ExecuteWait(JsonElement action)
    {
        var ms = action.TryGetProperty("ms", out var mp) ? mp.GetInt32() : 100;
        Thread.Sleep(ms);
        return $"Waited {ms}ms";
    }

    private string ExecuteSnapshot(JsonElement action)
    {
        var handle = action.TryGetProperty("handle", out var hp) ? hp.GetString() : null;

        Window? window = null;
        if (!string.IsNullOrEmpty(handle))
        {
            window = _sessionManager.GetWindow(handle);
            if (window == null) return $"Window not found: {handle}";
        }
        else
        {
            var focusedElement = _sessionManager.Automation.FocusedElement();
            if (focusedElement != null)
            {
                var current = focusedElement;
                while (current != null)
                {
                    if (current.Properties.ControlType.ValueOrDefault == FlaUI.Core.Definitions.ControlType.Window)
                    {
                        window = current.AsWindow();
                        handle = _sessionManager.RegisterWindow(window);
                        break;
                    }
                    current = current.Parent;
                }
            }
        }

        if (window == null) return "No window found";

        var snapshot = _snapshotBuilder.BuildSnapshot(handle!, window);
        return $"\n{snapshot}";
    }
}
