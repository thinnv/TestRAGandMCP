# Contract Processing System với AI Agent + .NET Aspire + Milvus + LLM

## Tổng quan hệ thống

Hệ thống xử lý hợp đồng thông minh được xây dựng với kiến trúc microservices hiện đại, tích hợp AI Agent để tự động hóa quy trình phân tích và xử lý hợp đồng. Hệ thống sử dụng .NET Aspire để quản lý và điều phối các dịch vụ, Milvus làm cơ sở dữ liệu vector, và tích hợp với các LLM để thực hiện các tác vụ AI.

## 🏗️ Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────────────────┐
│                    .NET Aspire Orchestration                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ Document    │  │ Document    │  │ Embedding   │              │
│  │ Upload      │  │ Parser      │  │ Service     │              │
│  │ Service     │  │ Service     │  │             │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
│                                                                 │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ Vector      │  │ Query       │  │ AI Agent    │              │
│  │ Service     │  │ Service     │  │ Orchestrator│              │
│  │ (Milvus)    │  │             │  │             │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ SQL Server  │  │ Redis Cache │  │ Azure Blob  │              │
│  │ Database    │  │             │  │ Storage     │              │
│  └─────────────┘  └─────────────┘  └─────────────┘              │
│                                                                 │
│                     ┌─────────────┐                             │
│                     │ Milvus      │                             │
│                     │ Vector DB   │                             │
│                     └─────────────┘                             │
└─────────────────────────────────────────────────────────────────┘
```

## 🚀 Các tính năng chính

### 1. **Upload và quản lý hợp đồng**
- Upload file PDF, DOC, DOCX
- Validation và virus scanning
- Lưu trữ trên Azure Blob Storage
- Quản lý metadata trong SQL Server

### 2. **Phân tích hợp đồng với AI**
- Trích xuất văn bản từ documents
- Phân tích cấu trúc hợp đồng
- Nhận diện entities (ngày tháng, số tiền, bên tham gia)
- Phân loại các điều khoản

### 3. **Vector Search và Embedding**
- Tạo embeddings cho nội dung hợp đồng
- Lưu trữ vectors trong Milvus
- Tìm kiếm semantic similarity
- Hybrid search (vector + keyword)

### 4. **AI Agent thông minh**
- Điều phối workflow tự động
- Chat interface với context
- Phân tích và so sánh hợp đồng
- Tóm tắt và báo cáo rủi ro

### 5. **Orchestration với Aspire**
- Service discovery tự động
- Health checks và monitoring
- Configuration management
- Distributed tracing

## 📋 Yêu cầu hệ thống

### Phần mềm cần thiết:
- **.NET 9.0 SDK**
- **Docker Desktop** (để chạy Milvus và dependencies)
- **Visual Studio 2022** hoặc **VS Code** với C# extension
- **SQL Server** (LocalDB hoặc container)
- **Azure Storage Emulator** hoặc Azure Storage Account

### Hardware tối thiểu:
- **RAM**: 8GB (khuyến nghị 16GB)
- **CPU**: 4 cores
- **Storage**: 20GB available space
- **Network**: Internet connection for LLM APIs

## 🛠️ Cài đặt và chạy hệ thống

### Bước 1: Clone repository
```bash
git clone <repository-url>
cd ContractProcessingSystem
```

### Bước 2: Cấu hình môi trường

#### 2.1 Cấu hình Azure OpenAI
Tạo file `appsettings.Development.json` trong mỗi service:

```json
{
  "OpenAI": {
    "Endpoint": "https://your-openai-endpoint.openai.azure.com",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4",
    "EmbeddingDeploymentName": "text-embedding-ada-002"
  }
}
```

#### 2.2 Cấu hình Connection Strings
```json
{
  "ConnectionStrings": {
    "database": "Server=(localdb)\\mssqllocaldb;Database=ContractProcessingDB;Trusted_Connection=true;",
    "storage": "UseDevelopmentStorage=true",
    "cache": "localhost:6379"
  }
}
```

### Bước 3: Khởi động infrastructure với Docker

#### 3.1 Tạo file `docker-compose.yml`:
```yaml
version: '3.8'
services:
  etcd:
    container_name: milvus-etcd
    image: quay.io/coreos/etcd:v3.5.5
    environment:
      - ETCD_AUTO_COMPACTION_MODE=revision
      - ETCD_AUTO_COMPACTION_RETENTION=1000
      - ETCD_QUOTA_BACKEND_BYTES=4294967296
      - ETCD_SNAPSHOT_COUNT=50000
    volumes:
      - etcd_data:/etcd
    command: etcd -advertise-client-urls=http://127.0.0.1:2379 -listen-client-urls http://0.0.0.0:2379 --data-dir /etcd
    healthcheck:
      test: ["CMD", "etcdctl", "endpoint", "health"]
      interval: 30s
      timeout: 20s
      retries: 3

  minio:
    container_name: milvus-minio
    image: minio/minio:RELEASE.2023-03-20T20-16-18Z
    environment:
      MINIO_ACCESS_KEY: minioadmin
      MINIO_SECRET_KEY: minioadmin
    ports:
      - "9001:9001"
      - "9000:9000"
    volumes:
      - minio_data:/data
    command: minio server /data --console-address ":9001"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 30s
      timeout: 20s
      retries: 3

  milvus:
    container_name: milvus-standalone
    image: milvusdb/milvus:v2.3.3
    command: ["milvus", "run", "standalone"]
    environment:
      ETCD_ENDPOINTS: etcd:2379
      MINIO_ADDRESS: minio:9000
    volumes:
      - milvus_data:/var/lib/milvus
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9091/healthz"]
      interval: 30s
      start_period: 90s
      timeout: 20s
      retries: 3
    ports:
      - "19530:19530"
      - "9091:9091"
    depends_on:
      - "etcd"
      - "minio"

  redis:
    container_name: redis-cache
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data

