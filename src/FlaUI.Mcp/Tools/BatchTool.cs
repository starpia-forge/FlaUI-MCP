using System.Text.Json;
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
        "Supports click, type, fill, wait, waitFor, snapshot, keys, hover, scroll, assert, and drag actions. " +
        "Returns results for each action.";

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
                            @enum = new[] { "click", "type", "fill", "wait", "waitFor", "snapshot", "keys", "hover", "scroll", "assert", "drag" },
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
                            description = "Window handle for snapshot / waitFor / assert actions"
                        },
                        condition = new
                        {
                            type = "string",
                            description = "Condition for waitFor / assert actions (visible, hidden, enabled, disabled, exists, missing, textEquals, textContains, checked, unchecked)"
                        },
                        selector = new
                        {
                            type = "object",
                            description = "Element locator {name?, automationId?, role?} for waitFor / assert when no ref is available"
                        },
                        timeoutMs = new
                        {
                            type = "integer",
                            description = "Per-action timeout override in milliseconds"
                        },
                        pollMs = new
                        {
                            type = "integer",
                            description = "Poll interval for waitFor action (default: 100ms)"
                        },
                        message = new
                        {
                            type = "string",
                            description = "Assertion label for assert action"
                        },
                        keys = new
                        {
                            type = "string",
                            description = "Key chord(s) for keys action (e.g. 'Ctrl+S', 'Tab')"
                        },
                        durationMs = new
                        {
                            type = "integer",
                            description = "Linger duration for hover action (default: 200ms) or drag smoothness (default: 300ms)"
                        },
                        direction = new
                        {
                            type = "string",
                            description = "Scroll direction: up, down, left, right"
                        },
                        amount = new
                        {
                            type = "integer",
                            description = "Scroll steps for scroll action (default: 3)"
                        },
                        usePattern = new
                        {
                            type = "boolean",
                            description = "Prefer ScrollPattern for scroll action (default: true)"
                        },
                        fromRef = new
                        {
                            type = "string",
                            description = "Source element ref for drag action"
                        },
                        toRef = new
                        {
                            type = "string",
                            description = "Target element ref for drag action"
                        },
                        toX = new { type = "integer", description = "Target X coordinate for drag action" },
                        toY = new { type = "integer", description = "Target Y coordinate for drag action" }
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

        for (int index = 0; index < actions.Count; index++)
        {
            var actionObj = actions[index];
            try
            {
                var actionType = actionObj.GetProperty("action").GetString();
                var actionTimeout = actionObj.TryGetProperty("timeoutMs", out var atProp)
                    ? atProp.GetInt32() : timeoutMs;

                var result = actionType switch
                {
                    "click"    => ExecuteClick(actionObj, actionTimeout),
                    "type"     => ExecuteType(actionObj, actionTimeout),
                    "fill"     => ExecuteFill(actionObj, actionTimeout),
                    "wait"     => ExecuteWait(actionObj),
                    "waitFor"  => ExecuteWaitFor(actionObj, actionTimeout),
                    "snapshot" => ExecuteSnapshot(actionObj),
                    "keys"     => ExecuteKeys(actionObj, actionTimeout),
                    "hover"    => ExecuteHover(actionObj, actionTimeout),
                    "scroll"   => ExecuteScroll(actionObj, actionTimeout),
                    "assert"   => ExecuteAssert(actionObj),
                    "drag"     => ExecuteDrag(actionObj, actionTimeout),
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

        var button      = action.TryGetProperty("button",      out var bp) ? bp.GetString() ?? "left" : "left";
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
        var refId  = action.TryGetProperty("ref",    out var rp) ? rp.GetString() : null;

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
        var refId = action.TryGetProperty("ref",   out var rp) ? rp.GetString() : null;
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

    private string ExecuteWaitFor(JsonElement action, int timeoutMs)
    {
        var condition = action.TryGetProperty("condition", out var cp) ? cp.GetString() : null;
        if (string.IsNullOrEmpty(condition)) return "Missing condition for waitFor";

        var refId       = action.TryGetProperty("ref",       out var rp) ? rp.GetString() : null;
        var handle      = action.TryGetProperty("handle",    out var hp) ? hp.GetString() : null;
        var text        = action.TryGetProperty("text",      out var tp) ? tp.GetString() : null;
        var pollMs      = action.TryGetProperty("pollMs",    out var pp) ? pp.GetInt32()  : 100;
        var waitTimeout = action.TryGetProperty("timeoutMs", out var wt) ? wt.GetInt32()  : timeoutMs;

        var selector = Selector.From(action);

        string lastObserved = "not polled";
        var started = DateTime.UtcNow;
        bool met = ActionExecutor.WaitUntil(() =>
        {
            var element = ElementResolver.Resolve(_sessionManager, _elementRegistry, refId, handle, selector);
            var (result, observed) = ConditionEvaluator.Evaluate(element, condition, text);
            lastObserved = observed;
            return result;
        }, waitTimeout, pollMs);

        var elapsed = (int)(DateTime.UtcNow - started).TotalMilliseconds;
        if (met) return $"Condition '{condition}' met after {elapsed}ms. Last: {lastObserved}";
        throw new TimeoutException(
            $"Timed out after {waitTimeout}ms waiting for '{condition}'. Last: {lastObserved}");
    }

    private string ExecuteKeys(JsonElement action, int timeoutMs)
    {
        var keys = action.TryGetProperty("keys", out var kp) ? kp.GetString() : null;
        if (string.IsNullOrEmpty(keys)) return "Missing keys";

        var refId = action.TryGetProperty("ref", out var rp) ? rp.GetString() : null;
        if (!string.IsNullOrEmpty(refId))
        {
            ActionExecutor.ExecuteWithRetry(
                _elementRegistry, _sessionManager, refId,
                e => { e.Focus(); Thread.Sleep(50); return true; }, timeoutMs);
        }
        KeysTool.SendKeys(keys);
        return $"Sent keys: {keys}";
    }

    private string ExecuteHover(JsonElement action, int timeoutMs)
    {
        var refId      = action.TryGetProperty("ref",       out var rp) ? rp.GetString() : null;
        if (string.IsNullOrEmpty(refId)) return "Missing ref";
        var durationMs = action.TryGetProperty("durationMs", out var dp) ? dp.GetInt32() : 200;
        return ActionExecutor.ExecuteWithRetry(
            _elementRegistry, _sessionManager, refId,
            e => HoverStrategy.Hover(e, refId, durationMs), timeoutMs);
    }

    private string ExecuteScroll(JsonElement action, int timeoutMs)
    {
        var refId     = action.TryGetProperty("ref",        out var rp) ? rp.GetString() : null;
        var direction = action.TryGetProperty("direction",  out var dp) ? dp.GetString() : null;
        if (string.IsNullOrEmpty(refId) || string.IsNullOrEmpty(direction))
            return "Missing ref or direction";
        var amount     = action.TryGetProperty("amount",      out var ap) ? ap.GetInt32()    : 3;
        var usePattern = !action.TryGetProperty("usePattern", out var up) || up.GetBoolean();
        return ActionExecutor.ExecuteWithRetry(
            _elementRegistry, _sessionManager, refId,
            e => ScrollStrategy.Scroll(e, refId, direction, amount, usePattern), timeoutMs);
    }

    private string ExecuteAssert(JsonElement action)
    {
        var condition = action.TryGetProperty("condition", out var cp) ? cp.GetString() : null;
        if (string.IsNullOrEmpty(condition)) return "Missing condition";

        var refId   = action.TryGetProperty("ref",     out var rp) ? rp.GetString() : null;
        var handle  = action.TryGetProperty("handle",  out var hp) ? hp.GetString() : null;
        var text    = action.TryGetProperty("text",    out var tp) ? tp.GetString() : null;
        var message = action.TryGetProperty("message", out var mp) ? mp.GetString() : condition;

        var selector = Selector.From(action);
        var element  = ElementResolver.Resolve(_sessionManager, _elementRegistry, refId, handle, selector);
        var (met, observed) = ConditionEvaluator.Evaluate(element, condition!, text);

        var result = AssertTool.FormatResult(met, condition!, text, observed, message!);
        if (!met) throw new AssertFailedException(result);
        return result;
    }

    private string ExecuteDrag(JsonElement action, int timeoutMs)
    {
        var fromRef = action.TryGetProperty("fromRef", out var frp) ? frp.GetString() : null;
        if (string.IsNullOrEmpty(fromRef)) return "Missing fromRef";

        var toRef = action.TryGetProperty("toRef", out var trp) ? trp.GetString() : null;
        int? toX  = action.TryGetProperty("toX",   out var txp) ? txp.GetInt32()  : null;
        int? toY  = action.TryGetProperty("toY",   out var typ) ? typ.GetInt32()  : null;
        var durationMs = action.TryGetProperty("durationMs", out var dp) ? dp.GetInt32() : 300;

        if (string.IsNullOrEmpty(toRef) && (toX == null || toY == null))
            return "Missing toRef or toX/toY";

        System.Drawing.Point? toPoint = null;
        string toName = toRef ?? $"({toX}, {toY})";

        if (!string.IsNullOrEmpty(toRef))
        {
            ActionExecutor.ExecuteWithRetry(
                _elementRegistry, _sessionManager, toRef,
                e => { toPoint = e.GetClickablePoint(); toName = e.Properties.Name.ValueOrDefault ?? toRef; return true; },
                timeoutMs);
        }
        else
        {
            toPoint = new System.Drawing.Point(toX!.Value, toY!.Value);
        }

        return ActionExecutor.ExecuteWithRetry(
            _elementRegistry, _sessionManager, fromRef,
            e => DragStrategy.Drag(e, fromRef, toPoint!.Value, toName, durationMs), timeoutMs);
    }

    private string ExecuteSnapshot(JsonElement action)
    {
        var handle     = action.TryGetProperty("handle", out var hp) ? hp.GetString() : null;
        var resolution = WindowResolver.ResolveOrFocused(_sessionManager, handle, registerFocused: true);

        if (resolution.Failure == WindowResolutionFailure.HandleNotFound)
            return $"Window not found: {handle}";
        if (resolution.Failure != WindowResolutionFailure.None)
            return "No window found";

        var snapshot = _snapshotBuilder.BuildSnapshot(resolution.Handle!, resolution.Window!);
        return $"\n{snapshot}";
    }
}
