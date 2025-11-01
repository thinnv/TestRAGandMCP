using ContractProcessingSystem.DocumentUpload.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContractProcessingSystem.DocumentUpload.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentUploadService _documentService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentUploadService documentService,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<ContractDocument>> UploadDocument(
        IFormFile file, 
        [FromForm] string uploadedBy = "system")
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        try
        {
            using var stream = file.OpenReadStream();
            var document = await _documentService.UploadDocumentAsync(
                file.FileName, 
                stream, 
                file.ContentType, 
                uploadedBy);

            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document");
            return StatusCode(500, "Internal server error occurred while uploading document");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContractDocument>> GetDocument(Guid id)
    {
        var document = await _documentService.GetDocumentAsync(id);
        if (document == null)
        {
            return NotFound();
        }

        return Ok(document);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContractDocument>>> GetDocuments(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest("Invalid pagination parameters");
        }

        var documents = await _documentService.GetDocumentsAsync(page, pageSize);
        return Ok(documents);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteDocument(Guid id)
    {
        var deleted = await _documentService.DeleteDocumentAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new { Service = "DocumentUpload", Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}