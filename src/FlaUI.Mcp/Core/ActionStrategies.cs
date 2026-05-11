using FlaUI.Core.AutomationElements;
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
