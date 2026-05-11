using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FlaUI.Mcp.Core;

/// <summary>
/// Produces a plain-text UIA property and pattern dump for one element.
/// All property reads are guarded — a single throwing property never fails the whole report.
/// </summary>
internal static class InspectionRenderer
{
    public static string Render(AutomationElement e, string refId, bool includePatterns)
    {
        var sb = new StringBuilder();
        AppendIdentity(sb, e, refId);
        AppendGeometry(sb, e);
        AppendState(sb, e);
        if (includePatterns)
        {
            AppendPatterns(sb, e);
            AppendHint(sb, e, refId);
        }
        return sb.ToString().TrimEnd();
    }

    private static void AppendIdentity(StringBuilder sb, AutomationElement e, string refId)
    {
        sb.AppendLine("=== Identity ===");
        sb.AppendLine($"ref:            {refId}");

        var ct = SafeAccess.Get(() => e.Properties.ControlType.ValueOrDefault);
        sb.AppendLine($"role:           {Roles.ToRole(ct)}");

        var name = SafeAccess.Get(() => e.Properties.Name.ValueOrDefault, "");
        if (!string.IsNullOrEmpty(name))
            sb.AppendLine($"name:           \"{SnapshotBuilder.EscapeName(name)}\"");

        var aid = SafeAccess.Get(() => e.Properties.AutomationId.ValueOrDefault, "");
        sb.AppendLine($"automationId:   {(string.IsNullOrEmpty(aid) ? "(none)" : aid)}");

        var cls = SafeAccess.Get(() => e.Properties.ClassName.ValueOrDefault, "");
        sb.AppendLine($"className:      {(string.IsNullOrEmpty(cls) ? "(none)" : cls)}");

        var lt = SafeAccess.Get(() => e.Properties.LocalizedControlType.ValueOrDefault, "");
        if (!string.IsNullOrEmpty(lt))
            sb.AppendLine($"localizedType:  {lt}");

        var help = SafeAccess.Get(() => e.Properties.HelpText.ValueOrDefault, "");
        if (!string.IsNullOrEmpty(help))
            sb.AppendLine($"helpText:       {help}");

        var accel = SafeAccess.Get(() => e.Properties.AcceleratorKey.ValueOrDefault, "");
        if (!string.IsNullOrEmpty(accel))
            sb.AppendLine($"acceleratorKey: {accel}");

        var access = SafeAccess.Get(() => e.Properties.AccessKey.ValueOrDefault, "");
        if (!string.IsNullOrEmpty(access))
            sb.AppendLine($"accessKey:      {access}");

        var fw = SafeAccess.Get(() => e.Properties.FrameworkId.ValueOrDefault, "");
        if (!string.IsNullOrEmpty(fw))
            sb.AppendLine($"frameworkId:    {fw}");

        var pid = SafeAccess.Get(() => e.Properties.ProcessId.ValueOrDefault, 0);
        sb.AppendLine($"processId:      {pid}");

        var rid = SafeAccess.Get(() => e.Properties.RuntimeId.ValueOrDefault, (int[]?)null);
        if (rid != null)
            sb.AppendLine($"runtimeId:      {string.Join("-", rid)}");

        sb.AppendLine();
    }

    private static void AppendGeometry(StringBuilder sb, AutomationElement e)
    {
        sb.AppendLine("=== Geometry ===");

        var rect = SafeAccess.Get(() => e.Properties.BoundingRectangle.ValueOrDefault,
            default(System.Drawing.RectangleF));
        sb.AppendLine($"boundingRect:   x={(int)rect.X}, y={(int)rect.Y}, w={(int)rect.Width}, h={(int)rect.Height}");

        var offscreen = SafeAccess.Get(() => e.Properties.IsOffscreen.ValueOrDefault, false);
        sb.AppendLine($"isOffscreen:    {BoolStr(offscreen)}");

        string clickable;
        try
        {
            var pt = e.GetClickablePoint();
            clickable = $"({(int)pt.X}, {(int)pt.Y})";
        }
        catch
        {
            clickable = "(unavailable)";
        }
        sb.AppendLine($"clickablePoint: {clickable}");

        sb.AppendLine();
    }

