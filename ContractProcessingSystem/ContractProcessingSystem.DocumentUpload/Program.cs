using ContractProcessingSystem.DocumentUpload.Data;
using ContractProcessingSystem.DocumentUpload.Services;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add Azure Blob Storage
builder.AddAzureBlobClient("storage");

// Add SQL Server
builder.AddSqlServerDbContext<DocumentContext>("database");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register application services
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<DocumentUploadService>();

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

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DocumentContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
