namespace FlaUI.Mcp.Tools;

/// <summary>
/// Raised inside a batch when an "assert" action's condition is not met.
/// Caught by the BatchTool outer handler and surfaced as an "ERROR: ..." entry
/// in the per-action result list.
/// </summary>
internal sealed class AssertFailedException : Exception
{
    public AssertFailedException(string message) : base(message) { }
}
