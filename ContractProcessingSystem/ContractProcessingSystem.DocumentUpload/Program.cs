using ContractProcessingSystem.DocumentUpload.Data;
using ContractProcessingSystem.DocumentUpload.Services;
using ContractProcessingSystem.DocumentUpload.MCPTools;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add Azure Blob Storage
builder.AddAzureBlobClient("blobs");

// Add SQL Server
builder.AddSqlServerDbContext<DocumentContext>("database");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register application services
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<DocumentUploadService>();

// Register MCP tools
builder.Services.AddScoped<DocumentUploadTools>();

// Configure MCP Server using official ModelContextProtocol.AspNetCore package
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(DocumentUploadTools).Assembly);

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

// Ensure database is created and initialize blob storage
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DocumentContext>();
    await context.Database.EnsureCreatedAsync();

    // Initialize blob storage container
    var documentService = scope.ServiceProvider.GetRequiredService<IDocumentUploadService>();
    try
    {
        await documentService.EnsureBlobStorageInitializedAsync();
        app.Logger.LogInformation("Blob storage initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to initialize blob storage during startup - will be created on first upload");
    }
}

app.Run();
