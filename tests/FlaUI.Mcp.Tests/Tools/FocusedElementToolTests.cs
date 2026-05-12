using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class FocusedElementToolTests : IDisposable
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;
    private readonly FocusedElementTool _tool;

    public FocusedElementToolTests()
    {
        _session = new SessionManager();
        _registry = new ElementRegistry();
        _tool = new FocusedElementTool(_session, _registry);
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public void Name_IsWindowsFocusedElement()
    {
        _tool.Name.Should().Be("windows_focused_element");
    }

    [Fact]
    public void Description_ContainsFocus()
    {
        _tool.Description.Should().NotBeNullOrWhiteSpace();
        _tool.Description.ToLower().Should().Contain("focus");
    }

    [Fact]
    public void InputSchema_HasEmptyProperties()
    {
        var json = JsonSerializer.Serialize(_tool.InputSchema);
        var doc = JsonDocument.Parse(json).RootElement;
        doc.GetProperty("properties").EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_NullArguments_DoesNotThrow_ReturnsMessage()
    {
        // FocusedElement() may return null or a real element depending on the environment.
        // Either way we expect no exception and a non-empty text response.
        var result = await _tool.ExecuteAsync(null);
        result.Content.Should().NotBeEmpty();
        result.Content[0].Text.Should().NotBeNullOrWhiteSpace();
    }
}
