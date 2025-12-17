# Document Context Query Guide

## ?? How to Ask Questions About Specific Contracts

This guide explains how to properly query the ContractProcessingSystem when you want answers about a **specific document** (e.g., "What happens if Jessica is laid off after 18 months?").

---

## ?? The Problem

**Without Document Context:**
```json
{
  "query": "What happens if Jessica is laid off after 18 months?",
  "maxResults": 10,
  "minScore": 0.3
}
```

**Result:** ? Returns results from ALL contracts in the database that mention "laid off" or "18 months"

---

## ? Solution 1: Use QueryService with document_id Filter

### **API Endpoint: Semantic Search with Filter**

```
POST https://localhost:7004/api/query/search
Content-Type: application/json

{
  "query": "What happens if Jessica is laid off after 18 months?",
  "maxResults": 10,
  "minScore": 0.3,
  "filters": {
    "document_id": "abc-123-def-456-789-012"
  }
}
```

### **Expected Response:**
```json
[
  {
    "documentId": "abc-123-def-456-789-012",
    "chunkId": "chunk-guid-here",
    "content": "10. TERMINATION\nAt-Will Employment: Either party may terminate...\nSeverance (if terminated without cause):\n- 2 weeks base salary for every year of service...",
    "score": 0.92,
    "metadata": {
      "title": "Employment Contract - Jessica Martinez",
      "contractType": "Employment Contract",
      "parties": [
        "Innovation Tech Solutions (EMPLOYER)",
        "Jessica Martinez (EMPLOYEE)"
      ],
      "contractValue": 145000,
      "currency": "USD"
    },
    "highlights": {
      "similarity": 0.92,
      "model": "text-embedding-004",
      "match_type": "qdrant_vector_search",
      "storage": "qdrant",
      "filtered_by_document": true
    }
  },
  {
    "documentId": "abc-123-def-456-789-012",
    "chunkId": "chunk-guid-2",
    "content": "4. BONUS AND EQUITY\nStock Options: 5,000 shares\nVesting Schedule: 4-year vest with 1-year cliff\nAcceleration: 25% of unvested options accelerate on termination without cause...",
    "score": 0.87,
    "metadata": {
      "title": "Employment Contract - Jessica Martinez",
      ...
    },
    "highlights": {
      "filtered_by_document": true
    }
  }
]
```

---

## ? Solution 2: Use AIAgent Chat (Recommended for Conversational Questions)

### **API Endpoint: AI Chat with Context**

```
POST https://localhost:7004/api/aiagent/chat
Content-Type: application/json

{
  "message": "What happens if Jessica is laid off after 18 months?",
  "contextDocumentId": "abc-123-def-456-789-012"
}
```

### **Expected Response:**
```json
{
  "success": true,
  "response": "If Jessica Martinez is terminated **without cause** after 18 months (1.5 years), here's what she would receive:\n\n**Severance Package:**\n- Base Severance: 2 weeks per year of service\n- Calculation: 1.5 years × 2 weeks = 3 weeks base salary\n- Amount: ~$8,365\n\n**Continued Benefits:**\n- Health insurance continued for 3 months (~$1,500 value)\n\n**Stock Option Acceleration:**\n- 25% of unvested options accelerate immediately\n- Total vested after acceleration: ~2,660 shares (53% of 5,000)\n\n**Non-Compete:**\n- Employer will pay 50% of base salary ($72,500) if they enforce the non-compete clause\n\n**Total Estimated Value:**\n- Severance: $8,365\n- Benefits: $1,500\n- Accelerated stock: Value depends on company valuation\n- Potential non-compete payment: $72,500 (if enforced)"
}
```

---

## ? Solution 3: Use MCP Tools (For AI Agent Integration)

### **MCP Tool: Chat**

```json
{
  "tool": "Chat",
  "parameters": {
    "message": "What happens if Jessica is laid off after 18 months?",
    "contextDocumentId": "abc-123-def-456-789-012"
  }
}
```

