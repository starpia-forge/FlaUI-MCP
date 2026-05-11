using FlaUI.Core.AutomationElements;

namespace FlaUI.Mcp.Core;

public static class TextExtractor
{
    public static string GetText(AutomationElement element)
    {
        string? result = null;

        try
        {
            if (element.Patterns.Value.IsSupported)
                result = element.Patterns.Value.Pattern.Value.ValueOrDefault;
        }
        catch { /* Value pattern not supported */ }

        if (string.IsNullOrEmpty(result))
            result = SafeAccess.Get(() => element.Properties.Name.ValueOrDefault);

        if (string.IsNullOrEmpty(result))
        {
            try
            {
                if (element.Patterns.Text.IsSupported)
                    result = element.Patterns.Text.Pattern.DocumentRange.GetText(-1);
            }
            catch { /* Text pattern read failed */ }
        }

        return result ?? "";
    }
}