volumes:
  etcd_data:
  minio_data:
  milvus_data:
  redis_data:
```

#### 3.2 Khởi động infrastructure:
```bash
docker-compose up -d
```

### Bước 4: Build và chạy solution

#### 4.1 Restore packages:
```bash
dotnet restore
```

#### 4.2 Build solution:
```bash
dotnet build
```

#### 4.3 Chạy Aspire AppHost:
```bash
cd ContractProcessingSystem.AppHost
dotnet run
```

## 📖 Hướng dẫn sử dụng API

### 1. Upload hợp đồng

```bash
curl -X POST "https://localhost:5000/api/documents/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@contract.pdf" \
  -F "uploadedBy=user@example.com"
```

**Response:**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "fileName": "contract.pdf",
  "contentType": "application/pdf",
  "fileSize": 1024000,
  "uploadedAt": "2024-11-01T10:00:00Z",
  "uploadedBy": "user@example.com",
  "status": "Uploaded"
}
```

### 2. Phân tích hợp đồng

```bash
curl -X POST "https://localhost:5001/api/parsing/{documentId}/parse"
```

**Response:**
```json
{
  "title": "Service Agreement",
  "contractDate": "2024-01-15",
  "expirationDate": "2025-01-15",
  "contractValue": 100000.00,
  "currency": "USD",
  "parties": ["Company A", "Company B"],
  "keyTerms": ["Monthly payment", "Termination clause", "Confidentiality"],
  "contractType": "Service Agreement"
}
```

### 3. Tìm kiếm semantic

```bash
curl -X POST "https://localhost:5004/api/query/search" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "payment terms and conditions",
    "maxResults": 10,
    "minScore": 0.7
  }'
```

### 4. Chat với AI Agent

```bash
curl -X POST "https://localhost:5005/api/ai-agent/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What are the key risks in this contract?",
    "contextDocumentId": "123e4567-e89b-12d3-a456-426614174000"
  }'
```

### 5. Tóm tắt hợp đồng

```bash
curl -X POST "https://localhost:5004/api/query/summarize" \
  -H "Content-Type: application/json" \
  -d '{
    "documentIds": ["123e4567-e89b-12d3-a456-426614174000"],
    "type": "RiskAssessment",
    "maxLength": 500
  }'
```

## 🔧 Workflow xử lý hợp đồng

### Quy trình tự động:

1. **Upload** → Document Upload Service
   - Validation file format và size
   - Virus scanning
   - Lưu trữ trên Azure Blob Storage
   - Tạo metadata record

2. **Parsing** → Document Parser Service
   - OCR cho scanned documents
   - Trích xuất text content
   - AI analysis để extract metadata
   - Structured data parsing

3. **Chunking** → Document Parser Service
   - Chia document thành chunks semantic
   - Phân loại chunk types (Header, Clause, Term, etc.)
   - Maintain context relationships

4. **Embedding** → Embedding Service
   - Generate vector embeddings cho mỗi chunk
   - Batch processing với rate limiting
   - Cache embeddings để tối ưu performance

5. **Vector Storage** → Vector Service
   - Store embeddings trong Milvus
   - Create indexes cho fast retrieval
   - Setup collection schema

6. **Ready for Search** → Query Service
   - Semantic search capabilities
   - Hybrid search (vector + keyword)
   - AI-powered result ranking

## 🎯 Use Cases thực tế

### 1. **Quản lý hợp đồng doanh nghiệp**
```bash
# Upload batch contracts
for file in contracts/*.pdf; do
  curl -X POST "https://localhost:5000/api/documents/upload" \
    -F "file=@$file" \
    -F "uploadedBy=admin@company.com"
done

# Search for specific terms
curl -X POST "https://localhost:5004/api/query/search" \
  -d '{"query": "termination clause 30 days notice"}'
```

### 2. **Due diligence tự động**
```bash
# Analyze contract risks
curl -X POST "https://localhost:5005/api/ai-agent/analyze/{contractId}"

# Compare multiple contracts
curl -X POST "https://localhost:5005/api/ai-agent/compare" \
  -d '{"documentIds": ["id1", "id2", "id3"]}'
```

