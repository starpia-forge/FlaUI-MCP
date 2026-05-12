using System.Drawing;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FlaUI.Mcp.Core;

/// <summary>
/// Builds agent-friendly accessibility snapshots from UI Automation trees
/// </summary>
public class SnapshotBuilder
{
    private readonly ElementRegistry _elementRegistry;
    private readonly int _maxDepth;

    public SnapshotBuilder(ElementRegistry elementRegistry, int maxDepth = 10)
    {
        _elementRegistry = elementRegistry;
        _maxDepth = maxDepth;
    }

    public string BuildSnapshot(string windowHandle, AutomationElement root) =>
        BuildSnapshot(windowHandle, root, verbose: false);

    public string BuildSnapshot(string windowHandle, AutomationElement root, bool verbose)
    {
        // Clear previous elements for this window
        _elementRegistry.ClearWindow(windowHandle);

        var sb = new StringBuilder();
        BuildElementSnapshot(sb, windowHandle, root, 0, verbose);
        return sb.ToString();
    }

    private void BuildElementSnapshot(StringBuilder sb, string windowHandle, AutomationElement element, int depth, bool verbose)
    {
        if (depth > _maxDepth) return;

        // Skip elements with no meaningful content
        var name = GetElementName(element);
        var role = GetElementRole(element);

        // Skip some noise elements, but keep elements with names or important roles
        if (ShouldSkipElement(element, name, role)) return;

        // Register element and get ref
        var refId = _elementRegistry.Register(windowHandle, element);

        // Build the line
        var indent = new string(' ', depth * 2);
        var line = BuildElementLine(element, refId, name, role, verbose);
        sb.AppendLine($"{indent}- {line}");

        // Process children
        try
        {
            var children = element.FindAllChildren();
            foreach (var child in children)
            {
                BuildElementSnapshot(sb, windowHandle, child, depth + 1, verbose);
            }
        }
        catch { /* child enumeration failed; tree may have mutated */ }
    }

    private string BuildElementLine(AutomationElement element, string refId, string? name, string role, bool verbose)
    {
        var parts = new List<string>();

        // Role first
        parts.Add(role);

        // Name in quotes if present
        if (!string.IsNullOrEmpty(name))
        {
            parts.Add($"\"{EscapeName(name)}\"");
        }

        // Ref
        parts.Add($"[ref={refId}]");

        // Verbose: AutomationId + BoundingRect
        if (verbose)
        {
            var aid  = SafeAccess.Get(() => element.Properties.AutomationId.ValueOrDefault ?? "", "");
            var rect = SafeAccess.Get(() => element.Properties.BoundingRectangle.ValueOrDefault, default(RectangleF));
            var suffix = BuildVerboseSuffix(aid, rect);
            parts.Add(suffix);
        }

        // State indicators
        var states = GetStateIndicators(element);
        if (states.Count > 0)
        {
            parts.AddRange(states.Select(s => $"[{s}]"));
        }

        return string.Join(" ", parts);
    }

    internal static string BuildVerboseSuffix(string automationId, RectangleF rect)
    {
        var rectStr = $"rect={(int)rect.X},{(int)rect.Y},{(int)rect.Width},{(int)rect.Height}";
        return string.IsNullOrEmpty(automationId)
            ? $"[{rectStr}]"
            : $"[aid={automationId}, {rectStr}]";
    }

    internal static string GetElementRole(ControlType controlType) => Roles.ToRole(controlType);

    private string GetElementRole(AutomationElement element)
    {
        try
        {
            return GetElementRole(element.Properties.ControlType.ValueOrDefault);
        }
        catch { return "element"; /* ControlType unreadable */ }
    }

    private string? GetElementName(AutomationElement element)
    {
        try
        {
            var name = element.Properties.Name.ValueOrDefault;
            if (!string.IsNullOrWhiteSpace(name)) return name;

            // Try automation ID as fallback for identification
            var automationId = element.Properties.AutomationId.ValueOrDefault;
            if (!string.IsNullOrWhiteSpace(automationId) && automationId.Length < 50)
            {
                return $"[{automationId}]";
            }

            return null;
        }
        catch { return null; /* property read failed */ }
    }

    private List<string> GetStateIndicators(AutomationElement element)
    {
        var states = new List<string>();

        try
        {
            if (!element.Properties.IsEnabled.ValueOrDefault)
                states.Add("disabled");

            if (element.Properties.IsOffscreen.ValueOrDefault)
                states.Add("offscreen");

            // Check for readonly (ValuePattern)
            if (element.Patterns.Value.IsSupported)
            {
                var valuePattern = element.Patterns.Value.Pattern;
                if (valuePattern.IsReadOnly.ValueOrDefault)
                    states.Add("readonly");
            }

            // Check toggle state
            if (element.Patterns.Toggle.IsSupported)
            {
                var toggleState = element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
                if (toggleState == ToggleState.On)
                    states.Add("checked");
                else if (toggleState == ToggleState.Indeterminate)
                    states.Add("indeterminate");
            }

            // Check selection state
            if (element.Patterns.SelectionItem.IsSupported)
            {
                if (element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault)
                    states.Add("selected");
            }

            // Check expanded state
            if (element.Patterns.ExpandCollapse.IsSupported)
            {
                var expandState = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault;
                if (expandState == ExpandCollapseState.Expanded)
                    states.Add("expanded");
                else if (expandState == ExpandCollapseState.Collapsed)
                    states.Add("collapsed");
            }
        }
        catch { /* state pattern read failed */ }

        return states;
    }

    private bool ShouldSkipElement(AutomationElement element, string? name, string role)
    {
        // Always include named elements
        if (!string.IsNullOrEmpty(name)) return false;

        // Always include actionable element types
        if (role is "button" or "textbox" or "checkbox" or "radio" or "combobox" 
            or "listitem" or "menuitem" or "tab" or "treeitem" or "link" or "slider")
        {
            return false;
        }

        // Include structural elements that might contain others
        if (role is "window" or "group" or "list" or "tree" or "tablist" 
            or "menu" or "menubar" or "toolbar" or "grid" or "table")
        {
            return false;
        }

        // Skip decorative/structural elements without names
        if (role is "element" or "thumb" or "scrollbar" or "separator" or "titlebar")
        {
            return true;
        }

        return false;
    }

    internal static string EscapeName(string name)
    {
        return name
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }
}
