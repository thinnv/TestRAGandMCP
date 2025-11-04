using ContractProcessingSystem.DocumentUpload.Services;
using ContractProcessingSystem.Shared.Models;
using ContractProcessingSystem.Shared.MCP.Official;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ContractProcessingSystem.DocumentUpload.Controllers;

/// <summary>
/// Enhanced DocumentUpload MCP Controller using official MCP patterns and compliance
/// This combines your proven business logic with official MCP protocol compliance
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EnhancedDocumentUploadMCPController : OfficialMCPServerBase
{
    private readonly IDocumentUploadService _documentService;

    public EnhancedDocumentUploadMCPController(
        IDocumentUploadService documentService,
        ILogger<EnhancedDocumentUploadMCPController> logger)
        : base(logger)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Get available MCP tools with official schema compliance
    /// </summary>
    protected override async Task<List<OfficialMCPTool>> GetAvailableTools()
    {
        await Task.CompletedTask;
        
        return new List<OfficialMCPTool>
        {
            new OfficialMCPTool
            {
                Name = "upload_contract_file",
                Description = "Upload a contract file with metadata and tags using official MCP patterns",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        fileName = new { type = "string", description = "Name of the file to upload" },
                        contentType = new { type = "string", description = "MIME type of the file" },
                        fileContent = new { type = "string", description = "Base64 encoded file content" },
                        uploadedBy = new { type = "string", description = "Email of the user uploading the file" },
                        tags = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "Optional tags for the document"
                        }
                    },
                    required = new[] { "fileName", "contentType", "fileContent", "uploadedBy" }
                }
            },

            new OfficialMCPTool
            {
                Name = "get_file_metadata",
                Description = "Retrieve metadata for a specific uploaded file",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        fileId = new { type = "string", description = "GUID of the file to retrieve metadata for" }
                    },
                    required = new[] { "fileId" }
                }
            },

            new OfficialMCPTool
            {
                Name = "list_uploaded_files",
                Description = "List uploaded files with optional filtering and pagination",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        limit = new { type = "integer", description = "Maximum number of files to return", minimum = 1, maximum = 100 },
                        offset = new { type = "integer", description = "Number of files to skip", minimum = 0 },
                        uploadedBy = new { type = "string", description = "Filter by uploader email (optional)" },
                        status = new { type = "string", description = "Filter by file status (optional)" }
                    },
                    required = new string[0]
                }
            },

            new OfficialMCPTool
            {
                Name = "delete_contract_file",
                Description = "Delete a contract file and its metadata",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        fileId = new { type = "string", description = "GUID of the file to delete" },
                        deletedBy = new { type = "string", description = "Email of the user deleting the file" }
                    },
                    required = new[] { "fileId", "deletedBy" }
                }
            },

            new OfficialMCPTool
            {
                Name = "update_file_metadata",
                Description = "Update metadata and tags for an uploaded file",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        fileId = new { type = "string", description = "GUID of the file to update" },
                        updatedBy = new { type = "string", description = "Email of the user updating the metadata" },
                        tags = new
                        {
                            type = "array",
                            items = new { type = "string" },
                            description = "New tags for the document"
                        },
                        description = new { type = "string", description = "New description for the document" }
                    },
                    required = new[] { "fileId", "updatedBy" }
                }
            },

            new OfficialMCPTool
            {
                Name = "get_upload_statistics",
                Description = "Get upload statistics and analytics with official MCP compliance",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        period = new
                        {
                            type = "string",
                            description = "Time period for statistics",
                            @enum = new[] { "day", "week", "month", "year" }
                        },
                        groupBy = new
                        {
                            type = "string",
                            description = "How to group the statistics",
                            @enum = new[] { "status", "uploader", "type" }
                        }
                    },
                    required = new string[0]
                }
            }
        };
    }

    /// <summary>
    /// Execute MCP tools with official error handling and response patterns
    /// </summary>
    protected override async Task<OfficialMCPToolResult> ExecuteTool(string toolName, JsonElement arguments)
    {
        try
        {
            Logger.LogInformation("Executing official MCP tool: {ToolName}", toolName);

            return toolName switch
            {
                "upload_contract_file" => await HandleUploadFile(arguments),
                "get_file_metadata" => await HandleGetFileMetadata(arguments),
                "list_uploaded_files" => await HandleListFiles(arguments),
                "delete_contract_file" => await HandleDeleteFile(arguments),
                "update_file_metadata" => await HandleUpdateMetadata(arguments),
                "get_upload_statistics" => await HandleGetStatistics(arguments),
                _ => OfficialMCPToolResult.Error($"Unknown tool: {toolName}", -32601)
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing official MCP tool {ToolName}", toolName);
            return OfficialMCPToolResult.Error($"Tool execution failed: {ex.Message}", -32000);
        }
    }

    /// <summary>
    /// Get available MCP resources with official compliance
    /// </summary>
    protected override async Task<List<OfficialMCPResource>> GetAvailableResources()
    {
        await Task.CompletedTask;
        
        return new List<OfficialMCPResource>
        {
            new OfficialMCPResource
            {
                Uri = "contract://files",
                Name = "Contract Files",
                Description = "List of all uploaded contract files with official MCP resource format",
                MimeType = "application/json"
            },
            new OfficialMCPResource
            {
                Uri = "contract://statistics",
                Name = "Upload Statistics",
                Description = "Upload statistics and metrics in official MCP format",
                MimeType = "application/json"
            },
            new OfficialMCPResource
            {
                Uri = "contract://metadata/{fileId}",
                Name = "File Metadata",
                Description = "Individual file metadata in official MCP resource format",
                MimeType = "application/json"
            }
        };
    }

    /// <summary>
    /// Read MCP resource content with official compliance
    /// </summary>
    protected override async Task<object> ReadResource(string uri)
    {
        return uri switch
        {
            "contract://files" => await GetAllFiles(),
            "contract://statistics" => await GetStatistics(),
            var uriPattern when uriPattern.StartsWith("contract://metadata/") => await GetFileMetadata(uriPattern),
            _ => throw new ArgumentException($"Resource not found: {uri}", nameof(uri))
        };
    }

    // Enhanced tool implementations with official MCP patterns
    private async Task<OfficialMCPToolResult> HandleUploadFile(JsonElement arguments)
    {
        try
        {
            var fileName = arguments.GetProperty("fileName").GetString()!;
            var contentType = arguments.GetProperty("contentType").GetString()!;
            var fileContentBase64 = arguments.GetProperty("fileContent").GetString()!;
            var uploadedBy = arguments.GetProperty("uploadedBy").GetString()!;
            
            var tags = new List<string>();
            if (arguments.TryGetProperty("tags", out var tagsElement))
            {
                tags = tagsElement.EnumerateArray()
                    .Select(t => t.GetString()!)
                    .ToList();
            }

            var fileContent = Convert.FromBase64String(fileContentBase64);
            var document = await _documentService.UploadDocumentAsync(fileName, contentType, fileContent, uploadedBy);

            // Update tags if provided
            if (tags.Any())
            {
                await _documentService.UpdateDocumentMetadataAsync(document.Id, tags: tags);
            }

            Logger.LogInformation("File uploaded successfully via official MCP: {DocumentId}", document.Id);

            return OfficialMCPToolResult.Success(new
            {
                success = true,
                documentId = document.Id,
                fileName = document.FileName,
                fileSize = document.FileSize,
                uploadedAt = document.UploadedAt,
                uploadedBy = document.UploadedBy,
                status = document.Status.ToString(),
                tags = tags,
                message = "File uploaded successfully using official MCP patterns",
                mcpCompliant = true
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading file via official MCP");
            return OfficialMCPToolResult.Error($"Upload failed: {ex.Message}", -32000);
        }
    }

    private async Task<OfficialMCPToolResult> HandleGetFileMetadata(JsonElement arguments)
    {
        try
        {
            var fileIdStr = arguments.GetProperty("fileId").GetString()!;
            if (!Guid.TryParse(fileIdStr, out var fileId))
            {
                return OfficialMCPToolResult.Error("Invalid file ID format", -32602);
            }

            var document = await _documentService.GetDocumentAsync(fileId);
            if (document == null)
            {
                return OfficialMCPToolResult.Error("File not found", -32002);
            }

            return OfficialMCPToolResult.Success(new
            {
                id = document.Id,
                fileName = document.FileName,
                contentType = document.ContentType,
                fileSize = document.FileSize,
                uploadedAt = document.UploadedAt,
                uploadedBy = document.UploadedBy,
                status = document.Status.ToString(),
                lastModified = document.LastModified,
                mcpCompliant = true,
                resourceUri = $"contract://metadata/{document.Id}"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting file metadata via official MCP");
            return OfficialMCPToolResult.Error($"Failed to get metadata: {ex.Message}", -32000);
        }
    }

    private async Task<OfficialMCPToolResult> HandleListFiles(JsonElement arguments)
    {
        try
        {
            var limit = arguments.TryGetProperty("limit", out var limitProp) ? limitProp.GetInt32() : 50;
            var offset = arguments.TryGetProperty("offset", out var offsetProp) ? offsetProp.GetInt32() : 0;
            var uploadedBy = arguments.TryGetProperty("uploadedBy", out var uploaderProp) ? uploaderProp.GetString() : null;
            var status = arguments.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

            var documents = await _documentService.GetDocumentsAsync(uploadedBy, status, limit, offset);

            return OfficialMCPToolResult.Success(new
            {
                files = documents.Select(d => new
                {
                    id = d.Id,
                    fileName = d.FileName,
                    contentType = d.ContentType,
                    fileSize = d.FileSize,
                    uploadedAt = d.UploadedAt,
                    uploadedBy = d.UploadedBy,
                    status = d.Status.ToString(),
                    lastModified = d.LastModified,
                    resourceUri = $"contract://metadata/{d.Id}"
                }),
                pagination = new
                {
                    limit = limit,
                    offset = offset,
                    total = documents.Count()
                },
                mcpCompliant = true,
                resourceUri = "contract://files"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error listing files via official MCP");
            return OfficialMCPToolResult.Error($"Failed to list files: {ex.Message}", -32000);
        }
    }

    private async Task<OfficialMCPToolResult> HandleDeleteFile(JsonElement arguments)
    {
        try
        {
            var fileIdStr = arguments.GetProperty("fileId").GetString()!;
            var deletedBy = arguments.GetProperty("deletedBy").GetString()!;

            if (!Guid.TryParse(fileIdStr, out var fileId))
            {
                return OfficialMCPToolResult.Error("Invalid file ID format", -32602);
            }

            var deleted = await _documentService.DeleteDocumentAsync(fileId);
            if (!deleted)
            {
                return OfficialMCPToolResult.Error("File not found or could not be deleted", -32002);
            }

            Logger.LogInformation("File deleted via official MCP: {FileId} by {DeletedBy}", fileId, deletedBy);

            return OfficialMCPToolResult.Success(new
            {
                success = true,
                fileId = fileId,
                deletedBy = deletedBy,
                deletedAt = DateTime.UtcNow,
                message = "File deleted successfully using official MCP patterns",
                mcpCompliant = true
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting file via official MCP");
            return OfficialMCPToolResult.Error($"Failed to delete file: {ex.Message}", -32000);
        }
    }

    private async Task<OfficialMCPToolResult> HandleUpdateMetadata(JsonElement arguments)
    {
        try
        {
            var fileIdStr = arguments.GetProperty("fileId").GetString()!;
            var updatedBy = arguments.GetProperty("updatedBy").GetString()!;

            if (!Guid.TryParse(fileIdStr, out var fileId))
            {
                return OfficialMCPToolResult.Error("Invalid file ID format", -32602);
            }

            var tags = new List<string>();
            if (arguments.TryGetProperty("tags", out var tagsElement))
            {
                tags = tagsElement.EnumerateArray()
                    .Select(t => t.GetString()!)
                    .ToList();
            }

            var description = arguments.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

            var updated = await _documentService.UpdateDocumentMetadataAsync(fileId, notes: description, tags: tags);
            if (!updated)
            {
                return OfficialMCPToolResult.Error("File not found or could not be updated", -32002);
            }

            Logger.LogInformation("File metadata updated via official MCP: {FileId} by {UpdatedBy}", fileId, updatedBy);

            return OfficialMCPToolResult.Success(new
            {
                success = true,
                fileId = fileId,
                updatedBy = updatedBy,
                updatedAt = DateTime.UtcNow,
                tags = tags,
                description = description,
                message = "Metadata updated successfully using official MCP patterns",
                mcpCompliant = true,
                resourceUri = $"contract://metadata/{fileId}"
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating metadata via official MCP");
            return OfficialMCPToolResult.Error($"Failed to update metadata: {ex.Message}", -32000);
        }
    }

    private async Task<OfficialMCPToolResult> HandleGetStatistics(JsonElement arguments)
    {
        try
        {
            var period = arguments.TryGetProperty("period", out var periodProp) ? periodProp.GetString() : "month";
            var groupBy = arguments.TryGetProperty("groupBy", out var groupProp) ? groupProp.GetString() : "status";

            var statistics = await _documentService.GetUploadStatisticsAsync(period!, groupBy!);

            return OfficialMCPToolResult.Success(new
            {
                statistics = statistics,
                mcpCompliant = true,
                resourceUri = "contract://statistics",
                generatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting statistics via official MCP");
            return OfficialMCPToolResult.Error($"Failed to get statistics: {ex.Message}", -32000);
        }
    }

    // Enhanced resource implementations with official MCP compliance
    private async Task<object> GetAllFiles()
    {
        var documents = await _documentService.GetDocumentsAsync(page: 1, pageSize: 100);
        return new
        {
            files = documents.Select(d => new
            {
                id = d.Id,
                fileName = d.FileName,
                contentType = d.ContentType,
                fileSize = d.FileSize,
                uploadedAt = d.UploadedAt,
                uploadedBy = d.UploadedBy,
                status = d.Status.ToString(),
                lastModified = d.LastModified,
                resourceUri = $"contract://metadata/{d.Id}"
            }),
            mcpCompliant = true,
            resourceType = "contract-files",
            generatedAt = DateTime.UtcNow
        };
    }

    private async Task<object> GetStatistics()
    {
        var statistics = await _documentService.GetUploadStatisticsAsync();
        return new
        {
            statistics = statistics,
            mcpCompliant = true,
            resourceType = "upload-statistics",
            generatedAt = DateTime.UtcNow
        };
    }

    private async Task<object> GetFileMetadata(string uri)
    {
        var fileIdStr = uri.Replace("contract://metadata/", "");
        if (!Guid.TryParse(fileIdStr, out var fileId))
        {
            throw new ArgumentException($"Invalid file ID in URI: {uri}");
        }

        var document = await _documentService.GetDocumentAsync(fileId);
        if (document == null)
        {
            throw new ArgumentException($"File not found: {fileId}");
        }

        return new
        {
            id = document.Id,
            fileName = document.FileName,
            contentType = document.ContentType,
            fileSize = document.FileSize,
            uploadedAt = document.UploadedAt,
            uploadedBy = document.UploadedBy,
            status = document.Status.ToString(),
            lastModified = document.LastModified,
            mcpCompliant = true,
            resourceType = "file-metadata",
            resourceUri = uri
        };
    }
}