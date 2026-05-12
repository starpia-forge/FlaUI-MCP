using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FlaUI.Mcp.Core;

internal static class DialogDriver
{
    private static readonly string[] AcceptButtonNames =
        ["Open", "Save", "OK", "&Open", "&Save", "&OK", "열기", "저장", "확인"];
    private static readonly string[] CancelButtonNames =
        ["Cancel", "&Cancel", "취소"];

    internal static AutomationElement? FindFileNameEdit(AutomationElement dialog)
    {
        return SafeAccess.Get(() =>
                dialog.FindFirstDescendant(cf => cf.ByAutomationId("1148")))
            ?? SafeAccess.Get(() =>
                dialog.FindFirstDescendant(cf => cf.ByAutomationId("FileNameControlHost")))
            ?? SafeAccess.Get(() =>
                dialog.FindFirstDescendant(cf =>
                    cf.ByControlType(ControlType.Edit).And(cf.ByClassName("Edit"))));
    }

    internal static AutomationElement? FindFilterCombo(AutomationElement dialog)
    {
        return SafeAccess.Get(() =>
                dialog.FindFirstDescendant(cf => cf.ByAutomationId("1136")))
            ?? SafeAccess.Get(() =>
                dialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox)));
    }

    internal static AutomationElement? FindAcceptButton(AutomationElement dialog) =>
        FindButtonByAutomationId(dialog, "1") ?? FindButtonByNames(dialog, AcceptButtonNames);

    internal static AutomationElement? FindCancelButton(AutomationElement dialog) =>
        FindButtonByAutomationId(dialog, "2") ?? FindButtonByNames(dialog, CancelButtonNames);

    internal static AutomationElement? FindButtonByName(AutomationElement dialog, string name) =>
        SafeAccess.Get(() => dialog.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName(name))));

    private static AutomationElement? FindButtonByAutomationId(AutomationElement dialog, string aid) =>
        SafeAccess.Get(() => dialog.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByAutomationId(aid))));

    private static AutomationElement? FindButtonByNames(AutomationElement dialog, string[] names)
    {
        foreach (var name in names)
        {
            var btn = FindButtonByName(dialog, name);
            if (btn != null) return btn;
        }
        return null;
    }

    /// <summary>Returns null on success, error message on failure.</summary>
    internal static string? TrySetPath(AutomationElement dialog, string path)
    {
        var edit = FindFileNameEdit(dialog);
        if (edit == null)
            return "Could not locate file name field in dialog.";
        if (!edit.Patterns.Value.IsSupported)
            return "File name field does not support Value pattern.";
        if (SafeAccess.Get(() => edit.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault, false))
            return "File name field is read-only.";
        try { edit.Patterns.Value.Pattern.SetValue(path); return null; }
        catch (Exception ex) { return $"Failed to set path: {ex.Message}"; }
    }

    /// <summary>Returns null on success, error message on failure.</summary>
    internal static string? TryPickFilter(AutomationElement dialog, string filter)
    {
        var combo = FindFilterCombo(dialog);
        if (combo == null)
            return "Could not locate filter combo box in dialog.";

        if (combo.Patterns.Value.IsSupported &&
            !SafeAccess.Get(() => combo.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault, false))
        {
            try { combo.Patterns.Value.Pattern.SetValue(filter); return null; }
            catch { /* fall through to ExpandCollapse path */ }
        }

        if (!combo.Patterns.ExpandCollapse.IsSupported)
            return "Filter combo box does not support ExpandCollapse pattern.";

        try { combo.Patterns.ExpandCollapse.Pattern.Expand(); } catch { }

        var items = SafeAccess.Get(
            () => combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)),
            Array.Empty<AutomationElement>());

        AutomationElement? match = null;
        foreach (var item in items)
        {
            var name = SafeAccess.Get(() => item.Properties.Name.ValueOrDefault, "");
            if (string.Equals(name, filter, StringComparison.OrdinalIgnoreCase) ||
                (name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                match = item;
                break;
            }
        }

        try { combo.Patterns.ExpandCollapse.Pattern.Collapse(); } catch { }

        if (match == null)
            return $"Filter '{filter}' not found in combo box.";
        if (!match.Patterns.SelectionItem.IsSupported)
            return "Filter item does not support SelectionItem pattern.";

        try { match.Patterns.SelectionItem.Pattern.Select(); return null; }
        catch (Exception ex) { return $"Failed to select filter: {ex.Message}"; }
    }

    /// <summary>Returns null on success, error message on failure.</summary>
    internal static string? TryInvoke(AutomationElement button, string buttonLabel)
    {
        if (!button.Patterns.Invoke.IsSupported)
            return $"{buttonLabel} button does not support Invoke pattern.";
        try { button.Patterns.Invoke.Pattern.Invoke(); return null; }
        catch (Exception ex) { return $"Failed to invoke {buttonLabel}: {ex.Message}"; }
    }
}
