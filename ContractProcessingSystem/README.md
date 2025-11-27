# Contract Processing System - Complete Workspace Documentation

## 🎯 System Overview

The **Contract Processing System** is a comprehensive, microservices-based platform for intelligent contract document processing, analysis, and management. Built with .NET 9 and leveraging cutting-edge AI technologies including Google Gemini and vector databases, the system provides end-to-end capabilities for contract lifecycle management.

### **Key Capabilities**
- 📄 **Document Upload & Storage** - Azure Blob Storage integration
- 🔍 **Intelligent Parsing** - AI-powered metadata extraction and document analysis
- 🧠 **Semantic Understanding** - Vector embeddings using Gemini text-embedding-004
- 🎯 **Vector Search** - Qdrant-powered similarity search
- 💬 **AI Query Service** - Natural language search and summarization
- 🤖 **AI Agent** - Orchestrated multi-service workflows
- 🔌 **MCP Integration** - Model Context Protocol for AI tool integration

---

## 📁 Project Structure

```
ContractProcessingSystem/
├── ContractProcessingSystem.DocumentUpload/      # Document upload and blob storage management
├── ContractProcessingSystem.DocumentParser/      # AI-powered document parsing and chunking
├── ContractProcessingSystem.EmbeddingService/    # Vector embedding generation (Gemini)
├── ContractProcessingSystem.VectorService/       # Qdrant vector database operations
├── ContractProcessingSystem.QueryService/        # Semantic search and AI summarization
├── ContractProcessingSystem.AIAgent/             # AI orchestration and workflow management
├── ContractProcessingSystem.Shared/              # Shared models, interfaces, and extensions
├── ContractProcessingSystem.ServiceDefaults/     # Aspire service defaults and telemetry
├── ContractProcessingSystem.AppHost/             # Aspire orchestration host
└── ContractProcessingSystem.MCPDemo/             # Model Context Protocol demo client
```

---

## 🏗️ Architecture

### **Microservices Architecture**
The system follows a distributed microservices pattern orchestrated by .NET Aspire:

```
┌─────────────────────────────────────────────────────────────┐
│                    Aspire AppHost                            │
│              (Orchestration & Service Discovery)             │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
┌───────▼────────┐  ┌────────▼────────┐  ┌────────▼────────┐
│  DocumentUpload │  │ DocumentParser  │  │ EmbeddingService│
│   (Port 7048)  │  │  (Port 7258)    │  │  (Port 7070)    │
└────────┬───────┘  └────────┬────────┘  └────────┬────────┘
         │                   │                     │
         │                   └──────┬──────────────┘
         │                          │
┌────────▼──────────┐  ┌───────────▼────────┐  ┌─────────────┐
│  VectorService    │  │   QueryService     │  │  AIAgent    │
│  (Port 7197)      │  │   (Port 7004)      │  │  (Port TBD) │
└───────────────────┘  └────────────────────┘  └─────────────┘
         │                                              │
┌────────▼──────────┐                                  │
│  Qdrant v1.16.0   │                                  │
│  (Ports 6333/6334)│◄─────────────────────────────────┘
└───────────────────┘

Infrastructure:
- SQL Server (Persistent) - Document metadata
- Redis (Cache) - Distributed caching
- Azure Storage Emulator - Blob storage
```

---

## 🔧 Core Services

### **1. DocumentUpload Service (Port 7048)**

**Purpose**: Document upload, storage, and metadata management

**Key Features**:
- Azure Blob Storage integration
- SQL Server metadata persistence
- Document lifecycle management
- MCP tools for AI integration

**Endpoints**:
```
POST   /api/documents/upload
GET    /api/documents/{id}
GET    /api/documents/{id}/content
GET    /api/documents/{id}/download
DELETE /api/documents/{id}
GET    /api/documents
POST   /api/documents/ensure-metadata
POST   /api/documents/ensure-storage
```

**Technologies**:
- Azure Blob Storage Client
- Entity Framework Core
- SQL Server
- MCP Server integration

---

### **2. DocumentParser Service (Port 7258)**

**Purpose**: AI-powered document text extraction and intelligent chunking

**Key Features**:
- Multi-format support (PDF, DOCX, TXT)
- AI metadata extraction using Gemini
- Semantic document chunking
- Contract type classification
- Rule-based fallback processing