    private static void AppendState(StringBuilder sb, AutomationElement e)
    {
        sb.AppendLine("=== State ===");

        var enabled = SafeAccess.Get(() => e.Properties.IsEnabled.ValueOrDefault, true);
        sb.AppendLine($"isEnabled:           {BoolStr(enabled)}");

        var kbFocusable = SafeAccess.Get(() => e.Properties.IsKeyboardFocusable.ValueOrDefault, false);
        sb.AppendLine($"isKeyboardFocusable: {BoolStr(kbFocusable)}");

        var hasFocus = SafeAccess.Get(() => e.Properties.HasKeyboardFocus.ValueOrDefault, false);
        sb.AppendLine($"hasKeyboardFocus:    {BoolStr(hasFocus)}");

        sb.AppendLine();
    }

    private static void AppendPatterns(StringBuilder sb, AutomationElement e)
    {
        sb.AppendLine("=== Patterns ===");

        // Invoke
        var hasInvoke = SafeAccess.Get(() => e.Patterns.Invoke.IsSupported, false);
        sb.AppendLine($"Invoke:         {(hasInvoke ? "supported" : "not supported")}");

        // Toggle
        if (SafeAccess.Get(() => e.Patterns.Toggle.IsSupported, false))
        {
            var state = SafeAccess.Get(() => e.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault, ToggleState.Off);
            sb.AppendLine($"Toggle:         supported (state={state})");
        }
        else sb.AppendLine("Toggle:         not supported");

        // Value
        if (SafeAccess.Get(() => e.Patterns.Value.IsSupported, false))
        {
            var val = SafeAccess.Get(() => e.Patterns.Value.Pattern.Value.ValueOrDefault ?? "", "");
            var ro = SafeAccess.Get(() => e.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault, false);
            sb.AppendLine($"Value:          supported (text=\"{SnapshotBuilder.EscapeName(val)}\", readonly={BoolStr(ro)})");
        }
        else sb.AppendLine("Value:          not supported");

        // RangeValue
        if (SafeAccess.Get(() => e.Patterns.RangeValue.IsSupported, false))
        {
            var val = SafeAccess.Get(() => e.Patterns.RangeValue.Pattern.Value.ValueOrDefault, 0.0);
            var min = SafeAccess.Get(() => e.Patterns.RangeValue.Pattern.Minimum.ValueOrDefault, 0.0);
            var max = SafeAccess.Get(() => e.Patterns.RangeValue.Pattern.Maximum.ValueOrDefault, 0.0);
            sb.AppendLine($"RangeValue:     supported (value={val:G}, min={min:G}, max={max:G})");
        }
        else sb.AppendLine("RangeValue:     not supported");

        // Selection
        if (SafeAccess.Get(() => e.Patterns.Selection.IsSupported, false))
        {
            var multi = SafeAccess.Get(() => e.Patterns.Selection.Pattern.CanSelectMultiple.ValueOrDefault, false);
            sb.AppendLine($"Selection:      supported (canMultiSelect={BoolStr(multi)})");
        }
        else sb.AppendLine("Selection:      not supported");

        // SelectionItem
        if (SafeAccess.Get(() => e.Patterns.SelectionItem.IsSupported, false))
        {
            var sel = SafeAccess.Get(() => e.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault, false);
            sb.AppendLine($"SelectionItem:  supported (isSelected={BoolStr(sel)})");
        }
        else sb.AppendLine("SelectionItem:  not supported");

        // ExpandCollapse
        if (SafeAccess.Get(() => e.Patterns.ExpandCollapse.IsSupported, false))
        {
            var state = SafeAccess.Get(() => e.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault,
                ExpandCollapseState.Collapsed);
            sb.AppendLine($"ExpandCollapse: supported (state={state})");
        }
        else sb.AppendLine("ExpandCollapse: not supported");

        // Scroll
        if (SafeAccess.Get(() => e.Patterns.Scroll.IsSupported, false))
        {
            var hPct = SafeAccess.Get(() => e.Patterns.Scroll.Pattern.HorizontalScrollPercent.ValueOrDefault, -1.0);
            var vPct = SafeAccess.Get(() => e.Patterns.Scroll.Pattern.VerticalScrollPercent.ValueOrDefault, -1.0);
            sb.AppendLine($"Scroll:         supported (hScroll={hPct:G}%, vScroll={vPct:G}%)");
        }
        else sb.AppendLine("Scroll:         not supported");

        // Window
        if (SafeAccess.Get(() => e.Patterns.Window.IsSupported, false))
        {
            var state = SafeAccess.Get(() => e.Patterns.Window.Pattern.WindowVisualState.ValueOrDefault,
                WindowVisualState.Normal);
            var canMax = SafeAccess.Get(() => e.Patterns.Window.Pattern.CanMaximize.ValueOrDefault, false);
            var canMin = SafeAccess.Get(() => e.Patterns.Window.Pattern.CanMinimize.ValueOrDefault, false);
            sb.AppendLine($"Window:         supported (state={state}, canMaximize={BoolStr(canMax)}, canMinimize={BoolStr(canMin)})");
        }
        else sb.AppendLine("Window:         not supported");

        // Transform
        if (SafeAccess.Get(() => e.Patterns.Transform.IsSupported, false))
        {
            var canMove = SafeAccess.Get(() => e.Patterns.Transform.Pattern.CanMove.ValueOrDefault, false);
            var canResize = SafeAccess.Get(() => e.Patterns.Transform.Pattern.CanResize.ValueOrDefault, false);
            sb.AppendLine($"Transform:      supported (canMove={BoolStr(canMove)}, canResize={BoolStr(canResize)})");
        }
        else sb.AppendLine("Transform:      not supported");

        // Grid
        if (SafeAccess.Get(() => e.Patterns.Grid.IsSupported, false))
        {
            var rows = SafeAccess.Get(() => e.Patterns.Grid.Pattern.RowCount.ValueOrDefault, 0);
            var cols = SafeAccess.Get(() => e.Patterns.Grid.Pattern.ColumnCount.ValueOrDefault, 0);
            sb.AppendLine($"Grid:           supported (rows={rows}, cols={cols})");
        }
        else sb.AppendLine("Grid:           not supported");

        // GridItem
        if (SafeAccess.Get(() => e.Patterns.GridItem.IsSupported, false))
        {
            var row = SafeAccess.Get(() => e.Patterns.GridItem.Pattern.Row.ValueOrDefault, 0);
            var col = SafeAccess.Get(() => e.Patterns.GridItem.Pattern.Column.ValueOrDefault, 0);
            var rs = SafeAccess.Get(() => e.Patterns.GridItem.Pattern.RowSpan.ValueOrDefault, 1);
            var cs = SafeAccess.Get(() => e.Patterns.GridItem.Pattern.ColumnSpan.ValueOrDefault, 1);
            sb.AppendLine($"GridItem:       supported (row={row}, col={col}, rowSpan={rs}, colSpan={cs})");
        }
        else sb.AppendLine("GridItem:       not supported");

        // Text (presence only — content via windows_get_text)
        var hasText = SafeAccess.Get(() => e.Patterns.Text.IsSupported, false);
        sb.AppendLine($"Text:           {(hasText ? "supported" : "not supported")}");

        sb.AppendLine();
    }

