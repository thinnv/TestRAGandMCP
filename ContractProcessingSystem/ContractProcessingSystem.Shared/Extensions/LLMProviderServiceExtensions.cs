using ContractProcessingSystem.Shared.AI;
using ContractProcessingSystem.Shared.AI.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ContractProcessingSystem.Shared.Extensions;

public static class LLMProviderServiceExtensions
{
    public static IServiceCollection AddLLMProviders(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure LLM providers from appsettings
        var providersConfig = new LLMProvidersConfiguration();
        var section = configuration.GetSection("LLMProviders");
        
        // Manual binding instead of using Bind extension
        providersConfig.DefaultProvider = section["DefaultProvider"] ?? "";
        providersConfig.EmbeddingProvider = section["EmbeddingProvider"] ?? "";
        
        if (Enum.TryParse<ProviderSelectionStrategy>(section["SelectionStrategy"], out var strategy))
            providersConfig.SelectionStrategy = strategy;
            
        if (bool.TryParse(section["EnableFallback"], out var enableFallback))
            providersConfig.EnableFallback = enableFallback;
            
        if (int.TryParse(section["RetryAttempts"], out var retryAttempts))
            providersConfig.RetryAttempts = retryAttempts;
            
        // Load providers from configuration
        var providersSection = section.GetSection("Providers");
        foreach (var providerSection in providersSection.GetChildren())
        {
            var provider = new ProviderConfiguration();
            if (Enum.TryParse<LLMProviderType>(providerSection["Type"], out var type))
                provider.Type = type;
            provider.Name = providerSection["Name"] ?? "";
            provider.ApiKey = providerSection["ApiKey"] ?? "";
            provider.Endpoint = providerSection["Endpoint"];
            provider.DefaultChatModel = providerSection["DefaultChatModel"] ?? "";
            provider.DefaultEmbeddingModel = providerSection["DefaultEmbeddingModel"] ?? "";
            if (bool.TryParse(providerSection["IsEnabled"], out var isEnabled))
                provider.IsEnabled = isEnabled;
            if (int.TryParse(providerSection["Priority"], out var priority))
                provider.Priority = priority;
                
            providersConfig.Providers.Add(provider);
        }
        
        services.AddSingleton(providersConfig);

        // Register the provider factory
        services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();

        // Register HTTP client for providers
        services.AddSingleton<HttpClient>();

        return services;
    }

    public static IServiceCollection AddLLMProvidersWithConfiguration(
        this IServiceCollection services, 
        Action<LLMProvidersConfiguration> configureProviders)
    {
        var configuration = new LLMProvidersConfiguration();
        configureProviders(configuration);
        services.AddSingleton(configuration);

        // Register the provider factory
        services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();

        // Register HTTP client factory for providers
        services.AddSingleton<HttpClient>();

        return services;
    }

    /// <summary>
    /// Creates a default configuration with OpenAI and Gemini providers
    /// </summary>
    public static LLMProvidersConfiguration CreateDefaultConfiguration(
        string openAIApiKey, 
        string? geminiApiKey = null)
    {
        var config = new LLMProvidersConfiguration
        {
            DefaultProvider = "OpenAI",
            EmbeddingProvider = "OpenAI",
            SelectionStrategy = ProviderSelectionStrategy.Priority,
            EnableFallback = true,
            RetryAttempts = 3,
            RetryDelay = TimeSpan.FromSeconds(1)
        };

        // Add OpenAI provider
        config.Providers.Add(new ProviderConfiguration
        {
            Type = LLMProviderType.OpenAI,
            Name = "OpenAI",
            ApiKey = openAIApiKey,
            DefaultChatModel = "gpt-3.5-turbo",
            DefaultEmbeddingModel = "text-embedding-3-small",
            IsEnabled = true,
            Priority = 1
        });

        // Add Gemini provider if API key is provided
        if (!string.IsNullOrEmpty(geminiApiKey))
        {
            config.Providers.Add(new ProviderConfiguration
            {
                Type = LLMProviderType.Gemini,
                Name = "Gemini",
                ApiKey = geminiApiKey,
                DefaultChatModel = "gemini-1.5-flash",
                DefaultEmbeddingModel = "text-embedding-004",
                IsEnabled = true,
                Priority = 2
            });

            // Set Gemini as fallback
            if (config.EnableFallback)
            {
                config.EmbeddingProvider = "OpenAI"; // Keep OpenAI for embeddings
            }
        }

        return config;
    }
}