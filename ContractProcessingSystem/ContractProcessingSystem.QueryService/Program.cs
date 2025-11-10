using ContractProcessingSystem.QueryService.Services;
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

// Configure Semantic Kernel for query processing
builder.Services.AddSingleton<Kernel>(provider =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    
    // Use Gemini API key from configuration
    var geminiApiKey = builder.Configuration["AI:Gemini:ApiKey"] ?? "";
    
    if (!string.IsNullOrEmpty(geminiApiKey))
    {
#pragma warning disable SKEXP0070
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: "gpt-3.5-turbo",
            apiKey: geminiApiKey);
#pragma warning restore SKEXP0070
    }
    
    return kernelBuilder.Build();
});

// Register application services
builder.Services.AddScoped<IQueryService, QueryService>();
builder.Services.AddScoped<QueryService>();

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
