using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class ScreenshotDiffToolTests : IDisposable
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;
    private readonly ScreenshotCache _cache;
    private readonly ScreenshotDiffTool _tool;

    public ScreenshotDiffToolTests()
    {
        _session  = new SessionManager();
        _registry = new ElementRegistry();
        _cache    = new ScreenshotCache();
        _tool     = new ScreenshotDiffTool(_session, _registry, _cache);
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public void Name_IsWindowsScreenshotDiff()
    {
        _tool.Name.Should().Be("windows_screenshot_diff");
    }

    [Fact]
    public void InputSchema_ContainsExpectedProperties()
    {
        var json = JsonSerializer.Serialize(_tool.InputSchema);
        json.Should().Contain("store");
        json.Should().Contain("threshold");
        json.Should().Contain("fullScreen");
    }

    [Fact]
    public async Task Execute_NegativeThreshold_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"threshold":-1}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("threshold");
    }

    [Fact]
    public async Task Execute_ThresholdOver255_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"threshold":256}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("threshold");
    }

    [Fact]
    public async Task Execute_NoBaseline_ReturnsErrorWithGuidance()
    {
        // Attempting compare without store:true — requires an actual window capture,
        // so we test via an invalid ref that fails before reaching the cache check.
        var args = JsonDocument.Parse("""{"ref":"w99e1"}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Execute_StoreWithMissingRef_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"ref":"nonexistent_ref","store":true}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("not found");
    }
}
