using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class ValueToolsTests : IDisposable
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;
    private readonly GetValueTool _getValueTool;
    private readonly SetValueTool _setValueTool;

    public ValueToolsTests()
    {
        _session = new SessionManager();
        _registry = new ElementRegistry();
        _getValueTool = new GetValueTool(_session, _registry);
        _setValueTool = new SetValueTool(_session, _registry);
    }

    public void Dispose() => _session.Dispose();

    // ── GetValueTool argument validation ─────────────────────────────────────

    [Fact]
    public async Task GetValue_NullArguments_ReturnsError()
    {
        var result = await _getValueTool.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task GetValue_EmptyObject_ReturnsErrorMentioningRef()
    {
        var result = await _getValueTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ref");
    }

    [Fact]
    public async Task GetValue_UnknownRef_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"ref":"w9e999"}""").RootElement;
        var result = await _getValueTool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().ContainAny("not found", "refresh");
    }

    [Fact]
    public async Task GetValue_EmptyRef_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"ref":""}""").RootElement;
        var result = await _getValueTool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
    }

    // ── SetValueTool argument validation ─────────────────────────────────────

    [Fact]
    public async Task SetValue_NullArguments_ReturnsError()
    {
        var result = await _setValueTool.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task SetValue_EmptyObject_ReturnsErrorMentioningRef()
    {
        var result = await _setValueTool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ref");
    }

    [Fact]
    public async Task SetValue_MissingValue_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"ref":"w1e1"}""").RootElement;
        var result = await _setValueTool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("value");
    }

    [Fact]
    public async Task SetValue_UnknownRef_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"ref":"w9e999","value":"hello"}""").RootElement;
        var result = await _setValueTool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().ContainAny("not found", "refresh");
    }

    [Fact]
    public async Task SetValue_EmptyRef_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"ref":"","value":42}""").RootElement;
        var result = await _setValueTool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task SetValue_ObjectValue_ReturnsErrorMentioningTypes()
    {
        var args = JsonDocument.Parse("""{"ref":"w9e999","value":{"nested":1}}""").RootElement;
        var result = await _setValueTool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().ContainAll("string", "number", "boolean");
    }

    [Fact]
    public async Task SetValue_ArrayValue_ReturnsErrorMentioningTypes()
    {
        var args = JsonDocument.Parse("""{"ref":"w9e999","value":[1,2,3]}""").RootElement;
        var result = await _setValueTool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().ContainAll("string", "number", "boolean");
    }
}
