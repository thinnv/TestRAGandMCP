using ContractProcessingSystem.EmbeddingService.Services;
using ContractProcessingSystem.Shared.Extensions;
using Microsoft.SemanticKernel;

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

// Register LLM providers instead of Azure OpenAI directly
builder.Services.AddLLMProviders(builder.Configuration);

// Keep Semantic Kernel for now (TODO: Replace with provider abstraction)
var openAIEndpoint = builder.Configuration["OpenAI:Endpoint"] ?? "https://your-openai-endpoint.openai.azure.com";
var openAIApiKey = builder.Configuration["OpenAI:ApiKey"] ?? "your-api-key";
var embeddingDeploymentName = builder.Configuration["OpenAI:EmbeddingDeploymentName"] ?? "text-embedding-ada-002";

// Configure Semantic Kernel for embeddings
builder.Services.AddSingleton<Kernel>(provider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates
    kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
        deploymentName: embeddingDeploymentName,
        endpoint: openAIEndpoint,
        apiKey: openAIApiKey);
#pragma warning restore SKEXP0010
    
    return kernelBuilder.Build();
});

// Register application services  
builder.Services.AddScoped<IEmbeddingService, ContractProcessingSystem.EmbeddingService.Services.EmbeddingService>();
builder.Services.AddScoped<EmbeddingBatchProcessor>();

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

app.Run();
