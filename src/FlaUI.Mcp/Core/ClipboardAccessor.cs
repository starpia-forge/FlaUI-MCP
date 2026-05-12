using System.Windows.Forms;

namespace FlaUI.Mcp.Core;

internal static class ClipboardAccessor
{
    internal static string ReadText()
    {
        string result = "";
        var thread = new Thread(() =>
        {
            if (Clipboard.ContainsText())
                result = Clipboard.GetText() ?? "";
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Clipboard read timed out after 5 s");
        return result;
    }

    internal static void WriteText(string text)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    Clipboard.Clear();
                else
                    Clipboard.SetText(text);
            }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Clipboard write timed out after 5 s");
        if (captured is not null)
            throw captured;
    }
}
