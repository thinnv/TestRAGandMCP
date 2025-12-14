using Qdrant.Client;
using Qdrant.Client.Grpc;
using Microsoft.Extensions.Caching.Memory;
using ContractProcessingSystem.Shared.Models;
using System.Text.Json;
using System.Collections.Concurrent;

namespace ContractProcessingSystem.VectorService.Services;

public class QdrantVectorService : IVectorService
{
    private readonly QdrantClient _qdrantClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<QdrantVectorService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly string _collectionName = "contract_embeddings";

    // Dynamic vector dimension - will be set based on first embedding stored
    // Made static to match the static _embeddingStore
    private static int _vectorDimension = 768; // Default to Gemini text-embedding-004 size
    private static readonly object _dimensionLock = new object();

    // Qdrant availability tracking
    private bool _qdrantAvailable = false;
    private bool _qdrantInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // Fallback in-memory store (used when Qdrant is unavailable OR as cache)
    private static readonly ConcurrentDictionary<Guid, VectorEmbedding> _embeddingStore = new();
    private static readonly ConcurrentDictionary<Guid, Guid> _chunkToDocumentMap = new();
    private static readonly ConcurrentDictionary<Guid, string> _chunkContentMap = new();

    public QdrantVectorService(
        QdrantClient qdrantClient,
        IMemoryCache cache,
        ILogger<QdrantVectorService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _qdrantClient = qdrantClient;
        _cache = cache;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;

        // Try to read vector dimension from configuration
        lock (_dimensionLock)
        {
            if (_embeddingStore.IsEmpty)
            {
                if (int.TryParse(configuration["VectorService:Dimension"], out var configuredDimension))
                {
                    _vectorDimension = configuredDimension;
                    _logger.LogInformation("Using configured vector dimension: {Dimension}", _vectorDimension);
                }
                else
                {
                    _logger.LogInformation("Using default vector dimension: {Dimension} (Gemini text-embedding-004)", _vectorDimension);
                }
            }
            else
            {
                // Detect from existing embeddings
                var existingDimension = _embeddingStore.Values.FirstOrDefault()?.Vector.Length;
                if (existingDimension.HasValue && existingDimension.Value != _vectorDimension)
                {
                    _logger.LogInformation("Detected vector dimension from existing embeddings: {Dimension} (was {OldDimension})",
                        existingDimension.Value, _vectorDimension);
                    _vectorDimension = existingDimension.Value;
                }
                else
                {
                    _logger.LogInformation("Using existing vector dimension: {Dimension} from {Count} stored embeddings",
                        _vectorDimension, _embeddingStore.Count);
                }
            }
        }

        // Try to initialize Qdrant asynchronously (don't block constructor)
        _ = Task.Run(async () => await EnsureQdrantInitializedAsync());
    }

