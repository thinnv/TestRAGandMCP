var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure Components
var sqlServer = builder.AddSqlServer("database")
    .WithDataVolume()
    .AddDatabase("contractsdb");

var redis = builder.AddRedis("cache")
    .WithDataVolume();

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("blobs");

// Milvus Vector Database
var milvus = builder.AddContainer("milvus", "milvusdb/milvus", "v2.3.3")
    .WithEndpoint(19530, targetPort: 19530, name: "grpc")
    .WithEndpoint(9091, targetPort: 9091, name: "http")
    .WithEnvironment("ETCD_ENDPOINTS", "etcd:2379")
    .WithEnvironment("MINIO_ADDRESS", "minio:9000")
    .WithVolume("milvus-data", "/var/lib/milvus")
    .WaitFor(builder.AddContainer("etcd", "quay.io/coreos/etcd", "v3.5.5")
        .WithEndpoint(2379, targetPort: 2379, name: "client")
        .WithEnvironment("ETCD_AUTO_COMPACTION_MODE", "revision")
        .WithEnvironment("ETCD_AUTO_COMPACTION_RETENTION", "1000")
        .WithEnvironment("ETCD_QUOTA_BACKEND_BYTES", "4294967296")
        .WithEnvironment("ETCD_SNAPSHOT_COUNT", "50000")
        .WithVolume("etcd-data", "/etcd"))
    .WaitFor(builder.AddContainer("minio", "minio/minio", "RELEASE.2023-03-20T20-16-18Z")
        .WithEndpoint(9000, targetPort: 9000, name: "api")
        .WithEndpoint(9001, targetPort: 9001, name: "console")
        .WithEnvironment("MINIO_ACCESS_KEY", "minioadmin")
        .WithEnvironment("MINIO_SECRET_KEY", "minioadmin")
        .WithVolume("minio-data", "/data")
        .WithArgs("server", "/data", "--console-address", ":9001"));

// Core Services
var documentUpload = builder.AddProject<Projects.ContractProcessingSystem_DocumentUpload>("document-upload")
    .WithReference(sqlServer)
    .WithReference(storage)
    .WithEnvironment("ConnectionStrings__database", sqlServer)
    .WithEnvironment("ConnectionStrings__storage", storage);

var documentParser = builder.AddProject<Projects.ContractProcessingSystem_DocumentParser>("document-parser")
    .WithReference(documentUpload)
    .WithEnvironment("Services__DocumentUpload", documentUpload.GetEndpoint("https"));

var embeddingService = builder.AddProject<Projects.ContractProcessingSystem_EmbeddingService>("embedding-service")
    .WithReference(redis)
    .WithReference(documentParser)
    .WithEnvironment("ConnectionStrings__cache", redis)
    .WithEnvironment("Services__DocumentParser", documentParser.GetEndpoint("https"));

var vectorService = builder.AddProject<Projects.ContractProcessingSystem_VectorService>("vector-service")
    .WithReference(redis)
    .WithReference(embeddingService)
    .WithEnvironment("ConnectionStrings__cache", redis)
    .WithEnvironment("Milvus__Host", "localhost")
    .WithEnvironment("Milvus__Port", "19530")
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
    .WithEnvironment("ConnectionStrings__cache", redis)
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
