using ContractProcessingSystem.Shared.AI;
using ContractProcessingSystem.Shared.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("🤖 Contract Processing System - Multi-Provider AI Demo");
Console.WriteLine("=====================================================\n");

try
{
    // Setup services
    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
    
    // Configure providers programmatically (since we don't have config files here)
    services.AddLLMProvidersWithConfiguration(config =>
    {
        config.DefaultProvider = "OpenAI";
        config.EmbeddingProvider = "OpenAI";
        config.EnableFallback = true;

        // Add OpenAI provider (you would set real API keys)
        config.Providers.Add(new ProviderConfiguration
        {
            Type = LLMProviderType.OpenAI,
            Name = "OpenAI",
            ApiKey = "demo-key", // Replace with real key
            DefaultChatModel = "gpt-3.5-turbo",
            DefaultEmbeddingModel = "text-embedding-3-small",
            IsEnabled = true,
            Priority = 1
        });

        // Add Gemini provider
        config.Providers.Add(new ProviderConfiguration
        {
            Type = LLMProviderType.Gemini,
            Name = "Gemini",
            ApiKey = "demo-key", // Replace with real key
            DefaultChatModel = "gemini-1.5-flash",
            DefaultEmbeddingModel = "text-embedding-004",
            IsEnabled = true,
            Priority = 2
        });
    });

    var serviceProvider = services.BuildServiceProvider();
    var factory = serviceProvider.GetRequiredService<ILLMProviderFactory>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    // Demo 1: List available providers
    Console.WriteLine("📋 Available AI Providers:");
    Console.WriteLine("---------------------------");
    
    var providers = factory.GetAvailableProviders();
    foreach (var provider in providers)
    {
        Console.WriteLine($"✅ {provider.ProviderName}");
        Console.WriteLine($"   - Supports Chat: {provider.SupportsChat}");
        Console.WriteLine($"   - Supports Embeddings: {provider.SupportsEmbeddings}");
        Console.WriteLine($"   - Available: {provider.IsAvailable}");
        Console.WriteLine();
    }

    // Demo 2: Get providers by type
    Console.WriteLine("🎯 Provider Selection:");
    Console.WriteLine("----------------------");
    
    var defaultProvider = factory.GetDefaultProvider();
    Console.WriteLine($"Default Provider: {defaultProvider.ProviderName}");
    
    var embeddingProvider = factory.GetEmbeddingProvider();
    Console.WriteLine($"Embedding Provider: {embeddingProvider.ProviderName}");
    
    Console.WriteLine();

    // Demo 3: Show configuration flexibility
    Console.WriteLine("⚙️ Configuration Flexibility:");
    Console.WriteLine("------------------------------");
    Console.WriteLine("✅ Provider abstraction implemented");
    Console.WriteLine("✅ Factory pattern for provider management");
    Console.WriteLine("✅ Configuration-driven provider selection");
    Console.WriteLine("✅ Fallback mechanism support");
    Console.WriteLine("✅ Priority-based provider ordering");
    Console.WriteLine("✅ Extensible for new AI providers");
    
    Console.WriteLine();
    Console.WriteLine("🎉 Multi-Provider AI System Successfully Implemented!");
    Console.WriteLine();
    Console.WriteLine("📝 Next Steps:");
    Console.WriteLine("   1. Set real API keys in configuration");
    Console.WriteLine("   2. Test with actual API calls");
    Console.WriteLine("   3. Integrate with contract processing services");
    Console.WriteLine("   4. Add error handling and retry logic");
    Console.WriteLine("   5. Create comprehensive test suite");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();