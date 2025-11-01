using Microsoft.Extensions.Logging;
using System.Text.Json;
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
            var model = options.Model ?? _config.DefaultEmbeddingModel;

            // Truncate text if too long
            var truncatedText = TruncateTextForEmbedding(text, options.MaxInputTokens);

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

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini embedding request failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
                IsAvailable = false;
                throw new LLMException(ProviderName, $"Embedding request failed: {response.StatusCode}");
            }

            var geminiResponse = JsonSerializer.Deserialize<GeminiEmbeddingResponse>(responseContent);
            
            if (geminiResponse?.Embedding?.Values == null)
            {
                throw new LLMException(ProviderName, "No embedding generated from Gemini");
            }

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
        // Gemini API doesn't support batch embeddings, so we'll process them sequentially
        var results = new List<float[]>();
        
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
                var embeddingSize = options?.AdditionalProperties.ContainsKey("embeddingSize") == true 
                    ? (int)options.AdditionalProperties["embeddingSize"] 
                    : 768; // Default Gemini embedding size
                results.Add(new float[embeddingSize]);
            }
        }

        return results.ToArray();
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
        public GeminiCandidate[]? Candidates { get; set; }
    }

    private class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
    }

    private class GeminiContent
    {
        public GeminiPart[]? Parts { get; set; }
    }

    private class GeminiPart
    {
        public string? Text { get; set; }
    }

    private class GeminiEmbeddingResponse
    {
        public GeminiEmbedding? Embedding { get; set; }
    }

    private class GeminiEmbedding
    {
        public float[]? Values { get; set; }
    }
}