using System.Collections.Generic;
using FlaUI.Core.Definitions;
using FlaUI.Mcp.Core;
using FluentAssertions;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class RolesTests
{
    // Representative sample of _primary entries — covers roles that were previously
    // missing from the inverse mapping. If Roles._primary is updated, add entries here.
    public static IEnumerable<object[]> PrimaryEntries() =>
    [
        [ControlType.Button,      "button"],
        [ControlType.Edit,        "textbox"],
        [ControlType.CheckBox,    "checkbox"],
        [ControlType.Group,       "group"],
        [ControlType.Image,       "image"],
        [ControlType.ToolTip,     "tooltip"],
        [ControlType.ScrollBar,   "scrollbar"],
        [ControlType.StatusBar,   "status"],
        [ControlType.Separator,   "separator"],
        [ControlType.Thumb,       "thumb"],
        [ControlType.TitleBar,    "titlebar"],
        [ControlType.Header,      "header"],
        [ControlType.HeaderItem,  "columnheader"],
        [ControlType.Custom,      "custom"],
    ];

    [Theory]
    [MemberData(nameof(PrimaryEntries))]
    public void Primary_RoundTrips_BothDirections(ControlType ct, string role)
    {
        Roles.ToRole(ct).Should().Be(role);
        Roles.ToControlType(role).Should().Be(ct);
    }

    [Fact]
    public void Pane_IsForwardAlias_ReversesTo_Group()
    {
        Roles.ToRole(ControlType.Pane).Should().Be("group");
        Roles.ToControlType("group").Should().Be(ControlType.Group);
    }

    [Fact]
    public void ToRole_Unknown_ReturnsElement()
    {
        Roles.ToRole((ControlType)999).Should().Be("element");
    }

    [Fact]
    public void ToControlType_Unknown_ReturnsCustom()
    {
        Roles.ToControlType("nonexistent").Should().Be(ControlType.Custom);
    }

    [Fact]
    public void ToControlType_IsCaseInsensitive()
    {
        Roles.ToControlType("BUTTON").Should().Be(ControlType.Button);
        Roles.ToControlType("Button").Should().Be(ControlType.Button);
    }
}