### **MCP Tool: SemanticSearch**

```json
{
  "tool": "SemanticSearch",
  "parameters": {
    "searchRequestJson": "{\"query\":\"What happens if Jessica is laid off after 18 months?\",\"maxResults\":10,\"minScore\":0.3,\"filters\":{\"document_id\":\"abc-123-def-456-789-012\"}}"
  }
}
```

---

## ?? Implementation Details

### **How Filtering Works:**

1. **QueryService** receives the search request with `filters.document_id`
2. **QueryService** passes filters to **VectorService**
3. **VectorService** applies the filter to:
   - **Qdrant search**: Uses Qdrant's native filtering (most efficient)
   - **In-memory fallback**: Filters results in code

### **Qdrant Filter Query:**
```csharp
var searchFilter = new Filter
{
    Must =
    {
        new Condition
        {
            Field = new FieldCondition
            {
                Key = "document_id",
                Match = new Match { Keyword = documentId }
            }
        }
    }
};

var results = await _qdrantClient.SearchAsync(
    collectionName: "contract_embeddings",
    vector: queryVector,
    limit: maxResults,
    scoreThreshold: minScore,
    filter: searchFilter  // ?? Filters at database level
);
```

### **In-Memory Filter Logic:**
```csharp
foreach (var embedding in _embeddingStore.Values)
{
    var documentId = _chunkToDocumentMap[embedding.ChunkId];
    
    // Skip if doesn't match filter
    if (filterDocumentId.HasValue && documentId != filterDocumentId.Value)
        continue;
    
    // Calculate similarity and add to results
    var similarity = CalculateCosineSimilarity(queryVector, embedding.Vector);
    if (similarity >= minScore)
        results.Add((embedding, similarity, documentId));
}
```

---

## ?? Comparison of Approaches

| Approach | Use Case | Response Format | Pros | Cons |
|----------|----------|----------------|------|------|
| **QueryService `/search`** | Programmatic search | JSON array of chunks | Fast, filtered results | Requires post-processing |
| **AIAgent `/chat`** | User-facing Q&A | Natural language answer | User-friendly, contextual | Slower (AI generation) |
| **MCP Tools** | AI agent integration | JSON with tool response | Agent-compatible | Requires MCP client |

---

## ?? When to Use Each Approach

### **Use QueryService `/search`**
- Building custom UI
- Need raw search results
- Want to control result presentation
- Programmatic integrations

**Example:**
```typescript
// Frontend code
const searchResults = await fetch('/api/query/search', {
  method: 'POST',
  body: JSON.stringify({
    query: userQuestion,
    filters: { document_id: selectedDocumentId }
  })
});

// Display results in custom UI
results.forEach(result => {
  displayChunk(result.content, result.score);
});
```

---

### **Use AIAgent `/chat`**
- End-user Q&A interface
- Need conversational answers
- Want AI to synthesize multiple chunks
- Building chatbots

**Example:**
```typescript
// Chatbot code
const answer = await fetch('/api/aiagent/chat', {
  method: 'POST',
  body: JSON.stringify({
    message: userQuestion,
    contextDocumentId: currentDocument.id
  })
});

// Display AI's answer
displayMessage(answer.response);
```

---

### **Use MCP Tools**
- Integrating with Claude Desktop
- Building AI agents
- Need tool-based interactions
- Model Context Protocol clients

**Example (Claude Desktop config):**
```json
{
  "mcpServers": {
    "contract-system": {
      "command": "dotnet",
      "args": ["run", "--project", "ContractProcessingSystem.AIAgent"],
      "env": {
        "ASPNETCORE_URLS": "https://localhost:7004"
      }
    }
  }
}
```

---

## ?? Complete Example Workflow

### **Scenario: User asks about Jessica's termination benefits**

#### **Step 1: Get Document ID**
```
GET https://localhost:7048/api/documents?fileName=Employment_Contract_Jessica_Martinez.pdf

Response:
{
  "id": "abc-123-def-456-789-012",
  "fileName": "Employment_Contract_Jessica_Martinez.pdf",
  "status": "ProcessingComplete"
}
```

