using System.Text.Json;

namespace PlaywrightWindows.Mcp;

/// <summary>
/// MCP Server that handles JSON-RPC over stdio
/// </summary>
public class McpServer
{
    private readonly ToolRegistry _toolRegistry;
    private bool _initialized = false;

    private const int ErrMethodNotFound = -32601;
    private const int ErrInternal       = -32603;
    private const int ErrServerUninit   = -32002;

    public McpServer(ToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        using var reader = new StreamReader(stdin);
        using var writer = new StreamWriter(stdout) { AutoFlush = true };

        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, McpProtocol.JsonOptions);
                if (request == null) continue;

                var response = await HandleRequestAsync(request);
                if (response != null)
                {
                    var responseJson = JsonSerializer.Serialize(response, McpProtocol.JsonOptions);
                    await writer.WriteLineAsync(responseJson);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing request: {ex.Message}");
            }
        }
    }

    private async Task<JsonRpcResponse?> HandleRequestAsync(JsonRpcRequest request)
    {
        // Spec: never respond to notifications (requests without an id)
        if (request.Id == null)
        {
            if (request.Method == "notifications/initialized")
                _initialized = true;
            return null;
        }

        try
        {
            // ping is always allowed, even before initialize
            if (request.Method == "ping")
                return OkResponse(request.Id, new { });

            if (request.Method == "initialize")
                return OkResponse(request.Id, HandleInitialize(request));

            if (!_initialized)
                return ErrorResponse(request.Id, ErrServerUninit, "Server not initialized. Send initialize first.");

            object result = request.Method switch
            {
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolCallAsync(request),
                _ => throw new MethodNotFoundException(request.Method)
            };
            return OkResponse(request.Id, result);
        }
        catch (MethodNotFoundException mnf)
        {
            return ErrorResponse(request.Id, ErrMethodNotFound, mnf.Message);
        }
        catch (Exception ex)
        {
            return ErrorResponse(request.Id, ErrInternal, ex.Message);
        }
    }

    private McpInitializeResult HandleInitialize(JsonRpcRequest request)
    {
        _initialized = true;
        return new McpInitializeResult
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = new McpCapabilities
            {
                Tools = new ToolsCapability { ListChanged = false }
            },
            ServerInfo = new McpServerInfo
            {
                Name = "flaui-mcp",
                Version = "0.1.0"
            }
        };
    }

    private McpToolsListResult HandleToolsList()
    {
        return new McpToolsListResult
        {
            Tools = _toolRegistry.GetToolDefinitions()
        };
    }

    private async Task<McpToolResult> HandleToolCallAsync(JsonRpcRequest request)
    {
        if (request.Params == null)
            return ErrorToolResult("Missing params");

        var callParams = JsonSerializer.Deserialize<McpToolCallParams>(
            request.Params.Value.GetRawText(),
            McpProtocol.JsonOptions);

        if (callParams == null)
            return ErrorToolResult("Invalid tool call params");

        return await _toolRegistry.ExecuteToolAsync(callParams.Name, callParams.Arguments);
    }

    private static JsonRpcResponse OkResponse(JsonElement? id, object result) =>
        new() { Id = id, Result = result };

    private static JsonRpcResponse ErrorResponse(JsonElement? id, int code, string message) =>
        new() { Id = id, Error = new JsonRpcError { Code = code, Message = message } };

    private static McpToolResult ErrorToolResult(string message) => new()
    {
        Content = new List<McpContent> { new() { Type = "text", Text = message } },
        IsError = true
    };

    private sealed class MethodNotFoundException : Exception
    {
        public MethodNotFoundException(string method)
            : base($"Method not found: {method}") { }
    }
}
