using System.Text.Json;

namespace FlaUI.Mcp.Core;

public readonly record struct Selector(string? Name, string? AutomationId, string? Role)
{
    public bool IsEmpty => Name is null && AutomationId is null && Role is null;

    public static Selector From(JsonElement parent, string propertyName = "selector")
    {
        if (parent.ValueKind != JsonValueKind.Object) return default;
        if (!parent.TryGetProperty(propertyName, out var sel)) return default;
        string? name   = sel.TryGetProperty("name",         out var n) ? n.GetString() : null;
        string? autoId = sel.TryGetProperty("automationId", out var a) ? a.GetString() : null;
        string? role   = sel.TryGetProperty("role",         out var r) ? r.GetString() : null;
        return new Selector(name, autoId, role);
    }
}
