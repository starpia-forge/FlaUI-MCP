using FlaUI.Core.WindowsAPI;
using FlaUI.Mcp.Core;
using FluentAssertions;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class KeyMapTests
{
    [Theory]
    [InlineData("ctrl",    VirtualKeyShort.CONTROL)]
    [InlineData("Ctrl",    VirtualKeyShort.CONTROL)]
    [InlineData("CTRL",    VirtualKeyShort.CONTROL)]
    [InlineData("control", VirtualKeyShort.CONTROL)]
    [InlineData("shift",   VirtualKeyShort.SHIFT)]
    [InlineData("alt",     VirtualKeyShort.LMENU)]
    [InlineData("tab",     VirtualKeyShort.TAB)]
    [InlineData("enter",   VirtualKeyShort.ENTER)]
    [InlineData("escape",  VirtualKeyShort.ESC)]
    [InlineData("esc",     VirtualKeyShort.ESC)]
    [InlineData("f1",      VirtualKeyShort.F1)]
    [InlineData("f12",     VirtualKeyShort.F12)]
    [InlineData("delete",  VirtualKeyShort.DELETE)]
    [InlineData("home",    VirtualKeyShort.HOME)]
    [InlineData("end",     VirtualKeyShort.END)]
    [InlineData("up",      VirtualKeyShort.UP)]
    [InlineData("down",    VirtualKeyShort.DOWN)]
    public void TryParse_KnownKey_ReturnsTrue(string token, VirtualKeyShort expected)
    {
        var found = KeyMap.TryParse(token, out var key);

        found.Should().BeTrue();
        key.Should().Be(expected);
    }

    [Theory]
    [InlineData("a", (VirtualKeyShort)0x41)]
    [InlineData("z", (VirtualKeyShort)0x5A)]
    [InlineData("A", (VirtualKeyShort)0x41)]
    [InlineData("0", (VirtualKeyShort)0x30)]
    [InlineData("9", (VirtualKeyShort)0x39)]
    public void TryParse_AlphanumericKey_ReturnsTrue(string token, VirtualKeyShort expected)
    {
        var found = KeyMap.TryParse(token, out var key);

        found.Should().BeTrue();
        key.Should().Be(expected);
    }

    [Fact]
    public void TryParse_UnknownKey_ReturnsFalse()
    {
        KeyMap.TryParse("xyz_unknown", out _).Should().BeFalse();
    }

    [Fact]
    public void ParseChord_SingleKey_ReturnsEmptyModifiers()
    {
        var (modifiers, mainKey) = KeyMap.ParseChord("Tab");

        modifiers.Should().BeEmpty();
        mainKey.Should().Be(VirtualKeyShort.TAB);
    }

    [Fact]
    public void ParseChord_AltF4_ReturnsLmenu()
    {
        var (modifiers, mainKey) = KeyMap.ParseChord("Alt+F4");

        modifiers.Should().ContainSingle().Which.Should().Be(VirtualKeyShort.LMENU);
        mainKey.Should().Be(VirtualKeyShort.F4);
    }

    [Fact]
    public void ParseChord_CtrlS_ReturnsSingleModifier()
    {
        var (modifiers, mainKey) = KeyMap.ParseChord("Ctrl+S");

        modifiers.Should().ContainSingle().Which.Should().Be(VirtualKeyShort.CONTROL);
        mainKey.Should().Be((VirtualKeyShort)0x53); // 'S'
    }

    [Fact]
    public void ParseChord_CtrlShiftN_ReturnsTwoModifiers()
    {
        var (modifiers, mainKey) = KeyMap.ParseChord("Ctrl+Shift+N");

        modifiers.Should().HaveCount(2);
        modifiers.Should().Contain(VirtualKeyShort.CONTROL);
        modifiers.Should().Contain(VirtualKeyShort.SHIFT);
        mainKey.Should().Be((VirtualKeyShort)0x4E); // 'N'
    }

    [Fact]
    public void ParseChord_UnknownKey_ThrowsArgumentException()
    {
        var act = () => KeyMap.ParseChord("Ctrl+BadKey");

        act.Should().Throw<ArgumentException>().WithMessage("*BadKey*");
    }

    [Fact]
    public void ParseSequence_MultipleChords_ReturnsAll()
    {
        var chords = KeyMap.ParseSequence("Ctrl+A Delete").ToList();

        chords.Should().HaveCount(2);
        chords[0].mainKey.Should().Be((VirtualKeyShort)0x41); // 'A'
        chords[1].mainKey.Should().Be(VirtualKeyShort.DELETE);
    }
}
