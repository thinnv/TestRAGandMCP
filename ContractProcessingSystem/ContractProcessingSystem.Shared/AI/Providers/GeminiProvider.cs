using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using ContractProcessingSystem.Shared.AI;

namespace ContractProcessingSystem.Shared.AI.Providers;

public class GeminiProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly ProviderConfiguration _config;
    private readonly ILogger<GeminiProvider> _logger;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    public string ProviderName => _config.Name;
    public bool SupportsEmbeddings => true;
    public bool SupportsChat => true;
    public bool IsAvailable { get; private set; } = true;

    public GeminiProvider(
        HttpClient httpClient,
        ProviderConfiguration config,
        ILogger<GeminiProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
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
            var model = options.Model ?? _config.DefaultChatModel;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = !string.IsNullOrEmpty(systemPrompt) ? $"{systemPrompt}\n\n{message}" : message }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = options.Temperature,
                    topP = options.TopP,
                    maxOutputTokens = options.MaxTokens
                }
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{BaseUrl}/models/{model}:generateContent?key={_config.ApiKey}";

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API request failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
                IsAvailable = false;
                throw new LLMException(ProviderName, $"API request failed: {response.StatusCode}");
            }

            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent);
            
            if (geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text == null)
            {
                throw new LLMException(ProviderName, "No response generated from Gemini");
            }

            return geminiResponse.Candidates.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? "";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Gemini HTTP request failed");
            IsAvailable = false;
            throw new LLMException(ProviderName, "HTTP request failed", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini response");
            throw new LLMException(ProviderName, "Response parsing failed", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Gemini chat completion");
            throw new LLMException(ProviderName, "Chat completion failed", ex);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, EmbeddingOptions? options = null)
    {
        try
        {
            options ??= new EmbeddingOptions();
            var model = options.Model ?? _config.DefaultEmbeddingModel ?? "text-embedding-004";

            // Truncate text if too long
            var truncatedText = TruncateTextForEmbedding(text, options.MaxInputTokens);

            _logger.LogDebug("Generating Gemini embedding with model: {Model}, text length: {Length}", 
                model, truncatedText.Length);

            var requestBody = new
            {
                model = $"models/{model}",
                content = new
                {
                    parts = new[]
                    {
                        new { text = truncatedText }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{BaseUrl}/models/{model}:embedContent?key={_config.ApiKey}";

            _logger.LogDebug("Sending embedding request to: {Url}", url);

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Gemini API response status: {StatusCode}", response.StatusCode);
            _logger.LogDebug("Response content: {Content}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini embedding request failed: {StatusCode} - {Content}", 
                    response.StatusCode, responseContent);
                IsAvailable = false;
                throw new LLMException(ProviderName, $"Embedding request failed: {response.StatusCode}. Response: {responseContent}");
            }

            // Try to deserialize with case-insensitive property matching
            var options2 = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var geminiResponse = JsonSerializer.Deserialize<GeminiEmbeddingResponse>(responseContent, options2);
            
            _logger.LogDebug("Deserialized response: Embedding={HasEmbedding}, Values={HasValues}", 
                geminiResponse?.Embedding != null, 
                geminiResponse?.Embedding?.Values != null);

            if (geminiResponse?.Embedding?.Values == null || geminiResponse.Embedding.Values.Length == 0)
            {
                _logger.LogError("No embedding values returned from Gemini. Raw response: {Response}", responseContent);
                throw new LLMException(ProviderName, $"No embedding generated from Gemini. Response: {responseContent}");
            }

            _logger.LogDebug("Successfully generated embedding with {Dimensions} dimensions", 
                geminiResponse.Embedding.Values.Length);

            return geminiResponse.Embedding.Values;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Gemini embedding HTTP request failed");
            IsAvailable = false;
            throw new LLMException(ProviderName, "Embedding HTTP request failed", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Gemini embedding response");
            throw new LLMException(ProviderName, "Embedding response parsing failed", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Gemini embedding generation");
            throw new LLMException(ProviderName, "Embedding generation failed", ex);
        }
    }

    public async Task<float[][]> GenerateEmbeddingsAsync(IEnumerable<string> texts, EmbeddingOptions? options = null)
    {
        try
        {
            options ??= new EmbeddingOptions();
            var model = options.Model ?? _config.DefaultEmbeddingModel ?? "gemini-embedding-001";
            var textsList = texts.ToList();

            _logger.LogInformation("Generating {Count} embeddings with Gemini model: {Model}", 
                textsList.Count, model);

            // Gemini supports batch embeddings with batchEmbedContents endpoint
            var requests = textsList.Select(text => new
            {
                model = $"models/{model}",
                content = new
                {
                    parts = new[]
                    {
                        new { text = TruncateTextForEmbedding(text, options.MaxInputTokens) }
                    }
                }
            }).ToList();

            var requestBody = new { requests };

            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"{BaseUrl}/models/{model}:batchEmbedContents?key={_config.ApiKey}";

            _logger.LogDebug("Sending batch embedding request to: {Url}", url);

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini batch embedding failed ({StatusCode}), falling back to sequential processing", 
                    response.StatusCode);
                
                // Fallback to sequential processing
                return await GenerateEmbeddingsSequentiallyAsync(textsList, options);
            }

            var geminiResponse = JsonSerializer.Deserialize<GeminiBatchEmbeddingResponse>(responseContent);
            
            if (geminiResponse?.Embeddings == null || !geminiResponse.Embeddings.Any())
            {
                _logger.LogWarning("No embeddings in batch response, falling back to sequential processing");
                return await GenerateEmbeddingsSequentiallyAsync(textsList, options);
            }

            var results = geminiResponse.Embeddings
                .Select(e => e.Values ?? new float[768])
                .ToArray();

            _logger.LogInformation("Successfully generated {Count} embeddings in batch", results.Length);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch embedding generation failed, falling back to sequential processing");
            
            // Fallback to sequential processing
            return await GenerateEmbeddingsSequentiallyAsync(texts.ToList(), options);
        }
    }

    private async Task<float[][]> GenerateEmbeddingsSequentiallyAsync(
        List<string> texts, 
        EmbeddingOptions? options)
    {
        _logger.LogInformation("Processing {Count} embeddings sequentially", texts.Count);
        
        var results = new List<float[]>();
        var embeddingSize = GetExpectedEmbeddingSize(options?.Model ?? _config.DefaultEmbeddingModel);
        
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

    private int GetExpectedEmbeddingSize(string? model)
    {
        // Gemini embedding model dimensions
        return model switch
        {
            "text-embedding-004" => 768,
            "embedding-001" => 768,
            "models/text-embedding-004" => 768,
            "models/embedding-001" => 768,
            _ => 768 // Default Gemini embedding size
        };
    }

    private static string TruncateTextForEmbedding(string text, int maxTokens)
    {
        // Approximation: 1 token ≈ 4 characters for English text
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

    // Response models for Gemini API
    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public GeminiCandidate[]? Candidates { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("parts")]
        public GeminiPart[]? Parts { get; set; }
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class GeminiEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public GeminiEmbedding? Embedding { get; set; }
    }

    private class GeminiEmbedding
    {
        [JsonPropertyName("values")]
        public float[]? Values { get; set; }
    }

    private class GeminiBatchEmbeddingResponse
    {
        [JsonPropertyName("embeddings")]
        public GeminiEmbedding[]? Embeddings { get; set; }
    }
}