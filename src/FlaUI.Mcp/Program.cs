using FlaUI.Mcp;
using FlaUI.Mcp.Core;
using FlaUI.Mcp.Tools;

// Create shared services
var sessionManager = new SessionManager();
var elementRegistry = new ElementRegistry();

// Register all tools
var toolRegistry = new ToolRegistry();
toolRegistry.RegisterTool(new LaunchTool(sessionManager));
toolRegistry.RegisterTool(new AttachTool(sessionManager));
toolRegistry.RegisterTool(new TrayListTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new TrayInvokeTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new SnapshotTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new InspectTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new GridCellTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new ClickTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new TypeTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new FillTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new GetTextTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new GetValueTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new SetValueTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new GetClipboardTool());
toolRegistry.RegisterTool(new SetClipboardTool());
toolRegistry.RegisterTool(new FocusedElementTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new ScreenshotTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new ListWindowsTool(sessionManager));
toolRegistry.RegisterTool(new FocusWindowTool(sessionManager));
toolRegistry.RegisterTool(new CloseWindowTool(sessionManager));
toolRegistry.RegisterTool(new WindowStateTool(sessionManager));
toolRegistry.RegisterTool(new BatchTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new WaitForTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new KeysTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new HoverTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new ScrollTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new AssertTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new DragTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new ContextMenuTool(sessionManager, elementRegistry));
toolRegistry.RegisterTool(new DismissMenuTool(sessionManager));

// Create and run MCP server
var server = new McpServer(toolRegistry);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await server.RunAsync(cts.Token);
}
finally
{
    sessionManager.Dispose();
}

