using FlaUI.Core.AutomationElements;

namespace FlaUI.Mcp.Core;

// Not thread-safe — assumes single-threaded sequential tool dispatch (MCP stdio loop).
public static class ElementResolver
{
    public static AutomationElement? Resolve(
        SessionManager session, ElementRegistry registry,
        string? refId, string? handle, Selector selector)
    {
        if (!string.IsNullOrEmpty(refId))
        {
            var entry = registry.GetEntry(refId);
            if (entry == null) return null;
            try
            {
                _ = entry.Element.Properties.IsEnabled.ValueOrDefault;
                return entry.Element;
            }
            catch
            {
                var refreshed = entry.TryResolve(session);
                if (refreshed != null) entry.Element = refreshed;
                return refreshed;
            }
        }

        if (string.IsNullOrEmpty(handle)) return null;
        var window = session.GetWindow(handle);
        if (window == null) return null;
        return ConditionEvaluator.FindBySelector(window, selector.Name, selector.AutomationId, selector.Role);
    }
}
