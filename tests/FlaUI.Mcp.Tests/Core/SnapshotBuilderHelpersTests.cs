using FlaUI.Core.Definitions;
using FlaUI.Mcp.Core;
using FluentAssertions;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class SnapshotBuilderHelpersTests
{
    // ── EscapeName ──────────────────────────────────────────────────────────

    [Fact]
    public void EscapeName_PlainString_ReturnsUnchanged()
    {
        SnapshotBuilder.EscapeName("hello world").Should().Be("hello world");
    }

    [Theory]
    [InlineData("with\nnewline", "with\\nnewline")]
    [InlineData("strip\r", "strip")]
    [InlineData("say \"hi\"", "say \\\"hi\\\"")]
    [InlineData("back\\slash", "back\\\\slash")]
    public void EscapeName_SpecialChars_AreEscapedCorrectly(string input, string expected)
    {
        SnapshotBuilder.EscapeName(input).Should().Be(expected);
    }

    // ── GetElementRole ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(ControlType.Button,      "button")]
    [InlineData(ControlType.Edit,        "textbox")]
    [InlineData(ControlType.Text,        "text")]
    [InlineData(ControlType.CheckBox,    "checkbox")]
    [InlineData(ControlType.RadioButton, "radio")]
    [InlineData(ControlType.ComboBox,    "combobox")]
    [InlineData(ControlType.List,        "list")]
    [InlineData(ControlType.ListItem,    "listitem")]
    [InlineData(ControlType.Menu,        "menu")]
    [InlineData(ControlType.MenuItem,    "menuitem")]
    [InlineData(ControlType.MenuBar,     "menubar")]
    [InlineData(ControlType.Tree,        "tree")]
    [InlineData(ControlType.TreeItem,    "treeitem")]
    [InlineData(ControlType.Tab,         "tablist")]
    [InlineData(ControlType.TabItem,     "tab")]
    [InlineData(ControlType.Table,       "table")]
    [InlineData(ControlType.DataItem,    "row")]
    [InlineData(ControlType.Header,      "header")]
    [InlineData(ControlType.HeaderItem,  "columnheader")]
    [InlineData(ControlType.Slider,      "slider")]
    [InlineData(ControlType.Spinner,     "spinbutton")]
    [InlineData(ControlType.ProgressBar, "progressbar")]
    [InlineData(ControlType.Hyperlink,   "link")]
    [InlineData(ControlType.Image,       "image")]
    [InlineData(ControlType.Pane,        "group")]
    [InlineData(ControlType.Group,       "group")]
    [InlineData(ControlType.Window,      "window")]
    [InlineData(ControlType.Document,    "document")]
    [InlineData(ControlType.ToolBar,     "toolbar")]
    [InlineData(ControlType.ToolTip,     "tooltip")]
    [InlineData(ControlType.ScrollBar,   "scrollbar")]
    [InlineData(ControlType.StatusBar,   "status")]
    [InlineData(ControlType.Separator,   "separator")]
    [InlineData(ControlType.Thumb,       "thumb")]
    [InlineData(ControlType.TitleBar,    "titlebar")]
    [InlineData(ControlType.DataGrid,    "grid")]
    [InlineData(ControlType.Custom,      "custom")]
    public void GetElementRole_KnownControlType_ReturnsExpectedRole(
        ControlType controlType, string expectedRole)
    {
        SnapshotBuilder.GetElementRole(controlType).Should().Be(expectedRole);
    }

    [Fact]
    public void GetElementRole_UnknownControlType_ReturnsElement()
    {
        SnapshotBuilder.GetElementRole((ControlType)999).Should().Be("element");
    }
}
