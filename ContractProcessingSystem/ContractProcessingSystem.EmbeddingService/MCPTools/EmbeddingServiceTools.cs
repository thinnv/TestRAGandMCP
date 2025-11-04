using System.ComponentModel;
using ModelContextProtocol.Server;
using ContractProcessingSystem.EmbeddingService.Services;
using ContractProcessingSystem.Shared.Models;

namespace ContractProcessingSystem.EmbeddingService.MCPTools;

/// <summary>
/// MCP Tools for Embedding Service using official ModelContextProtocol.AspNetCore package
/// </summary>
[McpServerToolType]
public class EmbeddingServiceTools
{
    private readonly Services.EmbeddingService _embeddingService;
    private readonly ILogger<EmbeddingServiceTools> _logger;

    public EmbeddingServiceTools(
        Services.EmbeddingService embeddingService,
        ILogger<EmbeddingServiceTools> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Generate vector embeddings for a list of text chunks.")]
    public async Task<string> GenerateEmbeddings(
        [Description("JSON array of chunks with Id, DocumentId, ChunkIndex, Content")] string chunksJson)
    {
        try
        {
            _logger.LogInformation("MCP Tool: GenerateEmbeddings called");

            var chunks = System.Text.Json.JsonSerializer.Deserialize<List<ContractChunk>>(chunksJson);
            
            if (chunks == null || chunks.Count == 0)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "No chunks provided"
                });
            }

            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                embeddings,
                count = embeddings.Length
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateEmbeddings MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Generate a single vector embedding for text content.")]
    public async Task<string> GenerateEmbedding(
        [Description("Text content to generate embedding for")] string text)
    {
        try
        {
            _logger.LogInformation("MCP Tool: GenerateEmbedding called");

            if (string.IsNullOrWhiteSpace(text))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Text content is required"
                });
            }

            var embedding = await _embeddingService.GenerateEmbeddingAsync(text);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                embedding
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateEmbedding MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Get the embedding generation status for a document.")]
    public async Task<string> GetEmbeddingStatus(
        [Description("Document ID (GUID) to check status")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: GetEmbeddingStatus called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var status = await _embeddingService.GetEmbeddingStatusAsync(docGuid);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetEmbeddingStatus MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

}
