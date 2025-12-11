# ContractProcessingSystem - Solution Summary

## ?? Overview

**ContractProcessingSystem** is an enterprise-grade, AI-powered contract management platform built with **.NET 9 Aspire**. It provides intelligent document processing, semantic search, and conversational AI capabilities for legal contract analysis.

---

## ??? Architecture

### High-Level Architecture

```
??????????????????????????????????????????????????????????????????????????????
?                           USER INTERFACE / API GATEWAY                      ?
??????????????????????????????????????????????????????????????????????????????
                                    ?
??????????????????????????????????????????????????????????????????????????????
?                              AI AGENT (Orchestrator)                        ?
?  • Document Processing Pipeline    • Contract Analysis & Comparison         ?
?  • Conversational Chat             • MCP Tools Integration                  ?
??????????????????????????????????????????????????????????????????????????????
                                    ?
        ?????????????????????????????????????????????????????????
        ?                           ?                           ?
        ?                           ?                           ?
?????????????????         ?????????????????         ?????????????????
?   Document    ?         ?   Document    ?         ?    Query      ?
?   Upload      ???????????   Parser      ?         ?   Service     ?
?   Service     ?         ?   Service     ?         ?               ?
?????????????????         ?????????????????         ?????????????????
                                  ?                         ?
                                  ?                         ?
                          ?????????????????                 ?
                          ?  Embedding    ?                 ?
                          ?  Service      ?                 ?
                          ?????????????????                 ?
                                  ?                         ?
                                  ?                         ?
                          ?????????????????                 ?
                          ?   Vector      ???????????????????
                          ?   Service     ?
                          ?   (Qdrant)    ?
                          ?????????????????
```

### Infrastructure Components

| Component | Purpose | Technology |
|-----------|---------|------------|
| **SQL Server** | Document metadata storage | SQL Server container |
| **Azure Blob Storage** | File storage | Azurite emulator (dev) |
| **Redis** | Caching & session | Redis container |
| **Qdrant** | Vector database | Qdrant v1.16.0 |

---

## ?? Microservices

### 1. DocumentUpload Service (Port 7048)

**Purpose**: Upload and store contract documents

**Key Features**:
- Multi-format support (PDF, Word, TXT)
- Blob storage integration
- Metadata management
- File validation

**API Endpoints**:
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/documents` | Upload document |
| GET | `/api/documents/{id}` | Get document metadata |
| GET | `/api/documents/{id}/content` | Download document |
| DELETE | `/api/documents/{id}` | Delete document |
| GET | `/api/documents` | List all documents |

---

### 2. DocumentParser Service (Port 7258)

**Purpose**: Extract text and metadata from contracts using AI

**Key Features**:
- PDF text extraction (iText7)
- Word document parsing (OpenXML)
- AI-powered metadata extraction
- Smart document chunking
- Rule-based fallback

**AI Extraction**:
- Contract title, type, and parties
- Financial values and currency
- Key dates (contract, expiration)
- Key terms and clauses
- Chunk classification (Header, Clause, Term, Condition, Signature)

**API Endpoints**:
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/parsing/{id}/parse` | Extract metadata |
| POST | `/api/parsing/{id}/chunk` | Create text chunks |
| GET | `/api/parsing/{id}/chunks` | Retrieve stored chunks |
| GET | `/api/parsing/{id}/status` | Get parsing status |
| DELETE | `/api/parsing/{id}/chunks` | Delete chunks |

---

### 3. EmbeddingService (Port 7070)

**Purpose**: Generate vector embeddings for semantic search

**Key Features**:
- Multi-provider support (Gemini, OpenAI, GitHub Models)
- Batch processing
- Automatic vector storage
- Embedding caching

**Embedding Models**:
| Provider | Model | Dimensions |
|----------|-------|------------|
| Gemini | text-embedding-004 | 768 |
| OpenAI | text-embedding-3-small | 1536 |
| GitHub Models | text-embedding-3-small | 1536 |

**API Endpoints**:
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/embeddings/generate` | Generate batch embeddings |
| POST | `/api/embeddings/generate-single` | Single text embedding |
| GET | `/api/embeddings/status/{id}` | Processing status |

---

### 4. VectorService (Port 7197)

**Purpose**: Store and search vector embeddings

**Key Features**:
- Qdrant integration (persistent)
- In-memory fallback
- Cosine similarity search
- Document filtering

**API Endpoints**:
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/vector/store` | Store embeddings |
| POST | `/api/vector/store-with-mapping` | Store with document mapping |
| POST | `/api/vector/search/similar` | Vector similarity search |
| POST | `/api/vector/search` | Semantic text search |
| DELETE | `/api/vector/{documentId}` | Delete document vectors |
| GET | `/api/vector/statistics` | Get storage stats |

---

### 5. QueryService (Port 7004)

**Purpose**: Semantic search and AI summarization

