using ContractProcessingSystem.EmbeddingService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContractProcessingSystem.EmbeddingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmbeddingsController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly EmbeddingBatchProcessor _batchProcessor;
    private readonly ILogger<EmbeddingsController> _logger;

    public EmbeddingsController(
        IEmbeddingService embeddingService,
        EmbeddingBatchProcessor batchProcessor,
        ILogger<EmbeddingsController> logger)
    {
        _embeddingService = embeddingService;
        _batchProcessor = batchProcessor;
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
}