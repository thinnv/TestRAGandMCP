using ContractProcessingSystem.VectorService.Services;
using Qdrant.Client;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// Configure Qdrant client
var qdrantHost = builder.Configuration["Qdrant:Host"] ?? "localhost";
var qdrantPort = int.Parse(builder.Configuration["Qdrant:Port"] ?? "6334");
var useHttps = bool.Parse(builder.Configuration["Qdrant:UseHttps"] ?? "false");

builder.Services.AddSingleton<QdrantClient>(provider =>
{
    var client = new QdrantClient(
        host: qdrantHost,
        port: qdrantPort,
        https: useHttps
    );
    return client;
});

// Register application services
builder.Services.AddScoped<IVectorService, QdrantVectorService>();
builder.Services.AddScoped<QdrantVectorService>();

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
