namespace ContractProcessingSystem.Shared.AI;

public interface ILLMProvider
{
    string ProviderName { get; }
    Task<string> GenerateTextAsync(string prompt, LLMGenerationOptions? options = null);
    Task<string> GenerateChatResponseAsync(string message, string? systemPrompt = null, LLMGenerationOptions? options = null);
    Task<float[]> GenerateEmbeddingAsync(string text, EmbeddingOptions? options = null);
    Task<float[][]> GenerateEmbeddingsAsync(IEnumerable<string> texts, EmbeddingOptions? options = null);
    bool SupportsEmbeddings { get; }
    bool SupportsChat { get; }
    bool IsAvailable { get; }
}

public interface ILLMProviderFactory
{
    ILLMProvider GetProvider(string providerName);
    ILLMProvider GetDefaultProvider();
    ILLMProvider GetEmbeddingProvider();
    IEnumerable<ILLMProvider> GetAvailableProviders();
}

public class LLMGenerationOptions
{
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 1000;
    public float TopP { get; set; } = 1.0f;
    public string? Model { get; set; }
    public bool Stream { get; set; } = false;
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}

public class EmbeddingOptions
{
    public string? Model { get; set; }
    public int MaxInputTokens { get; set; } = 8000;
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}

public enum LLMProviderType
{
    OpenAI,
    AzureOpenAI,
    Gemini,
    Claude,
    GitHubModels,
    Local
}

public class ProviderConfiguration
{
    public LLMProviderType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string DefaultChatModel { get; set; } = string.Empty;
    public string DefaultEmbeddingModel { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0; // Higher priority = preferred
    public Dictionary<string, object> AdditionalSettings { get; set; } = new();
}

public class LLMException : Exception
{
    public string ProviderName { get; }
    public string? ErrorCode { get; }

    public LLMException(string providerName, string message) : base(message)
    {
        ProviderName = providerName;
    }

    public LLMException(string providerName, string message, Exception innerException) : base(message, innerException)
    {
        ProviderName = providerName;
    }

    public LLMException(string providerName, string errorCode, string message) : base(message)
    {
        ProviderName = providerName;
        ErrorCode = errorCode;
    }
}