# Multi-Provider Contract Processing System

A comprehensive AI-powered contract processing system built with .NET 9, Aspire, and multiple AI providers (OpenAI & Google Gemini).

## 🏗️ Architecture

This system implements a microservices architecture using .NET Aspire for orchestration and supports multiple AI providers for enhanced flexibility and reliability.

### 🧩 Core Services

- **DocumentUpload Service** - Handles contract file uploads and storage
- **DocumentParser Service** - Extracts and chunks contract content using AI
- **EmbeddingService** - Generates vector embeddings with multi-provider support
- **VectorService** - Manages vector storage and similarity search (Milvus)
- **QueryService** - Provides semantic search and retrieval capabilities
- **AIAgent Service** - Multi-provider chat and contract analysis

### 🔧 Infrastructure Components

- **SQL Server** - Document metadata and relational data
- **Redis** - Caching layer for performance optimization
- **Azure Storage** - Blob storage for contract files
- **Milvus** - Vector database for semantic search
- **etcd & MinIO** - Supporting services for Milvus

## 🤖 Multi-Provider AI Support

The system supports multiple AI providers with automatic fallback:

### Supported Providers
- **OpenAI** (GPT-4, GPT-3.5-turbo, text-embedding-ada-002)
- **Google Gemini** (gemini-pro, text-embedding-004)

### Provider Configuration
Configure providers in `appsettings.json`:

```json
{
  "AI": {
    "DefaultProvider": "OpenAI",
    "OpenAI": {
      "ApiKey": "your-openai-api-key",
      "ChatModel": "gpt-4",
      "EmbeddingModel": "text-embedding-ada-002"
    },
    "Gemini": {
      "ApiKey": "your-gemini-api-key",
      "ChatModel": "gemini-pro",
      "EmbeddingModel": "text-embedding-004"
    }
  }
}
```

## 🚀 Getting Started

### Prerequisites

- .NET 9 SDK
- Docker Desktop
- Visual Studio 2022 or VS Code
- API keys for OpenAI and/or Google Gemini

### Running the System

1. **Clone the repository**
   ```bash
   git clone https://github.com/thinnv/TestRAGandMCP.git
   cd TestRAGandMCP
   ```

2. **Configure API Keys**
   Update `appsettings.json` files with your API keys

3. **Start Docker Desktop**
   Ensure Docker is running for containerized services

4. **Run with Aspire**
   ```bash
   cd ContractProcessingSystem
   dotnet run --project ContractProcessingSystem.AppHost
   ```

5. **Access the Dashboard**
   Open https://localhost:17139 to view the Aspire dashboard

## 📋 Features

### Document Processing
- ✅ PDF, DOCX, TXT file upload
- ✅ AI-powered content extraction
- ✅ Intelligent document chunking
- ✅ Metadata extraction and storage

### Vector Operations
- ✅ Multi-provider embedding generation
- ✅ Vector similarity search
- ✅ Semantic document retrieval
- ✅ Hybrid search capabilities

### AI Chat & Analysis
- ✅ Multi-provider chat interface
- ✅ Contract summarization
- ✅ Risk assessment
- ✅ Key terms extraction
- ✅ Contract comparison

### System Features
- ✅ Microservices architecture
- ✅ .NET Aspire orchestration
- ✅ Health monitoring
- ✅ Distributed logging
- ✅ Caching optimization
- ✅ Provider failover

## 🛠️ Development

### Project Structure
```
ContractProcessingSystem/
├── ContractProcessingSystem.AppHost/          # Aspire orchestration
├── ContractProcessingSystem.ServiceDefaults/  # Shared configurations
├── ContractProcessingSystem.Shared/           # Common models and interfaces
├── ContractProcessingSystem.DocumentUpload/   # File upload service
├── ContractProcessingSystem.DocumentParser/   # Document parsing service
├── ContractProcessingSystem.EmbeddingService/ # AI embedding service
├── ContractProcessingSystem.VectorService/    # Vector storage service
├── ContractProcessingSystem.QueryService/     # Search and retrieval
├── ContractProcessingSystem.AIAgent/          # AI chat and analysis
└── MultiProviderDemo/                         # Standalone demo app
```

### Multi-Provider Implementation
The system uses a factory pattern for AI provider abstraction:

```csharp
public interface ILLMProvider
{
    Task<string> ChatAsync(string prompt, string? systemPrompt = null);
    Task<float[]> GenerateEmbeddingAsync(string text);
    string ProviderName { get; }
}
```

### Building
```bash
dotnet build --configuration Release
```

### Testing
```bash
dotnet test
```

## 🔧 Configuration

### Environment Variables
- `OPENAI_API_KEY` - OpenAI API key
- `GEMINI_API_KEY` - Google Gemini API key
- `ConnectionStrings__database` - SQL Server connection
- `ConnectionStrings__cache` - Redis connection
- `ConnectionStrings__storage` - Azure Storage connection

### Service Endpoints
- **Aspire Dashboard**: https://localhost:17139
- **DocumentUpload**: https://localhost:7001
- **DocumentParser**: https://localhost:7002
- **EmbeddingService**: https://localhost:7003
- **VectorService**: https://localhost:7004
- **QueryService**: https://localhost:7005
- **AIAgent**: https://localhost:7006

## 🐳 Docker Support

The system includes Docker configurations for:
- Milvus vector database
- etcd (Milvus dependency)
- MinIO (Milvus storage)
- SQL Server
- Redis

Start containers via Aspire or manually:
```bash
docker-compose up -d
```

## 📊 Monitoring

Use the Aspire dashboard to monitor:
- Service health and status
- Request metrics and traces
- Log aggregation
- Resource utilization
- Service dependencies

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙋‍♂️ Support

For questions and support:
- Create an issue on GitHub
- Check the documentation in `/docs`
- Review the demo scripts in `demo-scripts.md`

## 🔄 Version History

### v1.0.0 - Initial Release
- Multi-provider AI support (OpenAI + Gemini)
- Complete microservices architecture
- Vector search with Milvus
- Aspire orchestration
- Docker containerization

---

**Built with ❤️ using .NET 9, Aspire, and cutting-edge AI technologies**