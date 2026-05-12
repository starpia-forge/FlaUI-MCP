using System.Text.Json;
using FlaUI.Mcp.Core;

namespace FlaUI.Mcp.Tools;

/// <summary>
/// Access a Grid/Table cell directly by (row, col) via UIA GridPattern.GetItem.
/// </summary>
public class GridCellTool : ToolBase
{
    private readonly SessionManager _session;
    private readonly ElementRegistry _registry;

    public GridCellTool(SessionManager session, ElementRegistry registry)
    {
        _session = session;
        _registry = registry;
    }

    public override string Name => "windows_grid_cell";

    public override string Description =>
        "Access a Grid or Table cell directly by (row, col) index via UIA GridPattern.GetItem. " +
        "Returns a new element ref registered to the same window — usable immediately with " +
        "windows_click, windows_get_value, windows_inspect, etc. " +
        "Works on virtualized data grids where offscreen cells are absent from the snapshot tree. " +
        "row and col are 0-based.";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            @ref = new
            {
                type = "string",
                description = "Element ref of the Grid or Table container (e.g. 'w1e5')."
            },
            row = new
            {
                type = "integer",
                description = "0-based row index."
            },
            col = new
            {
                type = "integer",
                description = "0-based column index."
            },
            timeoutMs = new
            {
                type = "integer",
                description = "Operation timeout in milliseconds. Default 5000."
            }
        },
        required = new[] { "ref", "row", "col" }
    };

    public override Task<McpToolResult> ExecuteAsync(JsonElement? arguments)
    {
        var refId = GetStringArgument(arguments, "ref");
        if (string.IsNullOrEmpty(refId))
            return Task.FromResult(ErrorResult("Missing required argument: ref"));

        var rowOpt = GetArgument<int?>(arguments, "row");
        if (rowOpt is null)
            return Task.FromResult(ErrorResult("Missing required argument: row"));

        var colOpt = GetArgument<int?>(arguments, "col");
        if (colOpt is null)
            return Task.FromResult(ErrorResult("Missing required argument: col"));

        var row = rowOpt.Value;
        var col = colOpt.Value;

        if (row < 0 || col < 0)
            return Task.FromResult(ErrorResult("row and col must be non-negative"));

        var timeoutMs = GetArgument<int?>(arguments, "timeoutMs") ?? ActionExecutor.DefaultTimeoutMs;

        var parentEntry = _registry.GetEntry(refId);
        if (parentEntry is null)
            return Task.FromResult(ErrorResult(
                $"Element '{refId}' not found. Call windows_snapshot to refresh element refs."));

        var windowHandle = parentEntry.WindowHandle;

        try
        {
            var line = ActionExecutor.ExecuteWithRetry(
                _registry, _session, refId,
                parent =>
                {
                    if (!SafeAccess.Get(() => parent.Patterns.Grid.IsSupported, false))
                        throw new InvalidOperationException(
                            $"Element '{refId}' does not support GridPattern. " +
                            "Use windows_inspect to verify supported patterns.");

                    var gridPattern = parent.Patterns.Grid.Pattern;
                    var rowCount = SafeAccess.Get(() => gridPattern.RowCount.ValueOrDefault, 0);
                    var colCount = SafeAccess.Get(() => gridPattern.ColumnCount.ValueOrDefault, 0);

                    if (row >= rowCount || col >= colCount)
                        throw new InvalidOperationException(
                            $"Index out of range: row={row}/{rowCount}, col={col}/{colCount}");

                    var cell = gridPattern.GetItem(row, col)
                        ?? throw new InvalidOperationException(
                            $"Grid returned null for cell ({row}, {col})");

                    var newRef = _registry.Register(windowHandle, cell);
                    return GridCellFormatter.FormatLine(newRef, cell);
                },
                timeoutMs);

            return Task.FromResult(TextResult(line));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorResult(
                $"Failed to get grid cell '{refId}' ({row},{col}): {ex.Message}. " +
                "Call windows_snapshot to refresh element refs."));
        }
    }
}
