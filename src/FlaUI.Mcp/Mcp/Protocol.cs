using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlaUI.Mcp;

/// <summary>
/// MCP Protocol message types and JSON-RPC handling
/// </summary>
public static class McpProtocol
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}

// JSON-RPC request
public record JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }
    
    [JsonPropertyName("method")]
    public string Method { get; init; } = "";
    
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}

// JSON-RPC response
public record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";
    
    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }
    
    [JsonPropertyName("result")]
    public object? Result { get; init; }
    
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }
}

public record JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }
    
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    
    [JsonPropertyName("data")]
    public object? Data { get; init; }
}

// MCP-specific types
public record McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "flaui-mcp";
    
    [JsonPropertyName("version")]
    public string Version { get; init; } = "0.1.0";
}

public record McpCapabilities
{
    [JsonPropertyName("tools")]
    public ToolsCapability? Tools { get; init; }
}

public record ToolsCapability
{
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; } = false;
}

public record McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "2024-11-05";
    
    [JsonPropertyName("capabilities")]
    public McpCapabilities Capabilities { get; init; } = new();
    
    [JsonPropertyName("serverInfo")]
    public McpServerInfo ServerInfo { get; init; } = new();
}

public record McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
    
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";
    
    [JsonPropertyName("inputSchema")]
    public object InputSchema { get; init; } = new { type = "object" };
}

public record McpToolsListResult
{
    [JsonPropertyName("tools")]
    public List<McpTool> Tools { get; init; } = new();
}

public record McpToolCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
    
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; init; }
}

public record McpToolResult
{
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; init; } = new();
    
    [JsonPropertyName("isError")]
    public bool? IsError { get; init; }
}

public record McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "text";
    
    [JsonPropertyName("text")]
    public string? Text { get; init; }
    
    [JsonPropertyName("data")]
    public string? Data { get; init; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }
}