**Key Features**:
- Semantic search using embeddings
- Hybrid search (semantic + keyword)
- AI-powered summarization
- Multiple summary types

**Summary Types**:
| Type | Description |
|------|-------------|
| `Overview` | Comprehensive contract overview |
| `KeyTerms` | Extract key terms and conditions |
| `RiskAssessment` | Analyze risks and liabilities |
| `Comparison` | Compare multiple contracts |

**API Endpoints**:
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/query/search` | Semantic search |
| POST | `/api/query/search/hybrid` | Hybrid search |
| POST | `/api/query/summarize` | Document summarization |

---

### 6. AIAgent Service (Port 7070)

**Purpose**: Orchestrate services and provide AI chat interface

**Key Features**:
- Complete processing pipeline orchestration
- Conversational AI chat
- Contract analysis
- Multi-document comparison
- MCP (Model Context Protocol) tools

**Processing Pipeline**:
```
Upload ? Parse (20%) ? Chunk (40%) ? Embed (60%) ? Store (80%) ? Complete (100%)
```

**API Endpoints**:
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/agent/process/{documentId}` | Full processing pipeline |
| POST | `/api/agent/chat` | AI chat |
| POST | `/api/agent/analyze/{documentId}` | Contract analysis |
| POST | `/api/agent/compare` | Compare contracts |
| GET | `/api/agent/workflow/{workflowId}` | Get workflow status |

---

## ?? AI/LLM Providers

### Supported Providers

| Provider | Chat | Embeddings | Cost | Priority |
|----------|------|------------|------|----------|
| **Gemini** | ? | ? | Free tier | 1 (Default) |
| **GitHub Models** | ? | ? | Free | 2 |
| **OpenAI** | ? | ? | Paid | 3 |

### Provider Configuration

```json
{
  "LLMProviders": {
    "DefaultProvider": "Gemini",
    "EmbeddingProvider": "Gemini",
    "EnableFallback": true,
    "Providers": [
      {
        "Type": "Gemini",
        "Name": "Gemini",
        "ApiKey": "your-api-key",
        "DefaultChatModel": "gemini-pro",
        "DefaultEmbeddingModel": "text-embedding-004",
        "IsEnabled": true,
        "Priority": 1
      }
    ]
  }
}
```

### Fallback Strategy

```
Primary Provider (Gemini) 
    ? (if unavailable)
Fallback Provider (GitHub Models)
    ? (if unavailable)  
Secondary Fallback (OpenAI)
```

---

## ?? Data Models

### ContractDocument
```csharp
record ContractDocument(
    Guid Id,
    string FileName,
    string ContentType,      // application/pdf, application/msword, text/plain
    long FileSize,
    DateTime UploadedAt,
    string UploadedBy,
    ContractStatus Status,   // Pending, Processing, Processed, Failed
    string? BlobPath,
    ContractMetadata? Metadata
);
```

