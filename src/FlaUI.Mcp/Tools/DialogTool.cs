using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

public class DialogTool : ToolBase
{
    private readonly SessionManager _session;
    private const int DefaultTimeoutMs = 5000;
    private static readonly string[] ValidActions = ["wait", "accept", "cancel", "click"];

    public DialogTool(SessionManager session) => _session = session;

    public override string Name => "windows_dialog";

    public override string Description =>
        "Drive Win32 common dialogs (#32770: File Open/Save, message boxes). " +
        "Polls for or resolves a dialog, optionally fills path / picks filter, then invokes the " +
        "terminal button. action='wait' returns the popup handle without taking action; 'accept' " +
        "presses Open/Save/OK; 'cancel' presses Cancel; 'click' presses the button named by 'button'. " +
        "Path is set via the Value pattern; filter via SelectionItem on the type combo. " +
        "Dialog is auto-registered as an 'm*' popup handle usable with windows_snapshot.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            action = new
            {
                type = "string",
                @enum = ValidActions,
                description = "wait | accept | cancel | click"
            },
            handle = new
            {
                type = "string",
                description = "Optional pre-registered popup handle (e.g. 'm1'). Omit to auto-detect or poll for a new dialog."
            },
            path = new
            {
                type = "string",
                description = "Optional file path to enter in the File name field before accepting."
            },
            filter = new
            {
                type = "string",
                description = "Optional filter combo item to select (exact or contains match on item name)."
            },
            button = new
            {
                type = "string",
                description = "Required for action='click': the button name to invoke (e.g. 'Yes', 'No', 'Don\\'t Save')."
            },
            timeout = new
            {
                type = "integer",
                description = $"Polling timeout in ms (default {DefaultTimeoutMs}). Ignored when 'handle' is provided."
            }
        },
        required = new[] { "action" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var action = GetStringArgument(arguments, "action");
        if (string.IsNullOrEmpty(action) || !ValidActions.Contains(action))
            return Task.FromResult(ErrorResult(
                $"Invalid or missing action. Use one of: {string.Join(", ", ValidActions)}."));

        var handle    = GetStringArgument(arguments, "handle");
        var path      = GetStringArgument(arguments, "path");
        var filter    = GetStringArgument(arguments, "filter");
        var button    = GetStringArgument(arguments, "button");
        var timeoutMs = GetArgument<int?>(arguments, "timeout") ?? DefaultTimeoutMs;

        if (action == "click" && string.IsNullOrEmpty(button))
            return Task.FromResult(ErrorResult("action='click' requires 'button' argument."));

        AutomationElement? dialog;
        string activeHandle;

        if (!string.IsNullOrEmpty(handle))
        {
            dialog = _session.GetPopup(handle);
            if (dialog == null)
                return Task.FromResult(ErrorResult($"No popup registered for handle: {handle}"));
            activeHandle = handle;
        }
        else
        {
            dialog = _session.FindActiveDialog();
            if (dialog == null)
            {
                var baseline = _session.SnapshotTopLevelDialogs();
                dialog = _session.PollForNewDialog(baseline, timeoutMs);
            }
            if (dialog == null)
                return Task.FromResult(ErrorResult(
                    $"No #32770 dialog appeared within {timeoutMs}ms."));
            activeHandle = _session.RegisterPopup(dialog);
        }

        if (!string.IsNullOrEmpty(path))
        {
            var err = DialogDriver.TrySetPath(dialog, path);
            if (err != null) return Task.FromResult(ErrorResult(err));
        }
        if (!string.IsNullOrEmpty(filter))
        {
            var err = DialogDriver.TryPickFilter(dialog, filter);
            if (err != null) return Task.FromResult(ErrorResult(err));
        }

        switch (action)
        {
            case "wait":
                return Task.FromResult(TextResult(
                    $"Dialog detected. Popup registered: {activeHandle}. " +
                    $"Use windows_snapshot {{ \"handle\": \"{activeHandle}\" }} to inspect the dialog tree."));

            case "accept":
                var ok = DialogDriver.FindAcceptButton(dialog);
                if (ok == null) return Task.FromResult(ErrorResult("Accept button not found on dialog."));
                var e1 = DialogDriver.TryInvoke(ok, "Accept");
                if (e1 != null) return Task.FromResult(ErrorResult(e1));
                _session.ClearPopup(activeHandle);
                return Task.FromResult(TextResult($"Accepted dialog {activeHandle}."));

            case "cancel":
                var cancelBtn = DialogDriver.FindCancelButton(dialog);
                if (cancelBtn == null) return Task.FromResult(ErrorResult("Cancel button not found on dialog."));
                var e2 = DialogDriver.TryInvoke(cancelBtn, "Cancel");
                if (e2 != null) return Task.FromResult(ErrorResult(e2));
                _session.ClearPopup(activeHandle);
                return Task.FromResult(TextResult($"Cancelled dialog {activeHandle}."));

            case "click":
                var btn = DialogDriver.FindButtonByName(dialog, button!);
                if (btn == null) return Task.FromResult(ErrorResult($"Button '{button}' not found on dialog."));
                var e3 = DialogDriver.TryInvoke(btn, button!);
                if (e3 != null) return Task.FromResult(ErrorResult(e3));
                _session.ClearPopup(activeHandle);
                return Task.FromResult(TextResult($"Clicked '{button}' on dialog {activeHandle}."));

            default:
                return Task.FromResult(ErrorResult($"Unhandled action: {action}"));
        }
    }
}
