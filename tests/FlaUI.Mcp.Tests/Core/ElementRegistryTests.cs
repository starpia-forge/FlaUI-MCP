using FlaUI.Core.Definitions;
using FlaUI.Mcp.Core;
using FluentAssertions;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class ElementRegistryTests
{
    private static ElementRegistry Registry() => new();

    [Fact]
    public void Register_FirstEntry_ReturnsW1E1()
    {
        var reg = Registry();
        var refId = reg.RegisterForTest("w1");
        refId.Should().Be("w1e1");
    }

    [Fact]
    public void Register_SecondEntryInSameWindow_IncrementsCounter()
    {
        var reg = Registry();
        reg.RegisterForTest("w1");
        var refId = reg.RegisterForTest("w1");
        refId.Should().Be("w1e2");
    }

    [Fact]
    public void Register_DifferentWindows_HaveIndependentCounters()
    {
        var reg = Registry();
        var a = reg.RegisterForTest("w1");
        var b = reg.RegisterForTest("w2");
        a.Should().Be("w1e1");
        b.Should().Be("w2e1");
    }

    [Fact]
    public void ClearWindow_RemovesOnlyThatWindowsRefs()
    {
        var reg = Registry();
        reg.RegisterForTest("w1");
        reg.RegisterForTest("w1");
        reg.RegisterForTest("w2");
        reg.RegisterForTest("w10"); // must NOT be cleared by ClearWindow("w1")

        reg.ClearWindow("w1");

        reg.HasElement("w1e1").Should().BeFalse();
        reg.HasElement("w1e2").Should().BeFalse();
        reg.HasElement("w2e1").Should().BeTrue();
        reg.HasElement("w10e1").Should().BeTrue(); // regression: StartsWith("w1e") must not match "w10e..."
    }

    [Fact]
    public void ClearWindow_ResetsCounter_SoNextRegisterStartsAt1()
    {
        var reg = Registry();
        reg.RegisterForTest("w1");
        reg.RegisterForTest("w1");
        reg.ClearWindow("w1");

        var newRef = reg.RegisterForTest("w1");
        newRef.Should().Be("w1e1");
    }

    [Fact]
    public void HasElement_And_GetEntry_RoundTrip()
    {
        var reg = Registry();
        var refId = reg.RegisterForTest("w3", autoId: "btnOk", name: "OK", ct: ControlType.Button);

        reg.HasElement(refId).Should().BeTrue();
        var entry = reg.GetEntry(refId);
        entry.Should().NotBeNull();
        entry!.AutomationId.Should().Be("btnOk");
        entry.Name.Should().Be("OK");
        entry.ControlType.Should().Be(ControlType.Button);
        entry.WindowHandle.Should().Be("w3");
    }

    [Fact]
    public void Concurrent_Register_ProducesUniqueRefIds()
    {
        var reg = Registry();
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 100, _ => results.Add(reg.RegisterForTest("w1")));

        results.Should().HaveCount(100);
        results.Distinct().Should().HaveCount(100, "all ref ids must be unique under concurrent access");
    }
}