### 3. **Compliance checking**
```bash
# Check specific compliance requirements
curl -X POST "https://localhost:5005/api/ai-agent/chat" \
  -d '{
    "message": "Does this contract comply with GDPR requirements?",
    "contextDocumentId": "contract-id"
  }'
```

## 📊 Monitoring và Performance

### Aspire Dashboard
- Truy cập: `https://localhost:15888`
- Theo dõi health status của tất cả services
- Distributed tracing cho requests
- Metrics và logging centralized

### Performance Metrics
- **Document Upload**: ~2s for 10MB files
- **Parsing**: ~10s per document
- **Embedding Generation**: ~5s per 1000 tokens
- **Vector Search**: <500ms for similarity queries
- **AI Analysis**: ~3s for contract summary

### Scaling considerations
- **Horizontal scaling**: Aspire hỗ trợ auto-scaling
- **Load balancing**: Built-in với service discovery
- **Caching strategy**: Redis cho embeddings và results
- **Database optimization**: Indexes cho metadata queries

## 🔒 Security và Compliance

### Data Protection
- **Encryption at rest**: Azure Blob Storage
- **Encryption in transit**: TLS 1.3 cho tất cả connections
- **PII Detection**: Automatic redaction của sensitive data
- **Access Control**: Role-based permissions

### API Security
- **Authentication**: JWT tokens
- **Authorization**: Policy-based access control
- **Rate Limiting**: Per-user và per-service limits
- **Input Validation**: Comprehensive sanitization

## 🐛 Troubleshooting

### Common Issues

#### 1. Milvus connection failed
```bash
# Check Milvus status
docker ps | grep milvus
docker logs milvus-standalone

# Restart Milvus
docker-compose restart milvus
```

#### 2. OpenAI API errors
```bash
# Check API key configuration
dotnet user-secrets list --project ContractProcessingSystem.DocumentParser

# Test connection
curl -H "Authorization: Bearer YOUR_API_KEY" \
  "https://your-endpoint.openai.azure.com/openai/deployments/gpt-4/completions"
```

#### 3. Database connection issues
```bash
# Check SQL Server LocalDB
sqllocaldb info
sqllocaldb start mssqllocaldb

# Reset database
dotnet ef database drop --project ContractProcessingSystem.DocumentUpload
dotnet ef database update --project ContractProcessingSystem.DocumentUpload
```

### Logs và Debugging
- **Structured Logging**: Serilog với rich context
- **Correlation IDs**: Track requests across services
- **Error Handling**: Comprehensive exception management
- **Health Checks**: Endpoint monitoring cho tất cả services

## 🔄 CI/CD và Deployment

### Development Environment
```bash
# Hot reload during development
dotnet watch run --project ContractProcessingSystem.AppHost
```

### Production Deployment
- **Container Images**: Docker containers cho tất cả services
- **Kubernetes**: Deployment manifests
- **Azure Container Apps**: Cloud-native deployment
- **Infrastructure as Code**: Bicep/ARM templates

## 📈 Future Enhancements

### Roadmap
1. **Web UI Dashboard** - React/Blazor frontend
2. **Advanced Analytics** - Business intelligence dashboards
3. **Workflow Automation** - Visual workflow designer
4. **Multi-language Support** - International contract processing
5. **Integration APIs** - Third-party system connectors
6. **Mobile Apps** - iOS/Android clients
7. **Real-time Collaboration** - Multi-user editing và commenting

### Extensibility
- **Plugin Architecture** - Custom processors
- **Custom AI Models** - Fine-tuned models cho specific domains
- **Third-party Integrations** - CRM, ERP, Legal systems
- **API Gateway** - Unified API surface với versioning

## 📚 Tài liệu tham khảo

### Architecture Documentation
- [System Architecture](./system-architecture.md)
- [API Documentation](./api-docs/)
- [Database Schema](./database-schema.md)
- [Deployment Guide](./deployment-guide.md)

### External Resources
- [.NET Aspire Documentation](https://docs.microsoft.com/en-us/dotnet/aspire/)
- [Milvus Documentation](https://milvus.io/docs)
- [Azure OpenAI Service](https://docs.microsoft.com/en-us/azure/cognitive-services/openai/)
- [Semantic Kernel](https://docs.microsoft.com/en-us/semantic-kernel/)

## 🤝 Contributing

### Development Setup
1. Fork repository
2. Create feature branch
3. Implement changes với tests
4. Submit pull request với documentation

### Code Standards
- **C# Coding Guidelines**: Microsoft standards
- **API Design**: RESTful principles
- **Testing**: Unit và integration tests
- **Documentation**: XML comments và README updates

---

**Hệ thống Contract Processing với AI Agent** - Một giải pháp hoàn chỉnh cho việc quản lý và phân tích hợp đồng thông minh với công nghệ AI tiên tiến.