using System.ComponentModel;
using ModelContextProtocol.Server;
using ContractProcessingSystem.DocumentUpload.Services;
using ContractProcessingSystem.DocumentUpload.Data;

namespace ContractProcessingSystem.DocumentUpload.MCPTools;

/// <summary>
/// MCP Tools for Document Upload using official ModelContextProtocol.AspNetCore package
/// </summary>
[McpServerToolType]
public class DocumentUploadTools
{
    private readonly DocumentUploadService _uploadService;
    private readonly ILogger<DocumentUploadTools> _logger;

    public DocumentUploadTools(
        DocumentUploadService uploadService,
        ILogger<DocumentUploadTools> logger)
    {
        _uploadService = uploadService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Upload a document to the system. Returns the document ID and metadata.")]
    public async Task<string> UploadDocument(
        [Description("The filename of the document")] string filename,
        [Description("The content type (MIME type) of the document")] string contentType,
        [Description("Base64 encoded document content")] string content,
        [Description("Username of the person uploading")] string uploadedBy = "mcp-client")
    {
        try
        {
            _logger.LogInformation("MCP Tool: UploadDocument called for {Filename}", filename);

            var bytes = Convert.FromBase64String(content);
            using var stream = new MemoryStream(bytes);

            var document = await _uploadService.UploadDocumentAsync(filename, stream, contentType, uploadedBy);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                documentId = document.Id,
                filename = document.FileName,
                contentType = document.ContentType,
                size = document.FileSize,
                uploadedAt = document.UploadedAt,
                uploadedBy = document.UploadedBy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UploadDocument MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Get details for a specific document by its ID.")]
    public async Task<string> GetDocument(
        [Description("The document ID (GUID) to retrieve")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: GetDocument called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var document = await _uploadService.GetDocumentAsync(docGuid);

            if (document == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Document not found"
                });
            }

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                document = new
                {
                    id = document.Id,
                    filename = document.FileName,
                    contentType = document.ContentType,
                    size = document.FileSize,
                    uploadedAt = document.UploadedAt,
                    uploadedBy = document.UploadedBy,
                    status = document.Status.ToString(),
                    lastModified = document.LastModified,
                    metadata = document.Metadata
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetDocument MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Download the content of a document by its ID. Returns base64 encoded content.")]
    public async Task<string> GetDocumentContent(
        [Description("The document ID (GUID) to download content for")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: GetDocumentContent called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var document = await _uploadService.GetDocumentAsync(docGuid);
            if (document == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Document not found"
                });
            }

            var content = await _uploadService.GetDocumentContentAsync(docGuid);
            var base64Content = Convert.ToBase64String(content);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                documentId = document.Id,
                filename = document.FileName,
                contentType = document.ContentType,
                size = document.FileSize,
                encoding = "base64",
                content = base64Content,
                downloadedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetDocumentContent MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("List all documents in the system with pagination support.")]
    public async Task<string> ListDocuments(
        [Description("Page number (starting from 1)")] int page = 1,
        [Description("Number of items per page")] int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("MCP Tool: ListDocuments called (page: {Page}, pageSize: {PageSize})", page, pageSize);

            var documents = await _uploadService.GetDocumentsAsync(page, pageSize);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                documents,
                page,
                pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ListDocuments MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Delete a document from the system by its ID.")]
    public async Task<string> DeleteDocument(
        [Description("The document ID (GUID) to delete")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: DeleteDocument called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var success = await _uploadService.DeleteDocumentAsync(docGuid);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success,
                documentId,
                message = success ? "Document deleted successfully" : "Document not found or could not be deleted"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteDocument MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Get statistics about document uploads grouped by time period and status.")]
    public async Task<string> GetUploadStatistics(
        [Description("Time period to group by (day, week, month, year)")] string period = "month",
        [Description("Group results by (status, user, contentType)")] string groupBy = "status")
    {
        try
        {
            _logger.LogInformation("MCP Tool: GetUploadStatistics called (period: {Period}, groupBy: {GroupBy})", period, groupBy);

            var stats = await _uploadService.GetUploadStatisticsAsync(period, groupBy);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                statistics = stats,
                period,
                groupBy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUploadStatistics MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
