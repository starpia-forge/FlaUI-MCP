using FlaUI.Core.WindowsAPI;

namespace FlaUI.Mcp.Core;

/// <summary>
/// Parses human-readable key strings ("Ctrl+S", "F5", "Tab") into VirtualKeyShort values.
/// Used by KeysTool and the batch "keys" action.
/// </summary>
public static class KeyMap
{
    private static readonly Dictionary<string, VirtualKeyShort> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Modifiers
            ["ctrl"]    = VirtualKeyShort.CONTROL,
            ["control"] = VirtualKeyShort.CONTROL,
            ["shift"]   = VirtualKeyShort.SHIFT,
            ["alt"]     = VirtualKeyShort.LMENU,
            ["win"]     = VirtualKeyShort.LWIN,
            // Navigation
            ["tab"]      = VirtualKeyShort.TAB,
            ["enter"]    = VirtualKeyShort.ENTER,
            ["return"]   = VirtualKeyShort.ENTER,
            ["escape"]   = VirtualKeyShort.ESC,
            ["esc"]      = VirtualKeyShort.ESC,
            ["space"]    = VirtualKeyShort.SPACE,
            ["backspace"] = VirtualKeyShort.BACK,
            ["delete"]   = VirtualKeyShort.DELETE,
            ["del"]      = VirtualKeyShort.DELETE,
            ["insert"]   = VirtualKeyShort.INSERT,
            ["ins"]      = VirtualKeyShort.INSERT,
            ["home"]     = VirtualKeyShort.HOME,
            ["end"]      = VirtualKeyShort.END,
            ["pageup"]   = VirtualKeyShort.PRIOR,
            ["pagedown"] = VirtualKeyShort.NEXT,
            ["pgup"]     = VirtualKeyShort.PRIOR,
            ["pgdn"]     = VirtualKeyShort.NEXT,
            ["up"]       = VirtualKeyShort.UP,
            ["down"]     = VirtualKeyShort.DOWN,
            ["left"]     = VirtualKeyShort.LEFT,
            ["right"]    = VirtualKeyShort.RIGHT,
            // Function keys
            ["f1"]  = VirtualKeyShort.F1,  ["f2"]  = VirtualKeyShort.F2,
            ["f3"]  = VirtualKeyShort.F3,  ["f4"]  = VirtualKeyShort.F4,
            ["f5"]  = VirtualKeyShort.F5,  ["f6"]  = VirtualKeyShort.F6,
            ["f7"]  = VirtualKeyShort.F7,  ["f8"]  = VirtualKeyShort.F8,
            ["f9"]  = VirtualKeyShort.F9,  ["f10"] = VirtualKeyShort.F10,
            ["f11"] = VirtualKeyShort.F11, ["f12"] = VirtualKeyShort.F12,
        };

    static KeyMap()
    {
        // A-Z: VirtualKeyShort values match ASCII upper-case codes (0x41-0x5A)
        for (char c = 'A'; c <= 'Z'; c++)
            Map[c.ToString().ToLower()] = (VirtualKeyShort)c;

        // 0-9: match ASCII digit codes (0x30-0x39)
        for (char c = '0'; c <= '9'; c++)
            Map[c.ToString()] = (VirtualKeyShort)c;
    }

    public static bool TryParse(string token, out VirtualKeyShort key)
        => Map.TryGetValue(token.Trim(), out key);

    /// <summary>
    /// Parse a chord string like "Ctrl+Shift+N" into modifiers and a main key.
    /// Throws ArgumentException if any token is unknown.
    /// </summary>
    public static (VirtualKeyShort[] modifiers, VirtualKeyShort mainKey) ParseChord(string chord)
    {
        var parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            throw new ArgumentException($"Empty chord: '{chord}'");

        var keys = new VirtualKeyShort[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!TryParse(parts[i], out keys[i]))
                throw new ArgumentException(
                    $"Unknown key '{parts[i]}' in chord '{chord}'. " +
                    $"Supported: letters (a-z), digits (0-9), Ctrl/Shift/Alt/Win, Tab, Enter, " +
                    $"Escape, Space, Backspace, Delete, Home, End, PageUp, PageDown, " +
                    $"Up/Down/Left/Right, F1-F12.");
        }

        return (keys[..^1], keys[^1]);
    }

    /// <summary>
    /// Parse a sequence of space-separated chords: "Ctrl+A Delete" → two chords.
    /// </summary>
    public static IEnumerable<(VirtualKeyShort[] modifiers, VirtualKeyShort mainKey)> ParseSequence(string keys)
        => keys.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Select(ParseChord);
}
