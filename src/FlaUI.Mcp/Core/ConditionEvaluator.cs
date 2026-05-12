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
        "checked", "unchecked",
        "valueEquals", "expanded", "focused", "selectionContains"
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

            case "valueEquals":
            {
                if (element == null) return (false, "element not found");
                var actual = ReadValuePatternString(element);
                if (actual is null)
                    return (false, "value pattern not supported");
                return (actual == (text ?? ""), $"\"{actual}\"");
            }

            case "expanded":
            {
                if (element == null) return (false, "element not found");
                var state = SafeAccess.Get(
                    () => element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault,
                    ExpandCollapseState.Collapsed);
                var expanded = state == ExpandCollapseState.Expanded;
                return (expanded, expanded ? "expanded" : state.ToString().ToLowerInvariant());
            }

            case "focused":
            {
                if (element == null) return (false, "element not found");
                var hasFocus = SafeAccess.Get(() => element.Properties.HasKeyboardFocus.ValueOrDefault, false);
                return (hasFocus, hasFocus ? "focused" : "not focused");
            }

            case "selectionContains":
            {
                if (element == null) return (false, "element not found");
                return SelectionContainsName(element, text ?? "");
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
    /// Map snapshot role strings (e.g. "button") to ControlType.
    /// Delegates to <see cref="Roles.ToControlType"/>.
    /// </summary>
    public static ControlType RoleToControlType(string role) => Roles.ToControlType(role);

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

    private static string? ReadValuePatternString(AutomationElement element)
    {
        try
        {
            if (element.Patterns.Value.IsSupported)
                return SafeAccess.Get(() => element.Patterns.Value.Pattern.Value.ValueOrDefault, null);
            if (element.Patterns.RangeValue.IsSupported)
                return element.Patterns.RangeValue.Pattern.Value.ValueOrDefault
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch { /* pattern not supported on this element */ }
        return null;
    }

    private static (bool found, string observed) SelectionContainsName(
        AutomationElement element, string targetName)
    {
        try
        {
            if (!SafeAccess.Get(() => element.Patterns.Selection.IsSupported, false))
                return (false, "selection pattern not supported");

            var children = element.FindAllChildren();
            var selectedNames = children
                .Where(c => SafeAccess.Get(() => c.Patterns.SelectionItem.IsSupported, false) &&
                            SafeAccess.Get(() => c.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault, false))
                .Select(c => SafeAccess.Get(() => c.Properties.Name.ValueOrDefault ?? "", ""))
                .ToArray();

            if (selectedNames.Length == 0)
                return (false, "selection empty");

            var found = selectedNames.Any(n => n == targetName);
            var preview = string.Join(", ", selectedNames.Take(3).Select(n => $"\"{n}\""));
            return (found, $"selected: [{preview}{(selectedNames.Length > 3 ? ", ..." : "")}]");
        }
        catch
        {
            return (false, "selection pattern not supported");
        }
    }
}
