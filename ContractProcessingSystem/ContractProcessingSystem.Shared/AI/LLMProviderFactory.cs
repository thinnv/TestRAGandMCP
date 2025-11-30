using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ContractProcessingSystem.Shared.AI.Providers;
using System.Collections.Concurrent;

namespace ContractProcessingSystem.Shared.AI;

public class LLMProviderFactory : ILLMProviderFactory
{
    private readonly ConcurrentDictionary<string, ILLMProvider> _providers;
    private readonly LLMProvidersConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LLMProviderFactory> _logger;

    public LLMProviderFactory(
        LLMProvidersConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<LLMProviderFactory> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _providers = new ConcurrentDictionary<string, ILLMProvider>();
        
        InitializeProviders();
    }

    public ILLMProvider GetProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider) && provider.IsAvailable)
        {
            return provider;
        }

        throw new ArgumentException($"Provider '{providerName}' not found or not available");
    }

    public ILLMProvider GetDefaultProvider()
    {
        var defaultProviderName = _configuration.DefaultProvider;
        
        if (!string.IsNullOrEmpty(defaultProviderName) && 
            _providers.TryGetValue(defaultProviderName, out var defaultProvider) && 
            defaultProvider.IsAvailable)
        {
            return defaultProvider;
        }

        // Fallback to first available provider
        var availableProvider = _providers.Values.FirstOrDefault(p => p.IsAvailable);
        if (availableProvider != null)
        {
            _logger.LogWarning("Default provider '{DefaultProvider}' not available, using '{FallbackProvider}'", 
                defaultProviderName, availableProvider.ProviderName);
            return availableProvider;
        }

        throw new InvalidOperationException("No available LLM providers found");
    }

    public ILLMProvider GetEmbeddingProvider()
    {
        var embeddingProviderName = _configuration.EmbeddingProvider;
        
        if (!string.IsNullOrEmpty(embeddingProviderName) && 
            _providers.TryGetValue(embeddingProviderName, out var embeddingProvider) && 
            embeddingProvider.IsAvailable && 
            embeddingProvider.SupportsEmbeddings)
        {
            return embeddingProvider;
        }

        // Fallback to first available provider that supports embeddings
        var availableEmbeddingProvider = _providers.Values
            .FirstOrDefault(p => p.IsAvailable && p.SupportsEmbeddings);
        
        if (availableEmbeddingProvider != null)
        {
            _logger.LogWarning("Embedding provider '{EmbeddingProvider}' not available, using '{FallbackProvider}'", 
                embeddingProviderName, availableEmbeddingProvider.ProviderName);
            return availableEmbeddingProvider;
        }

        throw new InvalidOperationException("No available embedding providers found");
    }

    public IEnumerable<ILLMProvider> GetAvailableProviders()
    {
        return _providers.Values.Where(p => p.IsAvailable).OrderBy(p => 
        {
            var config = _configuration.Providers.FirstOrDefault(c => c.Name == p.ProviderName);
            return config?.Priority ?? 0;
        }).Reverse();
    }

    private void InitializeProviders()
    {
        foreach (var config in _configuration.Providers.Where(p => p.IsEnabled))
        {
            try
            {
                var provider = CreateProvider(config);
                if (provider != null)
                {
                    _providers.TryAdd(config.Name, provider);
                    _logger.LogInformation("Initialized LLM provider: {ProviderName} ({ProviderType})", 
                        config.Name, config.Type);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize provider: {ProviderName} ({ProviderType})", 
                    config.Name, config.Type);
            }
        }
    }

    private ILLMProvider? CreateProvider(ProviderConfiguration config)
    {
        return config.Type switch
        {
            LLMProviderType.OpenAI => CreateOpenAIProvider(config),
            LLMProviderType.Gemini => CreateGeminiProvider(config),
            LLMProviderType.GitHubModels => CreateGitHubModelsProvider(config),
            _ => throw new NotSupportedException($"Provider type '{config.Type}' is not supported")
        };
    }

    private ILLMProvider CreateOpenAIProvider(ProviderConfiguration config)
    {
        try
        {
            var httpClient = new HttpClient();
            var logger = _serviceProvider.GetService(typeof(ILogger<OpenAIProvider>)) as ILogger<OpenAIProvider> ?? 
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAIProvider>.Instance;
            
            return new OpenAIProvider(httpClient, config, logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OpenAI provider");
            throw;
        }
    }

    private ILLMProvider CreateGeminiProvider(ProviderConfiguration config)
    {
        try
        {
            var httpClient = new HttpClient();
            var logger = _serviceProvider.GetService(typeof(ILogger<GeminiProvider>)) as ILogger<GeminiProvider> ?? 
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<GeminiProvider>.Instance;
            
            return new GeminiProvider(httpClient, config, logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Gemini provider");
            throw;
        }
    }

    private ILLMProvider CreateGitHubModelsProvider(ProviderConfiguration config)
    {
        try
        {
            var httpClient = new HttpClient();
            var logger = _serviceProvider.GetService(typeof(ILogger<GitHubModelsProvider>)) as ILogger<GitHubModelsProvider> ?? 
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubModelsProvider>.Instance;
            
            return new GitHubModelsProvider(httpClient, config, logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GitHub Models provider");
            throw;
        }
    }
}

public class LLMProvidersConfiguration
{
    public string DefaultProvider { get; set; } = string.Empty;
    public string EmbeddingProvider { get; set; } = string.Empty;
    public List<ProviderConfiguration> Providers { get; set; } = new();
    public ProviderSelectionStrategy SelectionStrategy { get; set; } = ProviderSelectionStrategy.Priority;
    public bool EnableFallback { get; set; } = true;
    public int RetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
}

public enum ProviderSelectionStrategy
{
    Priority,           // Use highest priority available provider
    RoundRobin,        // Rotate between available providers
    LoadBalancing,     // Balance load based on response times
    CostOptimized      // Select based on cost per token
}

public class ProviderMetrics
{
    public string ProviderName { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public DateTime LastUsed { get; set; }
    public bool IsHealthy => FailureCount == 0 || (double)SuccessCount / RequestCount > 0.9;
}