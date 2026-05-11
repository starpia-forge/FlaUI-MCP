using System.Text.Json;
using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;
using Xunit;

namespace FlaUI.Mcp.Tests.Tools;

public class AttachToolTests : IDisposable
{
    private readonly SessionManager _session;
    private readonly AttachTool _tool;

    public AttachToolTests()
    {
        _session = new SessionManager();
        _tool = new AttachTool(_session);
    }

    public void Dispose() => _session.Dispose();

    [Fact]
    public async Task Execute_BothPidAndProcessName_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"pid":1234,"processName":"notepad"}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("exactly one");
    }

    [Fact]
    public async Task Execute_NeitherPidNorProcessName_ReturnsError()
    {
        var args = JsonDocument.Parse("{}").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("either");
    }

    [Fact]
    public async Task Execute_NonexistentProcessName_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"processName":"__no_such_app_xyz__"}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("No running process");
    }

    [Fact]
    public async Task Execute_NonexistentPid_ReturnsError()
    {
        var args = JsonDocument.Parse("""{"pid":2147483647}""").RootElement;
        var result = await _tool.ExecuteAsync(args);
        result.IsError.Should().BeTrue();
        result.Content[0].Text.Should().Contain("No running process with pid=");
    }
}
