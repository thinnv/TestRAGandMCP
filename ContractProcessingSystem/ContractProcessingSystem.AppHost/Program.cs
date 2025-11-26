var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure Components
// Use SQL Server container - requires Docker Desktop to be running
// If Docker is not available, you can use SQL Server Express or LocalDB
// and configure the connection string in appsettings.json
var sqlServer = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Persistent);

var database = sqlServer.AddDatabase("database");

var redis = builder.AddRedis("cache")
    .WithDataVolume();

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();

var blobs = storage.AddBlobs("blobs");

// Qdrant Vector Database
var qdrant = builder.AddContainer("qdrant", "qdrant/qdrant", "v1.16.0")
    .WithEndpoint(6333, targetPort: 6333, name: "rest")
    .WithEndpoint(6334, targetPort: 6334, name: "grpc")
    .WithVolume("qdrant-storage", "/qdrant/storage")
    .WithLifetime(ContainerLifetime.Persistent);

// Core Services
var documentUpload = builder.AddProject<Projects.ContractProcessingSystem_DocumentUpload>("document-upload")
    .WithReference(database)
    .WithReference(blobs);

var documentParser = builder.AddProject<Projects.ContractProcessingSystem_DocumentParser>("document-parser")
    .WithReference(documentUpload)
    .WithEnvironment("Services__DocumentUpload", documentUpload.GetEndpoint("https"));

var embeddingService = builder.AddProject<Projects.ContractProcessingSystem_EmbeddingService>("embedding-service")
    .WithReference(redis)
    .WithReference(documentParser)
    .WithEnvironment("Services__DocumentParser", documentParser.GetEndpoint("https"));

var vectorService = builder.AddProject<Projects.ContractProcessingSystem_VectorService>("vector-service")
    .WithReference(redis)
    .WithReference(embeddingService)
    .WithEnvironment("Qdrant__Host", "localhost")
    .WithEnvironment("Qdrant__Port", "6334")
    .WithEnvironment("Services__EmbeddingService", embeddingService.GetEndpoint("https"));

var queryService = builder.AddProject<Projects.ContractProcessingSystem_QueryService>("query-service")
    .WithReference(vectorService)
    .WithReference(embeddingService)
    .WithReference(documentUpload)
    .WithEnvironment("Services__VectorService", vectorService.GetEndpoint("https"))
    .WithEnvironment("Services__EmbeddingService", embeddingService.GetEndpoint("https"))
    .WithEnvironment("Services__DocumentUpload", documentUpload.GetEndpoint("https"));

var aiAgent = builder.AddProject<Projects.ContractProcessingSystem_AIAgent>("ai-agent")
    .WithReference(redis)
    .WithReference(documentUpload)
    .WithReference(documentParser)
    .WithReference(embeddingService)
    .WithReference(vectorService)
    .WithReference(queryService)
    .WithEnvironment("Services__DocumentUpload", documentUpload.GetEndpoint("https"))
    .WithEnvironment("Services__DocumentParser", documentParser.GetEndpoint("https"))
    .WithEnvironment("Services__EmbeddingService", embeddingService.GetEndpoint("https"))
    .WithEnvironment("Services__VectorService", vectorService.GetEndpoint("https"))
    .WithEnvironment("Services__QueryService", queryService.GetEndpoint("https"));

// API Gateway (future enhancement)
// var gateway = builder.AddProject<Projects.ContractProcessingSystem_Gateway>("gateway")
//     .WithReference(aiAgent)
//     .WithReference(documentUpload)
//     .WithReference(queryService);

var app = builder.Build();

app.Run();
