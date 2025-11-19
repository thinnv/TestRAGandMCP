using ContractProcessingSystem.DocumentParser.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;

namespace ContractProcessingSystem.DocumentParser.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParsingController : ControllerBase
{
    private readonly IDocumentParsingService _parsingService;
    private readonly ILogger<ParsingController> _logger;

    public ParsingController(
        IDocumentParsingService parsingService,
        ILogger<ParsingController> logger)
    {
        _parsingService = parsingService;
        _logger = logger;
    }

    [HttpPost("{documentId:guid}/parse")]
    public async Task<ActionResult<ContractMetadata>> ParseDocument(Guid documentId)
    {
        try
        {
            var metadata = await _parsingService.ParseDocumentAsync(documentId);
            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse document {DocumentId}", documentId);
            return StatusCode(500, "Internal server error occurred while parsing document");
        }
    }

    [HttpPost("{documentId:guid}/chunk")]
    public async Task<ActionResult<List<ContractChunk>>> ChunkDocument(Guid documentId)
    {
        try
        {
            var chunks = await _parsingService.ChunkDocumentAsync(documentId);
            return Ok(chunks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to chunk document {DocumentId}", documentId);
            return StatusCode(500, "Internal server error occurred while chunking document");
        }
    }

    [HttpGet("{documentId:guid}/status")]
    public async Task<ActionResult<ProcessingStatus>> GetParsingStatus(Guid documentId)
    {
        var status = await _parsingService.GetParsingStatusAsync(documentId);
        return Ok(status);
    }

    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new { Service = "DocumentParser", Status = "Healthy", Timestamp = DateTime.UtcNow });
    }

    [HttpGet("test-ai")]
    public async Task<ActionResult> TestAI()
    {
        try
        {
            var testService = HttpContext.RequestServices.GetRequiredService<IDocumentParsingService>() as DocumentParsingService;
            
            // Test a simple prompt to verify AI connectivity
            var testResult = await testService!.TestAIConnectionAsync();
            
            return Ok(new { 
                Service = "DocumentParser", 
                AIStatus = testResult ? "Connected" : "Failed",
                Timestamp = DateTime.UtcNow 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI connection test failed");
            return StatusCode(500, new { 
                Service = "DocumentParser", 
                AIStatus = "Failed",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow 
            });
        }
    }

    [HttpGet("diagnostics")]
    public async Task<ActionResult> GetDiagnostics()
    {
        try
        {
            var diagnostics = new Dictionary<string, object>();
            
            // Check Semantic Kernel
            var kernel = HttpContext.RequestServices.GetService<Kernel>();
            diagnostics["SemanticKernel"] = new
            {
                IsConfigured = kernel != null,
                HasServices = kernel?.Services != null
            };

            // Check Configuration
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            diagnostics["Configuration"] = new
            {
                AIProvider = config["AI:Provider"],
                HasGeminiKey = !string.IsNullOrEmpty(config["AI:Gemini:ApiKey"]),
                GeminiModel = config["AI:Gemini:Model"],
                DocumentUploadUrl = config["Services:DocumentUpload"]
            };

            // Check HttpClient Factory
            var httpClientFactory = HttpContext.RequestServices.GetService<IHttpClientFactory>();
            diagnostics["HttpClientFactory"] = new
            {
                IsAvailable = httpClientFactory != null
            };

            // Test AI Connection
            var parsingService = HttpContext.RequestServices.GetService<DocumentParsingService>();
            if (parsingService != null)
            {
                var aiConnected = await parsingService.TestAIConnectionAsync();
                diagnostics["AIConnection"] = new
                {
                    IsConnected = aiConnected
                };
            }
            else
            {
                diagnostics["AIConnection"] = new
                {
                    IsConnected = false,
                    Error = "DocumentParsingService not available"
                };
            }

            return Ok(new
            {
                Service = "DocumentParser",
                Timestamp = DateTime.UtcNow,
                Diagnostics = diagnostics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running diagnostics");
            return StatusCode(500, new
            {
                Service = "DocumentParser",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    [HttpGet("validate-model")]
    public async Task<ActionResult> ValidateModel()
    {
        try
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var currentModel = config["AI:Gemini:Model"];
            var apiKey = config["AI:Gemini:ApiKey"];
            
            _logger.LogInformation("Validating Gemini model: {Model}", currentModel);

            // Test different known working model names
            var knownModels = new[] 
            { 
                "gemini-pro", 
                "gemini-1.5-pro", 
                "gemini-1.5-flash",
                "models/gemini-pro",
                "models/gemini-1.5-pro"
            };

            var results = new Dictionary<string, object>();
            
            foreach (var model in knownModels)
            {
                try
                {
                    using var httpClient = new HttpClient();
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                    
                    var requestBody = new
                    {
                        contents = new[]
                        {
                            new { parts = new[] { new { text = "Test" } } }
                        }
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    
                    var response = await httpClient.PostAsync(url, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    results[model] = new
                    {
                        StatusCode = (int)response.StatusCode,
                        IsSuccess = response.IsSuccessStatusCode,
                        Response = response.IsSuccessStatusCode ? "SUCCESS" : responseContent
                    };
                }
                catch (Exception ex)
                {
                    results[model] = new
                    {
                        StatusCode = 0,
                        IsSuccess = false,
                        Response = ex.Message
                    };
                }
            }

            return Ok(new
            {
                CurrentModel = currentModel,
                ModelValidation = results,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating models");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("list-models")]
    public async Task<ActionResult> ListAvailableModels()
    {
        try
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiKey = config["AI:Gemini:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest(new { Error = "No Gemini API key configured" });
            }

            using var httpClient = new HttpClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
            
            _logger.LogInformation("Fetching available models from: {Url}", url);
            
            var response = await httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch models: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return StatusCode(500, new { 
                    Error = "Failed to fetch available models",
                    StatusCode = response.StatusCode,
                    Response = responseContent
                });
            }

            // Parse the response to extract model information
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
            var models = new List<object>();
            
            if (jsonDoc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                foreach (var model in modelsArray.EnumerateArray())
                {
                    var name = model.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "Unknown";
                    var displayName = model.TryGetProperty("displayName", out var displayNameElement) ? displayNameElement.GetString() : name;
                    var supportedMethods = new List<string>();
                    
                    if (model.TryGetProperty("supportedGenerationMethods", out var methodsElement))
                    {
                        foreach (var method in methodsElement.EnumerateArray())
                        {
                            supportedMethods.Add(method.GetString() ?? "");
                        }
                    }

                    models.Add(new
                    {
                        Name = name,
                        DisplayName = displayName,
                        SupportedMethods = supportedMethods,
                        SupportsGeneration = supportedMethods.Contains("generateContent")
                    });
                }
            }

            return Ok(new
            {
                AvailableModels = models,
                CurrentModel = config["AI:Gemini:Model"],
                TotalModels = models.Count,
                GenerationCapableModels = models.Where(m => 
                    ((List<string>)((dynamic)m).SupportedMethods).Contains("generateContent")).ToList(),
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing available models");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("validate-api-key")]
    public async Task<ActionResult> ValidateApiKey()
    {
        try
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiKey = config["AI:Gemini:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest(new { 
                    IsValid = false,
                    Error = "No Gemini API key configured",
                    Suggestion = "Please set AI:Gemini:ApiKey in appsettings.json"
                });
            }

            // Test the API key by listing models
            using var httpClient = new HttpClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
            
            var response = await httpClient.GetAsync(url);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                return Ok(new {
                    IsValid = false,
                    StatusCode = (int)response.StatusCode,
                    Error = "API key appears to be invalid or has no access to Gemini models",
                    Response = responseContent,
                    Suggestions = new[]
                    {
                        "1. Verify the API key is correct",
                        "2. Check if the key has Gemini API access enabled", 
                        "3. Ensure billing is enabled in Google Cloud Console",
                        "4. Check API quotas and limits"
                    }
                });
            }

            // Parse models to check what's available
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
            var modelCount = 0;
            var generationModels = new List<string>();

            if (jsonDoc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                foreach (var model in modelsArray.EnumerateArray())
                {
                    modelCount++;
                    if (model.TryGetProperty("name", out var nameElement) && 
                        model.TryGetProperty("supportedGenerationMethods", out var methodsElement))
                    {
                        var methods = methodsElement.EnumerateArray().Select(m => m.GetString()).ToList();
                        if (methods.Contains("generateContent"))
                        {
                            generationModels.Add(nameElement.GetString() ?? "Unknown");
                        }
                    }
                }
            }

            return Ok(new {
                IsValid = true,
                ApiKeyStatus = "Valid",
                TotalModels = modelCount,
                GenerationCapableModels = generationModels,
                RecommendedModel = generationModels.FirstOrDefault(),
                CurrentConfiguredModel = config["AI:Gemini:Model"]
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API key");
            return StatusCode(500, new { 
                IsValid = false,
                Error = ex.Message 
            });
        }
    }

    [HttpGet("startup-validation")]
    public async Task<ActionResult> ValidateStartup()
    {
        try
        {
            var validationResults = new Dictionary<string, object>();

            // Check service registration
            var textExtractor = HttpContext.RequestServices.GetService<DocumentTextExtractor>();
            var parsingService = HttpContext.RequestServices.GetService<IDocumentParsingService>();
            var kernel = HttpContext.RequestServices.GetService<Kernel>();
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var httpClientFactory = HttpContext.RequestServices.GetService<IHttpClientFactory>();

            validationResults["ServiceRegistrations"] = new
            {
                DocumentTextExtractor = textExtractor != null,
                DocumentParsingService = parsingService != null,
                SemanticKernel = kernel != null,
                Configuration = config != null,
                HttpClientFactory = httpClientFactory != null
            };

            // Check configuration
            validationResults["Configuration"] = new
            {
                AIProvider = config["AI:Provider"] ?? "Not Set",
                HasGeminiKey = !string.IsNullOrEmpty(config["AI:Gemini:ApiKey"]),
                GeminiModel = config["AI:Gemini:Model"] ?? "Not Set",
                DocumentUploadUrl = config["Services:DocumentUpload"] ?? "Not Set"
            };

            // Test basic service functionality
            if (parsingService != null)
            {
                var mockStatus = await parsingService.GetParsingStatusAsync(Guid.NewGuid());
                validationResults["ServiceFunctionality"] = new
                {
                    CanCreateMockStatus = mockStatus != null
                };
            }

            // Check AI connectivity
            if (parsingService is DocumentParsingService docParsingService)
            {
                var aiConnected = await docParsingService.TestAIConnectionAsync();
                validationResults["AIConnectivity"] = new
                {
                    IsConnected = aiConnected,
                    FallbackMode = !aiConnected ? "Rule-based processing available" : null
                };
            }

            var allValidationsPass = ValidateStartupResults(validationResults);

            return Ok(new
            {
                Service = "DocumentParser",
                StartupValidation = "Complete",
                AllValidationsPass = allValidationsPass,
                ValidationResults = validationResults,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during startup validation");
            return StatusCode(500, new
            {
                Service = "DocumentParser",
                StartupValidation = "Failed",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private static bool ValidateStartupResults(Dictionary<string, object> results)
    {
        try
        {
            // Check service registrations
            var services = (dynamic)results["ServiceRegistrations"];
            if (!(bool)services.DocumentTextExtractor || !(bool)services.DocumentParsingService)
            {
                return false;
            }

            // Configuration is present
            var config = (dynamic)results["Configuration"];
            if (config.AIProvider == "Not Set")
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    [HttpGet("ai-provider")]
    public ActionResult GetCurrentProvider()
    {
        try
        {
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var currentProvider = config["AI:Provider"] ?? "Gemini";
            
            var providerDetails = new Dictionary<string, object>
            {
                ["CurrentProvider"] = currentProvider,
                ["AvailableProviders"] = new[] { "OpenAI", "AzureOpenAI", "Gemini" }
            };

            // Get provider-specific details
            switch (currentProvider.ToLower())
            {
                case "openai":
                    providerDetails["OpenAI"] = new
                    {
                        HasApiKey = !string.IsNullOrEmpty(config["AI:OpenAI:ApiKey"]),
                        Model = config["AI:OpenAI:Model"] ?? "gpt-4o-mini",
                        Endpoint = config["AI:OpenAI:Endpoint"]
                    };
                    break;
                    
                case "azureopenai":
                    providerDetails["AzureOpenAI"] = new
                    {
                        HasApiKey = !string.IsNullOrEmpty(config["AI:AzureOpenAI:ApiKey"]),
                        Endpoint = config["AI:AzureOpenAI:Endpoint"],
                        DeploymentName = config["AI:AzureOpenAI:DeploymentName"]
                    };
                    break;
                    
                case "gemini":
                default:
                    providerDetails["Gemini"] = new
                    {
                        HasApiKey = !string.IsNullOrEmpty(config["AI:Gemini:ApiKey"]),
                        Model = config["AI:Gemini:Model"] ?? "gemini-pro",
                        Endpoint = config["AI:Gemini:Endpoint"]
                    };
                    break;
            }

            return Ok(new
            {
                Service = "DocumentParser",
                ProviderInfo = providerDetails,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting AI provider information");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("ai-services-status")]
    public ActionResult GetAIServicesStatus()
    {
        try
        {
            var kernel = HttpContext.RequestServices.GetService<Kernel>();
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            
            if (kernel == null)
            {
                return Ok(new
                {
                    Status = "Error",
                    Message = "Semantic Kernel not registered",
                    Recommendation = "Check Program.cs configuration"
                });
            }

            // Check for registered AI services
            var textGenService = kernel.Services.GetService(typeof(Microsoft.SemanticKernel.TextGeneration.ITextGenerationService));
            var chatCompletionService = kernel.Services.GetService(typeof(Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService));
            
            var currentProvider = config["AI:Provider"] ?? "Not Set";
            
            return Ok(new
            {
                Service = "DocumentParser",
                KernelStatus = "Registered",
                CurrentProvider = currentProvider,
                Services = new
                {
                    TextGenerationService = new
                    {
                        IsRegistered = textGenService != null,
                        Type = textGenService?.GetType().Name ?? "Not Available"
                    },
                    ChatCompletionService = new
                    {
                        IsRegistered = chatCompletionService != null,
                        Type = chatCompletionService?.GetType().Name ?? "Not Available"
                    }
                },
                Status = (textGenService != null || chatCompletionService != null) ? "Ready" : "Not Ready",
                Recommendations = (textGenService == null && chatCompletionService == null) 
                    ? new[]
                    {
                        "1. Verify AI provider configuration in appsettings.json",
                        "2. Ensure API key is valid",
                        "3. Check that the selected provider is properly configured",
                        "4. Review logs for configuration errors",
                        "5. If no AI is available, the service will use rule-based processing"
                    }
                    : null,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking AI services status");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}