using FluentAssertions;
using FlaUI.Mcp.Core;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class ScreenshotCacheTests
{
    private readonly ScreenshotCache _cache = new();

    [Fact]
    public void Store_TryTake_RoundTrip()
    {
        var data = new byte[] { 1, 2, 3 };
        _cache.Store("handle:w1", data);
        var found = _cache.TryTake("handle:w1", out var result);
        found.Should().BeTrue();
        result.Should().Equal(data);
    }

    [Fact]
    public void TryTake_NonExistent_ReturnsFalse()
    {
        var found = _cache.TryTake("handle:w99", out var result);
        found.Should().BeFalse();
        result.Should().BeEmpty();
    }

    [Fact]
    public void TryTake_EvictsAfterReturn()
    {
        _cache.Store("key", new byte[] { 9 });
        _cache.TryTake("key", out _);
        _cache.TryTake("key", out _).Should().BeFalse();
        _cache.Count.Should().Be(0);
    }

    [Fact]
    public void Has_ReflectsState()
    {
        _cache.Has("x").Should().BeFalse();
        _cache.Store("x", new byte[] { 0 });
        _cache.Has("x").Should().BeTrue();
        _cache.TryTake("x", out _);
        _cache.Has("x").Should().BeFalse();
    }
}
