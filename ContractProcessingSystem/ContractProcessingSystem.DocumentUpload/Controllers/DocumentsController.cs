using ContractProcessingSystem.Shared.Services;
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

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(Guid id)
    {
        try
        {
            var document = await _documentService.GetDocumentAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            var content = await _documentService.GetDocumentContentAsync(id);
            
            return File(content, document.ContentType, document.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound("Document content not found in storage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document content for {DocumentId}", id);
            return StatusCode(500, "Internal server error occurred while retrieving document content");
        }
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        try
        {
            var document = await _documentService.GetDocumentAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            var content = await _documentService.GetDocumentContentAsync(id);
            
            return File(content, "application/octet-stream", document.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound("Document content not found in storage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document {DocumentId}", id);
            return StatusCode(500, "Internal server error occurred while downloading document");
        }
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

    [HttpPost("ensure-metadata")]
    public async Task<ActionResult<object>> EnsureAllMetadata()
    {
        try
        {
            var createdCount = await _documentService.EnsureAllDocumentsHaveMetadataAsync();
            return Ok(new 
            { 
                message = $"Ensured metadata exists for all documents", 
                createdMetadataRecords = createdCount,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure metadata for all documents");
            return StatusCode(500, "Internal server error occurred while ensuring metadata");
        }
    }

    [HttpPost("ensure-storage")]
    public async Task<ActionResult<object>> EnsureBlobStorage()
    {
        try
        {
            var created = await _documentService.EnsureBlobStorageInitializedAsync();
            return Ok(new 
            { 
                message = created ? "Blob storage container created" : "Blob storage container already exists",
                containerCreated = created,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure blob storage initialization");
            return StatusCode(500, "Internal server error occurred while ensuring blob storage");
        }
    }

    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new { Service = "DocumentUpload", Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}