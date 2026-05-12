using FlaUICapture = FlaUI.Core.Capturing.Capture;

namespace FlaUI.Mcp.Core;

internal static class ScreenshotCapturer
{
    internal readonly record struct CaptureResult(
        FlaUI.Core.Capturing.CaptureImage? Image,
        string? ScopeKey,
        string? Error);

    internal static CaptureResult Capture(
        SessionManager session, ElementRegistry registry,
        string? handle, string? refId, bool fullScreen)
    {
        if (fullScreen)
            return new CaptureResult(FlaUICapture.Screen(), "fullscreen", null);

        if (!string.IsNullOrEmpty(refId))
        {
            var element = registry.GetElement(refId);
            if (element == null)
                return new CaptureResult(null, null, $"Element not found: {refId}");
            return new CaptureResult(FlaUICapture.Element(element), $"ref:{refId}", null);
        }

        var resolution = WindowResolver.ResolveOrFocused(session, handle, registerFocused: false);
        if (resolution.Failure == WindowResolutionFailure.HandleNotFound)
            return new CaptureResult(null, null, $"Window not found: {handle}");
        if (resolution.Failure == WindowResolutionFailure.NoFocusedElement)
            return new CaptureResult(null, null, "No focused window found");
        if (resolution.Failure == WindowResolutionFailure.NoWindowAncestor)
            return new CaptureResult(null, null, "Could not find window for focused element");

        return new CaptureResult(
            FlaUICapture.Element(resolution.Window!),
            $"handle:{resolution.Handle ?? handle!}", null);
    }
}
