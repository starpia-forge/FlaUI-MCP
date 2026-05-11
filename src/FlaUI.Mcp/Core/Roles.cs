using FlaUI.Core.Definitions;

namespace FlaUI.Mcp.Core;

/// <summary>
/// Single source of truth for ControlType ↔ snapshot role string mapping.
/// Primary entries provide both directions. Forward aliases let multiple
/// ControlTypes serialize to the same role (e.g. Pane → "group") while the
/// reverse resolves to the primary type for that role.
/// </summary>
public static class Roles
{
    private static readonly (ControlType ct, string role)[] _primary =
    [
        (ControlType.Button,       "button"),
        (ControlType.Edit,         "textbox"),
        (ControlType.Text,         "text"),
        (ControlType.CheckBox,     "checkbox"),
        (ControlType.RadioButton,  "radio"),
        (ControlType.ComboBox,     "combobox"),
        (ControlType.List,         "list"),
        (ControlType.ListItem,     "listitem"),
        (ControlType.Menu,         "menu"),
        (ControlType.MenuItem,     "menuitem"),
        (ControlType.MenuBar,      "menubar"),
        (ControlType.Tree,         "tree"),
        (ControlType.TreeItem,     "treeitem"),
        (ControlType.Tab,          "tablist"),
        (ControlType.TabItem,      "tab"),
        (ControlType.Table,        "table"),
        (ControlType.DataItem,     "row"),
        (ControlType.Header,       "header"),
        (ControlType.HeaderItem,   "columnheader"),
        (ControlType.Slider,       "slider"),
        (ControlType.Spinner,      "spinbutton"),
        (ControlType.ProgressBar,  "progressbar"),
        (ControlType.Hyperlink,    "link"),
        (ControlType.Image,        "image"),
        (ControlType.Group,        "group"),
        (ControlType.Window,       "window"),
        (ControlType.Document,     "document"),
        (ControlType.ToolBar,      "toolbar"),
        (ControlType.ToolTip,      "tooltip"),
        (ControlType.ScrollBar,    "scrollbar"),
        (ControlType.StatusBar,    "status"),
        (ControlType.Separator,    "separator"),
        (ControlType.Thumb,        "thumb"),
        (ControlType.TitleBar,     "titlebar"),
        (ControlType.DataGrid,     "grid"),
        (ControlType.Custom,       "custom"),
    ];

    // Forward-only: ControlTypes that serialize to a role already in _primary,
    // but must NOT win the reverse lookup.
    private static readonly (ControlType ct, string role)[] _forwardAliases =
    [
        (ControlType.Pane, "group"),   // Pane snapshots as "group"; selector "group" → Group.
    ];

    private static readonly Dictionary<ControlType, string> _toRole =
        _primary.Concat(_forwardAliases).ToDictionary(p => p.ct, p => p.role);

    private static readonly Dictionary<string, ControlType> _toType =
        _primary.ToDictionary(p => p.role, p => p.ct, StringComparer.OrdinalIgnoreCase);

    public static string ToRole(ControlType ct) =>
        _toRole.TryGetValue(ct, out var role) ? role : "element";

    public static ControlType ToControlType(string role) =>
        _toType.TryGetValue(role, out var ct) ? ct : ControlType.Custom;
}
