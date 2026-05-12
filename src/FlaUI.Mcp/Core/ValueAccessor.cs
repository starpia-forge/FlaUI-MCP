using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FlaUI.Mcp.Core;

public readonly record struct ValueReadResult(
    string Pattern,
    string Value,
    bool? ReadOnly = null,
    double? Min = null,
    double? Max = null);

public static class ValueAccessor
{
    public static ValueReadResult? Read(AutomationElement element)
    {
        if (SafeAccess.Get(() => element.Patterns.Value.IsSupported, false))
        {
            var value = SafeAccess.Get(() => element.Patterns.Value.Pattern.Value.ValueOrDefault, "") ?? "";
            var readOnly = SafeAccess.Get(() => element.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault, false);
            return new ValueReadResult("Value", value, ReadOnly: readOnly);
        }

        if (SafeAccess.Get(() => element.Patterns.RangeValue.IsSupported, false))
        {
            var value = SafeAccess.Get(() => element.Patterns.RangeValue.Pattern.Value.ValueOrDefault, 0.0);
            var min = SafeAccess.Get(() => element.Patterns.RangeValue.Pattern.Minimum.ValueOrDefault, 0.0);
            var max = SafeAccess.Get(() => element.Patterns.RangeValue.Pattern.Maximum.ValueOrDefault, 0.0);
            return new ValueReadResult("RangeValue", value.ToString(CultureInfo.InvariantCulture), Min: min, Max: max);
        }

        if (SafeAccess.Get(() => element.Patterns.Toggle.IsSupported, false))
        {
            var state = SafeAccess.Get(() => element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault, ToggleState.Off);
            return new ValueReadResult("Toggle", state.ToString());
        }

        if (SafeAccess.Get(() => element.Patterns.SelectionItem.IsSupported, false))
        {
            var selected = SafeAccess.Get(() => element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault, false);
            return new ValueReadResult("SelectionItem", selected.ToString().ToLowerInvariant());
        }

        return null;
    }

    public static string Format(ValueReadResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"pattern: {result.Pattern}");
        sb.AppendLine($"value: {result.Value}");
        if (result.ReadOnly.HasValue)
            sb.AppendLine($"readonly: {result.ReadOnly.Value.ToString().ToLowerInvariant()}");
        if (result.Min.HasValue)
            sb.AppendLine($"min: {result.Min.Value.ToString(CultureInfo.InvariantCulture)}");
        if (result.Max.HasValue)
            sb.AppendLine($"max: {result.Max.Value.ToString(CultureInfo.InvariantCulture)}");
        return sb.ToString().TrimEnd();
    }

    public static string SetString(AutomationElement element, string refId, string value)
    {
        var name = SafeAccess.Get(() => element.Properties.Name.ValueOrDefault, refId) ?? refId;

        if (SafeAccess.Get(() => element.Patterns.Value.IsSupported, false) &&
            !SafeAccess.Get(() => element.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault, true))
        {
            element.Patterns.Value.Pattern.SetValue(value);
            return $"Set Value of {name} to \"{value}\"";
        }

        if (SafeAccess.Get(() => element.Patterns.Selection.IsSupported, false))
        {
            var children = element.FindAllChildren();
            foreach (var child in children)
            {
                var childName = SafeAccess.Get(() => child.Properties.Name.ValueOrDefault, "");
                if (childName == value && SafeAccess.Get(() => child.Patterns.SelectionItem.IsSupported, false))
                {
                    child.Patterns.SelectionItem.Pattern.Select();
                    return $"Selected \"{value}\" in {name}";
                }
            }
            throw new InvalidOperationException($"No selection item named \"{value}\" under {name}");
        }

        throw new NotSupportedException(
            $"Element {name} does not support Value pattern (writable) or Selection container");
    }

    public static string SetNumber(AutomationElement element, string refId, double value)
    {
        var name = SafeAccess.Get(() => element.Properties.Name.ValueOrDefault, refId) ?? refId;

        if (SafeAccess.Get(() => element.Patterns.RangeValue.IsSupported, false))
        {
            element.Patterns.RangeValue.Pattern.SetValue(value);
            return $"Set RangeValue of {name} to {value.ToString(CultureInfo.InvariantCulture)}";
        }

        // Fallback: some spinners expose only Value pattern with numeric string
        if (SafeAccess.Get(() => element.Patterns.Value.IsSupported, false) &&
            !SafeAccess.Get(() => element.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault, true))
        {
            var str = value.ToString(CultureInfo.InvariantCulture);
            element.Patterns.Value.Pattern.SetValue(str);
            return $"Set Value of {name} to \"{str}\"";
        }

        throw new NotSupportedException(
            $"Element {name} does not support RangeValue or writable Value pattern");
    }

    public static string SetBool(AutomationElement element, string refId, bool target)
    {
        var name = SafeAccess.Get(() => element.Properties.Name.ValueOrDefault, refId) ?? refId;

        if (!SafeAccess.Get(() => element.Patterns.Toggle.IsSupported, false))
            throw new NotSupportedException($"Element {name} does not support Toggle pattern");

        // Toggle.On == true; Off and Indeterminate == false
        bool IsMatch(ToggleState s) => target ? s == ToggleState.On : s != ToggleState.On;

        var state = SafeAccess.Get(() => element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault, ToggleState.Off);
        if (IsMatch(state))
            return $"Toggle of {name} already {(target ? "On" : "Off")}";

        // Cycle at most 3 times (Off → On → Indeterminate → Off)
        for (int i = 0; i < 3; i++)
        {
            element.Patterns.Toggle.Pattern.Toggle();
            state = SafeAccess.Get(() => element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault, ToggleState.Off);
            if (IsMatch(state))
                return $"Set Toggle of {name} to {state}";
        }

        throw new InvalidOperationException(
            $"Could not set Toggle of {name} to {(target ? "On" : "Off")} after 3 attempts");
    }
}
