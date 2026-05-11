using FluentAssertions;
using FlaUI.Mcp.Core;
using FlaUI.UIA3;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

public class PopupRegistryTests : IDisposable
{
    private readonly SessionManager _session;

    public PopupRegistryTests()
    {
        _session = new SessionManager();
    }

    public void Dispose() => _session.Dispose();

    // ── Popup handle minting ─────────────────────────────────────────────────

    [Fact]
    public void RegisterPopup_IncrementsHandleCounter()
    {
        // GetDesktop is a real AutomationElement we can use without spinning up a window
        var desktop = _session.Automation.GetDesktop();

        var h1 = _session.RegisterPopup(desktop);
        var h2 = _session.RegisterPopup(desktop);

        h1.Should().StartWith("m");
        h2.Should().StartWith("m");
        h1.Should().NotBe(h2);
    }

    [Fact]
    public void GetPopup_ReturnsRegisteredElement()
    {
        var desktop = _session.Automation.GetDesktop();
        var handle = _session.RegisterPopup(desktop);

        var retrieved = _session.GetPopup(handle);

        retrieved.Should().NotBeNull();
        retrieved.Should().BeSameAs(desktop);
    }

    [Fact]
    public void GetPopup_UnknownHandle_ReturnsNull()
    {
        _session.GetPopup("m9999").Should().BeNull();
    }

    [Fact]
    public void ClearPopup_RemovesRegistration()
    {
        var desktop = _session.Automation.GetDesktop();
        var handle = _session.RegisterPopup(desktop);

        _session.ClearPopup(handle);

        _session.GetPopup(handle).Should().BeNull();
    }

    // ── Namespace isolation: popup handles do not collide with window handles ─

    [Fact]
    public void PopupHandles_PrefixedM_DoNotConflictWithWindowHandles()
    {
        // Window handles are "w1", "w2"; popup handles are "m1", "m2"
        // GetWindow("m1") must return null even if a popup is registered at m1
        var desktop = _session.Automation.GetDesktop();
        _session.RegisterPopup(desktop);     // registers as m1

        _session.GetWindow("m1").Should().BeNull(
            because: "window registry and popup registry are independent");
    }

    // ── GetSnapshotRoot unified lookup ───────────────────────────────────────

    [Fact]
    public void GetSnapshotRoot_ForPopupHandle_ReturnsPopupElement()
    {
        var desktop = _session.Automation.GetDesktop();
        var handle = _session.RegisterPopup(desktop);

        var root = _session.GetSnapshotRoot(handle);
        root.Should().NotBeNull();
        root.Should().BeSameAs(desktop);
    }

    [Fact]
    public void GetSnapshotRoot_UnknownHandle_ReturnsNull()
    {
        _session.GetSnapshotRoot("m9999").Should().BeNull();
        _session.GetSnapshotRoot("w9999").Should().BeNull();
    }

    // ── Menu detection helpers return non-null collections ───────────────────

    [Fact]
    public void SnapshotTopLevelMenus_ReturnsHashSet_NeverNull()
    {
        // May be empty (no open menus), but must not throw or return null
        var menus = _session.SnapshotTopLevelMenus();
        menus.Should().NotBeNull();
    }
}
