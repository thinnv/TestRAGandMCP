using System.Text.Json;
using ContractProcessingSystem.Shared.MCP;
using ContractProcessingSystem.DocumentUpload.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContractProcessingSystem.DocumentUpload.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentUploadMCPController : BaseMCPServer
{
    private readonly IDocumentUploadService _documentService;

    public DocumentUploadMCPController(
        IDocumentUploadService documentService,
        ILogger<DocumentUploadMCPController> logger) 
        : base(logger, new MCPServerInfo
        {
            Name = "contract-document-upload",
            Version = "1.0.0",
            Description = "Contract document upload and file management MCP server",
            Capabilities = new MCPServerCapabilities
            {
                Tools = true,
                Resources = true,
                Prompts = false,
                Logging = true
            }
        })
    {
        _documentService = documentService;
    }

    protected override async Task<List<MCPTool>> GetAvailableTools()
    {
        await Task.CompletedTask;
        return new List<MCPTool>
        {
            new MCPTool
            {
                Name = "upload_contract_file",
                Description = "Upload a contract document file for processing",
                InputSchema = CreateToolSchema(new
                {
                    type = "object",
                    properties = new
                    {
                        fileName = new { type = "string", description = "Name of the file to upload" },
                        contentType = new { type = "string", description = "MIME type of the file" },
                        fileContent = new { type = "string", description = "Base64 encoded file content" },
                        uploadedBy = new { type = "string", description = "Email of the user uploading the file" },
                        tags = new { type = "array", items = new { type = "string" }, description = "Optional tags for the document" }
                    },
                    required = new[] { "fileName", "contentType", "fileContent", "uploadedBy" }
                })
            },
            new MCPTool
            {
                Name = "get_file_metadata",
                Description = "Retrieve metadata information for an uploaded file",
                InputSchema = CreateToolSchema(new
                {
                    type = "object",
                    properties = new
                    {
                        documentId = new { type = "string", description = "UUID of the document to retrieve" }
                    },
                    required = new[] { "documentId" }
                })
            },
            new MCPTool
            {
                Name = "list_uploaded_files",
                Description = "List all uploaded contract documents with filtering options",
                InputSchema = CreateToolSchema(new
                {
                    type = "object",
                    properties = new
                    {
                        uploadedBy = new { type = "string", description = "Filter by uploader email" },
                        status = new { type = "string", description = "Filter by document status" },
                        limit = new { type = "number", description = "Maximum number of files to return", @default = 50 },
                        offset = new { type = "number", description = "Number of files to skip", @default = 0 }
                    }
                })
            },
            new MCPTool
            {
                Name = "delete_uploaded_file",
                Description = "Delete an uploaded contract document",
                InputSchema = CreateToolSchema(new
                {
                    type = "object",
                    properties = new
                    {
                        documentId = new { type = "string", description = "UUID of the document to delete" }
                    },
                    required = new[] { "documentId" }
                })
            },
            new MCPTool
            {
                Name = "update_file_metadata",
                Description = "Update metadata for an uploaded contract document",
                InputSchema = CreateToolSchema(new
                {
                    type = "object",
                    properties = new
                    {
                        documentId = new { type = "string", description = "UUID of the document to update" },
                        title = new { type = "string", description = "New title for the document" },
                        tags = new { type = "array", items = new { type = "string" }, description = "Updated tags for the document" },
                        notes = new { type = "string", description = "Additional notes about the document" }
                    },
                    required = new[] { "documentId" }
                })
            },
            new MCPTool
            {
                Name = "get_upload_statistics",
                Description = "Get statistics about uploaded documents",
                InputSchema = CreateToolSchema(new
                {
                    type = "object",
                    properties = new
                    {
                        period = new { type = "string", description = "Time period for statistics (day, week, month, year)", @default = "month" },
                        groupBy = new { type = "string", description = "Group statistics by (status, uploader, type)", @default = "status" }
                    }
                })
            }
        };
    }

    protected override async Task<MCPToolResult> ExecuteTool(string toolName, JsonElement arguments)
    {
        Logger.LogInformation("Executing tool: {ToolName}", toolName);

        try
        {
            return toolName switch
            {
                "upload_contract_file" => await HandleUploadFile(arguments),
                "get_file_metadata" => await HandleGetFileMetadata(arguments),
                "list_uploaded_files" => await HandleListFiles(arguments),
                "delete_uploaded_file" => await HandleDeleteFile(arguments),
                "update_file_metadata" => await HandleUpdateMetadata(arguments),
                "get_upload_statistics" => await HandleGetStatistics(arguments),
                _ => CreateToolResult($"Unknown tool: {toolName}", true)
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return CreateToolResult($"Error executing tool: {ex.Message}", true);
        }
    }

    protected override async Task<List<MCPResource>> GetAvailableResources()
    {
        await Task.CompletedTask;
        return new List<MCPResource>
        {
            new MCPResource
            {
                Uri = "contract://files",
                Name = "Uploaded Contract Files",
                Description = "List of all uploaded contract documents",
                MimeType = "application/json"
            },
            new MCPResource
            {
                Uri = "contract://file/{id}",
                Name = "Individual Contract File",
                Description = "Specific contract document by ID",
                MimeType = "application/json"
            },
            new MCPResource
            {
                Uri = "contract://file/{id}/content",
                Name = "Contract File Content",
                Description = "Raw content of a contract document",
                MimeType = "application/octet-stream"
            },
            new MCPResource
            {
                Uri = "contract://statistics/uploads",
                Name = "Upload Statistics",
                Description = "Statistics about uploaded documents",
                MimeType = "application/json"
            }
        };
    }

    protected override async Task<object> ReadResource(string uri)
    {
        var parts = uri.Split('/');
        
        return parts[2] switch
        {
            "files" => await GetAllFiles(),
            "file" when parts.Length >= 4 => await GetFileResource(parts[3], parts.Length > 4 ? parts[4] : null),
            "statistics" when parts[3] == "uploads" => await GetUploadStatistics(),
            _ => throw new ArgumentException($"Unknown resource: {uri}")
        };
    }

    // Tool implementations
    private async Task<MCPToolResult> HandleUploadFile(JsonElement arguments)
    {
        var args = DeserializeToolArguments<UploadFileArgs>(arguments);
        if (args == null)
        {
            return CreateToolResult("Invalid upload file arguments", true);
        }

        try
        {
            // Convert base64 content to bytes
            var fileBytes = Convert.FromBase64String(args.FileContent);
            
            // Create a mock file for the service (you'd need to adapt this based on your actual service interface)
            var result = await _documentService.UploadDocumentAsync(
                args.FileName, 
                args.ContentType, 
                fileBytes, 
                args.UploadedBy);

            return CreateToolResultWithData(new
            {
                documentId = result.Id,
                fileName = result.FileName,
                status = result.Status.ToString(),
                uploadedAt = result.UploadedAt,
                message = "File uploaded successfully"
            }, $"Successfully uploaded contract file: {args.FileName}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading file {FileName}", args.FileName);
            return CreateToolResult($"Failed to upload file: {ex.Message}", true);
        }
    }

    private async Task<MCPToolResult> HandleGetFileMetadata(JsonElement arguments)
    {
        if (!ValidateRequiredParameter(arguments, "documentId", out var documentIdObj) ||
            !Guid.TryParse(documentIdObj?.ToString(), out var documentId))
        {
            return CreateToolResult("Invalid or missing document ID", true);
        }

        try
        {
            var document = await _documentService.GetDocumentAsync(documentId);
            if (document == null)
            {
                return CreateToolResult($"Document not found: {documentId}", true);
            }

            return CreateToolResultWithData(new
            {
                id = document.Id,
                fileName = document.FileName,
                contentType = document.ContentType,
                fileSizeBytes = document.FileSizeBytes,
                status = document.Status.ToString(),
                uploadedBy = document.UploadedBy,
                uploadedAt = document.UploadedAt,
                lastModified = document.LastModified,
                blobPath = document.BlobPath,
                metadata = document.Metadata
            }, $"Metadata for contract file: {document.FileName}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting file metadata for {DocumentId}", documentId);
            return CreateToolResult($"Failed to get file metadata: {ex.Message}", true);
        }
    }

    private async Task<MCPToolResult> HandleListFiles(JsonElement arguments)
    {
        var args = DeserializeToolArguments<ListFilesArgs>(arguments) ?? new ListFilesArgs();

        try
        {
            var documents = await _documentService.GetDocumentsAsync(
                args.UploadedBy, 
                args.Status, 
                args.Limit, 
                args.Offset);

            return CreateToolResultWithData(new
            {
                documents = documents.Select(d => new
                {
                    id = d.Id,
                    fileName = d.FileName,
                    contentType = d.ContentType,
                    status = d.Status.ToString(),
                    uploadedBy = d.UploadedBy,
                    uploadedAt = d.UploadedAt,
                    fileSizeBytes = d.FileSizeBytes
                }),
                total = documents.Count(),
                offset = args.Offset,
                limit = args.Limit
            }, $"Retrieved {documents.Count()} contract documents");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error listing files");
            return CreateToolResult($"Failed to list files: {ex.Message}", true);
        }
    }

    private async Task<MCPToolResult> HandleDeleteFile(JsonElement arguments)
    {
        if (!ValidateRequiredParameter(arguments, "documentId", out var documentIdObj) ||
            !Guid.TryParse(documentIdObj?.ToString(), out var documentId))
        {
            return CreateToolResult("Invalid or missing document ID", true);
        }

        try
        {
            var success = await _documentService.DeleteDocumentAsync(documentId);
            if (!success)
            {
                return CreateToolResult($"Document not found or could not be deleted: {documentId}", true);
            }

            return CreateToolResult($"Successfully deleted document: {documentId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting document {DocumentId}", documentId);
            return CreateToolResult($"Failed to delete document: {ex.Message}", true);
        }
    }

    private async Task<MCPToolResult> HandleUpdateMetadata(JsonElement arguments)
    {
        var args = DeserializeToolArguments<UpdateMetadataArgs>(arguments);
        if (args == null || !Guid.TryParse(args.DocumentId, out var documentId))
        {
            return CreateToolResult("Invalid update metadata arguments", true);
        }

        try
        {
            var success = await _documentService.UpdateDocumentMetadataAsync(
                documentId, 
                args.Title, 
                args.Tags, 
                args.Notes);

            if (!success)
            {
                return CreateToolResult($"Document not found or could not be updated: {documentId}", true);
            }

            return CreateToolResult($"Successfully updated metadata for document: {documentId}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating metadata for document {DocumentId}", documentId);
            return CreateToolResult($"Failed to update metadata: {ex.Message}", true);
        }
    }

    private async Task<MCPToolResult> HandleGetStatistics(JsonElement arguments)
    {
        var args = DeserializeToolArguments<StatisticsArgs>(arguments) ?? new StatisticsArgs();

        try
        {
            var stats = await _documentService.GetUploadStatisticsAsync(args.Period, args.GroupBy);

            return CreateToolResultWithData(stats, $"Upload statistics for period: {args.Period}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting upload statistics");
            return CreateToolResult($"Failed to get statistics: {ex.Message}", true);
        }
    }

    // Resource implementations
    private async Task<object> GetAllFiles()
    {
        var documents = await _documentService.GetDocumentsAsync(page: 1, pageSize: 100);
        return documents.Select(d => new
        {
            id = d.Id,
            fileName = d.FileName,
            status = d.Status.ToString(),
            uploadedAt = d.UploadedAt
        });
    }

    private async Task<object> GetFileResource(string fileId, string? subResource)
    {
        if (!Guid.TryParse(fileId, out var documentId))
        {
            throw new ArgumentException($"Invalid document ID: {fileId}");
        }

        var document = await _documentService.GetDocumentAsync(documentId);
        if (document == null)
        {
            throw new ArgumentException($"Document not found: {fileId}");
        }

        return subResource switch
        {
            "content" => await _documentService.GetDocumentContentAsync(documentId),
            null => document,
            _ => throw new ArgumentException($"Unknown sub-resource: {subResource}")
        };
    }

    private async Task<object> GetUploadStatistics()
    {
        return await _documentService.GetUploadStatisticsAsync("month", "status");
    }
}

// Tool argument models
public class UploadFileArgs
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileContent { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public List<string>? Tags { get; set; }
}

public class ListFilesArgs
{
    public string? UploadedBy { get; set; }
    public string? Status { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
}

public class UpdateMetadataArgs
{
    public string DocumentId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public List<string>? Tags { get; set; }
    public string? Notes { get; set; }
}

public class StatisticsArgs
{
    public string Period { get; set; } = "month";
    public string GroupBy { get; set; } = "status";
}