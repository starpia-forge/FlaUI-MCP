using FluentAssertions;
using FlaUI.Mcp.Core;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

[Collection("Clipboard")]
public class ClipboardAccessorTests
{
    [Fact]
    public void RoundTrip_SimpleText()
    {
        ClipboardAccessor.WriteText("hello");
        ClipboardAccessor.ReadText().Should().Be("hello");
    }

    [Fact]
    public void RoundTrip_MultilineAndTabs()
    {
        var text = "line1\r\nline2\ttabbed";
        ClipboardAccessor.WriteText(text);
        ClipboardAccessor.ReadText().Should().Be(text);
    }

    [Fact]
    public void WriteEmpty_ClearsClipboard()
    {
        ClipboardAccessor.WriteText("non-empty");
        ClipboardAccessor.WriteText("");
        ClipboardAccessor.ReadText().Should().Be("");
    }

    [Fact]
    public void RoundTrip_Unicode()
    {
        var text = "한글 ✓ 日本語";
        ClipboardAccessor.WriteText(text);
        ClipboardAccessor.ReadText().Should().Be(text);
    }
}
