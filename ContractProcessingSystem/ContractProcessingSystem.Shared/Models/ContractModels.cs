namespace ContractProcessingSystem.Shared.Models;

public record ContractDocument(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt,
    DateTime LastModified,
    string UploadedBy,
    ContractStatus Status,
    string? BlobPath = null,
    ContractMetadata? Metadata = null
)
{
    // Alias for compatibility
    public long FileSizeBytes => FileSize;
};

public record ContractMetadata(
    string? Title,
    DateTime? ContractDate,
    DateTime? ExpirationDate,
    decimal? ContractValue,
    string? Currency,
    List<string> Parties,
    List<string> KeyTerms,
    string? ContractType,
    Dictionary<string, object> CustomFields
);

public enum ContractStatus
{
    Uploaded,
    Parsing,
    Parsed,
    EmbeddingGeneration,
    EmbeddingComplete,
    ProcessingComplete,
    Failed
}

public record ContractChunk(
    Guid Id,
    Guid DocumentId,
    string Content,
    int ChunkIndex,
    int StartPosition,
    int EndPosition,
    ChunkType Type,
    Dictionary<string, object> Metadata
);

public enum ChunkType
{
    Header,
    Clause,
    Term,
    Condition,
    Signature,
    Other
}

public record VectorEmbedding(
    Guid Id,
    Guid ChunkId,
    float[] Vector,
    string Model,
    DateTime CreatedAt,
    Guid? DocumentId = null  // Added optional DocumentId for better tracking
);

public record SearchRequest(
    string Query,
    int MaxResults = 10,
    float MinScore = 0.7f,
    List<string>? DocumentTypes = null,
    Dictionary<string, object>? Filters = null
);

public record SearchResult(
    Guid DocumentId,
    Guid ChunkId,
    string Content,
    float Score,
    ContractMetadata Metadata,
    Dictionary<string, object> Highlights
);

public record SummarizationRequest(
    List<Guid> DocumentIds,
    SummaryType Type,
    int MaxLength = 500,
    string? Focus = null
);

public enum SummaryType
{
    Overview,
    KeyTerms,
    RiskAssessment,
    Comparison
}

public record SummarizationResult(
    string Summary,
    List<string> KeyPoints,
    Dictionary<string, object> Insights,
    DateTime GeneratedAt
);

public record ProcessingStatus(
    Guid RequestId,
    string Stage,
    float Progress,
    string? Message = null,
    DateTime LastUpdated = default
);

// New request model for storing embeddings with document mapping
public record StoreEmbeddingsRequest(
    VectorEmbedding[] Embeddings,
    Dictionary<Guid, Guid>? ChunkToDocumentMap = null,  // Maps ChunkId to DocumentId
    Dictionary<Guid, string>? ChunkContentMap = null     // Maps ChunkId to Content
);