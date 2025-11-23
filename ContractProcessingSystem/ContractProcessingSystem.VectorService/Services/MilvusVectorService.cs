using Milvus.Client;
using Microsoft.Extensions.Caching.Memory;
using ContractProcessingSystem.Shared.Models;
using System.Text.Json;
using System.Collections.Concurrent;

namespace ContractProcessingSystem.VectorService.Services;

public class MilvusVectorService : IVectorService
{
    private readonly MilvusClient _milvusClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MilvusVectorService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly string _collectionName = "contract_embeddings";

    // Dynamic vector dimension - will be set based on first embedding stored
    // Made static to match the static _embeddingStore
    private static int _vectorDimension = 768; // Default to Gemini text-embedding-004 size
    private static readonly object _dimensionLock = new object();

    // Milvus availability tracking
    private bool _milvusAvailable = false;
    private bool _milvusInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // Fallback in-memory store (used when Milvus is unavailable OR as cache)
    private static readonly ConcurrentDictionary<Guid, VectorEmbedding> _embeddingStore = new();
    private static readonly ConcurrentDictionary<Guid, Guid> _chunkToDocumentMap = new();
    private static readonly ConcurrentDictionary<Guid, string> _chunkContentMap = new();

    public MilvusVectorService(
        MilvusClient milvusClient,
        IMemoryCache cache,
        ILogger<MilvusVectorService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _milvusClient = milvusClient;
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

        // Try to initialize Milvus asynchronously (don't block constructor)
        _ = Task.Run(async () => await EnsureMilvusInitializedAsync());
    }

