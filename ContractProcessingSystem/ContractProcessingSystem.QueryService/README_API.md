# QueryService REST API Documentation

The QueryService provides intelligent search and summarization capabilities for contract documents using AI-powered semantic understanding.

## Base URL
```
https://localhost:7004/api/query
```

## Features

? **Semantic Search** - Natural language search using vector embeddings  
? **Hybrid Search** - Combined semantic and keyword-based search  
? **AI Summarization** - Intelligent document summarization with multiple styles  
? **Multi-document Analysis** - Compare and analyze multiple contracts  
? **Advanced Filtering** - Filter by document type, dates, parties, etc.  

---

## Endpoints

### 1. Health Check

Check if the service is running and healthy.

**Endpoint:** `GET /api/query/health`

**Response:**
```json
{
  "service": "QueryService",
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "features": [
    "Semantic Search",
    "Hybrid Search",
    "AI Summarization",
    "Multi-document Analysis"
  ]
}
```

---

### 2. Get Service Capabilities

Get detailed information about service features and configuration.

**Endpoint:** `GET /api/query/capabilities`

**Response:**
```json
{
  "service": "QueryService",
  "version": "1.0.0",
  "capabilities": {
    "semanticSearch": {
      "enabled": true,
      "maxResults": 100,
      "minScore": 0.0,
      "defaultScore": 0.7,
      "supportedFilters": ["document_id", "contract_type", "date_range", "parties"]
    },
    "hybridSearch": {
      "enabled": true,
      "combinesSemanticAndKeyword": true,
      "weightingStrategy": "70% Semantic, 30% Keyword"
    },
    "summarization": {
      "enabled": true,
      "supportedTypes": ["Overview", "KeyTerms", "RiskAssessment", "Comparison"],
      "maxLength": { "min": 50, "max": 5000, "default": 500 },
      "maxDocuments": 20
    }
  }
}
```

---

### 3. Semantic Search

Perform natural language search on contract documents using AI embeddings.

**Endpoint:** `POST /api/query/search`

**Request Body:**
```json
{
  "query": "What are the payment terms in the contract?",
  "maxResults": 10,
  "minScore": 0.7,
  "documentTypes": ["service_agreement", "purchase_order"],
  "filters": {
    "contract_type": "service_agreement",
    "date_range": {
      "start": "2023-01-01",
      "end": "2024-12-31"
    }
  }
}
```

**Parameters:**
- `query` (string, required): Natural language search query
- `maxResults` (int, optional, default: 10): Maximum number of results to return
- `minScore` (float, optional, default: 0.7): Minimum relevance score (0.0 - 1.0)
- `documentTypes` (array, optional): Filter by document types
- `filters` (object, optional): Additional filters

**Response:**
```json
[
  {
    "documentId": "123e4567-e89b-12d3-a456-426614174000",
    "chunkId": "456e7890-e89b-12d3-a456-426614174001",
    "content": "Payment shall be made within 30 days of invoice...",
    "score": 0.89,
    "metadata": {
      "title": "Service Agreement - Acme Corp",
      "contractDate": "2024-01-15",
      "parties": ["Acme Corp", "Client LLC"],
      "contractType": "Service Agreement"
    },
    "highlights": {
      "similarity": 0.89,
      "model": "text-embedding-004",
      "match_type": "qdrant_vector_search",
      "storage": "qdrant"
    }
  }
]
```

---

### 4. Hybrid Search

Combine semantic understanding with keyword matching for more precise results.

**Endpoint:** `POST /api/query/search/hybrid`

**Request Body:**
```json
{
  "query": "liability insurance coverage requirements",
  "filters": {
    "document_id": "123e4567-e89b-12d3-a456-426614174000"
  },
  "maxResults": 15,
  "minScore": 0.65
}
```

**Parameters:**
- `query` (string, required): Search query
- `filters` (object, optional): Filter criteria
- `maxResults` (int, optional, default: 10): Maximum results
- `minScore` (float, optional, default: 0.7): Minimum score threshold

**Response:** Same format as Semantic Search

**How it works:**
- 70% weight on semantic similarity
- 30% weight on keyword matching
- Results are re-ranked using combined scoring

---

### 5. Summarize Documents

Generate AI-powered summaries of contract documents.

**Endpoint:** `POST /api/query/summarize`

**Request Body:**
```json
{
  "documentIds": [
    "123e4567-e89b-12d3-a456-426614174000",
    "456e7890-e89b-12d3-a456-426614174001"
  ],
  "type": "Overview",
  "maxLength": 500,
  "focus": "financial obligations and payment schedules"
}
```

