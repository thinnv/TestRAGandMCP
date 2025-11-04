using System.Text.Json;
using System.Text;

namespace ContractProcessingSystem.Shared.MCP;

public class MCPClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<MCPClient> _logger;
    private int _requestId = 1;

    public MCPClient(HttpClient httpClient, string baseUrl, ILogger<MCPClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
        _logger = logger;
    }

    public async Task<MCPResponse> SendRequestAsync(string method, object? parameters = null)
    {
        var request = new MCPRequest
        {
            Id = _requestId++,
            Method = method,
            Params = parameters != null ? JsonSerializer.SerializeToElement(parameters) : null
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        _logger.LogDebug("Sending MCP request: {Method}", method);
        
        var response = await _httpClient.PostAsync($"{_baseUrl}/mcp", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MCP request failed: {response.StatusCode} - {responseContent}");
        }

        var mcpResponse = JsonSerializer.Deserialize<MCPResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (mcpResponse?.Error != null)
        {
            throw new MCPException(mcpResponse.Error.Code, mcpResponse.Error.Message, mcpResponse.Error.Data);
        }

        return mcpResponse ?? throw new InvalidOperationException("Invalid MCP response");
    }

    public async Task<MCPServerInfo> InitializeAsync()
    {
        var response = await SendRequestAsync("initialize", new { protocolVersion = "2024-11-05" });
        if (response.Result == null)
        {
            throw new InvalidOperationException("No server info in initialize response");
        }

        var serverInfo = JsonSerializer.Deserialize<MCPServerInfo>(
            JsonSerializer.Serialize(response.Result), 
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return serverInfo ?? throw new InvalidOperationException("Invalid server info");
    }

    public async Task<List<MCPTool>> ListToolsAsync()
    {
        var response = await SendRequestAsync("tools/list");
        if (response.Result == null)
        {
            return new List<MCPTool>();
        }

        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(response.Result));

        if (result != null && result.TryGetValue("tools", out var toolsObj))
        {
            var tools = JsonSerializer.Deserialize<List<MCPTool>>(
                JsonSerializer.Serialize(toolsObj),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return tools ?? new List<MCPTool>();
        }

        return new List<MCPTool>();
    }

    public async Task<MCPToolResult> CallToolAsync(string toolName, object arguments)
    {
        var toolCall = new MCPToolCall
        {
            Name = toolName,
            Arguments = JsonSerializer.SerializeToElement(arguments)
        };

        var response = await SendRequestAsync("tools/call", toolCall);
        
        if (response.Result == null)
        {
            throw new InvalidOperationException("No result in tool call response");
        }

        var toolResult = JsonSerializer.Deserialize<MCPToolResult>(
            JsonSerializer.Serialize(response.Result),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return toolResult ?? throw new InvalidOperationException("Invalid tool result");
    }

    public async Task<List<MCPResource>> ListResourcesAsync()
    {
        var response = await SendRequestAsync("resources/list");
        if (response.Result == null)
        {
            return new List<MCPResource>();
        }

        var result = JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(response.Result));

        if (result != null && result.TryGetValue("resources", out var resourcesObj))
        {
            var resources = JsonSerializer.Deserialize<List<MCPResource>>(
                JsonSerializer.Serialize(resourcesObj),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            return resources ?? new List<MCPResource>();
        }

        return new List<MCPResource>();
    }

    public async Task<object> ReadResourceAsync(string uri)
    {
        var response = await SendRequestAsync("resources/read", new { uri });
        
        return response.Result ?? throw new InvalidOperationException("No content in resource response");
    }
}

public class MCPException : Exception
{
    public int Code { get; }
    public new object? Data { get; }

    public MCPException(int code, string message, object? data = null) : base(message)
    {
        Code = code;
        Data = data;
    }
}

// Extension methods for easier client usage
public static class MCPClientExtensions
{
    public static async Task<T?> CallToolAsync<T>(this MCPClient client, string toolName, object arguments)
    {
        var result = await client.CallToolAsync(toolName, arguments);
        
        if (result.IsError)
        {
            throw new InvalidOperationException($"Tool call failed: {result.Content.FirstOrDefault()?.Text}");
        }

        var dataContent = result.Content.FirstOrDefault(c => c.Type == "application/json");
        if (dataContent?.Data != null)
        {
            return JsonSerializer.Deserialize<T>(
                JsonSerializer.Serialize(dataContent.Data),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        return default;
    }

    public static async Task<string> CallToolForTextAsync(this MCPClient client, string toolName, object arguments)
    {
        var result = await client.CallToolAsync(toolName, arguments);
        
        if (result.IsError)
        {
            throw new InvalidOperationException($"Tool call failed: {result.Content.FirstOrDefault()?.Text}");
        }

        return result.Content.FirstOrDefault()?.Text ?? string.Empty;
    }
}