using ContractProcessingSystem.DocumentParser.Services;
using ContractProcessingSystem.Shared.Extensions;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Register LLM providers
builder.Services.AddLLMProviders(builder.Configuration);

// Configure Semantic Kernel with multiple AI provider support
var aiProvider = builder.Configuration["AI:Provider"] ?? "Gemini";

builder.Services.AddSingleton<Kernel>(provider =>
{
    var serviceLogger = provider.GetRequiredService<ILogger<Program>>();
    var kernelBuilder = Kernel.CreateBuilder();
    
    try
    {
        serviceLogger.LogInformation("=== Configuring Semantic Kernel with {Provider} provider ===", aiProvider);
        serviceLogger.LogInformation("Configuration check - Provider: {Provider}, Has API Key: {HasKey}", 
            aiProvider, 
            !string.IsNullOrEmpty(builder.Configuration[$"AI:{aiProvider}:ApiKey"]));

        bool configuredSuccessfully = false;

        switch (aiProvider.ToLower())
        {
            case "openai":
                try
                {
                    ConfigureOpenAI(kernelBuilder, builder.Configuration, serviceLogger);
                    configuredSuccessfully = true;
                }
                catch (Exception ex)
                {
                    serviceLogger.LogError(ex, "Failed to configure OpenAI provider");
                    throw;
                }
                break;
                
            case "azureopenai":
                try
                {
                    ConfigureAzureOpenAI(kernelBuilder, builder.Configuration, serviceLogger);
                    configuredSuccessfully = true;
                }
                catch (Exception ex)
                {
                    serviceLogger.LogError(ex, "Failed to configure Azure OpenAI provider");
                    throw;
                }
                break;
                
            case "gemini":
            default:
                try
                {
                    ConfigureGemini(kernelBuilder, builder.Configuration, serviceLogger);
                    configuredSuccessfully = true;
                }
                catch (Exception ex)
                {
                    serviceLogger.LogError(ex, "Failed to configure Gemini provider");
                    // Don't throw for Gemini, allow graceful degradation
                    serviceLogger.LogWarning("Gemini configuration failed, will use rule-based processing");
                }
                break;
        }
        
        serviceLogger.LogInformation("Building Semantic Kernel... Configuration successful: {Success}", configuredSuccessfully);
        var kernel = kernelBuilder.Build();
        
        // Verify that required services are registered
        var hasTextGeneration = kernel.Services.GetService(typeof(Microsoft.SemanticKernel.TextGeneration.ITextGenerationService)) != null;
        var hasChatCompletion = kernel.Services.GetService(typeof(Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService)) != null;
        
        serviceLogger.LogInformation("=== Kernel Built Successfully ===");
        serviceLogger.LogInformation("Services registered - TextGeneration: {TextGen}, ChatCompletion: {Chat}", 
            hasTextGeneration, hasChatCompletion);
        
        if (!hasTextGeneration && !hasChatCompletion)
        {
            serviceLogger.LogError("CRITICAL: No AI services registered in kernel - AI functionality will not work!");
            serviceLogger.LogWarning("Service will fall back to rule-based processing for all operations");
        }
        else
        {
            serviceLogger.LogInformation("AI services successfully registered and available");
        }
        
        return kernel;
    }
    catch (Exception ex)
    {
        serviceLogger.LogError(ex, "FATAL: Failed to configure Semantic Kernel with {Provider} - Exception: {Message}", 
            aiProvider, ex.Message);
        serviceLogger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
        serviceLogger.LogWarning("Returning minimal kernel - will use rule-based processing");
        
        // Return a minimal kernel so the service doesn't crash
        return Kernel.CreateBuilder().Build();
    }
});

