using FluentAssertions;
using FlaUI.Mcp.Core;
using Xunit;

namespace FlaUI.Mcp.Tests.Core;

/// <summary>
/// Integration tests for SessionManager.AttachByProcess.
/// Tests that start real processes are marked with [Trait("Category","Integration")].
/// </summary>
public class SessionManagerAttachTests : IDisposable
{
    private readonly SessionManager _session;

    public SessionManagerAttachTests()
    {
        _session = new SessionManager();
    }

    public void Dispose() => _session.Dispose();

    // ── Argument validation ──────────────────────────────────────────────────

    [Fact]
    public void AttachByProcess_BothArguments_Throws()
    {
        var ex = Assert.Throws<Exception>(() => _session.AttachByProcess(1234, "notepad"));
        ex.Message.Should().Contain("exactly one");
    }

    [Fact]
    public void AttachByProcess_NeitherArgument_Throws()
    {
        var ex = Assert.Throws<Exception>(() => _session.AttachByProcess(null, null));
        ex.Message.Should().Contain("either");
    }

    // ── Process resolution errors ────────────────────────────────────────────

    [Fact]
    public void AttachByProcess_NonexistentPid_Throws()
    {
        var ex = Assert.Throws<Exception>(() => _session.AttachByProcess(2147483647, null));
        ex.Message.Should().Contain("No running process with pid=2147483647");
    }

    [Fact]
    public void AttachByProcess_NonexistentName_Throws()
    {
        var ex = Assert.Throws<Exception>(() => _session.AttachByProcess(null, "__no_such_process_xyz__"));
        ex.Message.Should().Contain("No running process named '__no_such_process_xyz__'");
    }

    [Fact]
    public void AttachByProcess_NameWithExeExtension_StripsExtension()
    {
        // .exe suffix should be stripped before lookup
        var ex = Assert.Throws<Exception>(() => _session.AttachByProcess(null, "__no_such_process_xyz__.exe"));
        ex.Message.Should().Contain("No running process named '__no_such_process_xyz__'");
    }

    // ── Integration: real processes ──────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void AttachByProcess_ByPid_ReturnsHandles()
    {
        using var notepad = LaunchNotepad();
        var attached = _session.AttachByProcess(notepad.Process.Id, null);
        attached.Should().HaveCountGreaterThan(0);
        attached[0].Handle.Should().MatchRegex(@"^w\d+$");
        attached[0].OwnerPid.Should().Be(notepad.Process.Id);
        attached[0].IsVisible.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AttachByProcess_ByProcessName_ReturnsHandles()
    {
        using var notepad = LaunchNotepad();
        var attached = _session.AttachByProcess(null, "notepad");
        attached.Should().HaveCountGreaterThan(0);
        attached[0].OwnerPid.Should().Be(notepad.Process.Id);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void AttachByProcess_SamePidTwice_DeduplicatesHandles()
    {
        // RegisterWindow deduplicates by HWND — same window gets the same handle both times
        using var notepad = LaunchNotepad();
        var first = _session.AttachByProcess(notepad.Process.Id, null);
        var second = _session.AttachByProcess(notepad.Process.Id, null);
        second[0].Handle.Should().Be(first[0].Handle);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static NotepadHandle LaunchNotepad() => new();

    /// <summary>
    /// Owns a launched notepad.exe and guarantees it is killed on Dispose.
    /// Plain Process.Dispose() only releases the OS handle — it does not terminate
    /// the process, which would leak notepad windows and stall test-host shutdown.
    /// </summary>
    private sealed class NotepadHandle : IDisposable
    {
        public System.Diagnostics.Process Process { get; }

        public NotepadHandle()
        {
            Process = System.Diagnostics.Process.Start("notepad.exe")
                ?? throw new InvalidOperationException("Failed to start notepad.exe");
            Thread.Sleep(2000);  // wait for window to appear
        }

        public void Dispose()
        {
            try
            {
                if (!Process.HasExited)
                {
                    Process.Kill(entireProcessTree: true);
                    Process.WaitForExit(3000);
                }
            }
            catch { /* best-effort cleanup — process may have already exited */ }
            finally { Process.Dispose(); }
        }
    }
}
