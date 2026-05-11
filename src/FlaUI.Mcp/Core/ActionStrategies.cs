using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace FlaUI.Mcp.Core;

/// <summary>
/// Shared click logic used by both ClickTool and BatchTool.
/// </summary>
public static class ClickStrategy
{
    public static string Click(AutomationElement element, string refId,
        string button = "left", bool doubleClick = false)
    {
        var name = element.Properties.Name.ValueOrDefault ?? refId;

        if (button == "left" && !doubleClick)
        {
            if (element.Patterns.Invoke.IsSupported)
            {
                element.Patterns.Invoke.Pattern.Invoke();
                return $"Invoked {name}";
            }
            if (element.Patterns.Toggle.IsSupported)
            {
                element.Patterns.Toggle.Pattern.Toggle();
                var state = element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
                return $"Toggled {name} to {state}";
            }
            if (element.Patterns.SelectionItem.IsSupported)
            {
                element.Patterns.SelectionItem.Pattern.Select();
                return $"Selected {name}";
            }
        }

        var clickPoint = element.GetClickablePoint();
        var mb = button switch
        {
            "right"  => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _        => MouseButton.Left
        };

        if (doubleClick)
        {
            Mouse.DoubleClick(clickPoint, mb);
            return $"Double-clicked {name}";
        }
        Mouse.Click(clickPoint, mb);
        return $"Clicked {name}";
    }
}

/// <summary>
/// Shared type logic used by both TypeTool and BatchTool.
/// </summary>
public static class TypeStrategy
{
    public static string Type(AutomationElement element, string refId, string text, bool pressEnter)
    {
        var name = element.Properties.Name.ValueOrDefault ?? refId;
        element.Focus();
        Thread.Sleep(50);
        Keyboard.Type(text);
        if (pressEnter) Keyboard.Press(VirtualKeyShort.ENTER);
        return $"{(pressEnter ? "Typed and submitted" : "Typed")} \"{text}\" into {name}";
    }

    public static string TypeToFocused(string text, bool pressEnter)
    {
        Keyboard.Type(text);
        if (pressEnter) Keyboard.Press(VirtualKeyShort.ENTER);
        return $"{(pressEnter ? "Typed and submitted" : "Typed")} \"{text}\" into focused element";
    }
}

/// <summary>
/// Shared fill logic used by both FillTool and BatchTool.
/// </summary>
public static class FillStrategy
{
    public static string Fill(AutomationElement element, string refId, string value)
    {
        var name = element.Properties.Name.ValueOrDefault ?? refId;

        if (element.Patterns.Value.IsSupported &&
            !element.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault)
        {
            element.Patterns.Value.Pattern.SetValue(value);
            return $"Filled {name} with \"{value}\"";
        }

        element.Focus();
        Thread.Sleep(50);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(value);
        return $"Filled {name} with \"{value}\"";
    }
}

/// <summary>
/// Shared hover logic used by HoverTool and the batch "hover" action.
/// </summary>
public static class HoverStrategy
{
    public static string Hover(AutomationElement element, string refId, int durationMs)
    {
        var name = element.Properties.Name.ValueOrDefault ?? refId;
        var point = element.GetClickablePoint();
        Mouse.Position = point;
        Thread.Sleep(durationMs);
        return $"Hovered {name}";
    }
}

/// <summary>
/// Shared scroll logic used by ScrollTool and the batch "scroll" action.
/// Prefers UIA ScrollPattern; falls back to mouse wheel for vertical scrolling.
/// </summary>
public static class ScrollStrategy
{
    public static string Scroll(AutomationElement element, string refId,
        string direction, int amount, bool usePattern)
    {
        var name = element.Properties.Name.ValueOrDefault ?? refId;

        if (usePattern && element.Patterns.Scroll.IsSupported)
        {
            var scroll = element.Patterns.Scroll.Pattern;
            for (int i = 0; i < amount; i++)
            {
                switch (direction)
                {
                    case "up":    scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallDecrement); break;
                    case "down":  scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallIncrement); break;
                    case "left":  scroll.Scroll(ScrollAmount.SmallDecrement, ScrollAmount.NoAmount); break;
                    case "right": scroll.Scroll(ScrollAmount.SmallIncrement, ScrollAmount.NoAmount); break;
                }
            }
            return $"Scrolled {name} {direction} x{amount} (pattern)";
        }

        // Mouse wheel fallback — horizontal requires ScrollPattern, not exposed via wheel
        if (direction is "left" or "right")
            throw new NotSupportedException(
                $"Horizontal scroll requires ScrollPattern support. " +
                $"Set usePattern:true or ensure the element supports UIA ScrollPattern.");

        var pt = element.GetClickablePoint();
        Mouse.Position = pt;
        int wheelClicks = direction == "up" ? amount : -amount;
        Mouse.Scroll(wheelClicks);

        return $"Scrolled {name} {direction} x{amount} (mouse wheel)";
    }
}

/// <summary>
/// Shared drag logic used by DragTool and the batch "drag" action.
/// Uses press + interpolated move + release for reliable drag behavior.
/// </summary>
public static class DragStrategy
{
    public static string Drag(AutomationElement fromElement, string fromRefId,
        System.Drawing.Point toPoint, string toName, int durationMs)
    {
        var fromName = fromElement.Properties.Name.ValueOrDefault ?? fromRefId;
        var fromPoint = fromElement.GetClickablePoint();

        Mouse.Position = fromPoint;
        Thread.Sleep(50);
        Mouse.Down(MouseButton.Left);
        Thread.Sleep(50);

        // Interpolated move so apps listening to WM_MOUSEMOVE receive the drag
        int steps = Math.Max(1, durationMs / 16);
        for (int i = 1; i <= steps; i++)
        {
            var frac = (double)i / steps;
            Mouse.Position = new System.Drawing.Point(
                (int)(fromPoint.X + (toPoint.X - fromPoint.X) * frac),
                (int)(fromPoint.Y + (toPoint.Y - fromPoint.Y) * frac));
            Thread.Sleep(16);
        }

        Thread.Sleep(50);
        Mouse.Up(MouseButton.Left);

        return $"Dragged {fromName} → {toName}";
    }
}
