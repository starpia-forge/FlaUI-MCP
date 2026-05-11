using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;

namespace FlaUI.Mcp.Core;

/// <summary>
/// Provides auto-wait and stale-element retry semantics for UI Automation actions.
/// </summary>
public static class ActionExecutor
{
    public const int DefaultTimeoutMs = 5000;
    private const int RetryDelayMs = 100;
    private const uint UIA_E_ELEMENTNOTAVAILABLE = 0x80040201;

    /// <summary>
    /// Execute an action against a ref'd element. On transient COM/stale errors,
    /// attempts to re-resolve the element via stored locator and retries until
    /// success or timeoutMs elapses.
    /// </summary>
    public static T ExecuteWithRetry<T>(
        ElementRegistry registry,
        SessionManager session,
        string refId,
        Func<AutomationElement, T> action,
        int timeoutMs = DefaultTimeoutMs)
    {
        var entry = registry.GetEntry(refId)
            ?? throw new InvalidOperationException(
                $"Element '{refId}' not found. Call windows_snapshot to refresh refs.");

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        Exception? lastEx = null;

        while (true)
        {
            try
            {
                return action(entry.Element);
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                lastEx = ex;
                var refreshed = entry.TryResolve(session);
                if (refreshed != null) entry.Element = refreshed;

                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException(
                        $"Action on '{refId}' timed out after {timeoutMs}ms. " +
                        $"Last error: {lastEx.Message}. " +
                        $"Call windows_snapshot to refresh element refs.",
                        lastEx);
                }
                Thread.Sleep(RetryDelayMs);
            }
        }
    }

    /// <summary>
    /// Poll a predicate until it returns true or the timeout elapses.
    /// </summary>
    public static bool WaitUntil(Func<bool> predicate, int timeoutMs, int pollMs = 50)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            try { if (predicate()) return true; } catch { /* swallow during polling */ }
            if (DateTime.UtcNow >= deadline) return false;
            Thread.Sleep(pollMs);
        }
    }

    public static bool IsTransient(Exception ex) =>
        ex is ElementNotAvailableException ||
        ex is NoClickablePointException ||
        (ex is COMException com && (uint)com.HResult == UIA_E_ELEMENTNOTAVAILABLE);
}
