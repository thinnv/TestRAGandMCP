using Azure.Storage.Blobs;
using ContractProcessingSystem.DocumentUpload.Data;
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

            // Save to database
            _context.Documents.Add(documentEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Document {DocumentId} uploaded successfully by {User}", 
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
        var entity = await _context.Documents
            .Include(d => d.Metadata)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        return entity != null ? MapToContractDocument(entity) : null;
    }

    public async Task<IEnumerable<ContractDocument>> GetDocumentsAsync(int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;
        
        var entities = await _context.Documents
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
            var query = _context.Documents.AsQueryable();

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
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                return false;
            }

            // Update document properties if provided
            if (!string.IsNullOrEmpty(title))
            {
                // You might want to add a Title field to the entity
                // For now, we'll store it in a custom metadata field
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
            var blobClient = containerClient.GetBlobClient(document.BlobPath);

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

    private async Task<string> UploadToBlobStorageAsync(Guid documentId, string fileName, Stream content, string contentType)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();

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

        return blobName;
    }

    private async Task DeleteFromBlobStorageAsync(string blobPath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync();
    }

    private static ContractDocument MapToContractDocument(ContractDocumentEntity entity)
    {
        return new ContractDocument(
            entity.Id,
            entity.FileName,
            entity.ContentType,
            entity.FileSize,
            entity.UploadedAt,
            entity.LastModified,
            entity.UploadedBy,
            entity.Status,
            entity.BlobPath
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