// Register application services with error handling
try
{
    builder.Services.AddScoped<DocumentTextExtractor>();
    builder.Logging.AddConsole();
    
    builder.Services.AddScoped<IDocumentParsingService>(provider =>
    {
        var svcLogger = provider.GetRequiredService<ILogger<DocumentParsingService>>();
        var textExtractor = provider.GetRequiredService<DocumentTextExtractor>();
        var kernel = provider.GetService<Kernel>(); // Allow null kernel
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var configuration = provider.GetRequiredService<IConfiguration>();
        
        return new DocumentParsingService(textExtractor, kernel!, svcLogger, httpClientFactory, configuration);
    });
    
    builder.Services.AddScoped<DocumentParsingService>(provider =>
        provider.GetRequiredService<IDocumentParsingService>() as DocumentParsingService 
        ?? throw new InvalidOperationException("DocumentParsingService not properly registered"));
}
catch (Exception ex)
{
    Console.WriteLine($"Error registering DocumentParser services: {ex.Message}");
    throw; // Re-throw to prevent invalid startup
}

// Register MCP tools
builder.Services.AddScoped<ContractProcessingSystem.DocumentParser.MCPTools.DocumentParserTools>();

// Configure MCP Server using official ModelContextProtocol.AspNetCore package
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(ContractProcessingSystem.DocumentParser.MCPTools.DocumentParserTools).Assembly);

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Validate services during startup
try
{
    using var scope = app.Services.CreateScope();
    
    // Test critical services
    var textExtractor = scope.ServiceProvider.GetService<DocumentTextExtractor>();
    var parsingService = scope.ServiceProvider.GetService<IDocumentParsingService>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    if (textExtractor == null)
        startupLogger.LogWarning("DocumentTextExtractor not properly registered");
    
    if (parsingService == null)
        startupLogger.LogError("IDocumentParsingService not properly registered");
    else
        startupLogger.LogInformation("DocumentParser services successfully registered");
    
    // Test AI connectivity (non-blocking)
    if (parsingService is DocumentParsingService docService)
    {
        try
        {
            var aiWorking = await docService.TestAIConnectionAsync();
            startupLogger.LogInformation("AI connectivity test: {Status}", aiWorking ? "Success" : "Failed (will use rule-based processing)");
        }
        catch (Exception ex)
        {
            startupLogger.LogWarning(ex, "AI connectivity test failed - will use rule-based processing");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Startup validation failed: {ex.Message}");
    // Don't fail startup for non-critical issues
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Parser API V1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Map Aspire default endpoints
app.MapDefaultEndpoints();

// Map MCP endpoint using official package
try
{
    app.MapMcp();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Failed to map MCP endpoints - MCP functionality will not be available");
}

app.Logger.LogInformation("DocumentParser service starting on {Urls}", string.Join(", ", app.Urls));

app.Run();

// Helper methods for configuring different AI providers
static void ConfigureOpenAI(IKernelBuilder kernelBuilder, IConfiguration configuration, ILogger logger)
{
    var apiKey = configuration["AI:OpenAI:ApiKey"];
    var model = configuration["AI:OpenAI:Model"] ?? "gpt-4o-mini";
    
    logger.LogInformation("Attempting to configure OpenAI...");
    logger.LogInformation("Model: {Model}, API Key present: {HasKey}, Key length: {Length}", 
        model, 
        !string.IsNullOrEmpty(apiKey),
        apiKey?.Length ?? 0);
    
    if (string.IsNullOrEmpty(apiKey))
    {
        logger.LogError("OpenAI API key not configured. Set AI:OpenAI:ApiKey in appsettings.json");
        throw new InvalidOperationException("OpenAI API key is required");
    }

    if (apiKey.Length < 20)
    {
        logger.LogError("OpenAI API key appears to be invalid (too short). Length: {Length}", apiKey.Length);
        throw new InvalidOperationException($"OpenAI API key appears invalid (length: {apiKey.Length})");
    }

    logger.LogInformation("Configuring OpenAI with model: {Model}", model);
    
    try
    {
        // Add OpenAI chat completion - this also registers IChatCompletionService
        #pragma warning disable SKEXP0010
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: model,
            apiKey: apiKey);
        #pragma warning restore SKEXP0010
        
        logger.LogInformation("? OpenAI configured successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to add OpenAI chat completion to kernel");
        throw;
    }
}

static void ConfigureAzureOpenAI(IKernelBuilder kernelBuilder, IConfiguration configuration, ILogger logger)
{
    var endpoint = configuration["AI:AzureOpenAI:Endpoint"];
    var apiKey = configuration["AI:AzureOpenAI:ApiKey"];
    var deploymentName = configuration["AI:AzureOpenAI:DeploymentName"] ?? "gpt-4";
    
    logger.LogInformation("Attempting to configure Azure OpenAI - Endpoint: {Endpoint}, Deployment: {Deployment}", 
        endpoint, deploymentName);
    
    if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
    {
        logger.LogError("Azure OpenAI configuration incomplete");
        throw new InvalidOperationException("Azure OpenAI configuration is incomplete");
    }

    try
    {
        #pragma warning disable SKEXP0010
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: deploymentName,
            endpoint: endpoint,
            apiKey: apiKey);
        #pragma warning restore SKEXP0010
        
        logger.LogInformation("Azure OpenAI configured successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to add Azure OpenAI to kernel");
        throw;
    }
}

static void ConfigureGemini(IKernelBuilder kernelBuilder, IConfiguration configuration, ILogger logger)
{
    var geminiApiKey = configuration["AI:Gemini:ApiKey"] ?? "";
    var configuredModel = configuration["AI:Gemini:Model"] ?? "gemini-pro";
    
    logger.LogInformation("Attempting to configure Gemini with configured model: {Model}", configuredModel);
    
    if (string.IsNullOrEmpty(geminiApiKey))
    {
        logger.LogWarning("Gemini API key not configured");
        throw new InvalidOperationException("Gemini API key is required");
    }

    var modelToUse = configuredModel;

    try
    {
        // First, try to validate the configured model
        var isConfiguredModelValid = ValidateGeminiModelAsync(configuredModel, geminiApiKey, logger).GetAwaiter().GetResult();
        
        if (!isConfiguredModelValid)
        {
            logger.LogWarning("Configured model '{ConfiguredModel}' is not available or doesn't support content generation", configuredModel);
            logger.LogInformation("Attempting to find an alternative working model...");
            
            // Only if configured model doesn't work, try to find an alternative
            var workingModel = FindWorkingGeminiModelAsync(geminiApiKey, logger).GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(workingModel))
            {
                logger.LogWarning("Using alternative model '{AlternativeModel}' instead of configured '{ConfiguredModel}'", 
                    workingModel, configuredModel);
                modelToUse = workingModel;
            }
            else
            {
                logger.LogWarning("No alternative model found, will attempt to use configured model '{ConfiguredModel}' anyway", 
                    configuredModel);
            }
        }
        else
        {
            logger.LogInformation("Configured model '{Model}' validated successfully", configuredModel);
        }
        
        #pragma warning disable SKEXP0070
        kernelBuilder.AddGoogleAIGeminiChatCompletion(
            modelId: modelToUse,
            apiKey: geminiApiKey);
        #pragma warning restore SKEXP0070
            
        logger.LogInformation("? Gemini configured successfully with model: {Model}", modelToUse);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to add Gemini to kernel");
        throw;
    }
}

