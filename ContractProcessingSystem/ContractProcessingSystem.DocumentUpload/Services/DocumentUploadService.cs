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
        var allowedTypes = new[] { "application/pdf", "application/msword", 
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
            entity.UploadedBy,
            entity.Status
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