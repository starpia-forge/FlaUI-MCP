using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FlaUI.Mcp.Core;

/// <summary>
/// Maps element refs (like "w1e5") to AutomationElements.
/// Stores locator metadata for stale-element re-resolution.
/// Refs are scoped to windows and regenerated on each snapshot.
/// </summary>
public class ElementRegistry
{
    /// <summary>
    /// A registered element together with locator metadata for re-resolution.
    /// </summary>
    public class Entry
    {
        public AutomationElement Element { get; set; } = null!;
        public string WindowHandle { get; init; } = "";
        public string? AutomationId { get; init; }
        public string? Name { get; init; }
        public ControlType ControlType { get; init; }

        /// <summary>
        /// Attempt to re-find the element in the window using stored locator metadata.
        /// Returns null if re-resolution fails.
        /// </summary>
        public AutomationElement? TryResolve(SessionManager session)
        {
            var window = session.GetWindow(WindowHandle);
            if (window == null) return null;
            try
            {
                if (!string.IsNullOrEmpty(AutomationId))
                {
                    var byId = window.FindFirstDescendant(cf => cf.ByAutomationId(AutomationId));
                    if (byId != null) return byId;
                }
                if (!string.IsNullOrEmpty(Name))
                {
                    return window.FindFirstDescendant(cf =>
                        cf.ByName(Name).And(cf.ByControlType(ControlType)));
                }
            }
            catch { /* tree is mutating; bail */ }
            return null;
        }
    }

    private readonly Dictionary<string, Entry> _entries = new();
    private readonly Dictionary<string, int> _windowCounters = new();
    private readonly object _sync = new();

    /// <summary>
    /// Register a test entry without a real AutomationElement (for unit tests only).
    /// </summary>
    internal string RegisterForTest(string windowHandle, string? autoId = null,
        string? name = null, ControlType ct = ControlType.Custom)
    {
        lock (_sync)
        {
            if (!_windowCounters.ContainsKey(windowHandle)) _windowCounters[windowHandle] = 0;
            var refId = $"{windowHandle}e{++_windowCounters[windowHandle]}";
            _entries[refId] = new Entry
            {
                Element = null!,
                WindowHandle = windowHandle,
                AutomationId = autoId,
                Name = name,
                ControlType = ct
            };
            return refId;
        }
    }

    /// <summary>
    /// Register an element and return its ref, capturing locator metadata.
    /// </summary>
    public string Register(string windowHandle, AutomationElement element)
    {
        string? autoId = null, name = null;
        var ctrlType = ControlType.Custom;
        try { autoId = element.Properties.AutomationId.ValueOrDefault; } catch { }
        try { name = element.Properties.Name.ValueOrDefault; } catch { }
        try { ctrlType = element.Properties.ControlType.ValueOrDefault; } catch { }

        lock (_sync)
        {
            if (!_windowCounters.ContainsKey(windowHandle)) _windowCounters[windowHandle] = 0;
            var refId = $"{windowHandle}e{++_windowCounters[windowHandle]}";
            _entries[refId] = new Entry
            {
                Element = element,
                WindowHandle = windowHandle,
                AutomationId = autoId,
                Name = name,
                ControlType = ctrlType
            };
            return refId;
        }
    }

    /// <summary>
    /// Get the full Entry (element + locator) for a ref.
    /// </summary>
    public Entry? GetEntry(string refId)
    {
        lock (_sync) { return _entries.TryGetValue(refId, out var e) ? e : null; }
    }

    /// <summary>
    /// Get the AutomationElement for a ref (backward-compatible shim).
    /// </summary>
    public AutomationElement? GetElement(string refId) => GetEntry(refId)?.Element;

    /// <summary>
    /// Check if a ref exists.
    /// </summary>
    public bool HasElement(string refId)
    {
        lock (_sync) { return _entries.ContainsKey(refId); }
    }

    /// <summary>
    /// Clear all elements for a window (called before new snapshot).
    /// </summary>
    public void ClearWindow(string windowHandle)
    {
        var prefix = windowHandle + "e";
        lock (_sync)
        {
            var keys = _entries.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var k in keys) _entries.Remove(k);
            _windowCounters[windowHandle] = 0;
        }
    }
}