**Endpoints**:
```
POST /api/parsing/{documentId}/parse
POST /api/parsing/{documentId}/chunk
GET  /api/parsing/{documentId}/chunks
GET  /api/parsing/{documentId}/metadata
GET  /api/parsing/{documentId}/status
GET  /api/parsing/ai-status
GET  /api/parsing/validate
```

**AI Capabilities**:
- **Metadata Extraction**: Title, parties, dates, contract value, key terms
- **Chunk Classification**: Header, Clause, Term, Condition, Signature
- **Contract Type Detection**: Employment, Service, IP License, etc.
- **Intelligent Value Extraction**: Multi-pattern recognition for various contract types

**Document Support**:
- PDF (via iText)
- Word/DOCX (via OpenXML)
- Plain text

---

### **3. EmbeddingService (Port 7070)**

**Purpose**: Vector embedding generation using Google Gemini

**Key Features**:
- Gemini text-embedding-004 model
- Batch embedding generation
- Redis caching
- Automatic vector storage integration
- Provider factory pattern (supports multiple LLM providers)

**Endpoints**:
```
POST /api/embeddings/generate
POST /api/embeddings/generate-and-store
POST /api/embeddings/generate-single
POST /api/embeddings/batch-process/{documentId}
GET  /api/embeddings/status/{documentId}
GET  /api/embeddings/health
```

**Configuration**:
```json
{
  "LLMProviders": {
    "DefaultProvider": "Gemini",
    "EmbeddingProvider": "Gemini",
    "Providers": [{
      "Type": "Gemini",
      "Name": "Gemini",
      "DefaultEmbeddingModel": "text-embedding-004"
    }]
  }
}
```

---

### **4. VectorService (Port 7197)**

**Purpose**: Vector database operations using Qdrant

**Key Features**:
- Qdrant v1.16.0 integration
- Persistent vector storage
- In-memory fallback
- Cosine similarity search
- Document-chunk mapping
- Dynamic dimension detection

**Endpoints**:
```
POST   /api/vector/store
POST   /api/vector/store-with-mapping
POST   /api/vector/search/similar
POST   /api/vector/search
DELETE /api/vector/documents/{documentId}
GET    /api/vector/health
GET    /api/vector/collection/info
DELETE /api/vector/collection/clear
```

**Qdrant Configuration**:
```json
{
  "Qdrant": {
    "Host": "localhost",
    "Port": "6334",
    "UseHttps": "false"
  },
  "VectorService": {
    "Dimension": 768
  }
}
```

**Features**:
- Automatic collection creation
- Payload indexing for efficient filtering
- Hybrid storage (Qdrant + in-memory cache)
- Dimension auto-detection (supports 768 for Gemini)

---

### **5. QueryService (Port 7004)**

**Purpose**: Intelligent search and AI-powered summarization

**Key Features**:
- Semantic search using vector embeddings
- Hybrid search (70% semantic + 30% keyword)
- AI summarization with multiple modes
- Multi-document analysis
- Gemini-powered insights

**Endpoints**:
```
POST /api/query/search              # Semantic search
POST /api/query/search/hybrid       # Hybrid search
POST /api/query/summarize           # AI summarization
GET  /api/query/health
GET  /api/query/capabilities
GET  /api/query/diagnostics
```

**Summarization Types**:
1. **Overview** - Executive summary with key points
2. **KeyTerms** - Extract payment, obligations, IP rights, etc.
3. **RiskAssessment** - Financial, operational, legal risk analysis
4. **Comparison** - Multi-contract comparison

**Search Capabilities**:
- Natural language queries
- Document type filtering
- Date range filtering
- Party-based filtering
- Minimum score thresholds

---

### **6. AIAgent Service (Port TBD)**

**Purpose**: AI orchestration and workflow automation

**Key Features**:
- Document processing pipeline orchestration
- Conversational AI interface
- Multi-service workflow coordination
- Contract analysis and comparison

**Capabilities**:
```
POST /api/aiagent/process/{documentId}    # Full pipeline
POST /api/aiagent/chat                    # Conversational AI
POST /api/aiagent/analyze/{documentId}    # Deep analysis
POST /api/aiagent/compare                 # Contract comparison
```

**Workflow Example**:
```
Document Upload → Parse → Chunk → Generate Embeddings → Store Vectors → Ready for Search
```

---

## 🔌 Model Context Protocol (MCP) Integration

### **What is MCP?**
MCP (Model Context Protocol) enables AI models to interact with your services through standardized tools and resources.

### **Available MCP Servers**

