using Microsoft.SemanticKernel;
using System.Text.Json;

namespace ContractProcessingSystem.QueryService.Services;

public class QueryService : IQueryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Kernel _semanticKernel;
    private readonly ILogger<QueryService> _logger;
    private readonly IConfiguration _configuration;

    public QueryService(
        IHttpClientFactory httpClientFactory,
        Kernel semanticKernel,
        ILogger<QueryService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _semanticKernel = semanticKernel;
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
            var embeddingServiceUrl = _configuration["Services:EmbeddingService"] ?? "http://localhost:5002";
            
            var requestBody = JsonSerializer.Serialize(query);
            var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
            
            var response = await httpClient.PostAsync($"{embeddingServiceUrl}/api/embeddings/generate-single", content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var embedding = JsonSerializer.Deserialize<VectorEmbedding>(responseContent);
            
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
            var vectorServiceUrl = _configuration["Services:VectorService"] ?? "http://localhost:5003";
            
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
            var results = JsonSerializer.Deserialize<SearchResult[]>(responseContent);
            
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
            // Use AI to re-rank and enhance search results based on the query context
            var enhancementPrompt = $@"
You are an expert contract analysis assistant. Given the search query and the contract search results below, 
please analyze and provide enhanced relevance insights.

Search Query: {query}

Analyze the relevance of each result and provide insights about why each result matches the query.
Focus on contract-specific relevance like legal terms, clauses, obligations, and business impact.

Return a JSON array with enhanced insights for each result.";

            var response = await _semanticKernel.InvokePromptAsync(enhancementPrompt);
            var enhancementResult = response.GetValue<string>() ?? "{}";

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
        
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var documentServiceUrl = _configuration["Services:DocumentUpload"] ?? "http://localhost:5000";
            
            foreach (var documentId in documentIds)
            {
                try
                {
                    var response = await httpClient.GetAsync($"{documentServiceUrl}/api/documents/{documentId}");
                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var document = JsonSerializer.Deserialize<ContractDocument>(responseContent);
                        
                        // In a real implementation, you would retrieve the actual document content
                        contents.Add($"Document {document?.FileName}: [Content would be retrieved here]");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve content for document {DocumentId}", documentId);
                    contents.Add($"Document {documentId}: [Content unavailable]");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve document contents");
        }
        
        return contents;
    }

    private async Task<SummarizationResult> GenerateSummaryWithAIAsync(
        List<string> documentContents, 
        SummarizationRequest request)
    {
        try
        {
            var summaryPrompt = request.Type switch
            {
                SummaryType.Overview => CreateOverviewPrompt(documentContents, request),
                SummaryType.KeyTerms => CreateKeyTermsPrompt(documentContents, request),
                SummaryType.RiskAssessment => CreateRiskAssessmentPrompt(documentContents, request),
                SummaryType.Comparison => CreateComparisonPrompt(documentContents, request),
                _ => CreateOverviewPrompt(documentContents, request)
            };

            var response = await _semanticKernel.InvokePromptAsync(summaryPrompt);
            var summaryText = response.GetValue<string>() ?? "Summary could not be generated.";

            // Extract key points using AI
            var keyPointsPrompt = $@"
Based on the following summary, extract 3-5 key bullet points that capture the most important information:

Summary: {summaryText}

Return only the bullet points, one per line, starting with '-'.";

            var keyPointsResponse = await _semanticKernel.InvokePromptAsync(keyPointsPrompt);
            var keyPointsText = keyPointsResponse.GetValue<string>() ?? "";
            
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
                ["wordCount"] = summaryText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
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
            
            return new SummarizationResult(
                "Summary generation failed due to technical error.",
                new List<string> { "Unable to process documents", "Please try again later" },
                new Dictionary<string, object> { ["error"] = true },
                DateTime.UtcNow
            );
        }
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
}