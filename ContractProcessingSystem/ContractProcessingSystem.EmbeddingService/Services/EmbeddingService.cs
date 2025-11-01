using ContractProcessingSystem.Shared.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;

namespace ContractProcessingSystem.EmbeddingService.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly ILLMProviderFactory _providerFactory;
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates
    private readonly ITextEmbeddingGenerationService _embeddingService;
#pragma warning restore SKEXP0001
    private readonly IMemoryCache _cache;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly ConcurrentDictionary<Guid, ProcessingStatus> _processingStatus;
    private readonly string _embeddingModel;

    public EmbeddingService(
        ILLMProviderFactory providerFactory,
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates
        ITextEmbeddingGenerationService embeddingService,
#pragma warning restore SKEXP0001
        IMemoryCache cache,
        ILogger<EmbeddingService> logger,
        IConfiguration configuration)
    {
        _providerFactory = providerFactory;
        _embeddingService = embeddingService;
        _cache = cache;
        _logger = logger;
        _processingStatus = new ConcurrentDictionary<Guid, ProcessingStatus>();
        _embeddingModel = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-ada-002";
    }

    public async Task<VectorEmbedding[]> GenerateEmbeddingsAsync(List<ContractChunk> chunks)
    {
        if (!chunks.Any())
        {
            return Array.Empty<VectorEmbedding>();
        }

        var documentId = chunks.First().DocumentId;
        
        try
        {
            _logger.LogInformation("Starting embedding generation for {ChunkCount} chunks from document {DocumentId}", 
                chunks.Count, documentId);

            // Update processing status
            UpdateProcessingStatus(documentId, "Generating embeddings", 0.0f);

            var embeddings = new List<VectorEmbedding>();
            var batchSize = 100; // Process embeddings in batches
            
            for (int i = 0; i < chunks.Count; i += batchSize)
            {
                var batch = chunks.Skip(i).Take(batchSize).ToList();
                var batchEmbeddings = await ProcessEmbeddingBatchAsync(batch);
                embeddings.AddRange(batchEmbeddings);

                // Update progress
                var progress = (float)(i + batch.Count) / chunks.Count;
                UpdateProcessingStatus(documentId, "Generating embeddings", progress);
                
                _logger.LogDebug("Processed batch {BatchStart}-{BatchEnd} of {TotalChunks} chunks", 
                    i + 1, i + batch.Count, chunks.Count);
            }

            UpdateProcessingStatus(documentId, "Embedding generation complete", 1.0f);
            
            _logger.LogInformation("Successfully generated {EmbeddingCount} embeddings for document {DocumentId}", 
                embeddings.Count, documentId);

            return embeddings.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for document {DocumentId}", documentId);
            UpdateProcessingStatus(documentId, $"Error: {ex.Message}", -1.0f);
            throw;
        }
    }

    public async Task<VectorEmbedding> GenerateEmbeddingAsync(string text)
    {
        try
        {
            // Check cache first
            var cacheKey = $"embedding_{text.GetHashCode()}";
            if (_cache.TryGetValue(cacheKey, out VectorEmbedding? cachedEmbedding) && cachedEmbedding != null)
            {
                return cachedEmbedding;
            }

            _logger.LogDebug("Generating embedding for text of length {TextLength}", text.Length);

            // Generate embedding using Semantic Kernel
            var embedding = await _embeddingService.GenerateEmbeddingAsync(text);
            
            var vectorEmbedding = new VectorEmbedding(
                Guid.NewGuid(),
                Guid.NewGuid(), // This would be the actual chunk ID in real usage
                embedding.ToArray(),
                _embeddingModel,
                DateTime.UtcNow
            );

            // Cache the result
            _cache.Set(cacheKey, vectorEmbedding, TimeSpan.FromHours(24));

            return vectorEmbedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text");
            throw;
        }
    }

    public async Task<ProcessingStatus> GetEmbeddingStatusAsync(Guid documentId)
    {
        if (_processingStatus.TryGetValue(documentId, out var status))
        {
            return status;
        }

        // Return default status if not found
        return new ProcessingStatus(
            documentId,
            "Not started",
            0.0f,
            "Embedding generation not initiated",
            DateTime.UtcNow
        );
    }

    private async Task<List<VectorEmbedding>> ProcessEmbeddingBatchAsync(List<ContractChunk> chunks)
    {
        var tasks = chunks.Select(async chunk =>
        {
            try
            {
                // Check if content is too long for embedding model (8192 tokens for Ada-002)
                var truncatedContent = TruncateTextForEmbedding(chunk.Content);
                
                var embedding = await _embeddingService.GenerateEmbeddingAsync(truncatedContent);
                
                return new VectorEmbedding(
                    Guid.NewGuid(),
                    chunk.Id,
                    embedding.ToArray(),
                    _embeddingModel,
                    DateTime.UtcNow
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for chunk {ChunkId}", chunk.Id);
                
                // Return a zero vector as fallback
                return new VectorEmbedding(
                    Guid.NewGuid(),
                    chunk.Id,
                    new float[1536], // Ada-002 embedding size
                    _embeddingModel,
                    DateTime.UtcNow
                );
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private void UpdateProcessingStatus(Guid documentId, string stage, float progress)
    {
        var status = new ProcessingStatus(
            documentId,
            stage,
            progress,
            progress < 0 ? "Error occurred during processing" : null,
            DateTime.UtcNow
        );

        _processingStatus.AddOrUpdate(documentId, status, (key, oldValue) => status);
    }

    private static string TruncateTextForEmbedding(string text)
    {
        // Simple approximation: 1 token ≈ 4 characters for English text
        // Ada-002 has a limit of ~8192 tokens, so we'll use 6000 tokens to be safe
        const int maxChars = 6000 * 4;
        
        if (text.Length <= maxChars)
        {
            return text;
        }

        // Truncate at word boundary
        var truncated = text.Substring(0, maxChars);
        var lastSpace = truncated.LastIndexOf(' ');
        
        return lastSpace > 0 ? truncated.Substring(0, lastSpace) : truncated;
    }
}

public class EmbeddingBatchProcessor
{
    private readonly ILogger<EmbeddingBatchProcessor> _logger;
    private readonly IEmbeddingService _embeddingService;

    public EmbeddingBatchProcessor(
        ILogger<EmbeddingBatchProcessor> logger,
        IEmbeddingService embeddingService)
    {
        _logger = logger;
        _embeddingService = embeddingService;
    }

    public async Task<bool> ProcessDocumentAsync(Guid documentId, List<ContractChunk> chunks)
    {
        try
        {
            _logger.LogInformation("Starting batch embedding processing for document {DocumentId} with {ChunkCount} chunks", 
                documentId, chunks.Count);

            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);
            
            // Here you would typically store the embeddings in a vector database
            // For now, we'll just log the completion
            _logger.LogInformation("Completed batch embedding processing for document {DocumentId}. Generated {EmbeddingCount} embeddings", 
                documentId, embeddings.Length);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process document embeddings for {DocumentId}", documentId);
            return false;
        }
    }
}