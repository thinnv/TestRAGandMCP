using ContractProcessingSystem.Shared.Models;

namespace ContractProcessingSystem.Shared.Services;

public interface IDocumentUploadService
{
    Task<ContractDocument> UploadDocumentAsync(
        string fileName, 
        Stream content, 
        string contentType, 
        string uploadedBy);
    
    // Overload for MCP server with byte array
    Task<ContractDocument> UploadDocumentAsync(
        string fileName, 
        string contentType, 
        byte[] content, 
        string uploadedBy);
    
    Task<ContractDocument?> GetDocumentAsync(Guid documentId);
    Task<IEnumerable<ContractDocument>> GetDocumentsAsync(
        int page = 1, 
        int pageSize = 20);
    
    // Extended methods for MCP server
    Task<IEnumerable<ContractDocument>> GetDocumentsAsync(
        string? uploadedBy = null, 
        string? status = null, 
        int limit = 50, 
        int offset = 0);
    
    Task<bool> DeleteDocumentAsync(Guid documentId);
    
    Task<bool> UpdateDocumentMetadataAsync(
        Guid documentId, 
        string? title = null, 
        List<string>? tags = null, 
        string? notes = null);
    
    Task<byte[]> GetDocumentContentAsync(Guid documentId);
    
    Task<object> GetUploadStatisticsAsync(string period = "month", string groupBy = "status");
    
    // New method for parsing service integration
    Task<bool> CreateOrUpdateMetadataFromParsingAsync(
        Guid documentId,
        ContractMetadata parsedMetadata);
    
    // Utility method to ensure all documents have metadata
    Task<int> EnsureAllDocumentsHaveMetadataAsync();
    
    // Utility method to ensure blob storage is initialized
    Task<bool> EnsureBlobStorageInitializedAsync();
}

public interface IDocumentParsingService
{
    Task<ContractMetadata> ParseDocumentAsync(Guid documentId);
    Task<List<ContractChunk>> ChunkDocumentAsync(Guid documentId);
    Task<ProcessingStatus> GetParsingStatusAsync(Guid documentId);
}

public interface IEmbeddingService
{
    Task<VectorEmbedding[]> GenerateEmbeddingsAsync(List<ContractChunk> chunks);
    Task<VectorEmbedding> GenerateEmbeddingAsync(string text);
    Task<ProcessingStatus> GetEmbeddingStatusAsync(Guid documentId);
}

public interface IVectorService
{
    Task StoreEmbeddingsAsync(VectorEmbedding[] embeddings);
    Task<SearchResult[]> SearchSimilarAsync(
        float[] queryVector, 
        int maxResults = 10, 
        float minScore = 0.7f);
    
    Task<SearchResult[]> SearchAsync(SearchRequest request);
    Task<bool> DeleteDocumentEmbeddingsAsync(Guid documentId);
}

public interface IQueryService
{
    Task<SearchResult[]> SemanticSearchAsync(SearchRequest request);
    Task<SummarizationResult> SummarizeAsync(SummarizationRequest request);
    Task<SearchResult[]> HybridSearchAsync(
        string query, 
        Dictionary<string, object>? filters = null);
}

public interface IAIAgentService
{
    Task<ProcessingStatus> ProcessDocumentAsync(Guid documentId);
    Task<string> ChatAsync(string message, Guid? contextDocumentId = null);
    Task<SummarizationResult> AnalyzeContractAsync(Guid documentId);
    Task<string> CompareContractsAsync(List<Guid> documentIds);
}

public interface IWorkflowService
{
    Task<Guid> StartWorkflowAsync(string workflowName, object parameters);
    Task<ProcessingStatus> GetWorkflowStatusAsync(Guid workflowId);
    Task<bool> CancelWorkflowAsync(Guid workflowId);
    Task UpdateWorkflowStatusAsync(Guid workflowId, string stage, float progress, string? message = null);
}