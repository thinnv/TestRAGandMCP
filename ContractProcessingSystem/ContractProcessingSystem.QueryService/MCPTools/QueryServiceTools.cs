using System.ComponentModel;
using ModelContextProtocol.Server;
using ContractProcessingSystem.QueryService.Services;
using ContractProcessingSystem.Shared.Models;

namespace ContractProcessingSystem.QueryService.MCPTools;

/// <summary>
/// MCP Tools for Query Service using official ModelContextProtocol.AspNetCore package
/// </summary>
[McpServerToolType]
public class QueryServiceTools
{
    private readonly Services.QueryService _queryService;
    private readonly ILogger<QueryServiceTools> _logger;

    public QueryServiceTools(
        Services.QueryService queryService,
        ILogger<QueryServiceTools> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Perform semantic search on contract documents using natural language queries.")]
    public async Task<string> SemanticSearch(
        [Description("JSON search request with Query, TopK, Filters, etc.")] string searchRequestJson)
    {
        try
        {
            _logger.LogInformation("MCP Tool: SemanticSearch called");

            var request = System.Text.Json.JsonSerializer.Deserialize<SearchRequest>(searchRequestJson);
            
            if (request == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid search request"
                });
            }

            var results = await _queryService.SemanticSearchAsync(request);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                results,
                count = results.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SemanticSearch MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Summarize contract content or search results using AI.")]
    public async Task<string> Summarize(
        [Description("JSON summarization request with Content, MaxLength, Style")] string summarizationRequestJson)
    {
        try
        {
            _logger.LogInformation("MCP Tool: Summarize called");

            var request = System.Text.Json.JsonSerializer.Deserialize<SummarizationRequest>(summarizationRequestJson);
            
            if (request == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid summarization request"
                });
            }

            var result = await _queryService.SummarizeAsync(request);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Summarize MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Perform hybrid search combining semantic search with metadata filters.")]
    public async Task<string> HybridSearch(
        [Description("Natural language search query")] string query,
        [Description("JSON dictionary of metadata filters (optional)")] string? filtersJson = null)
    {
        try
        {
            _logger.LogInformation("MCP Tool: HybridSearch called with query: {Query}", query);

            Dictionary<string, object>? filters = null;
            if (!string.IsNullOrWhiteSpace(filtersJson))
            {
                filters = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(filtersJson);
            }

            var results = await _queryService.HybridSearchAsync(query, filters);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                results,
                count = results.Length,
                query
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HybridSearch MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
