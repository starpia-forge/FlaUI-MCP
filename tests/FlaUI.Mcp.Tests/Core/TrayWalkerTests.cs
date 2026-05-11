using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.UIA3;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class TrayWalkerTests : IDisposable
{
    private readonly UIA3Automation _automation;

    public TrayWalkerTests()
    {
        _automation = new UIA3Automation();
    }

    public void Dispose() => _automation.Dispose();

    // ── Non-integration: structural invariants ───────────────────────────────

    [Fact]
    public void Enumerate_ReturnsList_NeverNull()
    {
        // Enumerate must not return null — always a (possibly empty) list
        var icons = TrayWalker.Enumerate(_automation, includeOverflow: false, includeSystem: false);
        icons.Should().NotBeNull();
    }

    [Fact]
    public void TrayIcon_SourceValues_AreValid()
    {
        // TraySource enum values match the expected set used in output formatting
        var values = Enum.GetValues<TraySource>();
        values.Should().Contain(TraySource.User);
        values.Should().Contain(TraySource.System);
        values.Should().Contain(TraySource.Overflow);
    }

    // ── Integration: requires a real Windows taskbar ─────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void HasShellTrayWnd_OnClassicTaskbar_ReturnsTrue()
    {
        // Fails on Win11 native taskbar (22H2+) — document as known limitation
        TrayWalker.HasShellTrayWnd(_automation).Should().BeTrue(
            because: "Shell_TrayWnd is present on Win10 and Win11 with classic taskbar");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Enumerate_IncludeSystem_FindsAtLeastOneIcon()
    {
        // Every normal Windows machine has at least one system tray icon (Volume, Network, etc.)
        if (!TrayWalker.HasShellTrayWnd(_automation))
            return; // Skip gracefully on unsupported taskbar

        var icons = TrayWalker.Enumerate(_automation, includeOverflow: false, includeSystem: true);
        icons.Should().HaveCountGreaterThan(0,
            because: "system tray should have at least one icon (Volume/Network)");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Enumerate_ExcludeOverflow_LeavesOverflowPopupClosed()
    {
        if (!TrayWalker.HasShellTrayWnd(_automation))
            return;

        // Note state before
        var wasOpen = IsOverflowOpen();

        TrayWalker.Enumerate(_automation, includeOverflow: false, includeSystem: false);

        // Overflow should remain in the same state — we didn't touch the chevron
        IsOverflowOpen().Should().Be(wasOpen);
    }

    private bool IsOverflowOpen()
    {
        try
        {
            var desktop = _automation.GetDesktop();
            return desktop.FindFirstChild(cf => cf.ByClassName("NotifyIconOverflowWindow")) != null;
        }
        catch { return false; }
    }
}
