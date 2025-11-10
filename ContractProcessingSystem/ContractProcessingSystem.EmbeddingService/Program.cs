using ContractProcessingSystem.EmbeddingService.Services;
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
builder.Services.AddMemoryCache();

// Register LLM providers
builder.Services.AddLLMProviders(builder.Configuration);

// Configure AI provider from appsettings (Gemini by default)
var aiProvider = builder.Configuration["AI:Provider"] ?? "Gemini";
var geminiApiKey = builder.Configuration["AI:Gemini:ApiKey"] ?? "";

// Register ITextEmbeddingGenerationService explicitly
#pragma warning disable SKEXP0010
builder.Services.AddOpenAITextEmbeddingGeneration(
    modelId: "text-embedding-ada-002",
    apiKey: geminiApiKey);
#pragma warning restore SKEXP0010

// Configure Semantic Kernel for embeddings
builder.Services.AddSingleton<Kernel>(provider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
    // Get the embedding service from DI
#pragma warning disable SKEXP0001
    var embeddingService = provider.GetRequiredService<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>();
#pragma warning restore SKEXP0001
    kernelBuilder.Services.AddSingleton(embeddingService);
    
    return kernelBuilder.Build();
});

// Register application services  
builder.Services.AddScoped<IEmbeddingService, ContractProcessingSystem.EmbeddingService.Services.EmbeddingService>();
builder.Services.AddScoped<ContractProcessingSystem.EmbeddingService.Services.EmbeddingService>();
builder.Services.AddScoped<EmbeddingBatchProcessor>();

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