    private static void AppendHint(StringBuilder sb, AutomationElement e, string refId)
    {
        string hint;

        if (SafeAccess.Get(() => e.Patterns.Invoke.IsSupported, false))
            hint = $"Supports Invoke — use windows_click {{\"ref\":\"{refId}\"}} to activate.";
        else if (SafeAccess.Get(() => e.Patterns.Toggle.IsSupported, false))
            hint = $"Supports Toggle — use windows_click to cycle its state.";
        else if (SafeAccess.Get(() => e.Patterns.Value.IsSupported, false) &&
                 !SafeAccess.Get(() => e.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault, false))
            hint = $"Accepts text input — use windows_fill {{\"ref\":\"{refId}\",\"text\":\"...\"}} to set.";
        else if (SafeAccess.Get(() => e.Patterns.ExpandCollapse.IsSupported, false))
            hint = "Expandable node — use windows_click to expand or collapse.";
        else if (SafeAccess.Get(() => e.Patterns.SelectionItem.IsSupported, false))
            hint = $"Selectable item — use windows_click {{\"ref\":\"{refId}\"}} to select.";
        else if (SafeAccess.Get(() => e.Patterns.Scroll.IsSupported, false))
            hint = $"Scroll container — use windows_scroll {{\"ref\":\"{refId}\",...}} to scroll within it.";
        else
            hint = "No standard interaction patterns — element may be display-only.";

        sb.AppendLine($"Hint: {hint}");
    }

    private static string BoolStr(bool value) => value ? "true" : "false";
}
