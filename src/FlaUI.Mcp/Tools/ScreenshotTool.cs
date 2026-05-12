using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

public class ScreenshotTool : ToolBase
{
    private readonly SessionManager _sessionManager;
    private readonly ElementRegistry _elementRegistry;

    public ScreenshotTool(SessionManager sessionManager, ElementRegistry elementRegistry)
    {
        _sessionManager = sessionManager;
        _elementRegistry = elementRegistry;
    }

    public override string Name => "windows_screenshot";

    public override string Description =>
        "Take a screenshot of a window or specific element. Returns the image as base64-encoded PNG.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            handle = new
            {
                type = "string",
                description = "Window handle. If omitted, captures the foreground window."
            },
            @ref = new
            {
                type = "string",
                description = "Element ref to capture. If omitted, captures the whole window."
            },
            fullScreen = new
            {
                type = "boolean",
                description = "Capture the entire screen (default: false)"
            }
        }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var handle     = GetStringArgument(arguments, "handle");
        var refId      = GetStringArgument(arguments, "ref");
        var fullScreen = GetBoolArgument(arguments, "fullScreen", false);

        var result = ScreenshotCapturer.Capture(_sessionManager, _elementRegistry, handle, refId, fullScreen);
        if (result.Error != null)
            return Task.FromResult(ErrorResult(result.Error));

        try
        {
            using var capture = result.Image!;
            using var stream = new MemoryStream();
            capture.Bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return Task.FromResult(ImageResult(stream.ToArray(), "image/png"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult($"Failed to capture screenshot: {ex.Message}"));
        }
    }
}
