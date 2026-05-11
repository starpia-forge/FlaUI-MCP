using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace FlaUI.Mcp.Core;

public readonly record struct AttachedWindow(
    string Handle,
    IntPtr Hwnd,
    string Title,
    bool IsVisible,
    int OwnerPid);

/// <summary>
/// Manages UI Automation sessions and launched applications
/// </summary>
public class SessionManager : IDisposable
{
    private const string Win32PopupMenuClass = "#32768";

    private readonly UIA3Automation _automation;
    private readonly Dictionary<string, Window> _windows = new();
    private readonly Dictionary<IntPtr, string> _hwndToHandle = new();
    private readonly Dictionary<string, AutomationElement> _popups = new();
    private readonly object _sync = new();
    private int _windowCounter = 0;
    private int _popupCounter = 0;

    public SessionManager()
    {
        _automation = new UIA3Automation();
    }

    public UIA3Automation Automation => _automation;

    public (string handle, Window window) LaunchApp(string appPath, string[]? args = null)
    {
        var desktop = _automation.GetDesktop();

        // Snapshot existing top-level window handles BEFORE launching.
        // HWND comparison is language-independent and works for UWP host processes.
        var preExistingHwnds = GetTopLevelHwnds(desktop);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = appPath,
            Arguments = args != null ? string.Join(" ", args) : "",
            UseShellExecute = true
        };

        var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
        {
            throw new Exception($"Failed to start process: {appPath}");
        }

        try
        {
            process.WaitForInputIdle(5000);
        }
        catch { /* Some processes don't support this */ }

        Thread.Sleep(1000);

        Window? window = null;

        // First try: match by PID (works for classic Win32/WPF/WinForms apps)
        var element = desktop.FindFirstDescendant(cf => cf.ByProcessId(process.Id));
        if (element != null)
            window = element.AsWindow();

        // Second try: find any top-level window whose HWND wasn't present before launch.
        // This catches UWP apps hosted by ApplicationFrameHost.exe (e.g. calc.exe → 계산기).
        if (window == null)
        {
            for (int i = 0; i < 10 && window == null; i++)
            {
                Thread.Sleep(500);
                window = FindNewWindow(desktop, preExistingHwnds);
            }
        }

        // Third try: fall back to title substring (already-running apps that brought an
        // existing window to focus, where the HWND is not new).
        if (window == null)
        {
            var appName = Path.GetFileNameWithoutExtension(appPath).ToLowerInvariant();
            window = desktop
                .FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                .Select(w => w.AsWindow())
                .FirstOrDefault(w => w?.Title?.ToLowerInvariant().Contains(appName) == true);
        }

        if (window == null)
        {
            throw new Exception($"Could not find window for {appPath}. Try using windows_list_windows and windows_focus instead.");
        }

