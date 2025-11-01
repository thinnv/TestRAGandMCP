# Demo Scripts for Contract Processing System

## Quick Start Demo

### Prerequisites
```bash
# Ensure infrastructure is running
docker-compose up -d

# Start the Aspire application
cd ContractProcessingSystem.AppHost
dotnet run
```

### Demo Script 1: Complete Document Processing Workflow

```bash
# 1. Upload a contract document
DOCUMENT_ID=$(curl -s -X POST "https://localhost:5000/api/documents/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@sample-contract.pdf" \
  -F "uploadedBy=demo@example.com" | jq -r '.id')

echo "Document uploaded with ID: $DOCUMENT_ID"

# 2. Parse the document
curl -X POST "https://localhost:5001/api/parsing/$DOCUMENT_ID/parse"

# 3. Generate chunks
curl -X POST "https://localhost:5001/api/parsing/$DOCUMENT_ID/chunk"

# 4. Generate embeddings (this would typically be called automatically)
curl -X POST "https://localhost:5002/api/embeddings/batch-process/$DOCUMENT_ID" \
  -H "Content-Type: application/json" \
  -d '[{"id": "chunk1", "documentId": "'$DOCUMENT_ID'", "content": "Sample chunk content"}]'

# 5. Search for similar content
curl -X POST "https://localhost:5004/api/query/search" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "payment terms and conditions",
    "maxResults": 5,
    "minScore": 0.7
  }'
```

### Demo Script 2: AI Agent Interaction

```bash
# Chat with AI about a specific contract
curl -X POST "https://localhost:5005/api/ai-agent/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What are the key payment terms in this contract?",
    "contextDocumentId": "'$DOCUMENT_ID'"
  }'

# Analyze contract comprehensively
curl -X POST "https://localhost:5005/api/ai-agent/analyze/$DOCUMENT_ID"

# Process document through AI workflow
curl -X POST "https://localhost:5005/api/ai-agent/process/$DOCUMENT_ID"
```

### Demo Script 3: Summarization and Comparison

```bash
# Generate risk assessment summary
curl -X POST "https://localhost:5004/api/query/summarize" \
  -H "Content-Type: application/json" \
  -d '{
    "documentIds": ["'$DOCUMENT_ID'"],
    "type": "RiskAssessment",
    "maxLength": 300,
    "focus": "financial obligations and penalties"
  }'

# Compare multiple contracts (when you have multiple documents)
curl -X POST "https://localhost:5005/api/ai-agent/compare" \
  -H "Content-Type: application/json" \
  -d '{
    "documentIds": ["'$DOCUMENT_ID'", "another-doc-id"]
  }'
```

## Testing Different Contract Types

### Service Agreement Demo
```bash
# Upload service agreement
SERVICE_CONTRACT=$(curl -s -X POST "https://localhost:5000/api/documents/upload" \
  -F "file=@service-agreement.pdf" \
  -F "uploadedBy=demo@example.com" | jq -r '.id')

# Ask specific questions about service terms
curl -X POST "https://localhost:5005/api/ai-agent/chat" \
  -d '{
    "message": "What are the service level agreements and penalties?",
    "contextDocumentId": "'$SERVICE_CONTRACT'"
  }'
```

### NDA Demo
```bash
# Upload NDA
NDA_CONTRACT=$(curl -s -X POST "https://localhost:5000/api/documents/upload" \
  -F "file=@nda.pdf" \
  -F "uploadedBy=demo@example.com" | jq -r '.id')

# Analyze confidentiality terms
curl -X POST "https://localhost:5005/api/ai-agent/chat" \
  -d '{
    "message": "What information is considered confidential and what are the disclosure restrictions?",
    "contextDocumentId": "'$NDA_CONTRACT'"
  }'
```

## Performance Testing

