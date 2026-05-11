using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUIApplication = FlaUI.Core.Application;

namespace FlaUI.Mcp.Core;

/// <summary>
/// Manages UI Automation sessions and launched applications
/// </summary>
public class SessionManager : IDisposable
{
    private readonly UIA3Automation _automation;
    private readonly Dictionary<string, FlaUIApplication> _applications = new();
    private readonly Dictionary<string, Window> _windows = new();
    private readonly Dictionary<IntPtr, string> _hwndToHandle = new();
    private readonly object _sync = new();
    private int _appCounter = 0;
    private int _windowCounter = 0;

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

    public string RegisterWindow(Window window)
    {
        IntPtr hwnd = IntPtr.Zero;
        try { hwnd = window.Properties.NativeWindowHandle.ValueOrDefault; } catch { }

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

    public List<(string handle, string title, string? processName)> ListWindows()
    {
        var desktop = _automation.GetDesktop();
        var windows = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

        var result = new List<(string, string, string?)>();
        foreach (var w in windows)
        {
            var window = w.AsWindow();
            if (window != null && !string.IsNullOrEmpty(window.Title))
            {
                var handle = RegisterWindow(window);
                string? processName = null;
                try
                {
                    processName = window.Properties.ProcessId.TryGetValue(out var pid)
                        ? System.Diagnostics.Process.GetProcessById(pid).ProcessName
                        : null;
                }
                catch { }

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

        IntPtr hwnd = IntPtr.Zero;
        try { hwnd = window.Properties.NativeWindowHandle.ValueOrDefault; } catch { }

        lock (_sync)
        {
            if (hwnd != IntPtr.Zero) _hwndToHandle.Remove(hwnd);
            _windows.Remove(handle);
        }

        window.Close();
    }

    private static HashSet<IntPtr> GetTopLevelHwnds(FlaUI.Core.AutomationElements.AutomationElement desktop) =>
        new(desktop
            .FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
            .Select(w => {
                try { return w.Properties.NativeWindowHandle.ValueOrDefault; }
                catch { return IntPtr.Zero; }
            })
            .Where(h => h != IntPtr.Zero));

    private static Window? FindNewWindow(FlaUI.Core.AutomationElements.AutomationElement desktop, HashSet<IntPtr> preExistingHwnds)
    {
        foreach (var w in desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)))
        {
            IntPtr hwnd = IntPtr.Zero;
            try { hwnd = w.Properties.NativeWindowHandle.ValueOrDefault; } catch { }

            if (hwnd != IntPtr.Zero && !preExistingHwnds.Contains(hwnd))
            {
                var win = w.AsWindow();
                if (win != null && !string.IsNullOrEmpty(win.Title))
                    return win;
            }
        }
        return null;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            foreach (var app in _applications.Values)
            {
                try { app.Close(); } catch { }
            }
            _applications.Clear();
            _hwndToHandle.Clear();
            _windows.Clear();
        }
        _automation.Dispose();
    }
}
