using Microsoft.SemanticKernel;
using System.Text.Json;
using System.Collections.Concurrent;
using ContractProcessingSystem.Shared.AI;

namespace ContractProcessingSystem.AIAgent.Services;

public class AIAgentService : IAIAgentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Kernel _semanticKernel;
    private readonly IWorkflowService _workflowService;
    private readonly ILogger<AIAgentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ILLMProviderFactory _llmProviderFactory;
    private readonly ConcurrentDictionary<Guid, string> _conversationContexts;

    public AIAgentService(
        IHttpClientFactory httpClientFactory,
        Kernel semanticKernel,
        IWorkflowService workflowService,
        ILogger<AIAgentService> logger,
        IConfiguration configuration,
        ILLMProviderFactory llmProviderFactory)
    {
        _httpClientFactory = httpClientFactory;
        _semanticKernel = semanticKernel;
        _workflowService = workflowService;
        _logger = logger;
        _configuration = configuration;
        _llmProviderFactory = llmProviderFactory;
        _conversationContexts = new ConcurrentDictionary<Guid, string>();
    }

    public async Task<ProcessingStatus> ProcessDocumentAsync(Guid documentId)
    {
        try
        {
            _logger.LogInformation("Starting AI-orchestrated document processing for {DocumentId}", documentId);

            // Start the complete document processing workflow
            var workflowId = await _workflowService.StartWorkflowAsync("ProcessDocument", new { DocumentId = documentId });
            
            // Execute the processing pipeline asynchronously
            _ = Task.Run(async () => await ExecuteDocumentProcessingPipelineAsync(documentId, workflowId));

            return new ProcessingStatus(
                workflowId,
                "Processing",
                0.1f,
                "AI Agent initiated document processing pipeline",
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start document processing for {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<string> ChatAsync(string message, Guid? contextDocumentId = null)
    {
        try
        {
            _logger.LogInformation("Processing chat message with context document: {DocumentId}", contextDocumentId);

            // Build context for the conversation
            var context = await BuildConversationContextAsync(message, contextDocumentId);
            
            // Create AI prompt with context
            var prompt = $@"
You are an expert legal contract assistant. You help users understand, analyze, and work with contract documents.

{context}

User Question: {message}

Please provide a helpful, accurate response. If you're discussing a specific contract, reference relevant sections and explain legal implications clearly. If you need more information to provide a complete answer, ask clarifying questions.

Response:";

            // Use LLM Provider Factory for proper Gemini/OpenAI support
            var llmProvider = _llmProviderFactory.GetDefaultProvider();
            var aiResponse = await llmProvider.GenerateTextAsync(prompt);

            // Store conversation context for future reference
            if (contextDocumentId.HasValue)
            {
                _conversationContexts.AddOrUpdate(
                    contextDocumentId.Value,
                    $"Q: {message}\nA: {aiResponse}",
                    (key, oldValue) => $"{oldValue}\n\nQ: {message}\nA: {aiResponse}"
                );
            }

            return aiResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process chat message");
            return "I apologize, but I encountered an error while processing your request. Please try again.";
        }
    }

    public async Task<SummarizationResult> AnalyzeContractAsync(Guid documentId)
    {
        try
        {
            _logger.LogInformation("Starting comprehensive contract analysis for {DocumentId}", documentId);

            // Retrieve document metadata and content
            var document = await GetDocumentInfoAsync(documentId);
            if (document == null)
            {
                throw new FileNotFoundException($"Document {documentId} not found");
            }

            // Get the actual document content
            var documentContent = await GetDocumentTextContentAsync(documentId);
            
            // Limit content size for API
            var contentSample = documentContent.Length > 20000 
                ? documentContent.Substring(0, 20000) + "\n\n[Content truncated for analysis...]"
                : documentContent;
            
            // Perform multi-faceted analysis
            var analysisPrompt = $@"
You are an expert contract analyst. Please provide a comprehensive analysis of the following contract document.

Document: {document.FileName}
Document Type: {document.Metadata?.ContractType ?? "Unknown"}
Parties: {string.Join(", ", document.Metadata?.Parties ?? new List<string>())}

Please analyze:
1. Contract Type and Purpose
2. Key Parties and Their Roles
3. Financial Terms and Obligations
4. Important Dates and Deadlines
5. Risk Assessment
6. Compliance Requirements
7. Termination Conditions
8. Notable Clauses or Provisions

Provide a structured analysis that would be useful for legal review and business decision-making.

Contract Content:
{contentSample}
";

            // Use LLM Provider Factory
            var llmProvider = _llmProviderFactory.GetDefaultProvider();
            var analysisText = await llmProvider.GenerateTextAsync(analysisPrompt);

            // Extract key insights
            var insights = await ExtractContractInsightsAsync(analysisText);

            // Generate actionable recommendations
            var keyPoints = await GenerateRecommendationsAsync(analysisText);

            return new SummarizationResult(
                analysisText,
                keyPoints,
                insights,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze contract {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<string> CompareContractsAsync(List<Guid> documentIds)
    {
        try
        {
            _logger.LogInformation("Starting contract comparison for {DocumentCount} documents", documentIds.Count);

            if (documentIds.Count < 2)
            {
                throw new ArgumentException("At least 2 documents required for comparison");
            }

            if (documentIds.Count > 5)
            {
                throw new ArgumentException("Maximum 5 documents can be compared at once");
            }

            // Retrieve all documents with their content
            var documentsData = new List<(ContractDocument Doc, string Content)>();
            foreach (var docId in documentIds)
            {
                var doc = await GetDocumentInfoAsync(docId);
                if (doc != null)
                {
                    var content = await GetDocumentTextContentAsync(docId);
                    // Limit each document to 5000 characters for comparison
                    var contentSample = content.Length > 5000 
                        ? content.Substring(0, 5000) + "\n[Truncated...]" 
                        : content;
                    documentsData.Add((doc, contentSample));
                }
            }

            if (documentsData.Count < 2)
            {
                throw new InvalidOperationException("Could not retrieve enough documents for comparison");
            }

            // Build comprehensive comparison prompt
            var documentSummaries = documentsData.Select((d, i) => $@"
DOCUMENT {i + 1}:
Filename: {d.Doc.FileName}
Type: {d.Doc.Metadata?.ContractType ?? "Unknown"}
Parties: {string.Join(", ", d.Doc.Metadata?.Parties ?? new List<string>())}
Value: {d.Doc.Metadata?.ContractValue?.ToString("N0") ?? "N/A"} {d.Doc.Metadata?.Currency ?? ""}
Key Terms: {string.Join(", ", d.Doc.Metadata?.KeyTerms?.Take(5) ?? new List<string>())}

Content:
{d.Content}
").ToList();

            var comparisonPrompt = $@"
You are an expert contract analyst. Please compare the following {documentsData.Count} contract documents and provide a detailed comparison analysis.

Documents to Compare:
{string.Join("\n" + new string('=', 80) + "\n", documentSummaries)}

Please provide:
1. Side-by-side comparison of key terms
2. Significant differences in obligations and responsibilities
3. Variations in financial terms
4. Different risk allocations
5. Timeline and deadline differences
6. Unique clauses in each contract
7. Recommendations for standardization or negotiation

Focus on business-critical differences that could impact decision-making.
Provide specific examples and quotes from the contracts.";

            // Use LLM Provider Factory
            var llmProvider = _llmProviderFactory.GetDefaultProvider();
            var comparisonResult = await llmProvider.GenerateTextAsync(comparisonPrompt);
            
            return comparisonResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare contracts");
            throw;
        }
    }

    private async Task ExecuteDocumentProcessingPipelineAsync(Guid documentId, Guid workflowId)
    {
        try
        {
            // Update workflow status - Parsing stage
            await _workflowService.UpdateWorkflowStatusAsync(workflowId, "Parsing Document", 0.2f);
            
            // Step 1: Parse the document
            await CallServiceAsync("DocumentParser", $"api/parsing/{documentId}/parse", HttpMethod.Post);
            
            // Update workflow status - Chunking stage
            await _workflowService.UpdateWorkflowStatusAsync(workflowId, "Chunking Document", 0.4f);
            
            // Step 2: Chunk the document
            var chunksResponse = await CallServiceAsync("DocumentParser", $"api/parsing/{documentId}/chunk", HttpMethod.Post);
            
            // Update workflow status - Embedding generation stage
            await _workflowService.UpdateWorkflowStatusAsync(workflowId, "Generating Embeddings", 0.6f);
            
            // Step 3: Generate embeddings (this also stores them in vector database)
            if (chunksResponse != null)
            {
                await CallServiceAsync("EmbeddingService", "api/embeddings/generate", HttpMethod.Post, chunksResponse);
            }
            
            // Update workflow status - Storing vectors stage
            await _workflowService.UpdateWorkflowStatusAsync(workflowId, "Storing Vectors", 0.8f);
            
            // Step 4: Wait for embedding generation and storage to complete
            // Poll the embedding service status to verify completion
            var embeddingComplete = await WaitForEmbeddingCompletionAsync(documentId, maxWaitSeconds: 60);
            
            if (!embeddingComplete)
            {
                _logger.LogWarning("Embedding generation did not complete within timeout for {DocumentId}", documentId);
                await _workflowService.UpdateWorkflowStatusAsync(workflowId, "Completed with warnings", 0.95f, 
                    "Document processed but embedding verification timed out");
            }
            else
            {
                // Update workflow status - Complete
                await _workflowService.UpdateWorkflowStatusAsync(workflowId, "Processing Complete", 1.0f);
            }
            
            _logger.LogInformation("Document processing pipeline completed for {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute document processing pipeline for {DocumentId}", documentId);
            await _workflowService.UpdateWorkflowStatusAsync(workflowId, "Failed", -1.0f, ex.Message);
            throw;
        }
    }

    private async Task<bool> WaitForEmbeddingCompletionAsync(Guid documentId, int maxWaitSeconds = 60)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var embeddingServiceUrl = _configuration["Services:EmbeddingService"] ?? "http://localhost:5002";
            
            _logger.LogInformation("Waiting for embedding completion for document {DocumentId}", documentId);
            
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromSeconds(2);
            
            while ((DateTime.UtcNow - startTime).TotalSeconds < maxWaitSeconds)
            {
                try
                {
                    var response = await httpClient.GetAsync($"{embeddingServiceUrl}/api/embeddings/status/{documentId}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var statusContent = await response.Content.ReadAsStringAsync();
                        var status = JsonSerializer.Deserialize<ProcessingStatus>(statusContent, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        
                        if (status != null)
                        {
                            _logger.LogDebug("Embedding status for {DocumentId}: {Stage} ({Progress:P0})", 
                                documentId, status.Stage, status.Progress);
                            
                            // Check if processing is complete (progress = 1.0) or failed (progress = -1.0)
                            if (status.Progress >= 1.0f)
                            {
                                _logger.LogInformation("Embedding generation completed for {DocumentId}", documentId);
                                return true;
                            }
                            else if (status.Progress < 0)
                            {
                                _logger.LogWarning("Embedding generation failed for {DocumentId}: {Message}", 
                                    documentId, status.Message);
                                return false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error polling embedding status for {DocumentId}", documentId);
                }
                
                // Wait before next poll
                await Task.Delay(pollInterval);
            }
            
            _logger.LogWarning("Timed out waiting for embedding completion for {DocumentId} after {Seconds}s", 
                documentId, maxWaitSeconds);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for embedding completion for {DocumentId}", documentId);
            return false;
        }
    }

    private async Task<string> BuildConversationContextAsync(string message, Guid? contextDocumentId)
    {
        var contextBuilder = new List<string>();
        
        if (contextDocumentId.HasValue)
        {
            // Get document information
            var document = await GetDocumentInfoAsync(contextDocumentId.Value);
            if (document != null)
            {
                contextBuilder.Add($"Context Document: {document.FileName} (ID: {contextDocumentId})");
                contextBuilder.Add($"Upload Date: {document.UploadedAt:yyyy-MM-dd}");
                contextBuilder.Add($"Status: {document.Status}");
            }

            // Get previous conversation context
            if (_conversationContexts.TryGetValue(contextDocumentId.Value, out var previousContext))
            {
                contextBuilder.Add("Previous Conversation:");
                contextBuilder.Add(previousContext);
            }

            // Get related search results if the message seems like a question
            if (IsSearchQuery(message))
            {
                try
                {
                    var searchResults = await PerformContextualSearchAsync(message, contextDocumentId.Value);
                    if (searchResults.Any())
                    {
                        contextBuilder.Add("Relevant Contract Sections:");
                        contextBuilder.AddRange(searchResults.Take(3).Select(r => $"- {r.Content.Substring(0, Math.Min(r.Content.Length, 200))}..."));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get contextual search results");
                }
            }
        }

        return string.Join("\n", contextBuilder);
    }

    private async Task<Dictionary<string, object>> ExtractContractInsightsAsync(string analysisText)
    {
        try
        {
            var insightsPrompt = $@"
Based on the following contract analysis, extract key metrics and insights as a JSON object:

Analysis: {analysisText.Substring(0, Math.Min(analysisText.Length, 4000))}

Extract insights about:
- Risk level (High/Medium/Low)
- Financial exposure (if mentioned)
- Contract duration
- Complexity score (1-10)
- Key obligations count
- Critical dates count

Return only a valid JSON object.";

            // Use LLM Provider Factory
            var llmProvider = _llmProviderFactory.GetDefaultProvider();
            var jsonResponse = await llmProvider.GenerateTextAsync(insightsPrompt);
            
            // Clean up markdown code blocks
            jsonResponse = CleanJsonResponse(jsonResponse);
            
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonResponse) ?? new Dictionary<string, object>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, object>
                {
                    ["analysis_completed"] = true,
                    ["timestamp"] = DateTime.UtcNow.ToString("O")
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract contract insights");
            return new Dictionary<string, object> { ["error"] = "Insight extraction failed" };
        }
    }

    private async Task<List<string>> GenerateRecommendationsAsync(string analysisText)
    {
        try
        {
            var recommendationsPrompt = $@"
Based on the following contract analysis, provide 3-5 specific, actionable recommendations:

Analysis: {analysisText.Substring(0, Math.Min(analysisText.Length, 4000))}

Focus on:
- Risk mitigation strategies
- Terms that need attention
- Negotiation opportunities
- Compliance requirements
- Process improvements

Return only the recommendations, one per line, starting with '-'.";

            // Use LLM Provider Factory
            var llmProvider = _llmProviderFactory.GetDefaultProvider();
            var recommendationsText = await llmProvider.GenerateTextAsync(recommendationsPrompt);
            
            return recommendationsText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Trim().StartsWith("-"))
                .Select(line => line.Trim().TrimStart('-').Trim())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate recommendations");
            return new List<string> { "Review contract terms carefully", "Consult legal counsel for complex provisions" };
        }
    }

    private string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "{}";
        }

        // Remove markdown code blocks (```json ... ``` or ``` ... ```)
        var cleaned = response.Trim();
        
        // Check for markdown code blocks
        if (cleaned.StartsWith("```"))
        {
            // Remove opening ```json or ```
            var lines = cleaned.Split('\n');
            if (lines.Length > 0)
            {
                var firstLine = lines[0].Trim();
                if (firstLine.StartsWith("```"))
                {
                    cleaned = string.Join('\n', lines.Skip(1));
                }
            }
            
            // Remove closing ```
            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned.Substring(0, cleaned.LastIndexOf("```")).Trim();
            }
        }

        // Find the first { and last } to extract just the JSON object
        var firstBrace = cleaned.IndexOf('{');
        var lastBrace = cleaned.LastIndexOf('}');
        
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return cleaned;
    }

    private async Task<ContractDocument?> GetDocumentInfoAsync(Guid documentId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var documentServiceUrl = _configuration["Services:DocumentUpload"] ?? "http://localhost:5000";
            
            var response = await httpClient.GetAsync($"{documentServiceUrl}/api/documents/{documentId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ContractDocument>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get document info for {DocumentId}", documentId);
        }
        
        return null;
    }

    private async Task<string> GetDocumentTextContentAsync(Guid documentId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var parserServiceUrl = _configuration["Services:DocumentParser"] ?? "http://localhost:5002";
            
            // Try to get parsed/chunked content first
            var chunksResponse = await httpClient.GetAsync($"{parserServiceUrl}/api/parsing/{documentId}/chunks");
            
            if (chunksResponse.IsSuccessStatusCode)
            {
                var chunksContent = await chunksResponse.Content.ReadAsStringAsync();
                var chunks = JsonSerializer.Deserialize<List<ContractChunk>>(chunksContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (chunks != null && chunks.Any())
                {
                    // Combine all chunks to get full document text
                    return string.Join("\n\n", chunks.OrderBy(c => c.ChunkIndex).Select(c => c.Content));
                }
            }

            // If chunks not available, get raw content and extract text
            var documentServiceUrl = _configuration["Services:DocumentUpload"] ?? "http://localhost:5000";
            var contentResponse = await httpClient.GetAsync($"{documentServiceUrl}/api/documents/{documentId}/content");
            
            if (contentResponse.IsSuccessStatusCode)
            {
                // For now, return a placeholder - actual text extraction would require DocumentTextExtractor
                _logger.LogWarning("Document chunks not available for {DocumentId}, raw content extraction not implemented in AIAgent", documentId);
                return "[Document content available but not yet extracted to text]";
            }

            throw new InvalidOperationException($"Could not retrieve document content for {documentId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document text content for {DocumentId}", documentId);
            throw;
        }
    }

    private async Task<SearchResult[]> PerformContextualSearchAsync(string query, Guid documentId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var queryServiceUrl = _configuration["Services:QueryService"] ?? "http://localhost:5004";
            
            var searchRequest = new SearchRequest(
                query, 
                5, 
                0.7f, 
                null, 
                new Dictionary<string, object> { ["document_id"] = documentId.ToString() }
            );
            
            var content = new StringContent(
                JsonSerializer.Serialize(searchRequest), 
                System.Text.Encoding.UTF8, 
                "application/json"
            );
            
            var response = await httpClient.PostAsync($"{queryServiceUrl}/api/query/search", content);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<SearchResult[]>(responseContent) ?? Array.Empty<SearchResult>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to perform contextual search");
        }
        
        return Array.Empty<SearchResult>();
    }

    private async Task<object?> CallServiceAsync(string serviceName, string endpoint, HttpMethod method, object? body = null)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var serviceUrl = _configuration[$"Services:{serviceName}"] ?? GetDefaultServiceUrl(serviceName);
            
            var request = new HttpRequestMessage(method, $"{serviceUrl}/{endpoint}");
            
            if (body != null && (method == HttpMethod.Post || method == HttpMethod.Put))
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
            }
            
            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return string.IsNullOrEmpty(content) ? null : JsonSerializer.Deserialize<object>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call service {ServiceName} at endpoint {Endpoint}", serviceName, endpoint);
            throw;
        }
    }

    private string GetDefaultServiceUrl(string serviceName)
    {
        return serviceName switch
        {
            "DocumentUpload" => "https://localhost:7048",
            "DocumentParser" => "https://localhost:7258",
            "EmbeddingService" => "https://localhost:7070",
            "VectorService" => "https://localhost:7197",
            "QueryService" => "https://localhost:7004",
            _ => throw new ArgumentException($"Unknown service: {serviceName}")
        };
    }

    private static bool IsSearchQuery(string message)
    {
        var searchKeywords = new[] { "find", "search", "what", "where", "how", "when", "show me", "tell me about", "explain" };
        var lowerMessage = message.ToLowerInvariant();
        return searchKeywords.Any(keyword => lowerMessage.Contains(keyword));
    }
}

public class WorkflowService : IWorkflowService
{
    private readonly ConcurrentDictionary<Guid, ProcessingStatus> _workflows;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(ILogger<WorkflowService> logger)
    {
        _workflows = new ConcurrentDictionary<Guid, ProcessingStatus>();
        _logger = logger;
    }

    public async Task<Guid> StartWorkflowAsync(string workflowName, object parameters)
    {
        var workflowId = Guid.NewGuid();
        
        _logger.LogInformation("Starting workflow {WorkflowName} with ID {WorkflowId}", workflowName, workflowId);
        
        var status = new ProcessingStatus(
            workflowId,
            "Started",
            0.0f,
            $"Workflow {workflowName} initiated",
            DateTime.UtcNow
        );
        
        _workflows.TryAdd(workflowId, status);
        
        return workflowId;
    }

    public async Task<ProcessingStatus> GetWorkflowStatusAsync(Guid workflowId)
    {
        if (_workflows.TryGetValue(workflowId, out var status))
        {
            return status;
        }
        
        throw new ArgumentException($"Workflow {workflowId} not found");
    }

    public async Task<bool> CancelWorkflowAsync(Guid workflowId)
    {
        if (_workflows.TryGetValue(workflowId, out var status))
        {
            var cancelledStatus = status with 
            { 
                Stage = "Cancelled", 
                Progress = -1.0f, 
                Message = "Workflow cancelled by user",
                LastUpdated = DateTime.UtcNow
            };
            
            _workflows.TryUpdate(workflowId, cancelledStatus, status);
            _logger.LogInformation("Workflow {WorkflowId} cancelled", workflowId);
            return true;
        }
        
        return false;
    }

    public async Task UpdateWorkflowStatusAsync(Guid workflowId, string stage, float progress, string? message = null)
    {
        if (_workflows.TryGetValue(workflowId, out var currentStatus))
        {
            var newStatus = currentStatus with
            {
                Stage = stage,
                Progress = progress,
                Message = message ?? $"Workflow at stage: {stage}",
                LastUpdated = DateTime.UtcNow
            };
            
            _workflows.TryUpdate(workflowId, newStatus, currentStatus);
            _logger.LogInformation("Workflow {WorkflowId} updated: {Stage} ({Progress:P0})", workflowId, stage, progress);
        }
    }
}