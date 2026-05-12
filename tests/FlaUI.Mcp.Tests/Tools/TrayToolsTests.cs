using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class TrayToolsTests : IDisposable
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;
    private readonly TrayListTool _list;
    private readonly TrayInvokeTool _invoke;

    public TrayToolsTests()
    {
        _session = new SessionManager();
        _registry = new ElementRegistry();
        _list = new TrayListTool(_session, _registry);
        _invoke = new TrayInvokeTool(_session, _registry);
    }

    public void Dispose() => _session.Dispose();

    // ── TrayInvokeTool argument validation ───────────────────────────────────

    [Fact]
    public async Task TrayInvoke_MissingRef_ReturnsError()
    {
        var args = JsonDocument.Parse("{}").RootElement;
        var result = await _invoke.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ref");
    }

    [Fact]
    public async Task TrayInvoke_UnknownRef_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"ref":"traye999"}""").RootElement;
        var result = await _invoke.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("not found in registry");
    }

    [Fact]
    public async Task TrayInvoke_NullArguments_ReturnsError()
    {
        var result = await _invoke.ExecuteAsync(null);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("ref");
    }

    // ── TrayListTool does not throw on valid arguments ───────────────────────

    [Fact]
    public async Task TrayList_NoArguments_DoesNotThrow()
    {
        // includeOverflow:false avoids opening the user's tray flyout and emitting
        // Escape during test runs (TrayWalker.ExpandOverflow fires real OS keystrokes).
        // Overflow expansion is an integration-only concern verified manually.
        var args = JsonDocument.Parse("""{"includeOverflow":false}""").RootElement;
        // May return empty result if Shell_TrayWnd absent (e.g. Win11 native taskbar)
        // but must NOT throw or return an unhandled exception
        var result = await _list.ExecuteAsync(args);
        result.Content.Should().HaveCount(1);
        result.Content[0].Text.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TrayList_IncludeSystemFalse_DoesNotThrow()
    {
        var args = JsonDocument.Parse("""{"includeSystem":false,"includeOverflow":false}""").RootElement;
        var result = await _list.ExecuteAsync(args);
        // IsError is bool? — null means success (not explicitly set to true)
        result.IsError.GetValueOrDefault().Should().BeFalse();
    }
}
