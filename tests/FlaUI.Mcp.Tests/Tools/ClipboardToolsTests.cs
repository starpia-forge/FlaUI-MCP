using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

[Collection("Clipboard")]
public class ClipboardToolsTests
{
    private readonly GetClipboardTool _get = new();
    private readonly SetClipboardTool _set = new();

    // --- GetClipboardTool ---

    [Fact]
    public async Task Get_NullArguments_ReturnsClipboardContent()
    {
        ClipboardAccessor.WriteText("test-get");
        var result = await _get.ExecuteAsync(null);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Be("test-get");
    }

    [Fact]
    public async Task Get_EmptyClipboard_ReturnsEmptyMessage()
    {
        ClipboardAccessor.WriteText("");
        var result = await _get.ExecuteAsync(null);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("empty");
    }

    // --- SetClipboardTool ---

    [Fact]
    public async Task Set_NullArguments_ReturnsError()
    {
        var result = await _set.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("text");
    }

    [Fact]
    public async Task Set_MissingText_ReturnsError()
    {
        var args = JsonDocument.Parse("{}").RootElement;
        var result = await _set.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("text");
    }

    [Fact]
    public async Task Set_ValidText_SetsClipboardAndReturnsCount()
    {
        var args = JsonDocument.Parse("""{"text":"abc"}""").RootElement;
        var result = await _set.ExecuteAsync(args);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("3 characters");
        ClipboardAccessor.ReadText().Should().Be("abc");
    }

    [Fact]
    public async Task Set_EmptyText_ClearsClipboard()
    {
        ClipboardAccessor.WriteText("something");
        var args = JsonDocument.Parse("""{"text":""}""").RootElement;
        var result = await _set.ExecuteAsync(args);
        result.IsError.Should().BeFalse();
        result.Content[0].Text.Should().Contain("cleared");
        ClipboardAccessor.ReadText().Should().Be("");
    }
}