    private async Task<bool> EnsureQdrantInitializedAsync()
    {
        if (_qdrantInitialized)
            return _qdrantAvailable;

        await _initLock.WaitAsync();
        try
        {
            if (_qdrantInitialized)
                return _qdrantAvailable;

            _logger.LogInformation("Initializing Qdrant connection for collection '{Collection}'", _collectionName);

            try
            {
                // Check if Qdrant is accessible
                var collections = await _qdrantClient.ListCollectionsAsync();
                var collectionExists = collections.Contains(_collectionName);

                if (!collectionExists)
                {
                    _logger.LogInformation("Collection '{Collection}' does not exist, creating...", _collectionName);
                    await CreateCollectionAsync();
                }
                else
                {
                    _logger.LogInformation("Collection '{Collection}' already exists", _collectionName);
                }

                _qdrantAvailable = true;
                _qdrantInitialized = true;
                _logger.LogInformation("? Qdrant initialized successfully - using persistent vector storage");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "?? Qdrant not available - falling back to in-memory storage");
                _qdrantAvailable = false;
                _qdrantInitialized = true;
                return false;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task CreateCollectionAsync()
    {
        try
        {
            _logger.LogInformation("Creating Qdrant collection '{Collection}' with {Dimension} dimensions",
                _collectionName, _vectorDimension);

            await _qdrantClient.CreateCollectionAsync(
                collectionName: _collectionName,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)_vectorDimension,
                    Distance = Distance.Cosine
                }
            );

            // Create payload index for efficient filtering
            await _qdrantClient.CreatePayloadIndexAsync(
                collectionName: _collectionName,
                fieldName: "document_id",
                schemaType: PayloadSchemaType.Keyword
            );

            await _qdrantClient.CreatePayloadIndexAsync(
                collectionName: _collectionName,
                fieldName: "chunk_id",
                schemaType: PayloadSchemaType.Keyword
            );

            _logger.LogInformation("Successfully created Qdrant collection '{Collection}'", _collectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Qdrant collection");
            throw;
        }
    }

    public async Task StoreEmbeddingsAsync(VectorEmbedding[] embeddings)
    {
        if (!embeddings.Any())
        {
            _logger.LogWarning("No embeddings provided for storage");
            return;
        }

        try
        {
            // Auto-detect vector dimension from first embedding
            lock (_dimensionLock)
            {
                if (_embeddingStore.IsEmpty && embeddings.Any())
                {
                    var firstDimension = embeddings[0].Vector.Length;
                    if (firstDimension != _vectorDimension)
                    {
                        _logger.LogInformation("Updating vector dimension from {OldDim} to {NewDim} based on received embeddings",
                            _vectorDimension, firstDimension);
                        _vectorDimension = firstDimension;
                    }
                }
                else if (!_embeddingStore.IsEmpty && embeddings.Any())
                {
                    var existingDimension = _embeddingStore.Values.First().Vector.Length;
                    var newDimension = embeddings[0].Vector.Length;

                    if (existingDimension != newDimension)
                    {
                        _logger.LogWarning(
                            "Dimension mismatch! Existing: {ExistingDim}, New: {NewDim}. " +
                            "Consider clearing the vector store.",
                            existingDimension, newDimension);
                    }
                }
            }

            _logger.LogInformation("Storing {EmbeddingCount} embeddings with {Dimension} dimensions",
                embeddings.Length, _vectorDimension);

            // Always store in in-memory cache first (fast access)
            foreach (var embedding in embeddings)
            {
                _embeddingStore[embedding.Id] = embedding;

                var cacheKey = $"embedding_{embedding.ChunkId}";
                _cache.Set(cacheKey, embedding, TimeSpan.FromHours(24));

                _logger.LogDebug("Cached embedding {EmbeddingId} for chunk {ChunkId}",
                    embedding.Id, embedding.ChunkId);
            }

            // Try to store in Qdrant if available
            if (_qdrantAvailable || await EnsureQdrantInitializedAsync())
            {
                try
                {
                    await StoreInQdrantAsync(embeddings);
                    _logger.LogInformation("? Stored {Count} embeddings in Qdrant (persistent)", embeddings.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "?? Failed to store in Qdrant, kept in memory cache");
                    _qdrantAvailable = false; // Mark as unavailable for future attempts
                }
            }
            else
            {
                _logger.LogInformation("?? Stored {Count} embeddings in memory (Qdrant unavailable)", embeddings.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store embeddings");
            throw;
        }
    }

    private async Task StoreInQdrantAsync(VectorEmbedding[] embeddings)
    {
        _logger.LogDebug("Inserting {Count} embeddings into Qdrant", embeddings.Length);

        try
        {
            var points = embeddings
                .Where(e => e.Vector != null && e.Vector.Length > 0 && !string.IsNullOrWhiteSpace(e.ChunkId.ToString()))  // ? Skip invalid embeddings
                .Select(e =>
                {
                    // ? PRIORITIZE embedding.DocumentId over chunk mapping
                    var documentId = e.DocumentId ?? 
                                   (_chunkToDocumentMap.TryGetValue(e.ChunkId, out var docId) ? docId : Guid.Empty);

                    // ?? Log warning if still Guid.Empty
                    if (documentId == Guid.Empty)
                    {
                        _logger.LogWarning("DocumentId is Guid.Empty for ChunkId={ChunkId}, EmbeddingId={EmbeddingId}", 
                            e.ChunkId, e.Id);
                    }

                    var content = _chunkContentMap.TryGetValue(e.ChunkId, out var chunkContent)
                        ? chunkContent
                        : "";

                    return new PointStruct
                    {
                        Id = new PointId { Uuid = e.Id.ToString() },
                        Vectors = e.Vector,
                        Payload =
                        {
                            ["chunk_id"] = e.ChunkId.ToString(),
                            ["document_id"] = documentId.ToString(),
                            ["content"] = content.Length > 1000 ? content.Substring(0, 1000) : content,
                            ["model"] = e.Model,
                            ["created_at"] = e.CreatedAt.Ticks,
                            ["has_document_id"] = documentId != Guid.Empty  // ? Track if document ID is valid
                        }
                    };
                }).ToList();

            if (points.Count == 0)
            {
                _logger.LogWarning("No valid embeddings to store in Qdrant (all were filtered out)");
                return;
            }

            // ? Log how many have valid vs empty document IDs
            var validDocIds = points.Count(p => (bool)p.Payload["has_document_id"].BoolValue);
            var emptyDocIds = points.Count - validDocIds;
            
            if (emptyDocIds > 0)
            {
                _logger.LogWarning("Storing {EmptyCount} embeddings with Guid.Empty documentId out of {TotalCount} total", 
                    emptyDocIds, points.Count);
            }

            await _qdrantClient.UpsertAsync(
                collectionName: _collectionName,
                points: points
            );

            _logger.LogInformation("Successfully upserted {Count} embeddings into Qdrant ({ValidDocIds} with valid documentId, {EmptyDocIds} with Guid.Empty)", 
                points.Count, validDocIds, emptyDocIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing embeddings in Qdrant");
            throw;
        }
    }

    public async Task<SearchResult[]> SearchSimilarAsync(
        float[] queryVector,
        int maxResults = 10,
        float minScore = 0.7f)
    {
        try
        {
            _logger.LogDebug("Performing vector similarity search with {VectorDim} dimensions",
                queryVector.Length);

            // Try Qdrant first if available
            if (_qdrantAvailable)
            {
                try
                {
                    var qdrantResults = await SearchInQdrantAsync(queryVector, maxResults, minScore);
                    if (qdrantResults != null && qdrantResults.Length > 0)
                    {
                        _logger.LogInformation("? Found {Count} results from Qdrant", qdrantResults.Length);
                        return qdrantResults;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Qdrant search failed, falling back to in-memory");
                    _qdrantAvailable = false;
                }
            }

            // Fallback to in-memory search
            return await SearchInMemoryAsync(queryVector, maxResults, minScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform vector similarity search");
            throw;
        }
    }

    private async Task<SearchResult[]?> SearchInQdrantAsync(
        float[] queryVector,
        int maxResults,
        float minScore)
    {
        _logger.LogInformation("=== Starting Qdrant Search ===");
        _logger.LogDebug("Searching Qdrant for similar vectors with {Dimensions} dimensions", queryVector.Length);

        try
        {
            var searchResults = await _qdrantClient.SearchAsync(
                collectionName: _collectionName,
                vector: queryVector,
                limit: (ulong)maxResults,
                scoreThreshold: minScore,
                payloadSelector: true
            ).ConfigureAwait(false);

            _logger.LogInformation("?? Qdrant returned {Count} raw results", searchResults.Count);

            if (!searchResults.Any())
            {
                _logger.LogInformation("No results found in Qdrant matching criteria");
                return Array.Empty<SearchResult>();
            }

            // Process all results in parallel for better performance
            var processingStartTime = DateTime.UtcNow;
            _logger.LogInformation("Starting parallel processing of {Count} results...", searchResults.Count);

            var resultTasks = searchResults.Select(async (result, index) =>
            {
                try
                {
                    var taskStartTime = DateTime.UtcNow;
                    _logger.LogDebug("[Task {Index}] Starting processing...", index);

                    var payload = result.Payload;
                    
                    if (!payload.ContainsKey("chunk_id") || !payload.ContainsKey("document_id"))
                    {
                        _logger.LogWarning("[Task {Index}] Search result missing required payload fields", index);
                        return null;
                    }

                    var chunkId = Guid.Parse(payload["chunk_id"].StringValue);
                    var documentId = Guid.Parse(payload["document_id"].StringValue);
                    var content = payload.ContainsKey("content") ? payload["content"].StringValue : "";
                    var model = payload.ContainsKey("model") ? payload["model"].StringValue : "unknown";

                    _logger.LogDebug("[Task {Index}] Parsed: ChunkId={ChunkId}, DocumentId={DocumentId}, Score={Score}", 
                        index, chunkId, documentId, result.Score);

                    // Fetch metadata (this is the slow part - now done in parallel)
                    _logger.LogDebug("[Task {Index}] Fetching metadata for DocumentId={DocumentId}...", index, documentId);
                    var metadataStartTime = DateTime.UtcNow;
                    
                    var metadata = await FetchDocumentMetadataAsync(documentId).ConfigureAwait(false);
                    
                    var metadataElapsed = (DateTime.UtcNow - metadataStartTime).TotalMilliseconds;
                    _logger.LogDebug("[Task {Index}] Metadata fetched in {ElapsedMs}ms", index, metadataElapsed);

                    var searchResult = new SearchResult(
                        DocumentId: documentId,
                        ChunkId: chunkId,
                        Content: content,
                        Score: result.Score,
                        Metadata: metadata,
                        Highlights: new Dictionary<string, object>
                        {
                            ["similarity"] = result.Score,
                            ["model"] = model,
                            ["match_type"] = "qdrant_vector_search",
                            ["vector_dimension"] = queryVector.Length,
                            ["storage"] = "qdrant"
                        }
                    );

                    var taskElapsed = (DateTime.UtcNow - taskStartTime).TotalMilliseconds;
                    _logger.LogDebug("[Task {Index}] Completed in {ElapsedMs}ms", index, taskElapsed);

                    return searchResult;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Task {Index}] Failed to parse individual search result", index);
                    return null;
                }
            }).ToList();

            _logger.LogInformation("Waiting for all {Count} parallel tasks to complete...", resultTasks.Count);

            // Wait for ALL metadata fetching to complete
            var results = await Task.WhenAll(resultTasks).ConfigureAwait(false);

            var processingElapsed = (DateTime.UtcNow - processingStartTime).TotalMilliseconds;
            _logger.LogInformation("All parallel tasks completed in {ElapsedMs}ms", processingElapsed);

            // Filter out null results (failed parsing)
            var validResults = results.Where(r => r != null).Select(r => r!).ToArray();

            _logger.LogInformation("?? Qdrant search returned {ValidCount} valid results (out of {TotalCount} raw results)", 
                validResults.Length, searchResults.Count);

            _logger.LogInformation("=== Qdrant Search Complete ===");

            return validResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "??? Error searching Qdrant");
            return null;
        }
    }

    private async Task<SearchResult[]> SearchInMemoryAsync(
        float[] queryVector,
        int maxResults,
        float minScore)
    {
        _logger.LogDebug("Searching in-memory store for similar vectors");

        if (!_embeddingStore.Any())
        {
            _logger.LogWarning("No embeddings in memory store");
            return Array.Empty<SearchResult>();
        }

        // Validate dimension
        var firstEmbedding = _embeddingStore.Values.FirstOrDefault();
        if (firstEmbedding != null && firstEmbedding.Vector.Length != queryVector.Length)
        {
            throw new ArgumentException(
                $"Query vector dimension ({queryVector.Length}) does not match " +
                $"stored embeddings dimension ({firstEmbedding.Vector.Length})");
        }

        // Perform cosine similarity search
        var results = new List<(VectorEmbedding Embedding, float Score)>();

        foreach (var embedding in _embeddingStore.Values)
        {
            if (embedding.Vector.Length != queryVector.Length)
                continue;

            var similarity = CalculateCosineSimilarity(queryVector, embedding.Vector);

            if (similarity >= minScore)
            {
                results.Add((embedding, similarity));
            }
        }

        // Sort and limit results
        results = results.OrderByDescending(r => r.Score).Take(maxResults).ToList();

        // Convert to SearchResult[]
        var searchResults = new List<SearchResult>();
        foreach (var (embedding, score) in results)
        {
            var content = _chunkContentMap.TryGetValue(embedding.ChunkId, out var chunkContent)
                ? chunkContent
                : await GetChunkContentAsync(embedding.ChunkId);

            var documentId = _chunkToDocumentMap.TryGetValue(embedding.ChunkId, out var docId)
                ? docId
                : embedding.ChunkId;

            var metadata = await FetchDocumentMetadataAsync(documentId);

            searchResults.Add(new SearchResult(
                DocumentId: documentId,
                ChunkId: embedding.ChunkId,
                Content: content ?? $"Content for chunk {embedding.ChunkId}",
                Score: score,
                Metadata: metadata,
                Highlights: new Dictionary<string, object>
                {
                    ["similarity"] = score,
                    ["model"] = embedding.Model,
                    ["match_type"] = "cosine_similarity",
                    ["embedding_id"] = embedding.Id,
                    ["vector_dimension"] = embedding.Vector.Length,
                    ["storage"] = "in-memory"
                }
            ));
        }

        _logger.LogInformation("?? Found {Count} results from in-memory store", searchResults.Count);
        return searchResults.ToArray();
    }

    public async Task<SearchResult[]> SearchAsync(SearchRequest request)
    {
        try
        {
            _logger.LogInformation("Performing semantic search with query: {Query}", request.Query);

            var queryEmbedding = await GenerateQueryEmbeddingAsync(request.Query);

            if (queryEmbedding == null || queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Failed to generate query embedding");
                return Array.Empty<SearchResult>();
            }

            _logger.LogDebug("Generated query embedding with {Dimension} dimensions", queryEmbedding.Length);

            return await SearchSimilarAsync(queryEmbedding, request.MaxResults, request.MinScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform semantic search");
            throw;
        }
    }

    public async Task<bool> DeleteDocumentEmbeddingsAsync(Guid documentId)
    {
        try
        {
            _logger.LogInformation("Deleting embeddings for document {DocumentId}", documentId);

            // Delete from in-memory store
            var chunkIdsToRemove = _chunkToDocumentMap
                .Where(kvp => kvp.Value == documentId)
                .Select(kvp => kvp.Key)
                .ToList();

            var removedCount = 0;
            foreach (var chunkId in chunkIdsToRemove)
            {
                var embeddingToRemove = _embeddingStore.Values
                    .FirstOrDefault(e => e.ChunkId == chunkId);

                if (embeddingToRemove != null)
                {
                    _embeddingStore.TryRemove(embeddingToRemove.Id, out _);
                    _chunkToDocumentMap.TryRemove(chunkId, out _);
                    _chunkContentMap.TryRemove(chunkId, out _);

                    var cacheKey = $"embedding_{chunkId}";
                    _cache.Remove(cacheKey);

                    removedCount++;
                }
            }

            // Try to delete from Qdrant if available
            if (_qdrantAvailable)
            {
                try
                {
                    await DeleteFromQdrantAsync(documentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete from Qdrant");
                }
            }

            _logger.LogInformation("Deleted {Count} embeddings for document {DocumentId}",
                removedCount, documentId);

            return removedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting embeddings for document {DocumentId}", documentId);
            return false;
        }
    }

    private async Task DeleteFromQdrantAsync(Guid documentId)
    {
        _logger.LogDebug("Deleting embeddings for document {DocumentId} from Qdrant", documentId);

        try
        {
            await _qdrantClient.DeleteAsync(
                collectionName: _collectionName,
                filter: new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "document_id",
                                Match = new Match { Keyword = documentId.ToString() }
                            }
                        }
                    }
                }
            );

            _logger.LogInformation("Successfully deleted embeddings for document {DocumentId} from Qdrant", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete from Qdrant");
        }
    }

    public void MapChunkToDocument(Guid chunkId, Guid documentId, string? content = null)
    {
        _chunkToDocumentMap[chunkId] = documentId;

        if (!string.IsNullOrEmpty(content))
        {
            _chunkContentMap[chunkId] = content;
        }
    }

    public async Task<int> ClearAllEmbeddingsAsync()
    {
        await Task.CompletedTask;

        lock (_dimensionLock)
        {
            var count = _embeddingStore.Count;

            _embeddingStore.Clear();
            _chunkToDocumentMap.Clear();
            _chunkContentMap.Clear();

            _vectorDimension = 768;

            _logger.LogWarning("Cleared {Count} embeddings from vector store", count);

            // Try to clear Qdrant collection
            if (_qdrantAvailable)
            {
                try
                {
                    _ = Task.Run(async () =>
                    {
                        await _qdrantClient.DeleteCollectionAsync(_collectionName);
                        await CreateCollectionAsync();
                        _logger.LogInformation("Cleared Qdrant collection '{Collection}'", _collectionName);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clear Qdrant collection");
                }
            }

            return count;
        }
    }

    public async Task<object> GetStatisticsAsync()
    {
        await Task.CompletedTask;

        var actualDimensions = _embeddingStore.Values
            .Select(e => e.Vector.Length)
            .Distinct()
            .ToList();

        return new
        {
            TotalEmbeddings = _embeddingStore.Count,
            TotalDocuments = _chunkToDocumentMap.Values.Distinct().Count(),
            TotalChunks = _chunkToDocumentMap.Count,
            VectorDimension = _vectorDimension,
            ActualDimensions = actualDimensions,
            CollectionName = _collectionName,
            QdrantAvailable = _qdrantAvailable,
            QdrantInitialized = _qdrantInitialized,
            StorageType = _qdrantAvailable ? "Qdrant (persistent) + In-Memory (cache)" : "In-Memory Only",
            Note = actualDimensions.Count > 1
                ? "WARNING: Multiple embedding dimensions detected!"
                : _qdrantAvailable
                    ? "Using Qdrant for persistent storage"
                    : "Qdrant unavailable - using in-memory fallback",
            EmbeddingModels = _embeddingStore.Values
                .Select(e => e.Model)
                .Distinct()
                .ToList()
        };
    }

    #region Private Helper Methods

    private float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
        {
            throw new ArgumentException(
                $"Vectors must have the same dimension. " +
                $"Vector A: {vectorA.Length}, Vector B: {vectorB.Length}");
        }

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0f || magnitudeB == 0f)
        {
            return 0f;
        }

        return dotProduct / (magnitudeA * magnitudeB);
    }

    private async Task<float[]?> GenerateQueryEmbeddingAsync(string query)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var embeddingServiceUrl = _configuration["Services:EmbeddingService"] ?? "http://localhost:5002";

            var response = await httpClient.PostAsJsonAsync(
                $"{embeddingServiceUrl}/api/embeddings/generate-single",
                query
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to generate query embedding: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                return null;
            }

            var embedding = await response.Content.ReadFromJsonAsync<VectorEmbedding>();

            if (embedding != null)
            {
                _logger.LogDebug("Generated query embedding: {Dimensions} dimensions, model: {Model}",
                    embedding.Vector.Length, embedding.Model);
            }

            return embedding?.Vector;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating query embedding");
            return null;
        }
    }

    private async Task<string?> GetChunkContentAsync(Guid chunkId)
    {
        if (_chunkContentMap.TryGetValue(chunkId, out var content))
        {
            return content;
        }

        var cacheKey = $"chunk_content_{chunkId}";
        if (_cache.TryGetValue(cacheKey, out string? cachedContent))
        {
            return cachedContent;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var parserServiceUrl = _configuration["Services:DocumentParser"] ?? "http://localhost:5001";

            var response = await httpClient.GetAsync($"{parserServiceUrl}/api/parsing/chunks/{chunkId}");

            if (response.IsSuccessStatusCode)
            {
                var chunk = await response.Content.ReadFromJsonAsync<ContractChunk>();
                return chunk?.Content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not fetch chunk content from service");
        }

        return null;
    }

    private async Task<ContractMetadata> FetchDocumentMetadataAsync(Guid documentId)
    {
        var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);  // ? Unique request ID
        
        try
        {
            _logger.LogInformation("[Request {RequestId}] ?? Starting metadata fetch for DocumentId={DocumentId}", 
                requestId, documentId);
            
            var httpClient = _httpClientFactory.CreateClient();
            
            // ? Removed manual timeout - Polly handles it globally (100s total, 30s per attempt)
            
            var documentServiceUrl = _configuration["Services:DocumentUpload"] ?? "http://localhost:5000";
            var url = $"{documentServiceUrl}/api/documents/{documentId}";

            _logger.LogDebug("[Request {RequestId}] Fetching metadata from: {Url}", requestId, url);
            
            var requestStartTime = DateTime.UtcNow;
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            var requestElapsed = (DateTime.UtcNow - requestStartTime).TotalMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                var document = await response.Content.ReadFromJsonAsync<ContractDocument>().ConfigureAwait(false);
                
                _logger.LogInformation("[Request {RequestId}] ? Metadata fetched in {ElapsedMs}ms for DocumentId={DocumentId}", 
                    requestId, requestElapsed, documentId);
                
                return document?.Metadata ?? CreateDefaultMetadata();
            }
            else
            {
                _logger.LogWarning("[Request {RequestId}] ?? Failed to fetch metadata: {StatusCode} (took {ElapsedMs}ms) for DocumentId={DocumentId}", 
                    requestId, response.StatusCode, requestElapsed, documentId);
                return CreateDefaultMetadata();
            }
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "[Request {RequestId}] ?? Timeout fetching metadata for DocumentId={DocumentId} (exceeded Polly timeout)", 
                requestId, documentId);
            return CreateDefaultMetadata();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Request {RequestId}] ? Error fetching metadata for DocumentId={DocumentId}", 
                requestId, documentId);
            return CreateDefaultMetadata();
        }
    }

    private ContractMetadata CreateDefaultMetadata()
    {
        return new ContractMetadata(
            Title: "Unknown Document",
            ContractDate: null,
            ExpirationDate: null,
            ContractValue: null,
            Currency: null,
            Parties: new List<string>(),
            KeyTerms: new List<string>(),
            ContractType: null,
            CustomFields: new Dictionary<string, object>()
        );
    }

    #endregion
}