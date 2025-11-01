using ContractProcessingSystem.DocumentParser.Services;
using ContractProcessingSystem.Shared.Extensions;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Register LLM providers instead of Azure OpenAI directly
builder.Services.AddLLMProviders(builder.Configuration);

// Configure Semantic Kernel - keep for now but will be replaced with provider abstraction
var openAIEndpoint = builder.Configuration["OpenAI:Endpoint"] ?? "https://your-openai-endpoint.openai.azure.com";
var openAIApiKey = builder.Configuration["OpenAI:ApiKey"] ?? "your-api-key";
var openAIDeploymentName = builder.Configuration["OpenAI:DeploymentName"] ?? "gpt-4";

builder.Services.AddSingleton<Kernel>(provider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    // TODO: Replace with provider abstraction
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: openAIDeploymentName,
        endpoint: openAIEndpoint,
        apiKey: openAIApiKey);
    
    return kernelBuilder.Build();
});

// Register application services
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<IDocumentParsingService, DocumentParsingService>();

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
