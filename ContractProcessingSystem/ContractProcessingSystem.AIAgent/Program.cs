using ContractProcessingSystem.AIAgent.Services;
using ContractProcessingSystem.Shared.Extensions;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add Redis for caching
builder.AddRedisClient("cache");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Register LLM providers
builder.Services.AddLLMProviders(builder.Configuration);

// Configure Semantic Kernel
var aiProvider = builder.Configuration["AI:Provider"] ?? "Gemini";
builder.Services.AddSingleton<Kernel>(provider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
    if (aiProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
    {
        var openAIEndpoint = builder.Configuration["AI:OpenAI:Endpoint"] ?? "https://your-openai-endpoint.openai.azure.com";
        var openAIApiKey = builder.Configuration["AI:OpenAI:ApiKey"] ?? "your-api-key";
        var openAIModel = builder.Configuration["AI:OpenAI:Model"] ?? "gpt-4";
        
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: openAIModel,
            endpoint: openAIEndpoint,
            apiKey: openAIApiKey);
    }
    else if (aiProvider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
    {
        // For Gemini, we'll use a basic kernel since SK doesn't have native Gemini support
        // The LLMProviderFactory will handle Gemini calls directly
        var geminiApiKey = builder.Configuration["AI:Gemini:ApiKey"] ?? "";
        // Create a minimal kernel for now - actual LLM calls will use LLMProviderFactory
    }
    
    return kernelBuilder.Build();
});

// Register application services
builder.Services.AddScoped<IAIAgentService, AIAgentService>();
builder.Services.AddScoped<AIAgentService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();

// Configure MCP Server using official ModelContextProtocol.AspNetCore package
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

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

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Map Aspire default endpoints
app.MapDefaultEndpoints();

// Map MCP endpoint using official package
app.MapMcp();

app.Run();
