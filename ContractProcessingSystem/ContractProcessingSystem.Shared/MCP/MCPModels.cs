using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContractProcessingSystem.Shared.MCP;

// MCP Protocol Models
public class MCPRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; set; }
    
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;
    
    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

public class MCPResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; set; }
    
    [JsonPropertyName("result")]
    public object? Result { get; set; }
    
    [JsonPropertyName("error")]
    public MCPError? Error { get; set; }
}

public class MCPError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

// Tool Definitions
public class MCPTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }
}

public class MCPToolCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; set; }
}

public class MCPToolResult
{
    [JsonPropertyName("content")]
    public List<MCPContent> Content { get; set; } = new();
    
    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

public class MCPContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

// Resource Models
public class MCPResource
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

public class MCPResourceTemplate
{
    [JsonPropertyName("uriTemplate")]
    public string UriTemplate { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

// Server Info
public class MCPServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }
    
    [JsonPropertyName("capabilities")]
    public MCPServerCapabilities Capabilities { get; set; } = new();
}

public class MCPServerCapabilities
{
    [JsonPropertyName("tools")]
    public bool Tools { get; set; } = true;
    
    [JsonPropertyName("resources")]
    public bool Resources { get; set; } = true;
    
    [JsonPropertyName("prompts")]
    public bool Prompts { get; set; } = false;
    
    [JsonPropertyName("logging")]
    public bool Logging { get; set; } = true;
}

// Standard Error Codes
public static class MCPErrorCodes
{
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    public const int ParseError = -32700;
    
    // Custom error codes for contract processing
    public const int DocumentNotFound = -32001;
    public const int ProcessingError = -32002;
    public const int AuthenticationError = -32003;
    public const int RateLimitExceeded = -32004;
    public const int ResourceUnavailable = -32005;
}