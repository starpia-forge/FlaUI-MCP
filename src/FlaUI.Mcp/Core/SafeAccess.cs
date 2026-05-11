namespace FlaUI.Mcp.Core;

// Inline property-get helper for UI Automation property accessors that routinely throw
// (e.g. element disposed during tree mutation, COM disconnect). Returns fallback silently
// — by design, since these failures are expected and recoverable in the caller's flow.
public static class SafeAccess
{
    public static T Get<T>(Func<T> getter, T fallback = default!)
    {
        try { return getter(); }
        catch { return fallback; }
    }
}