#### **Step 2: Ask Question with Context**
```
POST https://localhost:7004/api/aiagent/chat
Content-Type: application/json

{
  "message": "What happens if Jessica is laid off after 18 months?",
  "contextDocumentId": "abc-123-def-456-789-012"
}
```

#### **Step 3: Get Detailed Answer**
```json
{
  "success": true,
  "response": "Comprehensive answer about severance, benefits, stock options..."
}
```

---

## ?? Filtering Capabilities

### **Supported Filters:**

| Filter Key | Type | Example | Description |
|------------|------|---------|-------------|
| `document_id` | `string` (GUID) | `"abc-123-def..."` | Filter to specific document |
| `contract_type` | `string` | `"Employment Contract"` | Filter by contract type (future) |
| `date_range` | `object` | `{"start":"2023-01-01","end":"2024-12-31"}` | Filter by date range (future) |
| `parties` | `array` | `["Acme Corp"]` | Filter by contract parties (future) |

**Currently Implemented:** ? `document_id`  
**Planned:** ? `contract_type`, `date_range`, `parties`

---

## ?? Best Practices

### **1. Always Use Document Context for Specific Questions**
```
? BAD:  "What is the base salary?"
? GOOD: "What is the base salary?" + document_id filter
? GOOD: "What is Jessica Martinez's base salary?" (AI can infer)
```

### **2. Lower minScore for Broader Context**
```json
{
  "query": "termination policy",
  "minScore": 0.3,  // ?? Lower threshold for related content
  "filters": { "document_id": "..." }
}
```

### **3. Use Appropriate maxResults**
```json
{
  "query": "compensation package details",
  "maxResults": 20,  // ?? More results for complex topics
  "filters": { "document_id": "..." }
}
```

### **4. Combine with Follow-up Questions**
```
User: "What are the termination conditions?"
AI: "At-will employment with 2-week notice preferred..."

User: "And what about severance?"  // ?? Context preserved
AI: "Severance is 2 weeks per year of service..."
```

---

## ?? Troubleshooting

### **Problem: Getting results from multiple contracts**
**Solution:** Add `document_id` filter
```json
{ "filters": { "document_id": "your-document-guid" } }
```

### **Problem: No results returned**
**Possible causes:**
1. Document not processed yet (check status)
2. minScore too high (try 0.3)
3. Query doesn't match content (rephrase)
4. Wrong document_id

**Debug:**
```
GET https://localhost:7197/api/vector/collection/info

Response shows:
- TotalEmbeddings: 85
- TotalDocuments: 5
- QdrantAvailable: true
```

### **Problem: Empty or generic answers**
**Solution:** 
1. Check document has been chunked
2. Verify embeddings were generated
3. Try more specific questions

---

## ?? Related Documentation

- [DOCUMENT_CONTEXT_QUESTIONS_GUIDE.md](./DOCUMENT_CONTEXT_QUESTIONS_GUIDE.md) - Example questions
- [EMPLOYMENT_CONTRACT_PROCESSING_ANALYSIS.md](./EMPLOYMENT_CONTRACT_PROCESSING_ANALYSIS.md) - Contract processing details
- [README.md](./README.md) - System architecture
- [CONFIGURATION_GUIDE.md](./CONFIGURATION_GUIDE.md) - Setup instructions

---

## ? Summary

**To ask a question about a specific document:**

1. **Get the document ID** from DocumentUpload service
2. **Add it to your request:**
   - QueryService: Use `filters.document_id`
   - AIAgent: Use `contextDocumentId`
   - MCP: Use `contextDocumentId` parameter
3. **Ask your question** naturally

**Result:** You'll get **precise, context-specific answers** instead of results from all contracts!

---

**Version:** 1.0  
**Last Updated:** January 2025  
**Applies To:** ContractProcessingSystem v1.0  
**Changes Required:** VectorService, QueryService, IVectorService interface
