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
            
            // Execute the processing pipeline
            await ExecuteDocumentProcessingPipelineAsync(documentId);

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

            var response = await _semanticKernel.InvokePromptAsync(prompt);
            var aiResponse = response.GetValue<string>() ?? "I apologize, but I couldn't process your request at this time.";

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
            
            // Perform multi-faceted analysis
            var analysisPrompt = $@"
You are an expert contract analyst. Please provide a comprehensive analysis of the following contract document.

Document: {document?.FileName ?? "Unknown"}

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

Contract Content: [Document content would be inserted here in a real implementation]
";

            var response = await _semanticKernel.InvokePromptAsync(analysisPrompt);
            var analysisText = response.GetValue<string>() ?? "Analysis could not be completed.";

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

            // Retrieve all documents
            var documents = new List<object>();
            foreach (var docId in documentIds)
            {
                var doc = await GetDocumentInfoAsync(docId);
                if (doc != null)
                {
                    documents.Add(new { Id = docId, Info = doc });
                }
            }

            var comparisonPrompt = $@"
You are an expert contract analyst. Please compare the following {documents.Count} contract documents and provide a detailed comparison analysis.

Documents to Compare:
{string.Join("\n", documents.Select((d, i) => $"{i + 1}. Document ID: {documentIds[i]} - {((dynamic)d).Info?.FileName ?? "Unknown"}"))}

Please provide:
1. Side-by-side comparison of key terms
2. Significant differences in obligations and responsibilities
3. Variations in financial terms
4. Different risk allocations
5. Timeline and deadline differences
6. Unique clauses in each contract
7. Recommendations for standardization or negotiation

Focus on business-critical differences that could impact decision-making.

[Document contents would be inserted here in a real implementation]
";

            var response = await _semanticKernel.InvokePromptAsync(comparisonPrompt);
            return response.GetValue<string>() ?? "Contract comparison could not be completed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare contracts");
            throw;
        }
    }

    private async Task ExecuteDocumentProcessingPipelineAsync(Guid documentId)
    {
        try
        {
            // Step 1: Parse the document
            await CallServiceAsync("DocumentParser", $"api/parsing/{documentId}/parse", HttpMethod.Post);
            
            // Step 2: Chunk the document
            var chunks = await CallServiceAsync("DocumentParser", $"api/parsing/{documentId}/chunk", HttpMethod.Post);
            
            // Step 3: Generate embeddings
            if (chunks != null)
            {
                await CallServiceAsync("EmbeddingService", "api/embeddings/generate", HttpMethod.Post, chunks);
            }
            
            // Step 4: Store in vector database (would be called automatically by embedding service)
            
            _logger.LogInformation("Document processing pipeline completed for {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute document processing pipeline for {DocumentId}", documentId);
            throw;
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

            var response = await _semanticKernel.InvokePromptAsync(insightsPrompt);
            var jsonResponse = response.GetValue<string>() ?? "{}";
            
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

            var response = await _semanticKernel.InvokePromptAsync(recommendationsPrompt);
            var recommendationsText = response.GetValue<string>() ?? "";
            
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
                return JsonSerializer.Deserialize<ContractDocument>(content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get document info for {DocumentId}", documentId);
        }
        
        return null;
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
        
        // In a real implementation, this would trigger actual workflow execution
        _ = Task.Run(() => SimulateWorkflowExecution(workflowId, workflowName));
        
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

    private async Task SimulateWorkflowExecution(Guid workflowId, string workflowName)
    {
        try
        {
            var stages = new[]
            {
                ("Document Upload", 0.2f),
                ("Document Parsing", 0.4f),
                ("Embedding Generation", 0.6f),
                ("Vector Storage", 0.8f),
                ("Processing Complete", 1.0f)
            };

            foreach (var (stage, progress) in stages)
            {
                await Task.Delay(2000); // Simulate processing time
                
                if (_workflows.TryGetValue(workflowId, out var currentStatus) && currentStatus.Progress >= 0)
                {
                    var newStatus = currentStatus with
                    {
                        Stage = stage,
                        Progress = progress,
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    _workflows.TryUpdate(workflowId, newStatus, currentStatus);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow {WorkflowId} failed", workflowId);
            
            if (_workflows.TryGetValue(workflowId, out var currentStatus))
            {
                var errorStatus = currentStatus with
                {
                    Stage = "Failed",
                    Progress = -1.0f,
                    Message = ex.Message,
                    LastUpdated = DateTime.UtcNow
                };
                
                _workflows.TryUpdate(workflowId, errorStatus, currentStatus);
            }
        }
    }
}