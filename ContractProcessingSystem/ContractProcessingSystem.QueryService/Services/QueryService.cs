using ContractProcessingSystem.Shared.AI;
using System.Text.Json;

namespace ContractProcessingSystem.QueryService.Services;

public class QueryService : IQueryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILLMProviderFactory _llmProviderFactory;
    private readonly ILogger<QueryService> _logger;
    private readonly IConfiguration _configuration;

    public QueryService(
        IHttpClientFactory httpClientFactory,
        ILLMProviderFactory llmProviderFactory,
        ILogger<QueryService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _llmProviderFactory = llmProviderFactory;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<SearchResult[]> SemanticSearchAsync(SearchRequest request)
    {
        try
        {
            _logger.LogInformation("Performing semantic search for query: {Query}", request.Query);

            // Step 1: Generate embedding for the search query
            var queryEmbedding = await GenerateQueryEmbeddingAsync(request.Query);

            // Step 2: Search vector database using the embedding
            var vectorResults = await SearchVectorDatabaseAsync(queryEmbedding, request);

            // Step 3: Enhance results with AI-powered ranking and context
            var enhancedResults = await EnhanceSearchResultsAsync(vectorResults, request.Query);

            _logger.LogInformation("Semantic search completed. Found {ResultCount} results for query: {Query}", 
                enhancedResults.Length, request.Query);

            return enhancedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform semantic search for query: {Query}", request.Query);
            throw;
        }
    }

    public async Task<SummarizationResult> SummarizeAsync(SummarizationRequest request)
    {
        try
        {
            _logger.LogInformation("Starting summarization for {DocumentCount} documents with type: {Type}", 
                request.DocumentIds.Count, request.Type);

            // Step 1: Retrieve document content
            var documentContents = await RetrieveDocumentContentsAsync(request.DocumentIds);

            // Step 2: Generate summary using AI
            var summary = await GenerateSummaryWithAIAsync(documentContents, request);

            _logger.LogInformation("Summarization completed for {DocumentCount} documents", request.DocumentIds.Count);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize documents");
            throw;
        }
    }

    public async Task<SearchResult[]> HybridSearchAsync(string query, Dictionary<string, object>? filters = null)
    {
        try
        {
            _logger.LogInformation("Performing hybrid search for query: {Query}", query);

            // Combine semantic search with keyword-based filtering
            var semanticRequest = new SearchRequest(query, 20, 0.6f, null, filters);
            var semanticResults = await SemanticSearchAsync(semanticRequest);

            // Apply additional keyword filtering and ranking
            var hybridResults = await ApplyHybridRankingAsync(semanticResults, query);

            _logger.LogInformation("Hybrid search completed. Found {ResultCount} results", hybridResults.Length);

            return hybridResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform hybrid search for query: {Query}", query);
            throw;
        }
    }

    private async Task<float[]> GenerateQueryEmbeddingAsync(string query)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var embeddingServiceUrl = GetServiceUrl("EmbeddingService");
            
            _logger.LogDebug("Calling EmbeddingService at {Url}", embeddingServiceUrl);
            
            var requestBody = JsonSerializer.Serialize(query);
            var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync($"{embeddingServiceUrl}/api/embeddings/generate-single", content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Use case-insensitive deserialization
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var embedding = JsonSerializer.Deserialize<VectorEmbedding>(responseContent, jsonOptions);
            
            return embedding?.Vector ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate query embedding");
            
            // Fallback: return a mock embedding for demonstration
            return GenerateMockEmbedding();
        }
    }

    private async Task<SearchResult[]> SearchVectorDatabaseAsync(float[] queryEmbedding, SearchRequest request)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var vectorServiceUrl = GetServiceUrl("VectorService");
            
            _logger.LogDebug("Calling VectorService at {Url}", vectorServiceUrl);
            
            var vectorRequest = new
            {
                QueryVector = queryEmbedding,
                MaxResults = request.MaxResults,
                MinScore = request.MinScore
            };
            
            var requestBody = JsonSerializer.Serialize(vectorRequest);
            var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync($"{vectorServiceUrl}/api/vector/search/similar", content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // Use case-insensitive deserialization
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var results = JsonSerializer.Deserialize<SearchResult[]>(responseContent, jsonOptions);
            
            return results ?? Array.Empty<SearchResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search vector database");
            
            // Return empty results on failure
            return Array.Empty<SearchResult>();
        }
    }

    private async Task<SearchResult[]> EnhanceSearchResultsAsync(SearchResult[] vectorResults, string query)
    {
        try
        {
            // Get the default LLM provider (Gemini)
            var llmProvider = _llmProviderFactory.GetDefaultProvider();
            
            if (!llmProvider.IsAvailable || !llmProvider.SupportsChat)
            {
                _logger.LogWarning("LLM provider not available for enhancement, returning original results");
                return vectorResults;
            }

            // Use AI to re-rank and enhance search results based on the query context
            var enhancementPrompt = $@"
You are an expert contract analysis assistant. Given the search query and the contract search results below, 
please analyze and provide enhanced relevance insights.

Search Query: {query}

Analyze the relevance of each result and provide insights about why each result matches the query.
Focus on contract-specific relevance like legal terms, clauses, obligations, and business impact.

Return a JSON array with enhanced insights for each result.";

            var options = new LLMGenerationOptions
            {
                Temperature = 0.3f,
                MaxTokens = 1000
            };

            var response = await llmProvider.GenerateTextAsync(enhancementPrompt, options);

            // For now, return the original results
            // In a real implementation, you would parse the AI response and enhance the results
            return vectorResults;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enhance search results, returning original results");
            return vectorResults;
        }
    }

    private async Task<List<string>> RetrieveDocumentContentsAsync(List<Guid> documentIds)
    {
        var contents = new List<string>();
        
        // Configure JSON options for case-insensitive deserialization
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var documentServiceUrl = GetServiceUrl("DocumentUpload");
            
            _logger.LogDebug("Calling DocumentUpload service at {Url}", documentServiceUrl);
            
            foreach (var documentId in documentIds)
            {
                try
                {
                    // First, get document metadata
                    var metadataResponse = await httpClient.GetAsync($"{documentServiceUrl}/api/documents/{documentId}");
                    if (!metadataResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to retrieve metadata for document {DocumentId}: {StatusCode}", 
                            documentId, metadataResponse.StatusCode);
                        contents.Add($"Document {documentId}: [Metadata unavailable]");
                        continue;
                    }

                    var metadataContent = await metadataResponse.Content.ReadAsStringAsync();
                    
                    // Log the raw JSON for debugging
                    _logger.LogDebug("Raw metadata JSON for {DocumentId}: {Json}", 
                        documentId, 
                        metadataContent.Length > 200 ? metadataContent.Substring(0, 200) + "..." : metadataContent);
                    
                    var document = JsonSerializer.Deserialize<ContractDocument>(metadataContent, jsonOptions);
                    
                    if (document == null)
                    {
                        _logger.LogWarning("Failed to deserialize document metadata for {DocumentId}. Raw JSON: {Json}", 
                            documentId, metadataContent);
                        contents.Add($"Document {documentId}: [Invalid metadata]");
                        continue;
                    }

                    _logger.LogDebug("Successfully deserialized document: {FileName}, Status: {Status}", 
                        document.FileName, document.Status);

                    // For now, we'll use the DocumentParser service to get the parsed chunks
                    // In a real implementation, you could call /content endpoint and parse the binary content
                    var parserServiceUrl = GetServiceUrl("DocumentParser");
                    var chunksResponse = await httpClient.GetAsync($"{parserServiceUrl}/api/parsing/{documentId}/chunks");
                    
                    if (chunksResponse.IsSuccessStatusCode)
                    {
                        var chunksContent = await chunksResponse.Content.ReadAsStringAsync();
                        var chunks = JsonSerializer.Deserialize<List<ContractChunk>>(chunksContent, jsonOptions);
                        
                        if (chunks != null && chunks.Any())
                        {
                            // Combine chunk contents
                            var fullContent = string.Join("\n\n", chunks.Select(c => c.Content));
                            contents.Add($"Document {document.FileName}:\n{fullContent}");
                            _logger.LogDebug("Retrieved {ChunkCount} chunks for document {DocumentId}", chunks.Count, documentId);
                        }
                        else
                        {
                            contents.Add($"Document {document.FileName}: [No parsed content available]");
                        }
                    }
                    else
                    {
                        // Fallback: just use document metadata
                        var metadataInfo = document.Metadata != null 
                            ? $"\nTitle: {document.Metadata.Title}\nParties: {string.Join(", ", document.Metadata.Parties)}\nContract Type: {document.Metadata.ContractType}"
                            : "";
                        
                        contents.Add($"Document {document.FileName}:{metadataInfo}\n[Full content not available - document may need to be parsed first]");
                        _logger.LogWarning("Chunks not available for document {DocumentId}, using metadata only", documentId);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON deserialization failed for document {DocumentId}. Error: {Error}", 
                        documentId, jsonEx.Message);
                    contents.Add($"Document {documentId}: [JSON deserialization error - {jsonEx.Message}]");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve content for document {DocumentId}", documentId);
                    contents.Add($"Document {documentId}: [Content unavailable - {ex.Message}]");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve document contents");
            contents.Add("Error retrieving documents");
        }
        
        return contents;
    }

    private async Task<SummarizationResult> GenerateSummaryWithAIAsync(
        List<string> documentContents, 
        SummarizationRequest request)
    {
        try
        {
            // Get the default LLM provider (Gemini)
            var llmProvider = _llmProviderFactory.GetDefaultProvider();
            
            if (!llmProvider.IsAvailable || !llmProvider.SupportsChat)
            {
                _logger.LogWarning("LLM provider not available for summarization");
                return CreateFallbackSummary(request);
            }

            var summaryPrompt = request.Type switch
            {
                SummaryType.Overview => CreateOverviewPrompt(documentContents, request),
                SummaryType.KeyTerms => CreateKeyTermsPrompt(documentContents, request),
                SummaryType.RiskAssessment => CreateRiskAssessmentPrompt(documentContents, request),
                SummaryType.Comparison => CreateComparisonPrompt(documentContents, request),
                _ => CreateOverviewPrompt(documentContents, request)
            };

            var options = new LLMGenerationOptions
            {
                Temperature = 0.4f,
                MaxTokens = request.MaxLength * 2 // Rough estimate
            };

            var summaryText = await llmProvider.GenerateTextAsync(summaryPrompt, options);

            // Extract key points using AI
            var keyPointsPrompt = $@"
Based on the following summary, extract 3-5 key bullet points that capture the most important information:

Summary: {summaryText}

Return only the bullet points, one per line, starting with '-'.";

            var keyPointsOptions = new LLMGenerationOptions
            {
                Temperature = 0.3f,
                MaxTokens = 500
            };

            var keyPointsText = await llmProvider.GenerateTextAsync(keyPointsPrompt, keyPointsOptions);
            
            var keyPoints = keyPointsText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Trim().StartsWith("-"))
                .Select(line => line.Trim().TrimStart('-').Trim())
                .ToList();

            var insights = new Dictionary<string, object>
            {
                ["documentCount"] = request.DocumentIds.Count,
                ["summaryType"] = request.Type.ToString(),
                ["focus"] = request.Focus ?? "General",
                ["wordCount"] = summaryText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                ["llmProvider"] = llmProvider.ProviderName
            };

            return new SummarizationResult(
                summaryText,
                keyPoints,
                insights,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI summary");
            return CreateFallbackSummary(request);
        }
    }

    private SummarizationResult CreateFallbackSummary(SummarizationRequest request)
    {
        return new SummarizationResult(
            "Summary generation failed due to technical error.",
            new List<string> { "Unable to process documents", "Please try again later" },
            new Dictionary<string, object> { ["error"] = true },
            DateTime.UtcNow
        );
    }

    private async Task<SearchResult[]> ApplyHybridRankingAsync(SearchResult[] semanticResults, string query)
    {
        try
        {
            // Apply keyword-based scoring to complement semantic similarity
            var keywords = ExtractKeywords(query);
            
            foreach (var result in semanticResults)
            {
                var keywordScore = CalculateKeywordScore(result.Content, keywords);
                var enhancedScore = (result.Score * 0.7f) + (keywordScore * 0.3f); // Weighted combination
                
                // Update the result with enhanced score
                result.Highlights["hybrid_score"] = enhancedScore;
                result.Highlights["keyword_matches"] = CountKeywordMatches(result.Content, keywords);
            }
            
            // Re-sort by enhanced score
            return semanticResults
                .OrderByDescending(r => r.Highlights.GetValueOrDefault("hybrid_score", r.Score))
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply hybrid ranking, returning semantic results");
            return semanticResults;
        }
    }

    #region Service URL Configuration

    private string GetServiceUrl(string serviceName)
    {
        var configKey = $"Services:{serviceName}";
        var url = _configuration[configKey];
        
        if (!string.IsNullOrEmpty(url))
        {
            _logger.LogDebug("Using configured URL for {ServiceName}: {Url}", serviceName, url);
            return url;
        }
        
        var defaultUrl = GetDefaultServiceUrl(serviceName);
        _logger.LogWarning("No configuration found for {ServiceName} at {ConfigKey}, using default: {DefaultUrl}", 
            serviceName, configKey, defaultUrl);
        
        return defaultUrl;
    }

    private string GetDefaultServiceUrl(string serviceName)
    {
        return serviceName switch
        {
            "DocumentUpload" => "https://localhost:7048",
            "DocumentParser" => "https://localhost:7258",
            "EmbeddingService" => "https://localhost:7070",
            "VectorService" => "https://localhost:7197",
            _ => throw new ArgumentException($"Unknown service: {serviceName}", nameof(serviceName))
        };
    }

    #endregion

    #region Prompt Generation

    private string CreateOverviewPrompt(List<string> contents, SummarizationRequest request)
    {
        var combinedContent = string.Join("\n\n", contents);
        var focusClause = !string.IsNullOrEmpty(request.Focus) ? $" with special focus on: {request.Focus}" : "";
        
        return $@"
You are an expert contract analyst. Please provide a comprehensive overview summary of the following contract documents{focusClause}.

The summary should be approximately {request.MaxLength} words and include:
- Main purpose and scope of the contracts
- Key parties involved
- Important terms and conditions
- Notable obligations or restrictions
- Any significant dates or deadlines

Contract Documents:
{combinedContent.Substring(0, Math.Min(combinedContent.Length, 8000))}

Provide a clear, professional summary suitable for executive review.";
    }

    private string CreateKeyTermsPrompt(List<string> contents, SummarizationRequest request)
    {
        var combinedContent = string.Join("\n\n", contents);
        
        return $@"
You are an expert contract analyst. Please extract and summarize the key terms from the following contract documents.

Focus on:
- Payment terms and amounts
- Delivery/performance obligations
- Termination conditions
- Liability and warranty clauses
- Intellectual property rights
- Confidentiality requirements

Contract Documents:
{combinedContent.Substring(0, Math.Min(combinedContent.Length, 8000))}

Provide a structured summary of the most important terms, organized by category.";
    }

    private string CreateRiskAssessmentPrompt(List<string> contents, SummarizationRequest request)
    {
        var combinedContent = string.Join("\n\n", contents);
        
        return $@"
You are an expert legal risk analyst. Please assess the potential risks in the following contract documents.

Analyze:
- Financial risks and exposure
- Operational risks and dependencies
- Legal compliance issues
- Termination and penalty risks
- Force majeure and liability caps
- Intellectual property risks

Contract Documents:
{combinedContent.Substring(0, Math.Min(combinedContent.Length, 8000))}

Provide a risk assessment with specific recommendations for risk mitigation.";
    }

    private string CreateComparisonPrompt(List<string> contents, SummarizationRequest request)
    {
        var combinedContent = string.Join("\n\n", contents);
        
        return $@"
You are an expert contract analyst. Please compare the following contract documents and highlight key differences and similarities.

Focus on:
- Terms and conditions variations
- Pricing and payment differences
- Different obligations or requirements
- Varying risk allocations
- Timeline differences
- Unique clauses or provisions

Contract Documents:
{combinedContent.Substring(0, Math.Min(combinedContent.Length, 8000))}

Provide a detailed comparison analysis highlighting the most significant differences and commonalities.";
    }

    #endregion

    #region Helper Methods

    private List<string> ExtractKeywords(string query)
    {
        // Simple keyword extraction - in a real implementation, you might use NLP libraries
        var stopWords = new HashSet<string> { "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "is", "are", "was", "were", "a", "an" };
        
        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 2 && !stopWords.Contains(word.ToLowerInvariant()))
            .Select(word => word.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    private float CalculateKeywordScore(string content, List<string> keywords)
    {
        if (!keywords.Any()) return 0f;
        
        var contentLower = content.ToLowerInvariant();
        var matchCount = keywords.Count(keyword => contentLower.Contains(keyword));
        
        return (float)matchCount / keywords.Count;
    }

    private int CountKeywordMatches(string content, List<string> keywords)
    {
        var contentLower = content.ToLowerInvariant();
        return keywords.Count(keyword => contentLower.Contains(keyword));
    }

    private float[] GenerateMockEmbedding()
    {
        var random = new Random();
        var vector = new float[1536];
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }
        
        var magnitude = Math.Sqrt(vector.Sum(x => x * x));
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }
        
        return vector;
    }

    #endregion
}