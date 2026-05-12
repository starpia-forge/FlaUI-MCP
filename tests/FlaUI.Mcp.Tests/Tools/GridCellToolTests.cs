using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class GridCellToolTests : IDisposable
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;
    private readonly GridCellTool _tool;

    public GridCellToolTests()
    {
        _session = new SessionManager();
        _registry = new ElementRegistry();
        _tool = new GridCellTool(_session, _registry);
    }

    public void Dispose() => _session.Dispose();

    // ── Argument validation ───────────────────────────────────────────────────

    [Fact]
    public async Task GridCell_NullArguments_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task GridCell_EmptyObject_ReturnsErrorMentioningRef()
    {
        var result = await _tool.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ref");
    }

    [Fact]
    public async Task GridCell_MissingRow_ReturnsErrorMentioningRow()
    {
        var args = JsonDocument.Parse("""{"ref":"w1e1"}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("row");
    }

    [Fact]
    public async Task GridCell_MissingCol_ReturnsErrorMentioningCol()
    {
        var args = JsonDocument.Parse("""{"ref":"w1e1","row":0}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("col");
    }

    [Fact]
    public async Task GridCell_NegativeRow_ReturnsErrorMentioningNonNegative()
    {
        var args = JsonDocument.Parse("""{"ref":"w1e1","row":-1,"col":0}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("non-negative");
    }

    [Fact]
    public async Task GridCell_UnknownRef_ReturnsErrorMentioningNotFound()
    {
        var args = JsonDocument.Parse("""{"ref":"w9e999","row":0,"col":0}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().ContainAny("not found", "refresh");
    }
}