**DocumentUpload MCP Tools**:
- `upload_contract_file` - Upload documents
- `get_document` - Retrieve document metadata
- `get_document_content` - Download document content
- `list_documents` - List all documents
- `delete_document` - Remove documents

**Resources**:
- `contract://files` - List of all contract files
- `contract://statistics` - Upload statistics
- `contract://document/{id}` - Specific document details

**Endpoint**: `/mcp` (on each service)

---

## 🤖 AI Integration

### **LLM Provider Factory Pattern**

The system uses a flexible provider factory supporting multiple AI backends:

```csharp
public interface ILLMProvider
{
    Task<string> GenerateTextAsync(string prompt, LLMGenerationOptions? options = null);
    Task<string> GenerateChatResponseAsync(string message, string? systemPrompt = null);
    Task<float[]> GenerateEmbeddingAsync(string text, EmbeddingOptions? options = null);
    Task<float[][]> GenerateEmbeddingsAsync(IEnumerable<string> texts);
}
```

**Supported Providers**:
- ✅ Google Gemini (Primary)
- ✅ OpenAI
- ✅ Azure OpenAI
- 🔄 Claude (Planned)

**Configuration**:
```json
{
  "LLMProviders": {
    "DefaultProvider": "Gemini",
    "EmbeddingProvider": "Gemini",
    "SelectionStrategy": "Priority",
    "EnableFallback": true,
    "RetryAttempts": 3
  }
}
```

### **Gemini Integration**

**Models Used**:
- **Chat/Generation**: `gemini-2.5-flash`
- **Embeddings**: `text-embedding-004` (768 dimensions)

**Features**:
- Structured output parsing
- Low-temperature deterministic extraction
- Fallback to rule-based processing
- Comprehensive error handling

---

## 🗄️ Data Models

### **Core Models**

**ContractDocument**:
```csharp
public record ContractDocument(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSize,
    DateTime UploadedAt,
    DateTime LastModified,
    string UploadedBy,
    ContractStatus Status,
    string? BlobPath,
    ContractMetadata? Metadata
);
```

**ContractMetadata**:
```csharp
public record ContractMetadata(
    string? Title,
    DateTime? ContractDate,
    DateTime? ExpirationDate,
    decimal? ContractValue,
    string? Currency,
    List<string> Parties,
    List<string> KeyTerms,
    string? ContractType,
    Dictionary<string, object> CustomFields
);
```

**ContractChunk**:
```csharp
public record ContractChunk(
    Guid Id,
    Guid DocumentId,
    string Content,
    int ChunkIndex,
    int StartPosition,
    int EndPosition,
    ChunkType Type,  // Header, Clause, Term, Condition, Signature
    Dictionary<string, object> Metadata
);
```

**VectorEmbedding**:
```csharp
public record VectorEmbedding(
    Guid Id,
    Guid ChunkId,
    float[] Vector,
    string Model,
    DateTime CreatedAt,
    Guid? DocumentId  // Links back to source document
);
```

**SearchResult**:
```csharp
public record SearchResult(
    Guid DocumentId,
    Guid ChunkId,
    string Content,
    float Score,
    ContractMetadata Metadata,
    Dictionary<string, object> Highlights
);
```

---

## 🚀 Getting Started

### **Prerequisites**

**Required**:
- .NET 9 SDK
- Docker Desktop (for Qdrant, SQL Server, Redis, Azure Storage Emulator)
- Visual Studio 2022 or VS Code
- Google Gemini API Key

**Optional**:
- OpenAI API Key (for alternative LLM provider)

### **Configuration**

**1. Set Gemini API Key**:

Update in each service's `appsettings.json`:
```json
{
  "LLMProviders": {
    "Providers": [{
      "Type": "Gemini",
      "ApiKey": "YOUR_GEMINI_API_KEY_HERE"
    }]
  }
}
```

**2. Service URLs** (Auto-configured by Aspire):
```json
{
  "Services": {
    "DocumentUpload": "https://localhost:7048",
    "DocumentParser": "https://localhost:7258",
    "EmbeddingService": "https://localhost:7070",
    "VectorService": "https://localhost:7197"
  }
}
```

### **Running the System**

**Option 1: Aspire AppHost (Recommended)**:
```bash
cd ContractProcessingSystem.AppHost
dotnet run
```

