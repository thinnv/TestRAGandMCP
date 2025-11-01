using ContractProcessingSystem.DocumentParser.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContractProcessingSystem.DocumentParser.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParsingController : ControllerBase
{
    private readonly IDocumentParsingService _parsingService;
    private readonly ILogger<ParsingController> _logger;

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
            return Ok(chunks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to chunk document {DocumentId}", documentId);
            return StatusCode(500, "Internal server error occurred while chunking document");
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
        return Ok(new { Service = "DocumentParser", Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}