### Load Testing Script
```bash
#!/bin/bash
# Load test the document upload endpoint

for i in {1..10}; do
  (
    curl -X POST "https://localhost:5000/api/documents/upload" \
      -F "file=@test-contract-$i.pdf" \
      -F "uploadedBy=loadtest@example.com" &
  )
done
wait
echo "Load test completed"
```

### Concurrent Search Testing
```bash
#!/bin/bash
# Test concurrent search operations

queries=(
  "payment terms"
  "termination clause"
  "liability limitations"
  "intellectual property"
  "confidentiality obligations"
)

for query in "${queries[@]}"; do
  (
    curl -X POST "https://localhost:5004/api/query/search" \
      -H "Content-Type: application/json" \
      -d '{"query": "'$query'", "maxResults": 10}' &
  )
done
wait
```

## Monitoring and Health Checks

### Health Check Script
```bash
#!/bin/bash
# Check health of all services

services=(
  "document-upload:5000"
  "document-parser:5001"
  "embedding-service:5002"
  "vector-service:5003"
  "query-service:5004"
  "ai-agent:5005"
)

for service in "${services[@]}"; do
  name=${service%:*}
  port=${service#*:}
  
  echo "Checking $name..."
  response=$(curl -s -o /dev/null -w "%{http_code}" "https://localhost:$port/api/health" || echo "000")
  
  if [ "$response" = "200" ]; then
    echo "✅ $name is healthy"
  else
    echo "❌ $name is unhealthy (HTTP $response)"
  fi
done
```

### Infrastructure Health Check
```bash
#!/bin/bash
# Check infrastructure components

echo "Checking infrastructure..."

# Check Milvus
if curl -s "http://localhost:9091/healthz" > /dev/null; then
  echo "✅ Milvus is running"
else
  echo "❌ Milvus is not responding"
fi

# Check Redis
if redis-cli -h localhost -p 6379 ping | grep -q PONG; then
  echo "✅ Redis is running"
else
  echo "❌ Redis is not responding"
fi

# Check MinIO
if curl -s "http://localhost:9000/minio/health/live" > /dev/null; then
  echo "✅ MinIO is running"
else
  echo "❌ MinIO is not responding"
fi
```

## Development Utilities

### Database Reset Script
```bash
#!/bin/bash
# Reset all databases for fresh start

echo "Resetting databases..."

# Drop and recreate SQL Server database
cd ContractProcessingSystem.DocumentUpload
dotnet ef database drop --force
dotnet ef database update

# Clear Redis cache
redis-cli -h localhost -p 6379 FLUSHALL

# Reset Milvus collections (requires Milvus CLI or API calls)
echo "Milvus reset requires manual intervention via API or UI"

echo "Database reset completed"
```

### Log Analysis Script
```bash
#!/bin/bash
# Analyze application logs for errors

echo "Analyzing logs for errors and warnings..."

# Find recent error logs
find . -name "*.log" -mtime -1 -exec grep -l "ERROR\|WARN" {} \;

# Show error summary
echo "Error summary from last hour:"
find . -name "*.log" -mtime -1 -exec grep "ERROR" {} \; | wc -l
```

## Sample Data Generation

### Generate Test Contracts
```python
# generate_test_contracts.py
import os
from fpdf import FPDF

contracts = [
    {
        "title": "Service Agreement",
        "content": """
        SERVICE AGREEMENT
        
        This Service Agreement is entered into between Company A and Company B.
        
        Payment Terms: Monthly payments of $10,000 due on the 1st of each month.
        
        Termination: Either party may terminate with 30 days written notice.
        
        Confidentiality: All information shared is confidential for 5 years.
        """
    },
    {
        "title": "Non-Disclosure Agreement",
        "content": """
        NON-DISCLOSURE AGREEMENT
        
        This NDA is between Discloser and Recipient.
        
        Confidential Information includes technical data, business plans, customer lists.
        
        Term: This agreement remains in effect for 3 years.
        
        Penalties: $50,000 penalty for unauthorized disclosure.
        """
    }
]

for i, contract in enumerate(contracts):
    pdf = FPDF()
    pdf.add_page()
    pdf.set_font("Arial", size=12)
    
    for line in contract["content"].strip().split('\n'):
        pdf.cell(200, 10, txt=line.strip(), ln=1, align='L')
    
    pdf.output(f"test-contract-{i+1}.pdf")

print("Test contracts generated")
```

