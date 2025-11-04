using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ContractProcessingSystem.Shared.MCP;

public abstract class BaseMCPServer : ControllerBase
{
    protected readonly ILogger Logger;
    protected readonly MCPServerInfo ServerInfo;

    protected BaseMCPServer(ILogger logger, MCPServerInfo serverInfo)
    {
        Logger = logger;
        ServerInfo = serverInfo;
    }

    // Core MCP protocol endpoints
    [HttpPost("mcp")]
    public async Task<IActionResult> HandleMCPRequest([FromBody] MCPRequest request)
    {
        try
        {
            Logger.LogDebug("Received MCP request: {Method}", request.Method);

            var response = request.Method switch
            {
                "initialize" => await HandleInitialize(request),
                "tools/list" => await HandleListTools(request),
                "tools/call" => await HandleToolCall(request),
                "resources/list" => await HandleListResources(request),
                "resources/read" => await HandleReadResource(request),
                _ => CreateErrorResponse(request.Id, MCPErrorCodes.MethodNotFound, $"Unknown method: {request.Method}")
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling MCP request");
            return Ok(CreateErrorResponse(request.Id, MCPErrorCodes.InternalError, ex.Message));
        }
    }

    // Abstract methods for service-specific implementations
    protected abstract Task<List<MCPTool>> GetAvailableTools();
    protected abstract Task<MCPToolResult> ExecuteTool(string toolName, JsonElement arguments);
    protected abstract Task<List<MCPResource>> GetAvailableResources();
    protected abstract Task<object> ReadResource(string uri);

    // Protocol handlers
    protected virtual async Task<MCPResponse> HandleInitialize(MCPRequest request)
    {
        await Task.CompletedTask; // Make the method actually async
        var result = new
        {
            protocolVersion = "2024-11-05",
            serverInfo = ServerInfo
        };

        return CreateSuccessResponse(request.Id, result);
    }

    protected virtual async Task<MCPResponse> HandleListTools(MCPRequest request)
    {
        var tools = await GetAvailableTools();
        var result = new { tools };
        return CreateSuccessResponse(request.Id, result);
    }

    protected virtual async Task<MCPResponse> HandleToolCall(MCPRequest request)
    {
        if (!request.Params.HasValue)
        {
            return CreateErrorResponse(request.Id, MCPErrorCodes.InvalidParams, "Missing tool call parameters");
        }

        try
        {
            var toolCall = JsonSerializer.Deserialize<MCPToolCall>(request.Params.Value.GetRawText());
            if (toolCall == null)
            {
                return CreateErrorResponse(request.Id, MCPErrorCodes.InvalidParams, "Invalid tool call format");
            }

            Logger.LogInformation("Executing tool: {ToolName}", toolCall.Name);
            var result = await ExecuteTool(toolCall.Name, toolCall.Arguments);
            
            return CreateSuccessResponse(request.Id, result);
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Error parsing tool call parameters");
            return CreateErrorResponse(request.Id, MCPErrorCodes.InvalidParams, "Invalid tool call parameters");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing tool");
            return CreateErrorResponse(request.Id, MCPErrorCodes.InternalError, ex.Message);
        }
    }

    protected virtual async Task<MCPResponse> HandleListResources(MCPRequest request)
    {
        var resources = await GetAvailableResources();
        var result = new { resources };
        return CreateSuccessResponse(request.Id, result);
    }

    protected virtual async Task<MCPResponse> HandleReadResource(MCPRequest request)
    {
        if (!request.Params.HasValue)
        {
            return CreateErrorResponse(request.Id, MCPErrorCodes.InvalidParams, "Missing resource URI");
        }

        try
        {
            var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(request.Params.Value.GetRawText());
            if (parameters == null || !parameters.TryGetValue("uri", out var uriObj))
            {
                return CreateErrorResponse(request.Id, MCPErrorCodes.InvalidParams, "Missing resource URI");
            }

            var uri = uriObj.ToString();
            if (string.IsNullOrEmpty(uri))
            {
                return CreateErrorResponse(request.Id, MCPErrorCodes.InvalidParams, "Invalid resource URI");
            }

            Logger.LogInformation("Reading resource: {Uri}", uri);
            var content = await ReadResource(uri);
            
            return CreateSuccessResponse(request.Id, content);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error reading resource");
            return CreateErrorResponse(request.Id, MCPErrorCodes.InternalError, ex.Message);
        }
    }

    // Helper methods
    protected MCPResponse CreateSuccessResponse(object? id, object result)
    {
        return new MCPResponse
        {
            Id = id,
            Result = result
        };
    }

    protected MCPResponse CreateErrorResponse(object? id, int code, string message, object? data = null)
    {
        return new MCPResponse
        {
            Id = id,
            Error = new MCPError
            {
                Code = code,
                Message = message,
                Data = data
            }
        };
    }

    protected MCPToolResult CreateToolResult(string text, bool isError = false)
    {
        return new MCPToolResult
        {
            Content = new List<MCPContent>
            {
                new MCPContent
                {
                    Type = "text",
                    Text = text
                }
            },
            IsError = isError
        };
    }

    protected MCPToolResult CreateToolResultWithData(object data, string? description = null)
    {
        var content = new List<MCPContent>
        {
            new MCPContent
            {
                Type = "application/json",
                Data = data
            }
        };

        if (!string.IsNullOrEmpty(description))
        {
            content.Insert(0, new MCPContent
            {
                Type = "text",
                Text = description
            });
        }

        return new MCPToolResult
        {
            Content = content,
            IsError = false
        };
    }

    protected JsonElement CreateToolSchema(object schema)
    {
        var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    // Validation helpers
    protected bool ValidateRequiredParameter(JsonElement arguments, string paramName, out object? value)
    {
        value = null;
        
        if (!arguments.TryGetProperty(paramName, out var element))
        {
            return false;
        }

        try
        {
            value = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDecimal(),
                JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
                JsonValueKind.Array => element.EnumerateArray().Select(e => e.ToString()).ToArray(),
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText()),
                _ => element.GetRawText()
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected T? DeserializeToolArguments<T>(JsonElement arguments) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(arguments.GetRawText(), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deserializing tool arguments to {Type}", typeof(T).Name);
            return null;
        }
    }
}