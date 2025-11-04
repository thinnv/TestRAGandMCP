using ContractProcessingSystem.Shared.MCP.Official;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace ContractProcessingSystem.MCPDemo;

/// <summary>
/// Enhanced MCP Demo using official ModelContextProtocol patterns
/// </summary>
class EnhancedMCPDemo
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Enhanced Contract Processing MCP Demo ===");
        Console.WriteLine("Using official ModelContextProtocol patterns and compliance");
        Console.WriteLine();

        // Create host builder for dependency injection
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddHttpClient();
                services.AddLogging();
                
                // Add official MCP server configuration
                services.AddOfficialMCPServer<EnhancedMCPClient>("Contract Processing System", "1.0.0");
            })
            .Build();

        var serviceProvider = host.Services;
        var logger = serviceProvider.GetRequiredService<ILogger<EnhancedMCPDemo>>();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        try
        {
            // Create enhanced MCP client
            var httpClient = httpClientFactory.CreateClient();
            var enhancedClient = new EnhancedMCPClient(
                httpClient, 
                "https://localhost:7001/api/EnhancedDocumentUploadMCP", 
                serviceProvider.GetRequiredService<ILogger<EnhancedMCPClient>>());

            await RunEnhancedMCPDemo(enhancedClient, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running enhanced MCP demo");
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static async Task RunEnhancedMCPDemo(EnhancedMCPClient client, ILogger logger)
    {
        try
        {
            // Demo 1: Initialize MCP connection with official compliance
            logger.LogInformation("=== Demo 1: Initialize Enhanced MCP Connection ===");
            var initResult = await client.InitializeAsync();
            logger.LogInformation("Enhanced MCP server initialized: {Result}", JsonSerializer.Serialize(initResult, new JsonSerializerOptions { WriteIndented = true }));

            // Demo 2: List available tools with official schema
            logger.LogInformation("\n=== Demo 2: List Enhanced MCP Tools ===");
            var tools = await client.ListToolsAsync();
            logger.LogInformation("Available enhanced tools:");
            foreach (var tool in tools)
            {
                logger.LogInformation("  - {ToolName}: {Description}", tool.Name, tool.Description);
                logger.LogInformation("    Schema: {Schema}", JsonSerializer.Serialize(tool.InputSchema, new JsonSerializerOptions { WriteIndented = true }));
            }

            // Demo 3: List available resources with official compliance
            logger.LogInformation("\n=== Demo 3: List Enhanced MCP Resources ===");
            var resources = await client.ListResourcesAsync();
            logger.LogInformation("Available enhanced resources:");
            foreach (var resource in resources)
            {
                logger.LogInformation("  - {ResourceUri}: {Description} (MIME: {MimeType})", 
                    resource.Uri, resource.Description, resource.MimeType);
            }

            // Demo 4: Upload sample contract with enhanced MCP patterns
            logger.LogInformation("\n=== Demo 4: Upload Contract with Enhanced MCP ===");
            var sampleContract = CreateSampleContractContent();
            var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(sampleContract));

            var uploadResult = await client.CallToolAsync("upload_contract_file", new
            {
                fileName = "enhanced-sample-contract.txt",
                contentType = "text/plain",
                fileContent = base64Content,
                uploadedBy = "enhanced-demo@example.com",
                tags = new[] { "enhanced", "mcp", "official", "demo" }
            });

            logger.LogInformation("Enhanced upload result: {Result}", 
                JsonSerializer.Serialize(uploadResult, new JsonSerializerOptions { WriteIndented = true }));

            // Demo 5: List files with enhanced filtering
            logger.LogInformation("\n=== Demo 5: List Files with Enhanced MCP ===");
            var fileList = await client.CallToolAsync("list_uploaded_files", new
            {
                limit = 10,
                offset = 0,
                uploadedBy = "enhanced-demo@example.com"
            });

            logger.LogInformation("Enhanced file list: {FileList}", 
                JsonSerializer.Serialize(fileList, new JsonSerializerOptions { WriteIndented = true }));

            // Demo 6: Get enhanced statistics
            logger.LogInformation("\n=== Demo 6: Enhanced Upload Statistics ===");
            var statistics = await client.CallToolAsync("get_upload_statistics", new
            {
                period = "month",
                groupBy = "status"
            });

            logger.LogInformation("Enhanced statistics: {Statistics}", 
                JsonSerializer.Serialize(statistics, new JsonSerializerOptions { WriteIndented = true }));

            // Demo 7: Read resources with official compliance
            logger.LogInformation("\n=== Demo 7: Read Enhanced MCP Resources ===");
            try
            {
                var filesResource = await client.ReadResourceAsync("contract://files");
                logger.LogInformation("Enhanced files resource: {Resource}", 
                    JsonSerializer.Serialize(filesResource, new JsonSerializerOptions { WriteIndented = true }));

                var statsResource = await client.ReadResourceAsync("contract://statistics");
                logger.LogInformation("Enhanced statistics resource: {Resource}", 
                    JsonSerializer.Serialize(statsResource, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                logger.LogWarning("Could not read enhanced resources: {Error}", ex.Message);
            }

            // Demo 8: Test error handling with official patterns
            logger.LogInformation("\n=== Demo 8: Enhanced Error Handling ===");
            try
            {
                var errorResult = await client.CallToolAsync("non_existent_tool", new { });
                logger.LogInformation("This should not be reached");
            }
            catch (Exception ex)
            {
                logger.LogInformation("Enhanced error handling worked: {Error}", ex.Message);
            }

            logger.LogInformation("\n=== Enhanced MCP Demo Completed Successfully ===");
            logger.LogInformation("Official ModelContextProtocol patterns validated!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during enhanced MCP demo");
            throw;
        }
    }

    static string CreateSampleContractContent()
    {
        return @"
ENHANCED SERVICE AGREEMENT - MCP COMPLIANT

This Enhanced Service Agreement (""Agreement"") demonstrates official Model Context Protocol compliance.

CONTRACT DETAILS:
- Contract Type: Software Development Services
- MCP Compliance: Official ModelContextProtocol patterns
- Protocol Version: 2024-11-05
- Integration Pattern: Hybrid approach with proven business logic

PARTIES:
Provider: Enhanced Software Solutions Inc.
Address: 123 MCP Street, Protocol City, AI 12345
Email: mcp@enhanced-solutions.com

Client: Contract Processing Systems Ltd.
Address: 456 Integration Avenue, Microservices City, NET 67890
Email: contracts@cps-systems.com

TERMS:
1. SERVICES: Provider will deliver MCP-compliant software development services
2. PAYMENT: $150,000 USD total value, monthly invoices
3. DELIVERY: According to official MCP specification timelines
4. INTEGRATION: Full ModelContextProtocol compliance required
5. TOOLS: All services must provide standardized MCP tools and resources
6. RESOURCES: Resource URIs must follow contract:// schema pattern
7. COMPLIANCE: JSON-RPC 2.0 protocol for all communications

TECHNICAL REQUIREMENTS:
- Official ModelContextProtocol.AspNetCore package integration
- Tool schema validation per MCP specification
- Resource management with proper MIME types
- Error handling with official MCP error codes
- Logging and monitoring compliance

This contract demonstrates the enhanced MCP implementation combining
official package compliance with proven business logic patterns.

EXECUTION:
Provider: _____________________ Date: _________
Client: _______________________ Date: _________

[MCP-COMPLIANT DOCUMENT]
Generated by Enhanced Contract Processing System v1.0.0
Protocol: Model Context Protocol 2024-11-05
";
    }
}

/// <summary>
/// Enhanced MCP Client with official compliance patterns
/// </summary>
public class EnhancedMCPClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<EnhancedMCPClient> _logger;

    public EnhancedMCPClient(HttpClient httpClient, string baseUrl, ILogger<EnhancedMCPClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
    }

    public async Task<object> InitializeAsync()
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { }
        };

        return await SendRequestAsync(request);
    }

    public async Task<List<OfficialMCPTool>> ListToolsAsync()
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 2,
            method = "tools/list",
            @params = new { }
        };

        var response = await SendRequestAsync(request);
        var jsonElement = JsonSerializer.SerializeToElement(response);
        
        if (jsonElement.TryGetProperty("result", out var result) &&
            result.TryGetProperty("tools", out var toolsArray))
        {
            return toolsArray.EnumerateArray()
                .Select(t => new OfficialMCPTool
                {
                    Name = t.GetProperty("name").GetString()!,
                    Description = t.GetProperty("description").GetString()!,
                    InputSchema = t.TryGetProperty("inputSchema", out var schema) ? 
                        JsonSerializer.Deserialize<object>(schema.GetRawText())! : new { }
                })
                .ToList();
        }

        return new List<OfficialMCPTool>();
    }

    public async Task<List<OfficialMCPResource>> ListResourcesAsync()
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "resources/list",
            @params = new { }
        };

        var response = await SendRequestAsync(request);
        var jsonElement = JsonSerializer.SerializeToElement(response);
        
        if (jsonElement.TryGetProperty("result", out var result) &&
            result.TryGetProperty("resources", out var resourcesArray))
        {
            return resourcesArray.EnumerateArray()
                .Select(r => new OfficialMCPResource
                {
                    Uri = r.GetProperty("uri").GetString()!,
                    Name = r.GetProperty("name").GetString()!,
                    Description = r.GetProperty("description").GetString()!,
                    MimeType = r.TryGetProperty("mimeType", out var mimeType) ? 
                        mimeType.GetString()! : "application/json"
                })
                .ToList();
        }

        return new List<OfficialMCPResource>();
    }

    public async Task<object> CallToolAsync(string toolName, object arguments)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = DateTime.UtcNow.Ticks,
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments = arguments
            }
        };

        return await SendRequestAsync(request);
    }

    public async Task<object> ReadResourceAsync(string uri)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = DateTime.UtcNow.Ticks,
            method = "resources/read",
            @params = new
            {
                uri = uri
            }
        };

        return await SendRequestAsync(request);
    }

    private async Task<object> SendRequestAsync(object request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending enhanced MCP request to {Url}: {Request}", _baseUrl, json);

            var response = await _httpClient.PostAsync($"{_baseUrl}/mcp", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Received enhanced MCP response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"HTTP {response.StatusCode}: {responseContent}");
            }

            return JsonSerializer.Deserialize<object>(responseContent)!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending enhanced MCP request");
            throw;
        }
    }
}