using ContractProcessingSystem.VectorService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContractProcessingSystem.VectorService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VectorController : ControllerBase
{
    private readonly IVectorService _vectorService;
    private readonly ILogger<VectorController> _logger;

    public VectorController(
        IVectorService vectorService,
        ILogger<VectorController> logger)
    {
        _vectorService = vectorService;
        _logger = logger;
    }

    [HttpPost("store")]
    public async Task<ActionResult> StoreEmbeddings([FromBody] VectorEmbedding[] embeddings)
    {
        if (!embeddings.Any())
        {
            return BadRequest("No embeddings provided");
        }

        try
        {
            await _vectorService.StoreEmbeddingsAsync(embeddings);
            return Ok(new { Message = "Embeddings stored successfully", Count = embeddings.Length });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store embeddings");
            return StatusCode(500, "Internal server error occurred while storing embeddings");
        }
    }

    [HttpPost("search/similar")]
    public async Task<ActionResult<SearchResult[]>> SearchSimilar([FromBody] VectorSearchRequest request)
    {
        if (request.QueryVector?.Length == 0)
        {
            return BadRequest("Query vector is required");
        }

        try
        {
            var results = await _vectorService.SearchSimilarAsync(
                request.QueryVector!, 
                request.MaxResults, 
                request.MinScore);
            
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform vector similarity search");
            return StatusCode(500, "Internal server error occurred during search");
        }
    }

    [HttpPost("search")]
    public async Task<ActionResult<SearchResult[]>> Search([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Search query is required");
        }

        try
        {
            var results = await _vectorService.SearchAsync(request);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform semantic search");
            return StatusCode(500, "Internal server error occurred during search");
        }
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<ActionResult> DeleteDocumentEmbeddings(Guid documentId)
    {
        try
        {
            var deleted = await _vectorService.DeleteDocumentEmbeddingsAsync(documentId);
            if (deleted)
            {
                return Ok(new { Message = "Document embeddings deleted successfully", DocumentId = documentId });
            }
            else
            {
                return NotFound(new { Message = "No embeddings found for the specified document", DocumentId = documentId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document embeddings for {DocumentId}", documentId);
            return StatusCode(500, "Internal server error occurred while deleting embeddings");
        }
    }

    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new { Service = "VectorService", Status = "Healthy", Timestamp = DateTime.UtcNow });
    }

    [HttpGet("collection/info")]
    public async Task<ActionResult> GetCollectionInfo()
    {
        try
        {
            // Get real statistics from the vector service
            var vectorService = _vectorService as QdrantVectorService;
            
            if (vectorService != null)
            {
                var stats = await vectorService.GetStatisticsAsync();
                return Ok(stats);
            }
            
            // Fallback to basic info
            var info = new
            {
                CollectionName = "contract_embeddings",
                Status = "Ready",
                Timestamp = DateTime.UtcNow,
                EstimatedCount = 0,
                Message = "Statistics endpoint requires QdrantVectorService"
            };
            
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collection info");
            return StatusCode(500, "Internal server error occurred while getting collection info");
        }
    }

    [HttpDelete("collection/clear")]
    public async Task<ActionResult> ClearCollection()
    {
        try
        {
            var vectorService = _vectorService as QdrantVectorService;
            
            if (vectorService != null)
            {
                var clearedCount = await vectorService.ClearAllEmbeddingsAsync();
                return Ok(new
                {
                    Message = "Vector store cleared successfully",
                    ClearedCount = clearedCount,
                    Timestamp = DateTime.UtcNow
                });
            }
            
            return BadRequest("Clear operation requires QdrantVectorService");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear collection");
            return StatusCode(500, "Internal server error occurred while clearing collection");
        }
    }
}

public record VectorSearchRequest(
    float[]? QueryVector,
    int MaxResults = 10,
    float MinScore = 0.7f
);