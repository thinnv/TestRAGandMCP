using ContractProcessingSystem.EmbeddingService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ContractProcessingSystem.EmbeddingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmbeddingsController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly EmbeddingBatchProcessor _batchProcessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmbeddingsController> _logger;

    public EmbeddingsController(
        IEmbeddingService embeddingService,
        EmbeddingBatchProcessor batchProcessor,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<EmbeddingsController> logger)
    {
        _embeddingService = embeddingService;
        _batchProcessor = batchProcessor;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<VectorEmbedding[]>> GenerateEmbeddings([FromBody] List<ContractChunk> chunks)
    {
        if (!chunks.Any())
        {
            return BadRequest("No chunks provided");
        }

        try
        {
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);
            return Ok(embeddings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings");
            return StatusCode(500, "Internal server error occurred while generating embeddings");
        }
    }

    [HttpPost("generate-and-store")]
    public async Task<ActionResult<VectorEmbedding[]>> GenerateAndStoreEmbeddings([FromBody] List<ContractChunk> chunks)
    {
        if (!chunks.Any())
        {
            return BadRequest("No chunks provided");
        }

        try
        {
            _logger.LogInformation("Generating and storing embeddings for {ChunkCount} chunks", chunks.Count);
            
            // Generate embeddings
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);
            
            // Store in vector database
            await StoreEmbeddingsInVectorService(embeddings, chunks);
            
            return Ok(embeddings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and store embeddings");
            return StatusCode(500, "Internal server error occurred while generating and storing embeddings");
        }
    }

    [HttpPost("generate-single")]
    public async Task<ActionResult<VectorEmbedding>> GenerateSingleEmbedding([FromBody] string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("Text is required");
        }

        try
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(text);
            return Ok(embedding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate single embedding");
            return StatusCode(500, "Internal server error occurred while generating embedding");
        }
    }

    [HttpPost("batch-process/{documentId:guid}")]
    public async Task<ActionResult> BatchProcessDocument(Guid documentId, [FromBody] List<ContractChunk> chunks)
    {
        if (!chunks.Any())
        {
            return BadRequest("No chunks provided");
        }

        try
        {
            var success = await _batchProcessor.ProcessDocumentAsync(documentId, chunks);
            if (success)
            {
                return Accepted(new { Message = "Batch processing started", DocumentId = documentId });
            }
            else
            {
                return StatusCode(500, "Failed to start batch processing");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start batch processing for document {DocumentId}", documentId);
            return StatusCode(500, "Internal server error occurred while starting batch processing");
        }
    }

    [HttpGet("status/{documentId:guid}")]
    public async Task<ActionResult<ProcessingStatus>> GetStatus(Guid documentId)
    {
        var status = await _embeddingService.GetEmbeddingStatusAsync(documentId);
        return Ok(status);
    }

    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new { Service = "EmbeddingService", Status = "Healthy", Timestamp = DateTime.UtcNow });
    }

    private async Task StoreEmbeddingsInVectorService(VectorEmbedding[] embeddings, List<ContractChunk> chunks)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var vectorServiceUrl = _configuration["Services:VectorService"] ?? "https://localhost:7197";

            // Create chunk-to-document and chunk-to-content mappings
            var chunkToDocumentMap = chunks.ToDictionary(c => c.Id, c => c.DocumentId);
            var chunkContentMap = chunks.ToDictionary(c => c.Id, c => c.Content);

            var storeRequest = new StoreEmbeddingsRequest(
                embeddings,
                chunkToDocumentMap,
                chunkContentMap
            );

            var requestBody = JsonSerializer.Serialize(storeRequest);
            var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{vectorServiceUrl}/api/vector/store-with-mapping", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully stored {Count} embeddings in vector service", embeddings.Length);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to store embeddings in vector service: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not store embeddings in vector service (service may be unavailable)");
            // Don't throw - embeddings were generated successfully
        }
    }
}