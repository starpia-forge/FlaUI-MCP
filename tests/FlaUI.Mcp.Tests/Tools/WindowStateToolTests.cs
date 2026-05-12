using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class WindowStateToolTests : IDisposable
{
    private readonly SessionManager _session = new();
    private readonly WindowStateTool _tool;

    public WindowStateToolTests()
    {
        _tool = new WindowStateTool(_session);
    }

    public void Dispose() => _session.Dispose();

    private static JsonElement? Args(string json) =>
        JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task NullArguments_ReturnsError_MentionsAction()
    {
        var result = await _tool.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("action");
    }

    [Fact]
    public async Task EmptyObject_ReturnsError_MentionsAction()
    {
        var result = await _tool.ExecuteAsync(Args("{}"));
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("action");
    }

    [Fact]
    public async Task UnknownAction_ReturnsError_ListsValidActions()
    {
        var result = await _tool.ExecuteAsync(Args("""{"action":"bogus"}"""));
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().ContainAll("maximize", "minimize", "restore", "move", "resize");
    }

    [Fact]
    public async Task MoveWithoutX_ReturnsError_MentionsX()
    {
        var result = await _tool.ExecuteAsync(Args("""{"action":"move","y":100}"""));
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("x");
    }

    [Fact]
    public async Task MoveWithoutY_ReturnsError_MentionsY()
    {
        var result = await _tool.ExecuteAsync(Args("""{"action":"move","x":100}"""));
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("y");
    }

    [Fact]
    public async Task ResizeWithoutWidth_ReturnsError_MentionsWidth()
    {
        var result = await _tool.ExecuteAsync(Args("""{"action":"resize","height":600}"""));
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("width");
    }

    [Fact]
    public async Task ResizeWithoutHeight_ReturnsError_MentionsHeight()
    {
        var result = await _tool.ExecuteAsync(Args("""{"action":"resize","width":800}"""));
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("height");
    }

    [Fact]
    public async Task UnknownHandle_ReturnsError_MentionsNotFound()
    {
        var result = await _tool.ExecuteAsync(Args("""{"action":"maximize","handle":"w999"}"""));
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("not found");
    }
}
