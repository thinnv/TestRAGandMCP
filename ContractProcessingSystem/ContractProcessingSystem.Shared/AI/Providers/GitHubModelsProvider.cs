using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ContractProcessingSystem.Shared.AI.Providers;

/// <summary>
/// Provider for GitHub Models (models.github.com)
/// Uses OpenAI-compatible API endpoints with GitHub authentication
/// </summary>
public class GitHubModelsProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly ProviderConfiguration _config;
    private readonly ILogger<GitHubModelsProvider> _logger;
    private const string GitHubModelsBaseUrl = "https://models.inference.ai.azure.com";

    public string ProviderName => _config.Name;
    public bool SupportsEmbeddings => true;
    public bool SupportsChat => true;
    public bool IsAvailable { get; private set; } = true;

    public GitHubModelsProvider(
        HttpClient httpClient,
        ProviderConfiguration config,
        ILogger<GitHubModelsProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        // Set GitHub Personal Access Token for authentication
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
    }

    public async Task<string> GenerateTextAsync(string prompt, LLMGenerationOptions? options = null)
    {
        return await GenerateChatResponseAsync(prompt, null, options);
    }

    public async Task<string> GenerateChatResponseAsync(string message, string? systemPrompt = null, LLMGenerationOptions? options = null)
    {
        try
        {
            options ??= new LLMGenerationOptions();
            // GitHub Models supports: gpt-4o, gpt-4o-mini, Phi-3-medium-4k-instruct, etc.
            var model = options.Model ?? _config.DefaultChatModel ?? "gpt-4o-mini";
            var url = $"{GitHubModelsBaseUrl}/chat/completions";

            var messages = new List<object>();
            
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }
            
            messages.Add(new { role = "user", content = message });

            var request = new
            {
                model = model,
                messages = messages.ToArray(),
                max_tokens = options.MaxTokens,
                temperature = options.Temperature,
                top_p = options.TopP
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending request to GitHub Models: {Model}", model);

            var response = await _httpClient.PostAsync(url, httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GitHub Models API request failed: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                IsAvailable = false;
                throw new LLMException(ProviderName, $"API request failed: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<GitHubModelsChatResponse>(responseContent);
            
            if (result?.Choices?.FirstOrDefault()?.Message?.Content == null)
            {
                throw new LLMException(ProviderName, "No response generated from GitHub Models");
            }

            return result.Choices.First().Message.Content.Trim();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitHub Models HTTP request failed");
            IsAvailable = false;
            throw new LLMException(ProviderName, "HTTP request failed", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse GitHub Models response");
            throw new LLMException(ProviderName, "Response parsing failed", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GitHub Models chat completion");
            throw new LLMException(ProviderName, "Chat completion failed", ex);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, EmbeddingOptions? options = null)
    {
        try
        {
            options ??= new EmbeddingOptions();
            // GitHub Models supports: text-embedding-3-small, text-embedding-3-large
            var model = options.Model ?? _config.DefaultEmbeddingModel ?? "text-embedding-3-small";
            var url = $"{GitHubModelsBaseUrl}/embeddings";

            var request = new
            {
                model = model,
                input = TruncateTextForEmbedding(text, options.MaxInputTokens),
                encoding_format = "float"
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            _logger.LogDebug("Generating GitHub Models embedding with model: {Model}", model);

            var response = await _httpClient.PostAsync(url, httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GitHub Models embedding request failed: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                IsAvailable = false;
                throw new LLMException(ProviderName, $"Embedding request failed: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<GitHubModelsEmbeddingResponse>(responseContent);
            
            if (result?.Data?.FirstOrDefault()?.Embedding == null || result.Data.First().Embedding.Length == 0)
            {
                _logger.LogError("No embedding values returned from GitHub Models");
                throw new LLMException(ProviderName, "No embedding generated");
            }

            _logger.LogDebug("Successfully generated embedding with {Dimensions} dimensions", 
                result.Data.First().Embedding.Length);

            return result.Data.First().Embedding;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitHub Models embedding HTTP request failed");
            IsAvailable = false;
            throw new LLMException(ProviderName, "Embedding HTTP request failed", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse GitHub Models embedding response");
            throw new LLMException(ProviderName, "Embedding response parsing failed", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GitHub Models embedding generation");
            throw new LLMException(ProviderName, "Embedding generation failed", ex);
        }
    }

    public async Task<float[][]> GenerateEmbeddingsAsync(IEnumerable<string> texts, EmbeddingOptions? options = null)
    {
        try
        {
            options ??= new EmbeddingOptions();
            var model = options.Model ?? _config.DefaultEmbeddingModel ?? "text-embedding-3-small";
            var textsList = texts.ToList();

            _logger.LogInformation("Generating {Count} embeddings with GitHub Models model: {Model}", 
                textsList.Count, model);

            var url = $"{GitHubModelsBaseUrl}/embeddings";

            // Truncate texts
            var truncatedTexts = textsList
                .Select(text => TruncateTextForEmbedding(text, options.MaxInputTokens))
                .ToArray();

            var request = new
            {
                model = model,
                input = truncatedTexts,
                encoding_format = "float"
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub Models batch embedding failed ({StatusCode}), falling back to sequential processing", 
                    response.StatusCode);
                
                // Fallback to sequential processing
                return await GenerateEmbeddingsSequentiallyAsync(textsList, options);
            }

            var result = JsonSerializer.Deserialize<GitHubModelsEmbeddingResponse>(responseContent);
            
            if (result?.Data == null || !result.Data.Any())
            {
                _logger.LogWarning("No embeddings in batch response, falling back to sequential processing");
                return await GenerateEmbeddingsSequentiallyAsync(textsList, options);
            }

            var embeddings = result.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToArray();

            _logger.LogInformation("Successfully generated {Count} embeddings in batch", embeddings.Length);

            return embeddings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch embedding generation failed, falling back to sequential processing");
            return await GenerateEmbeddingsSequentiallyAsync(texts.ToList(), options);
        }
    }

    private async Task<float[][]> GenerateEmbeddingsSequentiallyAsync(
        List<string> texts, 
        EmbeddingOptions? options)
    {
        _logger.LogInformation("Processing {Count} embeddings sequentially", texts.Count);
        
        var results = new List<float[]>();
        var embeddingSize = 1536; // Default for text-embedding-3-small
        
        foreach (var text in texts)
        {
            try
            {
                var embedding = await GenerateEmbeddingAsync(text, options);
                results.Add(embedding);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for text, using zero vector");
                // Return a zero vector as fallback
                results.Add(new float[embeddingSize]);
            }
        }

        return results.ToArray();
    }

    private static string TruncateTextForEmbedding(string text, int maxTokens)
    {
        // Approximation: 1 token ? 4 characters for English text
        var maxChars = maxTokens * 4;
        
        if (text.Length <= maxChars)
        {
            return text;
        }

        // Truncate at word boundary
        var truncated = text.Substring(0, maxChars);
        var lastSpace = truncated.LastIndexOf(' ');
        
        return lastSpace > 0 ? truncated.Substring(0, lastSpace) : truncated;
    }

    #region Response Models

    private class GitHubModelsChatResponse
    {
        public ChatChoice[]? Choices { get; set; }
    }

    private class ChatChoice
    {
        public GitHubModelsChatMessage? Message { get; set; }
        public int Index { get; set; }
    }

    private class GitHubModelsChatMessage
    {
        public string? Content { get; set; }
        public string? Role { get; set; }
    }

    private class GitHubModelsEmbeddingResponse
    {
        public EmbeddingData[]? Data { get; set; }
    }

    private class EmbeddingData
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public int Index { get; set; }
    }

    #endregion
}
