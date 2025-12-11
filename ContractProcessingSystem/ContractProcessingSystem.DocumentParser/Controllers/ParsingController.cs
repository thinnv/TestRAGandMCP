using ContractProcessingSystem.DocumentParser.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace ContractProcessingSystem.DocumentParser.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParsingController : ControllerBase
{
    private readonly IDocumentParsingService _parsingService;
    private readonly ILogger<ParsingController> _logger;
    // In-memory storage for chunks (in production, use a database or Redis)
    private static readonly ConcurrentDictionary<Guid, List<ContractChunk>> _chunkStore = new();

    public ParsingController(
        IDocumentParsingService parsingService,
        ILogger<ParsingController> logger)
    {
        _parsingService = parsingService;
        _logger = logger;
    }

    [HttpPost("{documentId:guid}/parse")]
    public async Task<ActionResult<ContractMetadata>> ParseDocument(Guid documentId)
    {
        try
        {
            var metadata = await _parsingService.ParseDocumentAsync(documentId);
            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse document {DocumentId}", documentId);
            return StatusCode(500, "Internal server error occurred while parsing document");
        }
    }

    [HttpPost("{documentId:guid}/chunk")]
    public async Task<ActionResult<List<ContractChunk>>> ChunkDocument(Guid documentId)
    {
        try
        {
            var chunks = await _parsingService.ChunkDocumentAsync(documentId);
            
            // Store chunks for later retrieval
            _chunkStore[documentId] = chunks;
            _logger.LogInformation("Stored {ChunkCount} chunks for document {DocumentId}", chunks.Count, documentId);
            
            return Ok(chunks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to chunk document {DocumentId}", documentId);
            return StatusCode(500, "Internal server error occurred while chunking document");
        }
    }

    [HttpGet("{documentId:guid}/chunks")]
    public ActionResult<List<ContractChunk>> GetChunks(Guid documentId)
    {
        try
        {
            if (_chunkStore.TryGetValue(documentId, out var chunks))
            {
                _logger.LogInformation("Retrieved {ChunkCount} chunks for document {DocumentId}", chunks.Count, documentId);
                return Ok(chunks);
            }

            _logger.LogWarning("No chunks found for document {DocumentId}", documentId);
            return NotFound(new { Message = $"No chunks found for document {documentId}. Please chunk the document first." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve chunks for document {DocumentId}", documentId);
            return StatusCode(500, "Internal server error occurred while retrieving chunks");
        }
    }

    [HttpGet("chunks/{chunkId:guid}")]
    public ActionResult<ContractChunk> GetChunkById(Guid chunkId)
    {
        try
        {
            // Search through all stored chunks to find the one with matching ID
            foreach (var (docId, chunks) in _chunkStore)
            {
                var chunk = chunks.FirstOrDefault(c => c.Id == chunkId);
                if (chunk != null)
                {
                    _logger.LogDebug("Found chunk {ChunkId} in document {DocumentId}", chunkId, docId);
                    return Ok(chunk);
                }
            }

            _logger.LogWarning("Chunk {ChunkId} not found", chunkId);
            return NotFound(new { Message = $"Chunk {chunkId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve chunk {ChunkId}", chunkId);
            return StatusCode(500, "Internal server error occurred while retrieving chunk");
        }
    }

    [HttpDelete("{documentId:guid}/chunks")]
    public ActionResult DeleteChunks(Guid documentId)
    {
        try
        {
            if (_chunkStore.TryRemove(documentId, out var removedChunks))
            {
                _logger.LogInformation("Deleted {ChunkCount} chunks for document {DocumentId}", removedChunks.Count, documentId);
                return Ok(new { Message = $"Deleted {removedChunks.Count} chunks for document {documentId}" });
            }

            _logger.LogWarning("No chunks found to delete for document {DocumentId}", documentId);
            return NotFound(new { Message = $"No chunks found for document {documentId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chunks for document {DocumentId}", documentId);
            return StatusCode(500, "Internal server error occurred while deleting chunks");
        }
    }

    [HttpGet("{documentId:guid}/status")]
    public async Task<ActionResult<ProcessingStatus>> GetParsingStatus(Guid documentId)
    {
        var status = await _parsingService.GetParsingStatusAsync(documentId);
        return Ok(status);
    }

    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new { 
            Service = "DocumentParser", 
            Status = "Healthy", 
            Timestamp = DateTime.UtcNow,
            StoredDocuments = _chunkStore.Count,
            TotalChunks = _chunkStore.Values.Sum(c => c.Count)
        });
    }

    [HttpGet("test-ai")]
    public async Task<ActionResult> TestAI()
    {
        try
        {
            var testService = HttpContext.RequestServices.GetRequiredService<IDocumentParsingService>() as DocumentParsingService;
            
            // Test a simple prompt to verify AI connectivity
            var testResult = await testService!.TestAIConnectionAsync();
            
            return Ok(new { 
                Service = "DocumentParser", 
                AIStatus = testResult ? "Connected" : "Failed",
                Timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI connection test failed");
            return StatusCode(500, new { 
                Service = "DocumentParser", 
                AIStatus = "Failed",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow 
            });
        }
    }
}
