using ContractProcessingSystem.Shared.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;

namespace ContractProcessingSystem.EmbeddingService.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly ILLMProviderFactory _providerFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly ConcurrentDictionary<Guid, ProcessingStatus> _processingStatus;
    private readonly string _embeddingModel;
    private readonly IConfiguration _configuration;

    public EmbeddingService(
        ILLMProviderFactory providerFactory,
        IMemoryCache cache,
        ILogger<EmbeddingService> logger,
        IConfiguration configuration)
    {
        _providerFactory = providerFactory;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
        _processingStatus = new ConcurrentDictionary<Guid, ProcessingStatus>();
        _embeddingModel = configuration["AI:EmbeddingModel"] ?? 
                         configuration["AI:OpenAI:EmbeddingModel"] ?? 
                         "text-embedding-ada-002";
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
                _logger.LogDebug("Returning cached embedding for text hash {Hash}", text.GetHashCode());
                return cachedEmbedding;
            }

            _logger.LogDebug("Generating embedding for text of length {TextLength}", text.Length);

            // Get embedding provider from factory
            var embeddingProvider = _providerFactory.GetEmbeddingProvider();
            
            if (embeddingProvider == null || !embeddingProvider.SupportsEmbeddings)
            {
                _logger.LogError("No embedding provider available or provider doesn't support embeddings");
                throw new InvalidOperationException("No suitable embedding provider configured");
            }

            _logger.LogDebug("Using embedding provider: {ProviderName}", embeddingProvider.ProviderName);

            // Generate embedding using LLM provider
            var embeddingOptions = new EmbeddingOptions
            {
                Model = _embeddingModel
            };

            var embedding = await embeddingProvider.GenerateEmbeddingAsync(text, embeddingOptions);
            
            var vectorEmbedding = new VectorEmbedding(
                Guid.NewGuid(),
                Guid.NewGuid(), // This would be the actual chunk ID in real usage
                embedding,
                _embeddingModel,
                DateTime.UtcNow
            );

            // Cache the result
            _cache.Set(cacheKey, vectorEmbedding, TimeSpan.FromHours(24));

            _logger.LogDebug("Successfully generated embedding with {Dimensions} dimensions", embedding.Length);

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
        try
        {
            // Get embedding provider
            var embeddingProvider = _providerFactory.GetEmbeddingProvider();
            
            if (embeddingProvider == null || !embeddingProvider.SupportsEmbeddings)
            {
                _logger.LogError("No embedding provider available for batch processing");
                throw new InvalidOperationException("No suitable embedding provider configured");
            }

            _logger.LogDebug("Processing batch of {Count} chunks with provider: {ProviderName}", 
                chunks.Count, embeddingProvider.ProviderName);

            // Prepare texts for batch embedding generation
            var texts = chunks.Select(chunk => TruncateTextForEmbedding(chunk.Content)).ToList();

            // Generate embeddings in batch
            var embeddingOptions = new EmbeddingOptions
            {
                Model = _embeddingModel
            };

            var embeddings = await embeddingProvider.GenerateEmbeddingsAsync(texts, embeddingOptions);

            // Create VectorEmbedding objects
            var results = new List<VectorEmbedding>();
            for (int i = 0; i < chunks.Count; i++)
            {
                results.Add(new VectorEmbedding(
                    Guid.NewGuid(),
                    chunks[i].Id,
                    embeddings[i],
                    _embeddingModel,
                    DateTime.UtcNow
                ));
            }

            _logger.LogDebug("Successfully generated {Count} embeddings in batch", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Batch embedding generation failed, falling back to individual processing");
            
            // Fallback to individual processing
            var tasks = chunks.Select(async chunk =>
            {
                try
                {
                    var embeddingProvider = _providerFactory.GetEmbeddingProvider();
                    
                    if (embeddingProvider == null || !embeddingProvider.SupportsEmbeddings)
                    {
                        throw new InvalidOperationException("No embedding provider available");
                    }

                    var truncatedContent = TruncateTextForEmbedding(chunk.Content);
                    
                    var embeddingOptions = new EmbeddingOptions
                    {
                        Model = _embeddingModel
                    };

                    var embedding = await embeddingProvider.GenerateEmbeddingAsync(truncatedContent, embeddingOptions);
                    
                    return new VectorEmbedding(
                        Guid.NewGuid(),
                        chunk.Id,
                        embedding,
                        _embeddingModel,
                        DateTime.UtcNow
                    );
                }
                catch (Exception chunkEx)
                {
                    _logger.LogWarning(chunkEx, "Failed to generate embedding for chunk {ChunkId}", chunk.Id);
                    
                    // Return a zero vector as fallback
                    return new VectorEmbedding(
                        Guid.NewGuid(),
                        chunk.Id,
                        new float[1536], // Ada-002 embedding size as default
                        _embeddingModel,
                        DateTime.UtcNow
                    );
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }
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