// Helper method to validate a specific Gemini model
static async Task<bool> ValidateGeminiModelAsync(string modelId, string apiKey, ILogger logger)
{
    try
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        
        // Normalize model name (remove "models/" prefix if present)
        var normalizedModel = modelId.StartsWith("models/") ? modelId.Substring(7) : modelId;
        
        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
        
        logger.LogDebug("Validating model '{Model}' against Gemini API", normalizedModel);
        
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Could not fetch available models for validation: {StatusCode}", response.StatusCode);
            return false;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
        
        if (jsonDoc.RootElement.TryGetProperty("models", out var modelsArray))
        {
            foreach (var model in modelsArray.EnumerateArray())
            {
                if (model.TryGetProperty("name", out var nameElement) && 
                    model.TryGetProperty("supportedGenerationMethods", out var methodsElement))
                {
                    var name = nameElement.GetString();
                    if (string.IsNullOrEmpty(name))
                        continue;
                    
                    // Extract model name without "models/" prefix
                    var apiModelName = name.StartsWith("models/") ? name.Substring(7) : name;
                    
                    // Check for exact match or close variant match
                    // For example, "gemini-2.5-flash" should match "gemini-2.0-flash-exp" if exact doesn't exist
                    var isMatch = apiModelName.Equals(normalizedModel, StringComparison.OrdinalIgnoreCase);
                    
                    // Also check for close variants (e.g., gemini-2.5-flash vs gemini-2.0-flash-exp)
                    if (!isMatch && normalizedModel.Contains("2.5-flash", StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = apiModelName.Contains("flash", StringComparison.OrdinalIgnoreCase) &&
                                 (apiModelName.Contains("2.0", StringComparison.OrdinalIgnoreCase) || 
                                  apiModelName.Contains("2.5", StringComparison.OrdinalIgnoreCase));
                    }
                    
                    if (isMatch)
                    {
                        var methods = methodsElement.EnumerateArray().Select(m => m.GetString()).ToList();
                        var supportsGeneration = methods.Contains("generateContent");
                        
                        logger.LogDebug("Model '{Model}' (matched as '{ApiModel}') found in API. Supports generation: {SupportsGeneration}", 
                            normalizedModel, apiModelName, supportsGeneration);
                        
                        return supportsGeneration;
                    }
                }
            }
        }
        
        logger.LogDebug("Model '{Model}' not found in available models list", normalizedModel);
        return false;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Error validating model '{Model}'", modelId);
        return false;
    }
}

