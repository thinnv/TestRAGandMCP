using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ContractProcessingSystem.Shared.AI.Providers;

public class OpenAIProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly ProviderConfiguration _config;
    private readonly ILogger<OpenAIProvider> _logger;
    private const string OpenAIBaseUrl = "https://api.openai.com/v1";

    public string ProviderName => _config.Name;
    public bool SupportsEmbeddings => true;
    public bool SupportsChat => true;
    public bool IsAvailable { get; private set; } = true;

    public OpenAIProvider(
        HttpClient httpClient,
        ProviderConfiguration config,
        ILogger<OpenAIProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        // Set OpenAI headers
        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
    }

    public async Task<string> GenerateTextAsync(string prompt, LLMGenerationOptions? options = null)
    {
        try
        {
            var model = options?.Model ?? _config.DefaultChatModel ?? "gpt-3.5-turbo";
            var url = $"{OpenAIBaseUrl}/chat/completions";

            var request = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant." },
                    new { role = "user", content = prompt }
                },
                max_tokens = options?.MaxTokens ?? 1000,
                temperature = options?.Temperature ?? 0.7f
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenAIChatResponse>(responseJson);

            return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating text with OpenAI");
            throw new LLMException(ProviderName, "Text generation failed", ex);
        }
    }

    public async Task<string> GenerateChatResponseAsync(string message, string? systemPrompt = null, LLMGenerationOptions? options = null)
    {
        try
        {
            var model = options?.Model ?? _config.DefaultChatModel ?? "gpt-3.5-turbo";
            var url = $"{OpenAIBaseUrl}/chat/completions";

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
                max_tokens = options?.MaxTokens ?? 1000,
                temperature = options?.Temperature ?? 0.7f
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenAIChatResponse>(responseJson);

            return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating chat response with OpenAI");
            throw new LLMException(ProviderName, "Chat response generation failed", ex);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, EmbeddingOptions? options = null)
    {
        try
        {
            var model = options?.Model ?? _config.DefaultEmbeddingModel ?? "text-embedding-3-small";
            var url = $"{OpenAIBaseUrl}/embeddings";

            var request = new
            {
                model = model,
                input = text,
                encoding_format = "float"
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenAIEmbeddingResponse>(responseJson);

            return result?.Data?.FirstOrDefault()?.Embedding ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding with OpenAI");
            throw new LLMException(ProviderName, "Embedding generation failed", ex);
        }
    }

    public async Task<float[][]> GenerateEmbeddingsAsync(IEnumerable<string> texts, EmbeddingOptions? options = null)
    {
        try
        {
            var model = options?.Model ?? _config.DefaultEmbeddingModel ?? "text-embedding-3-small";
            var url = $"{OpenAIBaseUrl}/embeddings";

            var request = new
            {
                model = model,
                input = texts.ToArray(),
                encoding_format = "float"
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenAIEmbeddingResponse>(responseJson);

            return result?.Data?.Select(d => d.Embedding).ToArray() ?? Array.Empty<float[]>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings with OpenAI");
            throw new LLMException(ProviderName, "Embeddings generation failed", ex);
        }
    }

    // Response models
    private class OpenAIChatResponse
    {
        public ChatChoice[]? Choices { get; set; }
    }

    private class ChatChoice
    {
        public OpenAIChatMessage? Message { get; set; }
    }

    private class OpenAIChatMessage
    {
        public string? Content { get; set; }
    }

    private class OpenAIEmbeddingResponse
    {
        public EmbeddingData[]? Data { get; set; }
    }

    private class EmbeddingData
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}