This starts:
- All microservices
- SQL Server container
- Redis container
- Qdrant container (v1.16.0)
- Azure Storage Emulator
- Aspire Dashboard (https://localhost:PORT)

**Option 2: Individual Services**:
```bash
# Terminal 1 - DocumentUpload
cd ContractProcessingSystem.DocumentUpload
dotnet run

# Terminal 2 - DocumentParser
cd ContractProcessingSystem.DocumentParser
dotnet run

# Terminal 3 - EmbeddingService
cd ContractProcessingSystem.EmbeddingService
dotnet run

# Terminal 4 - VectorService
cd ContractProcessingSystem.VectorService
dotnet run

# Terminal 5 - QueryService
cd ContractProcessingSystem.QueryService
dotnet run
```

---

## 📊 Workflow Examples

### **Complete Document Processing Pipeline**

```
1. Upload Document
   POST /api/documents/upload
   → Document stored in Azure Blob Storage
   → Metadata saved to SQL Server
   → DocumentId returned

2. Parse Document
   POST /api/parsing/{documentId}/parse
   → Text extracted from PDF/DOCX
   → AI extracts metadata (parties, dates, value, terms)
   → Metadata saved

3. Chunk Document
   POST /api/parsing/{documentId}/chunk
   → Document split into semantic chunks
   → Chunks classified (Header, Clause, Term, etc.)
   → Chunks saved with positions

4. Generate Embeddings
   POST /api/embeddings/generate-and-store
   → Chunks converted to 768-d vectors
   → Embeddings stored in Qdrant
   → Chunk-to-document mapping created

5. Search & Query
   POST /api/query/search
   → Natural language query
   → Query converted to embedding
   → Qdrant similarity search
   → Ranked results returned

6. Summarize
   POST /api/query/summarize
   → Retrieve document chunks
   → AI generates summary
   → Key points extracted
   → Insights provided
```

---

## 🔍 Example API Calls

### **1. Upload a Contract**
```http
POST https://localhost:7048/api/documents/upload
Content-Type: multipart/form-data

file: @contract.pdf
uploadedBy: user@example.com
```

### **2. Semantic Search**
```http
POST https://localhost:7004/api/query/search
Content-Type: application/json

{
  "query": "What are the payment terms?",
  "maxResults": 10,
  "minScore": 0.7
}
```

### **3. Summarize Document**
```http
POST https://localhost:7004/api/query/summarize
Content-Type: application/json

{
  "documentIds": ["guid-1", "guid-2"],
  "type": "Overview",
  "maxLength": 500
}
```

### **4. Hybrid Search**
```http
POST https://localhost:7004/api/query/search/hybrid
Content-Type: application/json

{
  "query": "liability insurance requirements",
  "minScore": 0.65,
  "filters": {
    "contract_type": "service_agreement"
  }
}
```

---

## 🛠️ Development

### **Project Technologies**

| Project | Key Technologies |
|---------|-----------------|
| DocumentUpload | ASP.NET Core 9, EF Core, Azure Blob Storage |
| DocumentParser | Semantic Kernel, iText (PDF), OpenXML (DOCX) |
| EmbeddingService | Gemini API, Redis, LLM Provider Factory |
| VectorService | Qdrant Client 1.16.0, Qdrant DB v1.16.0 |
| QueryService | Gemini API, LLM Provider Factory |
| AIAgent | Semantic Kernel, Workflow orchestration |
| Shared | Provider pattern, Models, Extensions |
| AppHost | .NET Aspire 9.1.0 |

### **NuGet Packages**

**AI & ML**:
- Microsoft.SemanticKernel
- Microsoft.SemanticKernel.Connectors.Google (Gemini)
- Qdrant.Client 1.16.0

**Document Processing**:
- iText7 (PDF)
- DocumentFormat.OpenXml (DOCX)

**Infrastructure**:
- Aspire.Hosting
- Azure.Storage.Blobs
- StackExchange.Redis
- ModelContextProtocol.AspNetCore

---

## 📈 Monitoring & Observability

### **Aspire Dashboard**

Access at: `https://localhost:[ASPIRE_PORT]`

**Features**:
- Service health monitoring
- Distributed tracing
- Metrics visualization
- Log aggregation
- Resource monitoring

### **Health Checks**

Each service exposes health endpoints:
```
GET /health
GET /api/{service}/health
```

### **Diagnostics**

**QueryService Diagnostics**:
```
GET /api/query/diagnostics
```
Returns:
- Configured service URLs
- LLM provider status
- Dependency health

---

## 🔐 Security Considerations

**API Keys**:
- Store in user secrets for development
- Use Azure Key Vault for production
- Never commit keys to source control

**CORS**:
- Currently allows all origins (development)
- Restrict in production

**Authentication**: 
- Not implemented (add Azure AD, JWT, etc.)

---

## 📝 Configuration Reference

### **Service Discovery (Aspire)**

Services are auto-configured via Aspire's service discovery:
```csharp
.WithEnvironment("Services__VectorService", vectorService.GetEndpoint("https"))
```

### **LLM Providers**

Full configuration example:
```json
{
  "LLMProviders": {
    "DefaultProvider": "Gemini",
    "EmbeddingProvider": "Gemini",
    "SelectionStrategy": "Priority",
    "EnableFallback": true,
    "RetryAttempts": 3,
    "Providers": [
      {
        "Type": "Gemini",
        "Name": "Gemini",
        "ApiKey": "YOUR_KEY",
        "Endpoint": "https://generativelanguage.googleapis.com",
        "DefaultChatModel": "gemini-2.5-flash",
        "DefaultEmbeddingModel": "text-embedding-004",
        "IsEnabled": true,
        "Priority": 1
      }
    ]
  }
}
```

---

## 🧪 Testing

### **HTTP Test Files**

Each service includes `.http` files for testing:
- `ContractProcessingSystem.EmbeddingService.http`
- `ContractProcessingSystem.QueryService.http`

Use with:
- Visual Studio REST Client
- VS Code REST Client extension
- Postman (import)

### **MCP Demo**

```bash
cd ContractProcessingSystem.MCPDemo
dotnet run
```

Demonstrates MCP tool integration.

---

## 🚦 Status & Roadmap

### **Current Status**

✅ **Complete**:
- Microservices architecture
- Document upload & storage
- AI-powered parsing
- Vector embeddings (Gemini)
- Qdrant vector search
- Semantic & hybrid search
- AI summarization
- MCP integration
- Aspire orchestration

🔄 **In Progress**:
- Authentication & authorization
- Advanced workflow automation
- UI/Frontend
- Performance optimization

📋 **Planned**:
- Claude AI provider
- Advanced contract comparison
- Contract templates
- Version control
- Audit logging
- Compliance reporting

---

## 📚 Additional Resources

**Documentation**:
- [QueryService API Documentation](./ContractProcessingSystem.QueryService/README_API.md)
- [MCP Demo README](./ContractProcessingSystem.MCPDemo/README.md)

**External References**:
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Qdrant Documentation](https://qdrant.tech/documentation/)
- [Google Gemini API](https://ai.google.dev/docs)

---

## 🤝 Contributing

### **Development Workflow**

1. Create feature branch
2. Make changes
3. Test locally with Aspire
4. Run `dotnet build` to verify
5. Submit pull request

### **Code Standards**

- C# 13.0 features
- .NET 9 target
- Nullable reference types enabled
- Record types for DTOs
- Async/await pattern
- Comprehensive logging

---

## 📄 License

[Your License Here]

---

## 👥 Authors

[Your Team/Author Information]

---

## 🆘 Troubleshooting

### **Common Issues**

**"InvalidDataException" on startup**:
- Check `appsettings.json` for valid JSON
- Ensure all sections are complete

**"Qdrant unavailable"**:
- Ensure Docker Desktop is running
- VectorService falls back to in-memory storage

**"No embeddings generated"**:
- Verify Gemini API key
- Check network connectivity
- Review logs in Aspire dashboard

**"Document chunks not available"**:
- Ensure document was parsed first
- Check DocumentParser service status
- Verify document ID is correct

---

## 🎯 Quick Reference

### **Service Ports**
| Service | HTTPS Port |
|---------|-----------|
| DocumentUpload | 7048 |
| DocumentParser | 7258 |
| EmbeddingService | 7070 |
| VectorService | 7197 |
| QueryService | 7004 |
| AIAgent | TBD |

### **Infrastructure Ports**
| Component | Port(s) |
|-----------|---------|
| Qdrant REST | 6333 |
| Qdrant gRPC | 6334 |
| SQL Server | 1433 |
| Redis | 6379 |
| Azure Storage | 10000-10002 |

### **Key Endpoints**
```
Upload:     POST /api/documents/upload
Parse:      POST /api/parsing/{id}/parse
Embed:      POST /api/embeddings/generate-and-store
Search:     POST /api/query/search
Summarize:  POST /api/query/summarize
```

---

**Last Updated**: [Current Date]  
**Version**: 1.0.0  
**.NET Version**: 9.0  
**Aspire Version**: 9.1.0  