## API Testing with Postman Collection

### Postman Collection Export
```json
{
  "info": {
    "name": "Contract Processing System API",
    "description": "Complete API collection for testing the contract processing system"
  },
  "item": [
    {
      "name": "Upload Document",
      "request": {
        "method": "POST",
        "header": [],
        "body": {
          "mode": "formdata",
          "formdata": [
            {
              "key": "file",
              "type": "file",
              "src": "contract.pdf"
            },
            {
              "key": "uploadedBy",
              "value": "test@example.com",
              "type": "text"
            }
          ]
        },
        "url": {
          "raw": "https://localhost:5000/api/documents/upload",
          "protocol": "https",
          "host": ["localhost"],
          "port": "5000",
          "path": ["api", "documents", "upload"]
        }
      }
    },
    {
      "name": "Parse Document",
      "request": {
        "method": "POST",
        "header": [],
        "url": {
          "raw": "https://localhost:5001/api/parsing/{{documentId}}/parse",
          "protocol": "https",
          "host": ["localhost"],
          "port": "5001",
          "path": ["api", "parsing", "{{documentId}}", "parse"]
        }
      }
    },
    {
      "name": "Search Contracts",
      "request": {
        "method": "POST",
        "header": [
          {
            "key": "Content-Type",
            "value": "application/json"
          }
        ],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"query\": \"payment terms\",\n  \"maxResults\": 10,\n  \"minScore\": 0.7\n}"
        },
        "url": {
          "raw": "https://localhost:5004/api/query/search",
          "protocol": "https",
          "host": ["localhost"],
          "port": "5004",
          "path": ["api", "query", "search"]
        }
      }
    }
  ]
}
```

## Troubleshooting Scripts

### Service Connectivity Test
```bash
#!/bin/bash
# Test connectivity between services

echo "Testing service connectivity..."

# Test document upload to parser communication
UPLOAD_URL="https://localhost:5000"
PARSER_URL="https://localhost:5001"

echo "Testing upload -> parser communication..."
if curl -s "$UPLOAD_URL/api/health" && curl -s "$PARSER_URL/api/health"; then
  echo "✅ Upload and Parser services are reachable"
else
  echo "❌ Service connectivity issue"
fi

# Test embedding service to vector service
EMBEDDING_URL="https://localhost:5002"
VECTOR_URL="https://localhost:5003"

echo "Testing embedding -> vector communication..."
if curl -s "$EMBEDDING_URL/api/health" && curl -s "$VECTOR_URL/api/health"; then
  echo "✅ Embedding and Vector services are reachable"
else
  echo "❌ Service connectivity issue"
fi
```

### Performance Monitoring
```bash
#!/bin/bash
# Monitor system performance during operation

echo "Starting performance monitoring..."

# Monitor CPU and memory usage
while true; do
  echo "$(date): CPU: $(top -l 1 | grep "CPU usage" | cut -d' ' -f3 | cut -d'%' -f1)%, Memory: $(vm_stat | grep "Pages free" | awk '{print $3}')"
  sleep 5
done &

# Monitor service response times
while true; do
  for port in 5000 5001 5002 5003 5004 5005; do
    response_time=$(curl -o /dev/null -s -w "%{time_total}" "https://localhost:$port/api/health")
    echo "$(date): Service $port response time: ${response_time}s"
  done
  sleep 10
done
```

These demo scripts provide comprehensive testing capabilities for the Contract Processing System, covering everything from basic functionality to performance testing and troubleshooting.