// Helper method to find working Gemini model
static async Task<string?> FindWorkingGeminiModelAsync(string apiKey, ILogger logger)
{
    try
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        
        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
        
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Could not fetch available models: {StatusCode}", response.StatusCode);
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
        
        if (jsonDoc.RootElement.TryGetProperty("models", out var modelsArray))
        {
            // Prioritize flash models, especially 2.5 variants, then stable models over preview models
            var preferredModels = new[] 
            { 
                "gemini-2.5-flash",
                "gemini-2.0-flash-exp",
                "gemini-2.0-flash",
                "gemini-1.5-flash", 
                "gemini-1.5-flash-latest",
                "gemini-2.5-pro",
                "gemini-1.5-pro", 
                "gemini-pro" 
            };
            var availableModels = new List<string>();
            
            foreach (var model in modelsArray.EnumerateArray())
            {
                if (model.TryGetProperty("name", out var nameElement) && 
                    model.TryGetProperty("supportedGenerationMethods", out var methodsElement))
                {
                    var name = nameElement.GetString();
                    var methods = methodsElement.EnumerateArray().Select(m => m.GetString()).ToList();
                    
                    if (methods.Contains("generateContent") && !string.IsNullOrEmpty(name))
                    {
                        var modelName = name.StartsWith("models/") ? name.Substring(7) : name;
                        availableModels.Add(modelName);
                        logger.LogDebug("Available model: {Model}", modelName);
                    }
                }
            }
            
            logger.LogInformation("Found {Count} available models with content generation support", availableModels.Count);
            
            // Try preferred models first
            foreach (var preferred in preferredModels)
            {
                var match = availableModels.FirstOrDefault(m => 
                    m.Equals(preferred, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    logger.LogInformation("Found preferred working model: {Model}", match);
                    return match;
                }
            }
            
            // If no preferred model found, try to find any flash model
            var flashModel = availableModels.FirstOrDefault(m => 
                m.Contains("flash", StringComparison.OrdinalIgnoreCase));
            if (flashModel != null)
            {
                logger.LogInformation("Using flash model: {Model}", flashModel);
                return flashModel;
            }
            
            // If no flash model, return first available
            if (availableModels.Any())
            {
                var firstModel = availableModels.First();
                logger.LogInformation("Using first available model: {Model}", firstModel);
                return firstModel;
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error discovering working models");
    }
    
    return null;
}
