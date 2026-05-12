using System.Drawing.Imaging;
using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

public class ScreenshotDiffTool : ToolBase
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;
    private readonly ScreenshotCache _cache;
    private const int DefaultThreshold = 10;

    public ScreenshotDiffTool(SessionManager session, ElementRegistry registry, ScreenshotCache cache)
    {
        _session = session;
        _registry = registry;
        _cache = cache;
    }

    public override string Name => "windows_screenshot_diff";

    public override string Description =>
        "Detect visual changes between two points in time for a window, element, or full screen. " +
        "Call once with store:true to capture a baseline, perform actions, then call again (store omitted) " +
        "to capture and diff against the baseline. Returns a single bounding rectangle that contains all " +
        "changed pixels plus a changed-pixel count and percentage. Useful for verifying spinners completed, " +
        "dialogs appeared, or any region updated. Baseline is keyed by handle/ref/fullScreen scope and " +
        "evicted after compare.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            handle = new { type = "string", description = "Window handle (e.g. 'w1'). Omit to use focused window. Mutually exclusive with ref/fullScreen." },
            @ref = new { type = "string", description = "Element ref. Mutually exclusive with handle/fullScreen." },
            fullScreen = new { type = "boolean", description = "Diff the full screen." },
            store = new { type = "boolean", description = "If true, store baseline only (no diff). Default false." },
            threshold = new { type = "integer", description = "Per-channel RGB tolerance (0-255). Default 10. Higher = ignore more noise." }
        }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var handle     = GetStringArgument(arguments, "handle");
        var refId      = GetStringArgument(arguments, "ref");
        var fullScreen = GetBoolArgument(arguments, "fullScreen", false);
        var store      = GetBoolArgument(arguments, "store", false);
        var threshold  = GetArgument<int?>(arguments, "threshold") ?? DefaultThreshold;

        if (threshold < 0 || threshold > 255)
            return Task.FromResult(ErrorResult("threshold must be between 0 and 255."));

        ScreenshotCapturer.CaptureResult captureResult;
        try
        {
            captureResult = ScreenshotCapturer.Capture(_session, _registry, handle, refId, fullScreen);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to capture screenshot: {ex.Message}"));
        }

        if (captureResult.Error != null)
            return Task.FromResult(ErrorResult(captureResult.Error));

        var scopeKey = captureResult.ScopeKey!;
        try
        {
            using var image = captureResult.Image!;
            using var stream = new MemoryStream();
            image.Bitmap.Save(stream, ImageFormat.Png);
            var currentPng = stream.ToArray();

            if (store)
            {
                _cache.Store(scopeKey, currentPng);
                return Task.FromResult(TextResult(
                    $"Baseline stored for {scopeKey} ({image.Bitmap.Width}x{image.Bitmap.Height})"));
            }

            if (!_cache.TryTake(scopeKey, out var baselinePng))
                return Task.FromResult(ErrorResult(
                    $"No baseline found for {scopeKey}. Call with store:true first."));

            var diff = ImageDiffer.Compare(baselinePng, currentPng, threshold);

            if (!diff.SameSize)
                return Task.FromResult(ErrorResult(
                    $"Baseline size {diff.BeforeSize.Width}x{diff.BeforeSize.Height} does not match " +
                    $"current {diff.AfterSize.Width}x{diff.AfterSize.Height} (window resized?). " +
                    $"Baseline evicted; call with store:true again."));

            var size = $"{diff.AfterSize.Width}x{diff.AfterSize.Height}";
            if (diff.ChangedBounds == null)
                return Task.FromResult(TextResult(
                    $"Diff: {scopeKey} ({size})\nNo changes detected (threshold={threshold})"));

            var r = diff.ChangedBounds.Value;
            var pct = (diff.ChangedPixels * 100.0) / diff.TotalPixels;
            return Task.FromResult(TextResult(
                $"Diff: {scopeKey} ({size})\n" +
                $"Changed region: x={r.X}, y={r.Y}, w={r.Width}, h={r.Height}\n" +
                $"Changed pixels: {diff.ChangedPixels:N0} / {diff.TotalPixels:N0} ({pct:F2}%)"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to diff screenshots: {ex.Message}"));
        }
    }
}
