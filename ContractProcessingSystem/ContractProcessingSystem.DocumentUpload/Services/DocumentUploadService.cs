using Azure.Storage.Blobs;
using ContractProcessingSystem.DocumentUpload.Data;
using ContractProcessingSystem.Shared.Models;
using ContractProcessingSystem.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace ContractProcessingSystem.DocumentUpload.Services;

public class DocumentUploadService : IDocumentUploadService
{
    private readonly DocumentContext _context;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<DocumentUploadService> _logger;
    private readonly string _containerName = "contracts";

    public DocumentUploadService(
        DocumentContext context,
        BlobServiceClient blobServiceClient,
        ILogger<DocumentUploadService> logger)
    {
        _context = context;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<ContractDocument> UploadDocumentAsync(
        string fileName, 
        Stream content, 
        string contentType, 
        string uploadedBy)
    {
        try
        {
            // Validate file
            ValidateFile(fileName, content, contentType);

            // Create document entity
            var documentEntity = new ContractDocumentEntity
            {
                FileName = fileName,
                ContentType = contentType,
                FileSize = content.Length,
                UploadedBy = uploadedBy,
                Status = ContractStatus.Uploaded
            };

            // Upload to blob storage
            var blobPath = await UploadToBlobStorageAsync(documentEntity.Id, fileName, content, contentType);
            documentEntity.BlobPath = blobPath;

            // Create basic metadata record for the document
            documentEntity.Metadata = new ContractMetadataEntity
            {
                DocumentId = documentEntity.Id,
                Title = fileName, // Use filename as initial title
                Parties = new List<string>(),
                KeyTerms = new List<string>(),
                CustomFields = new Dictionary<string, object>
                {
                    ["uploadSource"] = "direct_upload",
                    ["originalFileName"] = fileName
                }
            };

            // Save to database
            _context.Documents.Add(documentEntity);
            _context.Metadata.Add(documentEntity.Metadata);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Document {DocumentId} uploaded successfully by {User} with basic metadata", 
                documentEntity.Id, uploadedBy);

            return MapToContractDocument(documentEntity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document {FileName} by {User}", fileName, uploadedBy);
            throw;
        }
    }

    public async Task<ContractDocument?> GetDocumentAsync(Guid documentId)
    {
        _logger.LogDebug("Getting document {DocumentId} with metadata", documentId);
        
        var entity = await _context.Documents
            .Include(d => d.Metadata)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (entity == null)
        {
            _logger.LogWarning("Document {DocumentId} not found", documentId);
            return null;
        }

        _logger.LogDebug("Document {DocumentId} found. Metadata is {MetadataStatus}", 
            documentId, entity.Metadata == null ? "NULL" : "PRESENT");

        if (entity.Metadata != null)
        {
            _logger.LogDebug("Metadata details: Title={Title}, Parties={PartyCount}, KeyTerms={KeyTermsCount}", 
                entity.Metadata.Title ?? "NULL", 
                entity.Metadata.Parties?.Count ?? 0, 
                entity.Metadata.KeyTerms?.Count ?? 0);
        }

        return MapToContractDocument(entity);
    }

    public async Task<IEnumerable<ContractDocument>> GetDocumentsAsync(int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;
        
        var entities = await _context.Documents
            .Include(d => d.Metadata)
            .OrderByDescending(d => d.UploadedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return entities.Select(MapToContractDocument);
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId)
    {
        try
        {
            var entity = await _context.Documents.FindAsync(documentId);
            if (entity == null) return false;

            // Delete from blob storage
            if (!string.IsNullOrEmpty(entity.BlobPath))
            {
                await DeleteFromBlobStorageAsync(entity.BlobPath);
            }

            // Delete from database
            _context.Documents.Remove(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Document {DocumentId} deleted successfully", documentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId}", documentId);
            return false;
        }
    }

    private void ValidateFile(string fileName, Stream content, string contentType)
    {
        var allowedTypes = new[] { "application/pdf", "application/msword","text/plain", 
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };
        
        if (!allowedTypes.Contains(contentType))
        {
            throw new ArgumentException($"File type {contentType} is not supported");
        }

        if (content.Length > 50 * 1024 * 1024) // 50MB limit
        {
            throw new ArgumentException("File size exceeds 50MB limit");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required");
        }
    }

    public async Task<ContractDocument> UploadDocumentAsync(
        string fileName, 
        string contentType, 
        byte[] content, 
        string uploadedBy)
    {
        using var stream = new MemoryStream(content);
        return await UploadDocumentAsync(fileName, stream, contentType, uploadedBy);
    }

    public async Task<IEnumerable<ContractDocument>> GetDocumentsAsync(
        string? uploadedBy = null, 
        string? status = null, 
        int limit = 50, 
        int offset = 0)
    {
        try
        {
            var query = _context.Documents
                .Include(d => d.Metadata)
                .AsQueryable();

            if (!string.IsNullOrEmpty(uploadedBy))
            {
                query = query.Where(d => d.UploadedBy == uploadedBy);
            }

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ContractStatus>(status, out var statusEnum))
            {
                query = query.Where(d => d.Status == statusEnum);
            }

            var documents = await query
                .OrderByDescending(d => d.UploadedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return documents.Select(MapToContractDocument);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents with filters");
            throw;
        }
    }

    public async Task<bool> UpdateDocumentMetadataAsync(
        Guid documentId, 
        string? title = null, 
        List<string>? tags = null, 
        string? notes = null)
    {
        try
        {
            var document = await _context.Documents
                .Include(d => d.Metadata)
                .FirstOrDefaultAsync(d => d.Id == documentId);
            
            if (document == null)
            {
                return false;
            }

            // Create metadata if it doesn't exist
            if (document.Metadata == null)
            {
                document.Metadata = new ContractMetadataEntity
                {
                    DocumentId = documentId
                };
                _context.Metadata.Add(document.Metadata);
            }

            // Update metadata properties if provided
            if (!string.IsNullOrEmpty(title))
            {
                document.Metadata.Title = title;
            }

            if (tags != null)
            {
                document.Metadata.KeyTerms = tags;
            }

            if (!string.IsNullOrEmpty(notes))
            {
                // Store notes in custom fields
                document.Metadata.CustomFields["notes"] = notes;
            }

            document.LastModified = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated metadata for document {DocumentId}", documentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document metadata for {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<byte[]> GetDocumentContentAsync(Guid documentId)
    {
        try
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null || string.IsNullOrEmpty(document.BlobPath))
            {
                throw new FileNotFoundException($"Document not found: {documentId}");
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            // Ensure container exists before trying to access blobs
            await containerClient.CreateIfNotExistsAsync();
            
            var blobClient = containerClient.GetBlobClient(document.BlobPath);

            // Check if blob exists before trying to download
            var exists = await blobClient.ExistsAsync();
            if (!exists.Value)
            {
                throw new FileNotFoundException($"Document content not found in storage: {documentId}");
            }

            var response = await blobClient.DownloadAsync();
            using var memoryStream = new MemoryStream();
            await response.Value.Content.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document content for {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<object> GetUploadStatisticsAsync(string period = "month", string groupBy = "status")
    {
        try
        {
            var cutoffDate = period switch
            {
                "day" => DateTime.UtcNow.AddDays(-1),
                "week" => DateTime.UtcNow.AddDays(-7),
                "month" => DateTime.UtcNow.AddMonths(-1),
                "year" => DateTime.UtcNow.AddYears(-1),
                _ => DateTime.UtcNow.AddMonths(-1)
            };

            var query = _context.Documents.Where(d => d.UploadedAt >= cutoffDate);

            var statistics = groupBy switch
            {
                "status" => await query
                    .GroupBy(d => d.Status)
                    .Select(g => new { Key = g.Key.ToString(), Count = g.Count(), TotalSize = g.Sum(d => d.FileSize) })
                    .ToListAsync(),
                "uploader" => await query
                    .GroupBy(d => d.UploadedBy)
                    .Select(g => new { Key = g.Key, Count = g.Count(), TotalSize = g.Sum(d => d.FileSize) })
                    .ToListAsync(),
                "type" => await query
                    .GroupBy(d => d.ContentType)
                    .Select(g => new { Key = g.Key, Count = g.Count(), TotalSize = g.Sum(d => d.FileSize) })
                    .ToListAsync(),
                _ => await query
                    .GroupBy(d => d.Status)
                    .Select(g => new { Key = g.Key.ToString(), Count = g.Count(), TotalSize = g.Sum(d => d.FileSize) })
                    .ToListAsync()
            };

            return new
            {
                Period = period,
                GroupBy = groupBy,
                TotalDocuments = await query.CountAsync(),
                TotalSize = await query.SumAsync(d => d.FileSize),
                Statistics = statistics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upload statistics");
            throw;
        }
    }

    public async Task<bool> CreateOrUpdateMetadataFromParsingAsync(
        Guid documentId,
        ContractMetadata parsedMetadata)
    {
        try
        {
            var document = await _context.Documents
                .Include(d => d.Metadata)
                .FirstOrDefaultAsync(d => d.Id == documentId);
            
            if (document == null)
            {
                return false;
            }

            // Create or update metadata
            if (document.Metadata == null)
            {
                document.Metadata = new ContractMetadataEntity
                {
                    DocumentId = documentId
                };
                _context.Metadata.Add(document.Metadata);
            }

            // Update with parsed metadata
            if (!string.IsNullOrEmpty(parsedMetadata.Title))
            {
                document.Metadata.Title = parsedMetadata.Title;
            }

            if (parsedMetadata.ContractDate.HasValue)
            {
                document.Metadata.ContractDate = parsedMetadata.ContractDate;
            }

            if (parsedMetadata.ExpirationDate.HasValue)
            {
                document.Metadata.ExpirationDate = parsedMetadata.ExpirationDate;
            }

            if (parsedMetadata.ContractValue.HasValue)
            {
                document.Metadata.ContractValue = parsedMetadata.ContractValue;
            }

            if (!string.IsNullOrEmpty(parsedMetadata.Currency))
            {
                document.Metadata.Currency = parsedMetadata.Currency;
            }

            if (parsedMetadata.Parties?.Any() == true)
            {
                document.Metadata.Parties = parsedMetadata.Parties;
            }

            if (parsedMetadata.KeyTerms?.Any() == true)
            {
                document.Metadata.KeyTerms = parsedMetadata.KeyTerms;
            }

            if (!string.IsNullOrEmpty(parsedMetadata.ContractType))
            {
                document.Metadata.ContractType = parsedMetadata.ContractType;
            }

            if (parsedMetadata.CustomFields?.Any() == true)
            {
                foreach (var field in parsedMetadata.CustomFields)
                {
                    document.Metadata.CustomFields[field.Key] = field.Value;
                }
            }

            document.LastModified = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created/updated metadata from parsing for document {DocumentId}", documentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating metadata from parsing for {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<int> EnsureAllDocumentsHaveMetadataAsync()
    {
        try
        {
            var documentsWithoutMetadata = await _context.Documents
                .Where(d => d.Metadata == null)
                .ToListAsync();

            int createdCount = 0;
            foreach (var document in documentsWithoutMetadata)
            {
                var metadata = new ContractMetadataEntity
                {
                    DocumentId = document.Id,
                    Title = document.FileName,
                    Parties = new List<string>(),
                    KeyTerms = new List<string>(),
                    CustomFields = new Dictionary<string, object>
                    {
                        ["created"] = "auto_generated",
                        ["originalFileName"] = document.FileName
                    }
                };

                _context.Metadata.Add(metadata);
                createdCount++;
            }

            if (createdCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created metadata for {Count} documents that were missing it", createdCount);
            }

            return createdCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring documents have metadata");
            throw;
        }
    }

    public async Task<bool> EnsureBlobStorageInitializedAsync()
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var created = await containerClient.CreateIfNotExistsAsync();
            
            if (created?.Value != null)
            {
                _logger.LogInformation("Created blob container: {ContainerName}", _containerName);
                return true;
            }
            
            _logger.LogDebug("Blob container already exists: {ContainerName}", _containerName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring blob storage container exists");
            throw;
        }
    }

    private async Task<string> UploadToBlobStorageAsync(Guid documentId, string fileName, Stream content, string contentType)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var containerResult = await containerClient.CreateIfNotExistsAsync();
            
            if (containerResult?.Value != null)
            {
                _logger.LogInformation("Created blob container '{ContainerName}' for document upload", _containerName);
            }

            var blobName = $"{documentId:N}/{fileName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            content.Position = 0;
            await blobClient.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                {
                    ContentType = contentType
                }
            });

            _logger.LogDebug("Uploaded blob '{BlobName}' to container '{ContainerName}'", blobName, _containerName);
            return blobName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading to blob storage for document {DocumentId}", documentId);
            throw;
        }
    }

    private async Task DeleteFromBlobStorageAsync(string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        
        // Ensure container exists before trying to delete (defensive programming)
        await containerClient.CreateIfNotExistsAsync();
        
        var blobClient = containerClient.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync();
    }

    private static ContractDocument MapToContractDocument(ContractDocumentEntity entity)
    {
        ContractMetadata? metadata = null;
        if (entity.Metadata != null)
        {
            metadata = new ContractMetadata(
                entity.Metadata.Title,
                entity.Metadata.ContractDate,
                entity.Metadata.ExpirationDate,
                entity.Metadata.ContractValue,
                entity.Metadata.Currency,
                entity.Metadata.Parties,
                entity.Metadata.KeyTerms,
                entity.Metadata.ContractType,
                entity.Metadata.CustomFields
            );
        }

        return new ContractDocument(
            entity.Id,
            entity.FileName,
            entity.ContentType,
            entity.FileSize,
            entity.UploadedAt,
            entity.LastModified,
            entity.UploadedBy,
            entity.Status,
            entity.BlobPath,
            metadata
        );
    }
}

// Extension for DocumentContext to include metadata navigation
public static class DocumentContextExtensions
{
    public static IQueryable<ContractDocumentEntity> IncludeMetadata(this DbSet<ContractDocumentEntity> documents)
    {
        return documents.Include("Metadata");
    }
}