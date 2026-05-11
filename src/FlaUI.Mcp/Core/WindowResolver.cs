using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FlaUI.Mcp.Core;

public enum WindowResolutionFailure { None, HandleNotFound, NoFocusedElement, NoWindowAncestor }

public readonly record struct WindowResolution(Window? Window, string? Handle, WindowResolutionFailure Failure);

public static class WindowResolver
{
    public static WindowResolution ResolveOrFocused(SessionManager session, string? handle, bool registerFocused)
    {
        if (!string.IsNullOrEmpty(handle))
        {
            var win = session.GetWindow(handle);
            return win == null
                ? new WindowResolution(null, handle, WindowResolutionFailure.HandleNotFound)
                : new WindowResolution(win, handle, WindowResolutionFailure.None);
        }

        var focused = session.Automation.FocusedElement();
        if (focused == null)
            return new WindowResolution(null, null, WindowResolutionFailure.NoFocusedElement);

        var current = focused;
        while (current != null)
        {
            if (current.Properties.ControlType.ValueOrDefault == ControlType.Window)
            {
                var win = current.AsWindow();
                var h = registerFocused ? session.RegisterWindow(win) : null;
                return new WindowResolution(win, h, WindowResolutionFailure.None);
            }
            current = current.Parent;
        }

        return new WindowResolution(null, null, WindowResolutionFailure.NoWindowAncestor);
    }
}
