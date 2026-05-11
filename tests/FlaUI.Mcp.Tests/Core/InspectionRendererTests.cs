using System.Text.RegularExpressions;
using FluentAssertions;
using FlaUI.Mcp.Core;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class InspectionRendererTests : IDisposable
{
    private readonly SessionManager _session;

    public InspectionRendererTests()
    {
        _session = new SessionManager();
    }

    public void Dispose() => _session.Dispose();

    // GetDesktop() is a real AutomationElement available without launching any window.

    [Fact]
    public void Render_NeverThrows()
    {
        var desktop = _session.Automation.GetDesktop();
        var act = () => InspectionRenderer.Render(desktop, "w0e0", includePatterns: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void Render_ContainsAllSectionHeaders()
    {
        var desktop = _session.Automation.GetDesktop();
        var report = InspectionRenderer.Render(desktop, "w0e0", includePatterns: true);

        report.Should().Contain("=== Identity ===");
        report.Should().Contain("=== Geometry ===");
        report.Should().Contain("=== State ===");
        report.Should().Contain("=== Patterns ===");
    }

    [Fact]
    public void Render_IncludesPassedRefId()
    {
        var desktop = _session.Automation.GetDesktop();
        const string refId = "w7e42";
        var report = InspectionRenderer.Render(desktop, refId, includePatterns: false);
        report.Should().Contain($"ref:            {refId}");
    }

    [Fact]
    public void Render_PatternsFalse_OmitsPatternSectionAndHint()
    {
        var desktop = _session.Automation.GetDesktop();
        var report = InspectionRenderer.Render(desktop, "w0e0", includePatterns: false);

        report.Should().NotContain("=== Patterns ===");
        report.Should().NotContain("Hint:");
    }

    [Fact]
    public void Render_PatternsTrue_IncludesHint()
    {
        var desktop = _session.Automation.GetDesktop();
        var report = InspectionRenderer.Render(desktop, "w0e0", includePatterns: true);
        report.Should().Contain("Hint:");
    }

    [Fact]
    public void Render_BoundingRectLine_HasFourComponents()
    {
        var desktop = _session.Automation.GetDesktop();
        var report = InspectionRenderer.Render(desktop, "w0e0", includePatterns: false);

        var match = Regex.IsMatch(report, @"boundingRect:\s+x=-?\d+, y=-?\d+, w=\d+, h=\d+");
        match.Should().BeTrue(because: "boundingRect line must contain x/y/w/h integer fields");
    }

    [Fact]
    public void Render_ContainsProcessIdLine()
    {
        var desktop = _session.Automation.GetDesktop();
        var report = InspectionRenderer.Render(desktop, "w0e0", includePatterns: false);

        report.Should().MatchRegex(@"processId:\s+\d+");
    }

    [Fact]
    public void Render_ContainsAllKnownPatternNames()
    {
        var desktop = _session.Automation.GetDesktop();
        var report = InspectionRenderer.Render(desktop, "w0e0", includePatterns: true);

        // All 13 pattern rows must appear (supported or not)
        foreach (var name in new[]
        {
            "Invoke", "Toggle", "Value", "RangeValue", "Selection", "SelectionItem",
            "ExpandCollapse", "Scroll", "Window", "Transform", "Grid", "GridItem", "Text"
        })
        {
            report.Should().Contain($"{name}:", because: $"pattern '{name}' row must appear in output");
        }
    }
}
