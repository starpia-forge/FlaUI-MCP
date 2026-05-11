using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FlaUI.Mcp.Core;

/// <summary>
/// Evaluates a named condition against an AutomationElement (which may be null).
/// Shared by WaitForTool (polling) and AssertTool (single-shot).
/// </summary>
public static class ConditionEvaluator
{
    public static readonly IReadOnlyList<string> ValidConditions =
    [
        "visible", "hidden", "enabled", "disabled",
        "exists", "missing",
        "textEquals", "textContains",
        "checked", "unchecked"
    ];

    /// <summary>
    /// Returns (met, human-readable description of last observed state).
    /// A null element is valid input: conditions that require presence return false.
    /// </summary>
    public static (bool met, string lastObserved) Evaluate(
        AutomationElement? element, string condition, string? text)
    {
        switch (condition)
        {
            case "exists":
                return (element != null,
                    element != null ? "element found" : "element not found");

            case "missing":
                return (element == null,
                    element == null ? "element absent" : "element present");

            case "visible":
            {
                if (element == null) return (false, "element not found");
                var offscreen = SafeAccess.Get(() => element.Properties.IsOffscreen.ValueOrDefault);
                return (!offscreen, offscreen ? "offscreen" : "visible");
            }

            case "hidden":
            {
                if (element == null) return (true, "element not found (counts as hidden)");
                var offscreen = SafeAccess.Get(() => element.Properties.IsOffscreen.ValueOrDefault);
                return (offscreen, offscreen ? "offscreen" : "visible");
            }

            case "enabled":
            {
                if (element == null) return (false, "element not found");
                var enabled = SafeAccess.Get(() => element.Properties.IsEnabled.ValueOrDefault, fallback: true);
                return (enabled, enabled ? "enabled" : "disabled");
            }

            case "disabled":
            {
                if (element == null) return (false, "element not found");
                var enabled = SafeAccess.Get(() => element.Properties.IsEnabled.ValueOrDefault, fallback: true);
                return (!enabled, enabled ? "enabled" : "disabled");
            }

            case "textEquals":
            {
                if (element == null) return (false, "element not found");
                var actual = TextExtractor.GetText(element);
                return (actual == (text ?? ""), $"\"{actual}\"");
            }

            case "textContains":
            {
                if (element == null) return (false, "element not found");
                var actual = TextExtractor.GetText(element);
                return (actual.Contains(text ?? "", StringComparison.Ordinal), $"\"{actual}\"");
            }

            case "checked":
            {
                if (element == null) return (false, "element not found");
                var isChecked = ReadCheckedState(element);
                return (isChecked, isChecked ? "checked" : "unchecked");
            }

            case "unchecked":
            {
                if (element == null) return (false, "element not found");
                var isChecked = ReadCheckedState(element);
                return (!isChecked, isChecked ? "checked" : "unchecked");
            }

            default:
                return (false, $"unknown condition '{condition}'");
        }
    }

    /// <summary>
    /// Locate a descendant within a window by selector fields (name, automationId, role).
    /// Returns null if not found or on error.
    /// </summary>
    public static AutomationElement? FindBySelector(
        AutomationElement window,
        string? name, string? automationId, string? role)
    {
        try
        {
            if (!string.IsNullOrEmpty(automationId))
                return window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(role))
            {
                var ct = RoleToControlType(role);
                return window.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ct)));
            }

            if (!string.IsNullOrEmpty(name))
                return window.FindFirstDescendant(cf => cf.ByName(name));
        }
        catch { /* selector matched nothing or tree mutated */ }
        return null;
    }

    /// <summary>
    /// Map snapshot role strings (e.g. "button") to ControlType — inverse of SnapshotBuilder.GetElementRole.
    /// </summary>
    public static ControlType RoleToControlType(string role) => role.ToLowerInvariant() switch
    {
        "button"      => ControlType.Button,
        "textbox"     => ControlType.Edit,
        "text"        => ControlType.Text,
        "checkbox"    => ControlType.CheckBox,
        "radio"       => ControlType.RadioButton,
        "combobox"    => ControlType.ComboBox,
        "list"        => ControlType.List,
        "listitem"    => ControlType.ListItem,
        "menu"        => ControlType.Menu,
        "menuitem"    => ControlType.MenuItem,
        "menubar"     => ControlType.MenuBar,
        "tree"        => ControlType.Tree,
        "treeitem"    => ControlType.TreeItem,
        "tablist"     => ControlType.Tab,
        "tab"         => ControlType.TabItem,
        "table"       => ControlType.Table,
        "row"         => ControlType.DataItem,
        "slider"      => ControlType.Slider,
        "spinbutton"  => ControlType.Spinner,
        "progressbar" => ControlType.ProgressBar,
        "link"        => ControlType.Hyperlink,
        "group"       => ControlType.Group,
        "window"      => ControlType.Window,
        "document"    => ControlType.Document,
        "toolbar"     => ControlType.ToolBar,
        "grid"        => ControlType.DataGrid,
        _             => ControlType.Custom
    };

    private static bool ReadCheckedState(AutomationElement element)
    {
        try
        {
            if (element.Patterns.Toggle.IsSupported)
                return element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault == ToggleState.On;
            if (element.Patterns.SelectionItem.IsSupported)
                return element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault;
        }
        catch { /* pattern not supported on this element */ }
        return false;
    }
}
