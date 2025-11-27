using ContractProcessingSystem.QueryService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContractProcessingSystem.QueryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly IQueryService _queryService;
    private readonly ILogger<QueryController> _logger;

    public QueryController(
        IQueryService queryService,
        ILogger<QueryController> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    /// <summary>
    /// Perform semantic search on contract documents using natural language queries
    /// </summary>
    /// <param name="request">Search request with query, filters, and parameters</param>
    /// <returns>Array of search results with relevance scores</returns>
    [HttpPost("search")]
    [ProducesResponseType(typeof(SearchResult[]), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SearchResult[]>> SemanticSearch([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { Error = "Search query is required" });
        }

        try
        {
            _logger.LogInformation("Semantic search request for query: {Query}", request.Query);
            var results = await _queryService.SemanticSearchAsync(request);
            
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform semantic search for query: {Query}", request.Query);
            return StatusCode(500, new { Error = "Internal server error occurred during search", Message = ex.Message });
        }
    }

    /// <summary>
    /// Perform hybrid search combining semantic and keyword-based matching
    /// </summary>
    /// <param name="request">Hybrid search request</param>
    /// <returns>Array of search results ranked by combined relevance</returns>
    [HttpPost("search/hybrid")]
    [ProducesResponseType(typeof(SearchResult[]), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SearchResult[]>> HybridSearch([FromBody] HybridSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { Error = "Search query is required" });
        }

        try
        {
            _logger.LogInformation("Hybrid search request for query: {Query}", request.Query);
            var results = await _queryService.HybridSearchAsync(request.Query, request.Filters);
            
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform hybrid search for query: {Query}", request.Query);
            return StatusCode(500, new { Error = "Internal server error occurred during hybrid search", Message = ex.Message });
        }
    }

    /// <summary>
    /// Summarize contract documents with AI-powered analysis
    /// </summary>
    /// <param name="request">Summarization request with document IDs and options</param>
    /// <returns>Summarization result with summary, key points, and insights</returns>
    [HttpPost("summarize")]
    [ProducesResponseType(typeof(SummarizationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<SummarizationResult>> Summarize([FromBody] SummarizationRequest request)
    {
        if (!request.DocumentIds.Any())
        {
            return BadRequest(new { Error = "At least one document ID is required" });
        }

        if (request.MaxLength < 50 || request.MaxLength > 5000)
        {
            return BadRequest(new { Error = "MaxLength must be between 50 and 5000 words" });
        }

        try
        {
            _logger.LogInformation("Summarization request for {DocumentCount} documents, type: {Type}", 
                request.DocumentIds.Count, request.Type);
            
            var result = await _queryService.SummarizeAsync(request);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize documents");
            return StatusCode(500, new { Error = "Internal server error occurred during summarization", Message = ex.Message });
        }
    }

    /// <summary>
    /// Get query service health status
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(200)]
    public ActionResult Health()
    {
        return Ok(new 
        { 
            Service = "QueryService", 
            Status = "Healthy", 
            Timestamp = DateTime.UtcNow,
            Features = new[]
            {
                "Semantic Search",
                "Hybrid Search",
                "AI Summarization",
                "Multi-document Analysis"
            }
        });
    }

    /// <summary>
    /// Get service capabilities and configuration
    /// </summary>
    [HttpGet("capabilities")]
    [ProducesResponseType(200)]
    public ActionResult GetCapabilities()
    {
        return Ok(new
        {
            Service = "QueryService",
            Version = "1.0.0",
            Capabilities = new
            {
                SemanticSearch = new
                {
                    Enabled = true,
                    MaxResults = 100,
                    MinScore = 0.0f,
                    DefaultScore = 0.7f,
                    SupportedFilters = new[] { "document_id", "contract_type", "date_range", "parties" }
                },
                HybridSearch = new
                {
                    Enabled = true,
                    CombinesSemanticAndKeyword = true,
                    WeightingStrategy = "70% Semantic, 30% Keyword"
                },
                Summarization = new
                {
                    Enabled = true,
                    SupportedTypes = new[] { "Overview", "KeyTerms", "RiskAssessment", "Comparison" },
                    MaxLength = new { Min = 50, Max = 5000, Default = 500 },
                    MaxDocuments = 20
                },
                AIEnhancement = new
                {
                    Enabled = true,
                    Provider = "Gemini",
                    Features = new[] { "Result Ranking", "Context Analysis", "Insight Generation" }
                }
            },
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get service dependencies and their configured URLs
    /// </summary>
    [HttpGet("diagnostics")]
    [ProducesResponseType(200)]
    public ActionResult GetDiagnostics()
    {
        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        
        var services = new Dictionary<string, object>
        {
            ["VectorService"] = new
            {
                ConfiguredUrl = config["Services:VectorService"],
                DefaultUrl = "https://localhost:7197",
                IsConfigured = !string.IsNullOrEmpty(config["Services:VectorService"])
            },
            ["EmbeddingService"] = new
            {
                ConfiguredUrl = config["Services:EmbeddingService"],
                DefaultUrl = "https://localhost:7070",
                IsConfigured = !string.IsNullOrEmpty(config["Services:EmbeddingService"])
            },
            ["DocumentUpload"] = new
            {
                ConfiguredUrl = config["Services:DocumentUpload"],
                DefaultUrl = "https://localhost:7048",
                IsConfigured = !string.IsNullOrEmpty(config["Services:DocumentUpload"])
            }
        };

        var llmConfig = new
        {
            DefaultProvider = config["LLMProviders:DefaultProvider"],
            EmbeddingProvider = config["LLMProviders:EmbeddingProvider"],
            EnableFallback = config["LLMProviders:EnableFallback"],
            RetryAttempts = config["LLMProviders:RetryAttempts"]
        };

        return Ok(new
        {
            Service = "QueryService",
            Status = "Healthy",
            Dependencies = services,
            LLMConfiguration = llmConfig,
            Timestamp = DateTime.UtcNow,
            Note = "Service URLs are read from appsettings.json 'Services' section with fallback to defaults"
        });
    }
}

/// <summary>
/// Request model for hybrid search
/// </summary>
public record HybridSearchRequest(
    string Query,
    Dictionary<string, object>? Filters = null,
    int MaxResults = 10,
    float MinScore = 0.7f
);