### ContractMetadata
```csharp
record ContractMetadata(
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

### ContractChunk
```csharp
record ContractChunk(
    Guid Id,
    Guid DocumentId,
    string Content,
    int ChunkIndex,
    int StartPosition,
    int EndPosition,
    ChunkType Type,          // Header, Clause, Term, Condition, Signature, Other
    Dictionary<string, object> Metadata
);
```

### VectorEmbedding
```csharp
record VectorEmbedding(
    Guid Id,
    Guid ChunkId,
    float[] Vector,
    string Model,
    DateTime CreatedAt,
    Guid? DocumentId
);
```

---

## ?? Processing Flows

### Flow 1: Document Upload & Processing

```
1. User uploads document (PDF/Word/TXT)
2. DocumentUpload stores file in Blob Storage + metadata in SQL
3. User triggers processing via AIAgent
4. DocumentParser extracts text and metadata
5. DocumentParser creates semantic chunks
6. EmbeddingService generates vector embeddings
7. VectorService stores embeddings in Qdrant
8. Document ready for search and analysis
```

### Flow 2: Semantic Search

```
1. User submits search query
2. QueryService generates query embedding
3. VectorService performs similarity search
4. Results ranked by relevance score
5. Optional: AI enhances result descriptions
6. Return ranked results with highlights
```

### Flow 3: AI Chat with Context

```
1. User sends message with optional document context
2. AIAgent retrieves document metadata
3. AIAgent performs contextual search
4. Builds prompt with conversation history
5. LLM generates response
6. Store conversation for future context
```

---

## ??? MCP Tools (Model Context Protocol)

All services expose MCP tools for AI assistants:

### AIAgent Tools
- `ProcessDocument` - Full processing pipeline
- `Chat` - Conversational AI
- `AnalyzeContract` - Comprehensive analysis
- `CompareContracts` - Multi-document comparison

### QueryService Tools
- `SemanticSearch` - Vector similarity search
- `Summarize` - AI summarization
- `HybridSearch` - Combined search

### VectorService Tools
- `StoreEmbeddings` - Store vectors
- `SearchSimilar` - Similarity search
- `DeleteDocumentEmbeddings` - Remove vectors

### DocumentParser Tools
- `ParseDocument` - Extract metadata
- `ChunkDocument` - Create chunks
- `GetParsingStatus` - Check status

---

## ?? Project Structure

```
ContractProcessingSystem/
??? ContractProcessingSystem.AppHost/           # Aspire orchestration
??? ContractProcessingSystem.ServiceDefaults/   # Shared Aspire config
??? ContractProcessingSystem.Shared/            # Shared code
?   ??? AI/
?   ?   ??? ILLMProvider.cs                    # Provider interface
?   ?   ??? LLMProviderFactory.cs              # Provider factory
?   ?   ??? Providers/
?   ?       ??? GeminiProvider.cs
?   ?       ??? GitHubModelsProvider.cs
?   ?       ??? OpenAIProvider.cs
?   ??? Models/
?   ?   ??? ContractModels.cs                  # Shared data models
?   ??? Services/
?   ?   ??? IServices.cs                       # Service interfaces
?   ??? Extensions/
?       ??? LLMProviderServiceExtensions.cs
??? ContractProcessingSystem.DocumentUpload/    # File storage
??? ContractProcessingSystem.DocumentParser/    # Text extraction
??? ContractProcessingSystem.EmbeddingService/  # Vector generation
??? ContractProcessingSystem.VectorService/     # Qdrant integration
??? ContractProcessingSystem.QueryService/      # Search & summarization
??? ContractProcessingSystem.AIAgent/           # Orchestration & chat
??? ContractProcessingSystem.MCPDemo/           # MCP demonstration
```

---

## ?? Quick Start

### Prerequisites
- .NET 9 SDK
- Docker Desktop (for containers)
- API Key (Gemini, GitHub, or OpenAI)

### Run with Aspire

```bash
cd ContractProcessingSystem.AppHost
dotnet run
```

### Configure API Keys

1. Edit `appsettings.Development.json` in each service
2. Or use User Secrets:
```bash
dotnet user-secrets set "LLMProviders:Providers:0:ApiKey" "your-api-key"
```

### Test the API

```bash
# Upload a document
curl -X POST https://localhost:7048/api/documents \
  -F "file=@contract.pdf"

# Process document
curl -X POST https://localhost:7070/api/agent/process/{documentId}

# Search contracts
curl -X POST https://localhost:7004/api/query/search \
  -H "Content-Type: application/json" \
  -d '{"query": "payment terms", "maxResults": 10}'

# Chat about a contract
curl -X POST https://localhost:7070/api/agent/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What are the key obligations?", "contextDocumentId": "{id}"}'
```

---

## ?? Performance Characteristics

| Operation | Typical Latency |
|-----------|-----------------|
| Document Upload | 100-500ms |
| Text Extraction | 1-5s (depends on size) |
| Embedding Generation | 500ms-2s |
| Semantic Search | 100-300ms |
| Hybrid Search | 150-400ms |
| AI Summarization | 2-10s |
| Contract Analysis | 5-15s |

---

## ?? Security Considerations

- API keys stored in User Secrets (dev) or Key Vault (prod)
- HTTPS enforced for all endpoints
- Input validation on all API endpoints
- File type validation for uploads
- Rate limiting recommended for production

---

## ?? Documentation

- `README.md` - This summary file
- `CONFIGURATION_GUIDE.md` - Multi-provider configuration
- `README_GITHUB_MODELS.md` - GitHub Models setup
- `README_API.md` - QueryService API documentation

---

## ?? Use Cases

1. **Contract Search** - Find specific clauses or terms across all contracts
2. **Contract Analysis** - AI-powered analysis of contract terms and risks
3. **Contract Comparison** - Compare multiple contracts side-by-side
4. **Conversational Q&A** - Ask questions about contracts in natural language
5. **Risk Assessment** - Identify potential risks and liabilities
6. **Key Terms Extraction** - Extract important terms and obligations

---

## ?? Technology Stack

| Category | Technology |
|----------|------------|
| **Framework** | .NET 9, ASP.NET Core |
| **Orchestration** | .NET Aspire |
| **AI/LLM** | Gemini, GitHub Models, OpenAI |
| **Vector DB** | Qdrant |
| **Database** | SQL Server |
| **Storage** | Azure Blob Storage |
| **Caching** | Redis |
| **Document Parsing** | iText7, OpenXML |
| **MCP** | ModelContextProtocol.AspNetCore |

---

## ?? Version

- **Solution Version**: 1.0.0
- **.NET Version**: 9.0
- **Last Updated**: January 2025

---

## ?? Contributing

1. Follow the existing code patterns
2. Add unit tests for new features
3. Update documentation
4. Test with all LLM providers

---

*ContractProcessingSystem - Intelligent Contract Processing Made Simple* ??
