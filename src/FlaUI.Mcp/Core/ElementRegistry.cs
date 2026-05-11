using FlaUI.Core.AutomationElements;

namespace PlaywrightWindows.Mcp.Core;

/// <summary>
/// Maps element refs (like "w1e5") to AutomationElements.
/// Refs are scoped to windows and regenerated on each snapshot.
/// </summary>
public class ElementRegistry
{
    private readonly Dictionary<string, AutomationElement> _elements = new();
    private readonly Dictionary<string, int> _windowCounters = new();
    private readonly object _sync = new();

    /// <summary>
    /// Clear all elements for a window (called before new snapshot)
    /// </summary>
    public void ClearWindow(string windowHandle)
    {
        var prefix = windowHandle + "e";
        lock (_sync)
        {
            var keysToRemove = _elements.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
            {
                _elements.Remove(key);
            }
            _windowCounters[windowHandle] = 0;
        }
    }

    /// <summary>
    /// Register an element and return its ref
    /// </summary>
    public string Register(string windowHandle, AutomationElement element)
    {
        lock (_sync)
        {
            if (!_windowCounters.ContainsKey(windowHandle))
            {
                _windowCounters[windowHandle] = 0;
            }

            var refId = $"{windowHandle}e{++_windowCounters[windowHandle]}";
            _elements[refId] = element;
            return refId;
        }
    }

    /// <summary>
    /// Get an element by its ref
    /// </summary>
    public AutomationElement? GetElement(string refId)
    {
        lock (_sync)
        {
            return _elements.TryGetValue(refId, out var element) ? element : null;
        }
    }

    /// <summary>
    /// Check if a ref exists
    /// </summary>
    public bool HasElement(string refId)
    {
        lock (_sync)
        {
            return _elements.ContainsKey(refId);
        }
    }
}
