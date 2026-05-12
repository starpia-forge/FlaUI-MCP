using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FlaUI.Mcp.Core;

public static class WindowStateController
{
    private const string InspectHint = "Use windows_inspect to see which patterns are supported.";

    public static string SetVisualState(Window window, string handle, WindowVisualState state)
    {
        if (!SafeAccess.Get(() => window.Patterns.Window.IsSupported, false))
            throw new NotSupportedException(
                $"Window '{handle}' does not support the Window pattern. {InspectHint}");

        if (state == WindowVisualState.Maximized &&
            !SafeAccess.Get(() => window.Patterns.Window.Pattern.CanMaximize.ValueOrDefault, false))
            throw new NotSupportedException($"Window '{handle}' cannot be maximized. {InspectHint}");

        if (state == WindowVisualState.Minimized &&
            !SafeAccess.Get(() => window.Patterns.Window.Pattern.CanMinimize.ValueOrDefault, false))
            throw new NotSupportedException($"Window '{handle}' cannot be minimized. {InspectHint}");

        window.Patterns.Window.Pattern.SetWindowVisualState(state);

        var label = state switch
        {
            WindowVisualState.Maximized => "Maximized",
            WindowVisualState.Minimized => "Minimized",
            _ => "Restored"
        };
        return $"{label} window {handle}";
    }

    public static string Move(Window window, string handle, int x, int y)
    {
        if (!SafeAccess.Get(() => window.Patterns.Transform.IsSupported, false))
            throw new NotSupportedException(
                $"Window '{handle}' does not support the Transform pattern. {InspectHint}");

        if (!SafeAccess.Get(() => window.Patterns.Transform.Pattern.CanMove.ValueOrDefault, false))
            throw new NotSupportedException($"Window '{handle}' cannot be moved. {InspectHint}");

        window.Patterns.Transform.Pattern.Move(x, y);
        return $"Moved window {handle} to ({x}, {y})";
    }

    public static string Resize(Window window, string handle, int width, int height)
    {
        if (!SafeAccess.Get(() => window.Patterns.Transform.IsSupported, false))
            throw new NotSupportedException(
                $"Window '{handle}' does not support the Transform pattern. {InspectHint}");

        if (!SafeAccess.Get(() => window.Patterns.Transform.Pattern.CanResize.ValueOrDefault, false))
            throw new NotSupportedException($"Window '{handle}' cannot be resized. {InspectHint}");

        window.Patterns.Transform.Pattern.Resize(width, height);
        return $"Resized window {handle} to {width}x{height}";
    }
}
