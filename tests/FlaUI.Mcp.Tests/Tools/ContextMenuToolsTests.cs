using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class ContextMenuToolsTests : IDisposable
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;
    private readonly ContextMenuTool _contextMenu;
    private readonly DismissMenuTool _dismiss;

    public ContextMenuToolsTests()
    {
        _session = new SessionManager();
        _registry = new ElementRegistry();
        _contextMenu = new ContextMenuTool(_session, _registry);
        _dismiss = new DismissMenuTool(_session);
    }

    public void Dispose() => _session.Dispose();

    // ── ContextMenuTool argument validation ──────────────────────────────────

    [Fact]
    public async Task ContextMenu_NoArguments_ReturnsError()
    {
        var result = await _contextMenu.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ref");
    }

    [Fact]
    public async Task ContextMenu_NullArguments_ReturnsError()
    {
        var result = await _contextMenu.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ContextMenu_OnlyX_NoY_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"x":100}""").RootElement;
        var result = await _contextMenu.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ref");
    }

    [Fact]
    public async Task ContextMenu_UnknownRef_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"ref":"w9e999"}""").RootElement;
        var result = await _contextMenu.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().ContainAny("not found", "refresh");
    }

    // ── DismissMenuTool argument validation ──────────────────────────────────

    [Fact]
    public async Task DismissMenu_MissingHandle_ReturnsError()
    {
        var result = await _dismiss.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("handle");
    }

    [Fact]
    public async Task DismissMenu_NullArguments_ReturnsError()
    {
        var result = await _dismiss.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task DismissMenu_NonExistentHandle_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"handle":"m9999"}""").RootElement;
        var result = await _dismiss.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("m9999");
    }

    // ── DismissMenuTool clears popup registry ────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DismissMenu_ClearsPopupRegistration()
    {
        var desktop = _session.Automation.GetDesktop();
        var handle = _session.RegisterPopup(desktop);

        _session.GetPopup(handle).Should().NotBeNull();

        var args = JsonDocument.Parse($$$"""{"handle":"{{{handle}}}"}""").RootElement;
        await _dismiss.ExecuteAsync(args);

        _session.GetPopup(handle).Should().BeNull(
            because: "DismissMenuTool must call ClearPopup after sending Escape");
    }
}
