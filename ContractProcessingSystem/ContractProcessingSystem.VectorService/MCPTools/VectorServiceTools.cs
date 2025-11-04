using System.ComponentModel;
using ModelContextProtocol.Server;
using ContractProcessingSystem.VectorService.Services;
using ContractProcessingSystem.Shared.Models;

namespace ContractProcessingSystem.VectorService.MCPTools;

/// <summary>
/// MCP Tools for Vector Service using official ModelContextProtocol.AspNetCore package
/// </summary>
[McpServerToolType]
public class VectorServiceTools
{
    private readonly MilvusVectorService _vectorService;
    private readonly ILogger<VectorServiceTools> _logger;

    public VectorServiceTools(
        MilvusVectorService vectorService,
        ILogger<VectorServiceTools> logger)
    {
        _vectorService = vectorService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Store vector embeddings in the vector database.")]
    public async Task<string> StoreEmbeddings(
        [Description("JSON array of embeddings with Id, DocumentId, ChunkId, Vector, Metadata")] string embeddingsJson)
    {
        try
        {
            _logger.LogInformation("MCP Tool: StoreEmbeddings called");

            var embeddings = System.Text.Json.JsonSerializer.Deserialize<VectorEmbedding[]>(embeddingsJson);
            
            if (embeddings == null || embeddings.Length == 0)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No embeddings provided"
                });
            }

            await _vectorService.StoreEmbeddingsAsync(embeddings);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                count = embeddings.Length,
                message = "Embeddings stored successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in StoreEmbeddings MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Search for similar vectors using a query vector.")]
    public async Task<string> SearchSimilar(
        [Description("Query vector as JSON array of floats")] string queryVectorJson,
        [Description("Number of results to return")] int topK = 10,
        [Description("Minimum similarity score (0.0-1.0)")] float threshold = 0.7f)
    {
        try
        {
            _logger.LogInformation("MCP Tool: SearchSimilar called (topK: {TopK}, threshold: {Threshold})", topK, threshold);

            var queryVector = System.Text.Json.JsonSerializer.Deserialize<float[]>(queryVectorJson);
            
            if (queryVector == null || queryVector.Length == 0)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid query vector"
                });
            }

            var results = await _vectorService.SearchSimilarAsync(queryVector, topK, threshold);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                results,
                count = results.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchSimilar MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Advanced search with filters and options.")]
    public async Task<string> Search(
        [Description("JSON search request with QueryVector, TopK, Filters, etc.")] string searchRequestJson)
    {
        try
        {
            _logger.LogInformation("MCP Tool: Search called");

            var request = System.Text.Json.JsonSerializer.Deserialize<SearchRequest>(searchRequestJson);
            
            if (request == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid search request"
                });
            }

            var results = await _vectorService.SearchAsync(request);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                results,
                count = results.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Search MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Delete all embeddings for a specific document.")]
    public async Task<string> DeleteDocumentEmbeddings(
        [Description("Document ID (GUID) whose embeddings should be deleted")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: DeleteDocumentEmbeddings called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var success = await _vectorService.DeleteDocumentEmbeddingsAsync(docGuid);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success,
                documentId,
                message = success ? "Document embeddings deleted successfully" : "Failed to delete embeddings"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteDocumentEmbeddings MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
