using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace ContractProcessingSystem.Shared.MCP.Official;

/// <summary>
/// Enhanced MCP Server Base using official package concepts
/// This is a hybrid approach combining your patterns with official MCP compliance
/// </summary>
public abstract class OfficialMCPServerBase : ControllerBase
{
    protected readonly ILogger Logger;

    protected OfficialMCPServerBase(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Main MCP endpoint that handles all JSON-RPC requests with official compliance
    /// </summary>
    [HttpPost("mcp")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> HandleMCPRequest([FromBody] JsonElement request)
    {
        try
        {
            Logger.LogDebug("Received MCP request: {Request}", request.ToString());

            if (!request.TryGetProperty("jsonrpc", out var jsonrpcProp) || 
                jsonrpcProp.GetString() != "2.0")
            {
                return Ok(CreateErrorResponse(request, -32600, "Invalid JSON-RPC version"));
            }

            if (!request.TryGetProperty("method", out var methodProp))
            {
                return Ok(CreateErrorResponse(request, -32600, "Missing method"));
            }

            var method = methodProp.GetString()!;
            var hasParams = request.TryGetProperty("params", out var paramsProp);
            var parameters = hasParams ? paramsProp : JsonDocument.Parse("{}").RootElement;

            var response = method switch
            {
                "initialize" => await HandleInitialize(request),
                "tools/list" => await HandleListTools(request),
                "tools/call" => await HandleCallTool(request, parameters),
                "resources/list" => await HandleListResources(request),
                "resources/read" => await HandleReadResource(request, parameters),
                _ => CreateErrorResponse(request, -32601, $"Method not found: {method}")
            };

            Logger.LogDebug("MCP response: {Response}", JsonSerializer.Serialize(response));
            return Ok(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error in MCP request handling");
            return Ok(CreateErrorResponse(request, -32603, "Internal error"));
        }
    }

    /// <summary>
    /// Health check endpoint for MCP server
    /// </summary>
    [HttpGet("mcp/health")]
    public IActionResult Health()
    {
        return Ok(new 
        { 
            service = GetType().Name,
            status = "healthy",
            timestamp = DateTime.UtcNow,
            protocol = "Model Context Protocol",
            version = "2024-11-05"
        });
    }

    // Abstract methods that services must implement
    protected abstract Task<List<OfficialMCPTool>> GetAvailableTools();
    protected abstract Task<OfficialMCPToolResult> ExecuteTool(string toolName, JsonElement arguments);
    protected abstract Task<List<OfficialMCPResource>> GetAvailableResources();
    protected abstract Task<object> ReadResource(string uri);

    // Protocol handlers with official MCP compliance
    protected virtual async Task<object> HandleInitialize(JsonElement request)
    {
        await Task.CompletedTask;
        var result = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { },
                resources = new { }
            },
            serverInfo = new
            {
                name = GetType().Name.Replace("Controller", "").Replace("MCP", ""),
                version = "1.0.0"
            }
        };

        return CreateSuccessResponse(request, result);
    }

    protected virtual async Task<object> HandleListTools(JsonElement request)
    {
        var tools = await GetAvailableTools();
        var result = new { tools = tools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            inputSchema = t.InputSchema
        })};

        return CreateSuccessResponse(request, result);
    }

    protected virtual async Task<object> HandleCallTool(JsonElement request, JsonElement parameters)
    {
        if (!parameters.TryGetProperty("name", out var nameProp))
        {
            return CreateErrorResponse(request, -32602, "Missing tool name");
        }

        var toolName = nameProp.GetString()!;
        var arguments = parameters.TryGetProperty("arguments", out var argsProp) 
            ? argsProp 
            : JsonDocument.Parse("{}").RootElement;

        var result = await ExecuteTool(toolName, arguments);
        
        if (result.IsError)
        {
            return CreateErrorResponse(request, result.ErrorCode ?? -32000, result.ErrorMessage!, result.Content);
        }

        return CreateSuccessResponse(request, new { content = result.Content });
    }

    protected virtual async Task<object> HandleListResources(JsonElement request)
    {
        var resources = await GetAvailableResources();
        var result = new { resources = resources.Select(r => new
        {
            uri = r.Uri,
            name = r.Name,
            description = r.Description,
            mimeType = r.MimeType
        })};

        return CreateSuccessResponse(request, result);
    }

    protected virtual async Task<object> HandleReadResource(JsonElement request, JsonElement parameters)
    {
        if (!parameters.TryGetProperty("uri", out var uriProp))
        {
            return CreateErrorResponse(request, -32602, "Missing resource URI");
        }

        var uri = uriProp.GetString()!;
        var content = await ReadResource(uri);

        return CreateSuccessResponse(request, new { contents = new[] { new
        {
            uri = uri,
            mimeType = "application/json",
            text = JsonSerializer.Serialize(content)
        }}});
    }

    protected static object CreateSuccessResponse(JsonElement request, object result)
    {
        return new
        {
            jsonrpc = "2.0",
            id = request.TryGetProperty("id", out var idProp) ? idProp : JsonDocument.Parse("null").RootElement,
            result = result
        };
    }

    protected static object CreateErrorResponse(JsonElement request, int errorCode, string message, object? data = null)
    {
        return new
        {
            jsonrpc = "2.0",
            id = request.TryGetProperty("id", out var idProp) ? idProp : JsonDocument.Parse("null").RootElement,
            error = new
            {
                code = errorCode,
                message = message,
                data = data
            }
        };
    }
}

/// <summary>
/// Official MCP-compliant tool definition
/// </summary>
public class OfficialMCPTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object InputSchema { get; set; } = new { };
}

/// <summary>
/// Official MCP-compliant resource definition
/// </summary>
public class OfficialMCPResource
{
    public string Uri { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/json";
}

/// <summary>
/// MCP tool result for official package integration
/// </summary>
public class OfficialMCPToolResult
{
    public bool IsError { get; set; }
    public object? Content { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ErrorCode { get; set; }

    public static OfficialMCPToolResult Success(object content)
    {
        return new OfficialMCPToolResult 
        { 
            IsError = false, 
            Content = content 
        };
    }

    public static OfficialMCPToolResult Error(string message, int code = -32000)
    {
        return new OfficialMCPToolResult 
        { 
            IsError = true, 
            ErrorMessage = message, 
            ErrorCode = code 
        };
    }
}

/// <summary>
/// Service extensions for registering official MCP servers
/// </summary>
public static class OfficialMCPServiceExtensions
{
    public static IServiceCollection AddOfficialMCPServer<T>(
        this IServiceCollection services,
        string serverName,
        string serverVersion = "1.0.0")
        where T : class
    {
        // Register the service implementation
        services.AddScoped<T>();
        
        // Add MCP-specific configuration
        services.Configure<OfficialMCPServerOptions>(options =>
        {
            options.ServerName = serverName;
            options.ServerVersion = serverVersion;
        });

        return services;
    }
}

/// <summary>
/// Configuration options for official MCP servers
/// </summary>
public class OfficialMCPServerOptions
{
    public string ServerName { get; set; } = string.Empty;
    public string ServerVersion { get; set; } = "1.0.0";
}