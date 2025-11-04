using ContractProcessingSystem.VectorService.Services;
using Milvus.Client;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

// Configure Milvus client
var milvusHost = builder.Configuration["Milvus:Host"] ?? "localhost";
var milvusPort = int.Parse(builder.Configuration["Milvus:Port"] ?? "19530");

builder.Services.AddSingleton<MilvusClient>(provider =>
{
    var client = new MilvusClient(milvusHost, milvusPort);
    return client;
});

// Register application services
builder.Services.AddScoped<IVectorService, MilvusVectorService>();
builder.Services.AddScoped<MilvusVectorService>();

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