**Parameters:**
- `documentIds` (array, required): List of document IDs to summarize
- `type` (enum, required): Summarization type
  - `Overview`: General contract overview
  - `KeyTerms`: Extract key terms and conditions
  - `RiskAssessment`: Analyze risks and liabilities
  - `Comparison`: Compare multiple contracts
- `maxLength` (int, optional, default: 500): Maximum length in words (50-5000)
- `focus` (string, optional): Specific aspect to focus on

**Response:**
```json
{
  "summary": "This service agreement establishes a relationship between Acme Corp and Client LLC for the provision of software development services. The contract includes standard payment terms of Net 30, with a total contract value of $150,000...",
  "keyPoints": [
    "Contract value: $150,000",
    "Payment terms: Net 30 days",
    "Duration: 12 months from signing",
    "Termination clause: 30 days written notice",
    "Intellectual property rights assigned to client"
  ],
  "insights": {
    "documentCount": 2,
    "summaryType": "Overview",
    "focus": "financial obligations and payment schedules",
    "wordCount": 487,
    "llmProvider": "Gemini"
  },
  "generatedAt": "2024-01-15T10:30:00Z"
}
```

---

## Summary Types Explained

### Overview
Provides a comprehensive summary covering:
- Main purpose and scope
- Key parties involved
- Important terms and conditions
- Notable obligations
- Significant dates and deadlines

### KeyTerms
Focuses on extracting and summarizing:
- Payment terms and amounts
- Delivery/performance obligations
- Termination conditions
- Liability and warranty clauses
- IP rights and confidentiality

### RiskAssessment
Analyzes potential risks including:
- Financial exposure
- Operational dependencies
- Legal compliance issues
- Termination and penalty risks
- Force majeure and liability caps

### Comparison
Compares multiple contracts highlighting:
- Terms and conditions variations
- Pricing differences
- Different obligations
- Risk allocation differences
- Unique clauses

---

## Example Use Cases

### 1. Find Payment Terms
```bash
curl -X POST https://localhost:7004/api/query/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "payment terms and invoicing schedule",
    "maxResults": 5,
    "minScore": 0.75
  }'
```

### 2. Compare Multiple Contracts
```bash
curl -X POST https://localhost:7004/api/query/summarize \
  -H "Content-Type: application/json" \
  -d '{
    "documentIds": ["doc1-guid", "doc2-guid", "doc3-guid"],
    "type": "Comparison",
    "maxLength": 1000,
    "focus": "pricing and payment terms"
  }'
```

### 3. Risk Analysis
```bash
curl -X POST https://localhost:7004/api/query/summarize \
  -H "Content-Type: application/json" \
  -d '{
    "documentIds": ["doc1-guid"],
    "type": "RiskAssessment",
    "maxLength": 750
  }'
```

### 4. Hybrid Search for Specific Clauses
```bash
curl -X POST https://localhost:7004/api/query/search/hybrid \
  -H "Content-Type: application/json" \
  -d '{
    "query": "force majeure pandemic natural disaster",
    "maxResults": 10,
    "minScore": 0.6
  }'
```

---

## Error Responses

### 400 Bad Request
```json
{
  "error": "Search query is required"
}
```

### 500 Internal Server Error
```json
{
  "error": "Internal server error occurred during search",
  "message": "Detailed error message"
}
```

---

## Integration with Other Services

The QueryService integrates with:

1. **VectorService** - For semantic search using embeddings
2. **EmbeddingService** - For query embedding generation
3. **DocumentUpload** - For document metadata retrieval
4. **Gemini AI** - For summarization and analysis

---

## Performance Considerations

- **Semantic Search**: Typically 100-300ms for 10 results
- **Hybrid Search**: Slightly slower (~150-400ms) due to dual ranking
- **Summarization**: 2-10 seconds depending on document size and complexity
- **Caching**: Query embeddings are cached for faster repeated searches

---

## Testing

Use the included `ContractProcessingSystem.QueryService.http` file with VS Code REST Client extension or similar tools for easy API testing.

---

## Configuration

Configure in `appsettings.json`:

```json
{
  "Services": {
    "VectorService": "https://localhost:7003",
    "EmbeddingService": "https://localhost:7002",
    "DocumentUpload": "https://localhost:7000"
  },
  "LLMProviders": {
    "DefaultProvider": "Gemini",
    "Providers": [
      {
        "Type": "Gemini",
        "Name": "Gemini",
        "ApiKey": "your-api-key",
        "DefaultChatModel": "gemini-2.5-flash"
      }
    ]
  }
}
```

---

## MCP Tools

The QueryService also exposes Model Context Protocol (MCP) tools for AI agent integration:

- `SemanticSearch` - Search via MCP
- `Summarize` - Summarization via MCP
- `HybridSearch` - Hybrid search via MCP

Access via: `https://localhost:7004/mcp`
