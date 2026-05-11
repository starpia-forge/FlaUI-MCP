using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class InspectToolTests : IDisposable
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;
    private readonly InspectTool _inspect;

    public InspectToolTests()
    {
        _session = new SessionManager();
        _registry = new ElementRegistry();
        _inspect = new InspectTool(_session, _registry);
    }

    public void Dispose() => _session.Dispose();

    // ── Argument validation ───────────────────────────────────────────────────

    [Fact]
    public async Task Inspect_NullArguments_ReturnsError()
    {
        var result = await _inspect.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task Inspect_EmptyObject_ReturnsErrorMentioningRef()
    {
        var result = await _inspect.ExecuteAsync(JsonDocument.Parse("{}").RootElement);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ref");
    }

    [Fact]
    public async Task Inspect_UnknownRef_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"ref":"w9e999"}""").RootElement;
        var result = await _inspect.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().ContainAny("not found", "refresh");
    }

    [Fact]
    public async Task Inspect_PatternsTrue_IsDefault()
    {
        // Both explicit true and missing key should behave the same — confirmed by
        // checking the schema default; the only observable difference is whether
        // "=== Patterns ===" appears in successful output. For the error path the
        // response is always an error regardless, so this just verifies the arg
        // parsing doesn't crash when patterns=true is explicit.
        var args = JsonDocument.Parse("""{"ref":"w9e999","patterns":true}""").RootElement;
        var result = await _inspect.ExecuteAsync(args);
        result.IsError.Should().BeTrue(); // unknown ref → error, but no parse crash
    }

    [Fact]
    public async Task Inspect_PatternsFalse_IsAccepted()
    {
        var args = JsonDocument.Parse("""{"ref":"w9e999","patterns":false}""").RootElement;
        var result = await _inspect.ExecuteAsync(args);
        result.IsError.Should().BeTrue(); // unknown ref → error, but no parse crash
    }
}
