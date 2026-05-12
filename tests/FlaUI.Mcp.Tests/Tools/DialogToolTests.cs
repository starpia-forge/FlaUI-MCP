using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class DialogToolTests : IDisposable
{
    private readonly SessionManager _session;
    private readonly DialogTool _tool;

    public DialogToolTests()
    {
        _session = new SessionManager();
        _tool = new DialogTool(_session);
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public void Name_IsWindowsDialog()
    {
        _tool.Name.Should().Be("windows_dialog");
    }

    [Fact]
    public async Task Execute_MissingAction_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("action");
    }

    [Fact]
    public async Task Execute_InvalidAction_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"action":"explode"}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("action");
    }

    [Fact]
    public async Task Execute_ClickWithoutButton_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"action":"click"}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("button");
    }

    [Fact]
    public async Task Execute_UnknownHandle_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"action":"accept","handle":"m9999"}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("m9999");
    }

    [Fact]
    public async Task Execute_NullArguments_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("action");
    }

    [Fact]
    public void InputSchema_RequiresAction()
    {
        var schema = System.Text.Json.JsonSerializer.Serialize(_tool.InputSchema);
        schema.Should().Contain("\"action\"");
        schema.Should().Contain("required");
    }

    [Fact]
    public void InputSchema_HasAllActionEnumValues()
    {
        var schema = System.Text.Json.JsonSerializer.Serialize(_tool.InputSchema);
        schema.Should().Contain("wait");
        schema.Should().Contain("accept");
        schema.Should().Contain("cancel");
        schema.Should().Contain("click");
    }
}
