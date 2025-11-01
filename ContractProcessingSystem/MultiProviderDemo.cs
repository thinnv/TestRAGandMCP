using ContractProcessingSystem.Shared.AI;
using ContractProcessingSystem.Shared.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace ContractProcessingSystem.Demo;

public class MultiProviderDemo
{
    public static async Task RunDemo()
    {
        Console.WriteLine("🤖 Contract Processing System - Multi-Provider AI Demo");
        Console.WriteLine("=====================================================");

        // Setup configuration and services
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();
        
        // Add logging
        services.AddLogging(builder => builder.AddConsole());
        
        // Add LLM providers from configuration
        services.AddLLMProviders(configuration);
        
        var serviceProvider = services.BuildServiceProvider();
        var providerFactory = serviceProvider.GetRequiredService<ILLMProviderFactory>();
        var logger = serviceProvider.GetRequiredService<ILogger<MultiProviderDemo>>();

        try
        {
            // Demo 1: List available providers
            await DemoAvailableProviders(providerFactory, logger);
            
            // Demo 2: Test text generation with default provider
            await DemoTextGeneration(providerFactory, logger);
            
            // Demo 3: Test embeddings with embedding provider
            await DemoEmbeddings(providerFactory, logger);
            
            // Demo 4: Test provider fallback
            await DemoProviderFallback(providerFactory, logger);
            
            // Demo 5: Chat with different providers
            await DemoChatResponses(providerFactory, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Demo failed");
        }
    }

    private static async Task DemoAvailableProviders(ILLMProviderFactory factory, ILogger logger)
    {
        Console.WriteLine("\n📋 Available AI Providers:");
        Console.WriteLine("---------------------------");
        
        var providers = factory.GetAvailableProviders();
        foreach (var provider in providers)
        {
            Console.WriteLine($"✅ {provider.ProviderName}");
            Console.WriteLine($"   - Supports Chat: {provider.SupportsChat}");
            Console.WriteLine($"   - Supports Embeddings: {provider.SupportsEmbeddings}");
            Console.WriteLine($"   - Available: {provider.IsAvailable}");
        }
    }

    private static async Task DemoTextGeneration(ILLMProviderFactory factory, ILogger logger)
    {
        Console.WriteLine("\n🤖 Text Generation Demo:");
        Console.WriteLine("-------------------------");
        
        try
        {
            var provider = factory.GetDefaultProvider();
            Console.WriteLine($"Using provider: {provider.ProviderName}");
            
            var prompt = "Explain what a software contract is in one sentence.";
            Console.WriteLine($"Prompt: {prompt}");
            
            var response = await provider.GenerateTextAsync(prompt, new LLMGenerationOptions
            {
                MaxTokens = 100,
                Temperature = 0.7f
            });
            
            Console.WriteLine($"Response: {response}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Text generation failed");
        }
    }

    private static async Task DemoEmbeddings(ILLMProviderFactory factory, ILogger logger)
    {
        Console.WriteLine("\n🔍 Embeddings Demo:");
        Console.WriteLine("-------------------");
        
        try
        {
            var provider = factory.GetEmbeddingProvider();
            Console.WriteLine($"Using provider: {provider.ProviderName}");
            
            var texts = new[]
            {
                "This is a software development contract.",
                "The payment terms are net 30 days.",
                "Intellectual property rights are retained by the client."
            };
            
            Console.WriteLine("Generating embeddings for contract texts...");
            
            var embeddings = await provider.GenerateEmbeddingsAsync(texts);
            
            Console.WriteLine($"Generated {embeddings.Length} embeddings");
            for (int i = 0; i < embeddings.Length; i++)
            {
                Console.WriteLine($"Text {i + 1}: {embeddings[i].Length} dimensions");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Embeddings generation failed");
        }
    }

    private static async Task DemoProviderFallback(ILLMProviderFactory factory, ILogger logger)
    {
        Console.WriteLine("\n🔄 Provider Fallback Demo:");
        Console.WriteLine("---------------------------");
        
        try
        {
            // Try to get a specific provider
            var providers = factory.GetAvailableProviders().ToList();
            
            foreach (var provider in providers)
            {
                Console.WriteLine($"Testing provider: {provider.ProviderName}");
                
                if (provider.SupportsChat)
                {
                    try
                    {
                        var response = await provider.GenerateChatResponseAsync(
                            "Hello, can you help me understand contracts?",
                            "You are a helpful legal assistant.",
                            new LLMGenerationOptions { MaxTokens = 50 });
                        
                        Console.WriteLine($"✅ Success: {response.Substring(0, Math.Min(response.Length, 100))}...");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Failed: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("⏭️ Skipped: Does not support chat");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Provider fallback demo failed");
        }
    }

    private static async Task DemoChatResponses(ILLMProviderFactory factory, ILogger logger)
    {
        Console.WriteLine("\n💬 Chat Response Comparison:");
        Console.WriteLine("----------------------------");
        
        var question = "What are the key elements of a software development contract?";
        var systemPrompt = "You are a legal expert specializing in software contracts.";
        
        Console.WriteLine($"Question: {question}");
        Console.WriteLine();
        
        var providers = factory.GetAvailableProviders().Where(p => p.SupportsChat);
        
        foreach (var provider in providers)
        {
            try
            {
                Console.WriteLine($"🤖 {provider.ProviderName} Response:");
                Console.WriteLine("----------------------------------------");
                
                var response = await provider.GenerateChatResponseAsync(
                    question,
                    systemPrompt,
                    new LLMGenerationOptions 
                    { 
                        MaxTokens = 200,
                        Temperature = 0.7f
                    });
                
                Console.WriteLine(response);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine();
            }
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.MultiProvider.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}

// Example of programmatic configuration
public class ProgrammaticProviderDemo
{
    public static async Task RunProgrammaticDemo(string openAIKey, string geminiKey)
    {
        Console.WriteLine("🛠️ Programmatic Provider Configuration Demo");
        Console.WriteLine("============================================");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());

        // Configure providers programmatically
        services.AddLLMProvidersWithConfiguration(config =>
        {
            config.DefaultProvider = "OpenAI";
            config.EmbeddingProvider = "OpenAI";
            config.EnableFallback = true;

            // Add OpenAI
            config.Providers.Add(new ProviderConfiguration
            {
                Type = LLMProviderType.OpenAI,
                Name = "OpenAI",
                ApiKey = openAIKey,
                DefaultChatModel = "gpt-3.5-turbo",
                DefaultEmbeddingModel = "text-embedding-3-small",
                IsEnabled = true,
                Priority = 1
            });

            // Add Gemini as fallback
            config.Providers.Add(new ProviderConfiguration
            {
                Type = LLMProviderType.Gemini,
                Name = "Gemini",
                ApiKey = geminiKey,
                DefaultChatModel = "gemini-1.5-flash",
                DefaultEmbeddingModel = "text-embedding-004",
                IsEnabled = true,
                Priority = 2
            });
        });

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<ILLMProviderFactory>();
        
        // Test the configuration
        var defaultProvider = factory.GetDefaultProvider();
        Console.WriteLine($"Default provider: {defaultProvider.ProviderName}");
        
        var embeddingProvider = factory.GetEmbeddingProvider();
        Console.WriteLine($"Embedding provider: {embeddingProvider.ProviderName}");
        
        Console.WriteLine($"Available providers: {string.Join(", ", factory.GetAvailableProviders().Select(p => p.ProviderName))}");
    }
}