        var windowHandle = RegisterWindow(window);
        return (windowHandle, window);
    }

    public (string handle, Window window) AttachToWindow(string title)
    {
        var desktop = _automation.GetDesktop();
        var window = desktop.FindFirstDescendant(cf => cf.ByName(title))?.AsWindow();

        if (window == null)
        {
            throw new Exception($"Window not found: {title}");
        }

        var handle = RegisterWindow(window);
        return (handle, window);
    }

    public List<AttachedWindow> AttachByProcess(int? pid, string? processName)
    {
        var (resolvedPid, label) = ResolveTargetPid(pid, processName);
        var windows = EnumerateWindowsForPid(resolvedPid);

        if (windows.Count == 0)
            throw new Exception(
                $"Process '{label}' (pid={resolvedPid}) has no UIA-visible windows. " +
                $"It may be running headless, as a message-only window, or with only a tray icon. " +
                $"If it has a tray icon, use windows_tray_list / windows_tray_invoke to surface its window first.");

        var result = new List<AttachedWindow>(windows.Count);
        foreach (var window in windows)
        {
            var hwnd = SafeAccess.Get(() => window.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);
            var title = SafeAccess.Get(() => window.Title ?? string.Empty, string.Empty);
            var isVisible = !SafeAccess.Get(() => window.Properties.IsOffscreen.ValueOrDefault, false);
            var handle = RegisterWindow(window);
            result.Add(new AttachedWindow(handle, hwnd, title, isVisible, resolvedPid));
        }
        return result;
    }

    public string RegisterWindow(Window window)
    {
        var hwnd = SafeAccess.Get(() => window.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);

        lock (_sync)
        {
            if (hwnd != IntPtr.Zero && _hwndToHandle.TryGetValue(hwnd, out var existing))
            {
                _windows[existing] = window;
                return existing;
            }
            var handle = $"w{++_windowCounter}";
            _windows[handle] = window;
            if (hwnd != IntPtr.Zero) _hwndToHandle[hwnd] = handle;
            return handle;
        }
    }

    public Window? GetWindow(string handle)
    {
        lock (_sync)
        {
            return _windows.TryGetValue(handle, out var window) ? window : null;
        }
    }

    /// <summary>
    /// Returns the snapshot root element for any handle — both window (w*) and popup (m*) handles.
    /// </summary>
    public AutomationElement? GetSnapshotRoot(string handle)
    {
        lock (_sync)
        {
            if (_windows.TryGetValue(handle, out var win)) return win;
            if (_popups.TryGetValue(handle, out var popup)) return popup;
            return null;
        }
    }

    /// <summary>
    /// Register a transient popup (context menu, tooltip, etc.) and return a handle like "m1".
    /// Popups are keyed by a separate counter from windows and stored as raw AutomationElements.
    /// </summary>
    public string RegisterPopup(AutomationElement element)
    {
        lock (_sync)
        {
            var handle = $"m{++_popupCounter}";
            _popups[handle] = element;
            return handle;
        }
    }

    /// <summary>Returns the popup element for a given "m*" handle, or null if not registered.</summary>
    public AutomationElement? GetPopup(string handle)
    {
        lock (_sync) { return _popups.TryGetValue(handle, out var p) ? p : null; }
    }

    /// <summary>Remove a popup from the registry (call after dismissal).</summary>
    public void ClearPopup(string handle)
    {
        lock (_sync) { _popups.Remove(handle); }
    }

    /// <summary>
    /// Snapshot HWNDs of all currently-visible Win32 popup menus on the desktop.
    /// Used as a baseline before triggering a right-click that may produce a context menu.
    /// </summary>
    public HashSet<IntPtr> SnapshotTopLevelMenus()
    {
        var desktop = _automation.GetDesktop();
        return new HashSet<IntPtr>(
            desktop
                .FindAllChildren(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu)
                      .Or(cf.ByClassName(Win32PopupMenuClass)))
                .Select(e => SafeAccess.Get(() => e.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero))
                .Where(h => h != IntPtr.Zero));
    }

    /// <summary>
    /// Poll the desktop for a new popup menu that wasn't in <paramref name="baseline"/>.
    /// Returns the new menu element or null if none appears within <paramref name="timeoutMs"/>.
    /// </summary>
    public AutomationElement? PollForNewMenu(HashSet<IntPtr> baseline, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var desktop = _automation.GetDesktop();
        while (DateTime.UtcNow < deadline)
        {
            var menu = FindNewMenu(desktop, baseline);
            if (menu != null) return menu;
            Thread.Sleep(100);
        }
        return null;
    }

    private static AutomationElement? FindNewMenu(AutomationElement desktop, HashSet<IntPtr> baseline)
    {
        foreach (var e in desktop.FindAllChildren(cf =>
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu)
              .Or(cf.ByClassName(Win32PopupMenuClass))))
        {
            var hwnd = SafeAccess.Get(() => e.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);
            if (hwnd != IntPtr.Zero && !baseline.Contains(hwnd))
                return e;
        }
        return null;
    }

    public List<(string handle, string title, string? processName)> ListWindows(bool includeHidden = false)
    {
        var desktop = _automation.GetDesktop();
        var windows = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

        var result = new List<(string, string, string?)>();
        foreach (var w in windows)
        {
            var window = w.AsWindow();
            if (window != null && (includeHidden || !string.IsNullOrEmpty(window.Title)))
            {
                var handle = RegisterWindow(window);
                string? processName = null;
                try
                {
                    processName = window.Properties.ProcessId.TryGetValue(out var pid)
                        ? System.Diagnostics.Process.GetProcessById(pid).ProcessName
                        : null;
                }
                catch { /* process exited or access denied — best effort process name */ }

                result.Add((handle, window.Title, processName));
            }
        }
        return result;
    }

    public void FocusWindow(string handle)
    {
        var window = GetWindow(handle);
        if (window == null)
        {
            throw new Exception($"Window not found: {handle}");
        }
        window.Focus();
    }

    public void CloseWindow(string handle)
    {
        var window = GetWindow(handle);
        if (window == null)
        {
            throw new Exception($"Window not found: {handle}");
        }

        var hwnd = SafeAccess.Get(() => window.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);

        lock (_sync)
        {
            if (hwnd != IntPtr.Zero) _hwndToHandle.Remove(hwnd);
            _windows.Remove(handle);
        }

        window.Close();
    }

    internal HashSet<IntPtr> SnapshotTopLevelHwnds() =>
        GetTopLevelHwnds(_automation.GetDesktop());

    internal Window? PollForNewWindow(HashSet<IntPtr> baseline, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var desktop = _automation.GetDesktop();
        while (DateTime.UtcNow < deadline)
        {
            var wnd = FindNewWindow(desktop, baseline);
            if (wnd != null) return wnd;
            Thread.Sleep(200);
        }
        return null;
    }

    private static HashSet<IntPtr> GetTopLevelHwnds(FlaUI.Core.AutomationElements.AutomationElement desktop) =>
        new(desktop
            .FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
            .Select(w => SafeAccess.Get(() => w.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero))
            .Where(h => h != IntPtr.Zero));

    private static Window? FindNewWindow(FlaUI.Core.AutomationElements.AutomationElement desktop, HashSet<IntPtr> preExistingHwnds)
    {
        foreach (var w in desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)))
        {
            var hwnd = SafeAccess.Get(() => w.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);

            if (hwnd != IntPtr.Zero && !preExistingHwnds.Contains(hwnd))
            {
                var win = w.AsWindow();
                if (win != null && !string.IsNullOrEmpty(win.Title))
                    return win;
            }
        }
        return null;
    }

    private static (int pid, string label) ResolveTargetPid(int? pid, string? processName)
    {
        if (pid.HasValue && !string.IsNullOrEmpty(processName))
            throw new Exception("Provide exactly one of 'pid' or 'processName', not both.");
        if (!pid.HasValue && string.IsNullOrEmpty(processName))
            throw new Exception("Provide either 'pid' or 'processName'.");

        if (pid.HasValue)
        {
            try
            {
                using var p = System.Diagnostics.Process.GetProcessById(pid.Value);
                return (pid.Value, p.ProcessName);
            }
            catch (ArgumentException)
            {
                throw new Exception($"No running process with pid={pid.Value}.");
            }
        }

        var name = processName!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;
        var processes = System.Diagnostics.Process.GetProcessesByName(name);
        try
        {
            if (processes.Length == 0)
                throw new Exception($"No running process named '{name}'.");
            if (processes.Length > 1)
            {
                var details = string.Join(", ", processes.Select(p =>
                {
                    var title = SafeMainTitle(p);
                    return string.IsNullOrEmpty(title) ? $"pid={p.Id}" : $"pid={p.Id} (\"{title}\")";
                }));
                throw new Exception($"Ambiguous process name '{name}': {details}. Re-call with explicit pid.");
            }
            return (processes[0].Id, name);
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    private List<Window> EnumerateWindowsForPid(int pid)
    {
        var seen = new HashSet<IntPtr>();
        var result = new List<Window>();

        // Stage 1: FlaUI Application API — returns top-level windows even if hidden
        try
        {
            using var app = FlaUI.Core.Application.Attach(pid);
            foreach (var w in app.GetAllTopLevelWindows(_automation))
            {
                if (w == null) continue;
                var hwnd = SafeAccess.Get(() => w.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);
                if (hwnd != IntPtr.Zero && seen.Add(hwnd))
                    result.Add(w);
            }
        }
        catch { /* elevated process or exited between resolve and attach — fall through */ }

        // Stage 2: Desktop UIA child walk — catches any window UIA exposes as desktop child
        if (result.Count == 0)
        {
            var desktop = _automation.GetDesktop();
            foreach (var e in desktop.FindAllChildren(cf =>
                cf.ByProcessId(pid).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))))
            {
                var w = e.AsWindow();
                if (w == null) continue;
                var hwnd = SafeAccess.Get(() => w.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);
                if (hwnd != IntPtr.Zero && seen.Add(hwnd))
                    result.Add(w);
            }
        }

        return result;
    }

    private static string SafeMainTitle(System.Diagnostics.Process p)
    {
        try { return p.MainWindowTitle ?? string.Empty; }
        catch { return string.Empty; }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _hwndToHandle.Clear();
            _windows.Clear();
            _popups.Clear();
        }
        _automation.Dispose();
    }
}