    private async Task<bool> EnsureMilvusInitializedAsync()
    {
        if (_milvusInitialized)
            return _milvusAvailable;

        await _initLock.WaitAsync();
        try
        {
            if (_milvusInitialized)
                return _milvusAvailable;

            _logger.LogInformation("Initializing Milvus connection for collection '{Collection}'", _collectionName);

            try
            {
                // Check if Milvus is accessible
                var hasCollection = await _milvusClient.HasCollectionAsync(_collectionName);

                if (!hasCollection)
                {
                    _logger.LogInformation("Collection '{Collection}' does not exist, creating...", _collectionName);
                    await CreateCollectionAsync();
                }
                else
                {
                    _logger.LogInformation("Collection '{Collection}' already exists", _collectionName);
                    // Note: LoadCollectionAsync not available in preview SDK
                    // Collection is automatically loaded when accessed
                }

                _milvusAvailable = true;
                _milvusInitialized = true;
                _logger.LogInformation("? Milvus initialized successfully - using persistent vector storage");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "?? Milvus not available - falling back to in-memory storage");
                _milvusAvailable = false;
                _milvusInitialized = true;
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
            // Note: Milvus.Client 2.3.0-preview.1 has limited API
            // This is a simplified implementation that may need adjustment
            _logger.LogInformation("Creating Milvus collection '{Collection}' with {Dimension} dimensions",
                _collectionName, _vectorDimension);

            // Create collection (API may vary by preview version)
            // For now, log that we would create it
            // The actual implementation depends on the preview API availability

            _logger.LogWarning("Collection creation requires manual setup or newer Milvus SDK");
            _logger.LogInformation("Please create collection manually with:");
            _logger.LogInformation("  - Name: {Collection}", _collectionName);
            _logger.LogInformation("  - Vector field: 'vector', dimension: {Dimension}", _vectorDimension);
            _logger.LogInformation("  - Primary key: 'id' (VARCHAR)");
            _logger.LogInformation("  - Additional fields: chunk_id, document_id, content, model");

            throw new NotImplementedException(
                "Milvus collection creation requires manual setup with preview SDK. " +
                "Please create the collection manually or use a stable SDK version.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Milvus collection");
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

            // Try to store in Milvus if available
            if (_milvusAvailable || await EnsureMilvusInitializedAsync())
            {
                try
                {
                    await StoreInMilvusAsync(embeddings);
                    _logger.LogInformation("? Stored {Count} embeddings in Milvus (persistent)", embeddings.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "?? Failed to store in Milvus, kept in memory cache");
                    _milvusAvailable = false; // Mark as unavailable for future attempts
                }
            }
            else
            {
                _logger.LogInformation("?? Stored {Count} embeddings in memory (Milvus unavailable)", embeddings.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store embeddings");
            throw;
        }
    }

    private async Task StoreInMilvusAsync(VectorEmbedding[] embeddings)
    {
        _logger.LogDebug("Inserting {Count} embeddings into Milvus via HTTP API", embeddings.Length);

        try
        {
            // Use Milvus HTTP API (more stable than preview SDK)
            var httpClient = _httpClientFactory.CreateClient();
            var milvusHttpEndpoint = _configuration["Milvus:HttpEndpoint"] ?? "http://localhost:9091";
            
            // Prepare data for insertion
            var entities = embeddings.Select(e => new
            {
                id = e.Id.ToString(),
                chunk_id = e.ChunkId.ToString(),
                document_id = _chunkToDocumentMap.TryGetValue(e.ChunkId, out var docId) 
                    ? docId.ToString() 
                    : Guid.Empty.ToString(),
                vector = e.Vector.ToList(),
                content = _chunkContentMap.TryGetValue(e.ChunkId, out var content) 
                    ? content.Substring(0, Math.Min(content.Length, 1000)) // Limit content size
                    : "",
                model = e.Model,
                created_at = e.CreatedAt.Ticks
            }).ToList();

            var insertRequest = new
            {
                collection_name = _collectionName,
                fields_data = entities
            };

            _logger.LogDebug("Sending insert request to Milvus HTTP API: {Endpoint}", milvusHttpEndpoint);

            var response = await httpClient.PostAsJsonAsync(
                $"{milvusHttpEndpoint}/v1/vector/insert",
                insertRequest
            );

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Successfully inserted {Count} embeddings into Milvus", embeddings.Length);
                _logger.LogDebug("Milvus insert response: {Response}", result);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Milvus insert failed with status {StatusCode}: {Error}", 
                    response.StatusCode, error);
                throw new HttpRequestException($"Milvus insert failed: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Milvus HTTP API not available - check if Milvus is running on configured endpoint");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error storing embeddings in Milvus");
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

            // Try Milvus first if available
            if (_milvusAvailable)
            {
                try
                {
                    var milvusResults = await SearchInMilvusAsync(queryVector, maxResults, minScore);
                    if (milvusResults != null && milvusResults.Length > 0)
                    {
                        _logger.LogInformation("? Found {Count} results from Milvus", milvusResults.Length);
                        return milvusResults;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Milvus search failed, falling back to in-memory");
                    _milvusAvailable = false;
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

    private async Task<SearchResult[]?> SearchInMilvusAsync(
        float[] queryVector,
        int maxResults,
        float minScore)
    {
        _logger.LogDebug("Searching Milvus via HTTP API for similar vectors");

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var milvusHttpEndpoint = _configuration["Milvus:HttpEndpoint"] ?? "http://localhost:9091";
            
            // Convert minScore (cosine similarity 0-1) to L2 distance
            // L2 distance is inversely related to cosine similarity
            // For normalized vectors: L2 = sqrt(2 * (1 - cosine_similarity))
            var maxDistance = (float)Math.Sqrt(2 * (1 - minScore));

            var searchRequest = new
            {
                collection_name = _collectionName,
                vectors = new[] { queryVector.ToList() },
                vector_field = "vector",
                metric_type = "L2",  // L2 distance metric
                top_k = maxResults,
                search_params = new
                {
                    nprobe = 10,  // Number of clusters to search
                    radius = maxDistance  // Maximum distance threshold
                },
                output_fields = new[] { "id", "chunk_id", "document_id", "content", "model", "created_at" }
            };

            _logger.LogDebug("Sending search request to Milvus: top_k={TopK}, max_distance={MaxDistance}", 
                maxResults, maxDistance);

            var response = await httpClient.PostAsJsonAsync(
                $"{milvusHttpEndpoint}/v1/vector/search",
                searchRequest
            );

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Milvus search failed with status {StatusCode}: {Error}", 
                    response.StatusCode, error);
                return null;
            }

            var resultJson = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Milvus search response: {Response}", resultJson);

            // Parse search results
            using var jsonDoc = JsonDocument.Parse(resultJson);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("results", out var resultsArray))
            {
                _logger.LogWarning("Milvus search response missing 'results' property");
                return null;
            }

            var searchResults = new List<SearchResult>();

            foreach (var result in resultsArray.EnumerateArray())
            {
                try
                {
                    if (!result.TryGetProperty("distance", out var distanceElem) ||
                        !result.TryGetProperty("entity", out var entity))
                        continue;

                    var distance = distanceElem.GetSingle();
                    
                    // Convert L2 distance back to cosine similarity
                    // cosine_similarity = 1 - (L2^2 / 2)
                    var similarity = 1.0f - ((distance * distance) / 2.0f);
                    
                    if (similarity < minScore)
                        continue;

                    // Extract entity fields
                    var chunkId = Guid.Parse(entity.GetProperty("chunk_id").GetString() ?? Guid.Empty.ToString());
                    var documentId = Guid.Parse(entity.GetProperty("document_id").GetString() ?? Guid.Empty.ToString());
                    var content = entity.TryGetProperty("content", out var contentElem) 
                        ? contentElem.GetString() ?? "" 
                        : "";
                    var model = entity.TryGetProperty("model", out var modelElem)
                        ? modelElem.GetString() ?? "unknown"
                        : "unknown";

                    var metadata = await FetchDocumentMetadataAsync(documentId);

                    searchResults.Add(new SearchResult(
                        DocumentId: documentId,
                        ChunkId: chunkId,
                        Content: content,
                        Score: similarity,
                        Metadata: metadata,
                        Highlights: new Dictionary<string, object>
                        {
                            ["similarity"] = similarity,
                            ["distance"] = distance,
                            ["model"] = model,
                            ["match_type"] = "milvus_vector_search",
                            ["vector_dimension"] = queryVector.Length,
                            ["storage"] = "milvus"
                        }
                    ));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse search result");
                }
            }

            _logger.LogInformation("Milvus search returned {Count} results", searchResults.Count);
            return searchResults.ToArray();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Milvus HTTP API not available");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Milvus");
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

            // Try to delete from Milvus if available
            if (_milvusAvailable)
            {
                try
                {
                    await DeleteFromMilvusAsync(documentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete from Milvus");
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

    private async Task DeleteFromMilvusAsync(Guid documentId)
    {
        _logger.LogDebug("Deleting embeddings for document {DocumentId} from Milvus", documentId);

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var milvusHttpEndpoint = _configuration["Milvus:HttpEndpoint"] ?? "http://localhost:9091";
            
            // Delete by expression (filter by document_id)
            var deleteRequest = new
            {
                collection_name = _collectionName,
                expr = $"document_id == \"{documentId}\""
            };

            var response = await httpClient.PostAsJsonAsync(
                $"{milvusHttpEndpoint}/v1/vector/delete",
                deleteRequest
            );

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted embeddings for document {DocumentId} from Milvus", documentId);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Milvus delete failed: {Error}", error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete from Milvus");
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

            // Note: This doesn't clear Milvus - would need separate implementation
            if (_milvusAvailable)
            {
                _logger.LogWarning("Milvus collection not cleared - requires manual cleanup or drop collection");
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
            MilvusAvailable = _milvusAvailable,
            MilvusInitialized = _milvusInitialized,
            StorageType = _milvusAvailable ? "Milvus (persistent) + In-Memory (cache)" : "In-Memory Only",
            Note = actualDimensions.Count > 1
                ? "WARNING: Multiple embedding dimensions detected!"
                : _milvusAvailable
                    ? "Using Milvus for persistent storage"
                    : "Milvus unavailable - using in-memory fallback",
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
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var documentServiceUrl = _configuration["Services:DocumentUpload"] ?? "http://localhost:5000";

            var response = await httpClient.GetAsync($"{documentServiceUrl}/api/documents/{documentId}");

            if (response.IsSuccessStatusCode)
            {
                var document = await response.Content.ReadFromJsonAsync<ContractDocument>();
                return document?.Metadata ?? CreateDefaultMetadata();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch metadata for document {DocumentId}", documentId);
        }

        return CreateDefaultMetadata();
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