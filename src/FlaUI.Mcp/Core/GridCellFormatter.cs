using FlaUI.Core.AutomationElements;

namespace FlaUI.Mcp.Core;

internal static class GridCellFormatter
{
    internal static string FormatLine(string refId, AutomationElement cell)
    {
        var role = SafeAccess.Get(
            () => Roles.ToRole(cell.Properties.ControlType.ValueOrDefault),
            "element");
        var name = SafeAccess.Get(() => cell.Properties.Name.ValueOrDefault ?? "", "");
        return FormatLineRaw(refId, role, name);
    }

    internal static string FormatLineRaw(string refId, string role, string name) =>
        string.IsNullOrEmpty(name)
            ? $"[ref={refId}] {role}"
            : $"[ref={refId}] {role} \"{SnapshotBuilder.EscapeName(name)}\"";
}
