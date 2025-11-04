# Official ModelContextProtocol.AspNetCore Implementation

## Overview

The DocumentUpload service now uses the **official ModelContextProtocol.AspNetCore package** (v0.4.0-preview.3) to provide MCP server capabilities.

## What Changed

### 1. Package Integration

**Added Package Reference:**
- `ModelContextProtocol.AspNetCore` v0.4.0-preview.3 to `ContractProcessingSystem.DocumentUpload.csproj`
- Also available in `ContractProcessingSystem.Shared.csproj` for shared MCP types

### 2. MCP Tools Implementation

**File:** `ContractProcessingSystem.DocumentUpload/MCPTools/DocumentUploadTools.cs`

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;  // Official package namespace

[McpServerToolType]  // Marks class as containing MCP tools
public class DocumentUploadTools
{
    [McpServerTool]  // Marks method as an MCP tool
    [Description("Upload a document to the system")]
    public async Task<string> UploadDocument(
        [Description("The filename")] string filename,
        [Description("Content type")] string contentType,
        [Description("Base64 content")] string content)
    {
        // Implementation using DocumentUploadService
    }
    
    // 4 more tools: GetDocument, ListDocuments, DeleteDocument, GetUploadStatistics
}
```

**Key Features:**
- Uses `[McpServerToolType]` attribute to mark the tools class
- Uses `[McpServerTool]` attribute on each tool method
- Uses `[Description]` attributes for documentation (from System.ComponentModel)
- Tools are automatically discovered and registered by the MCP framework

### 3. Program.cs Configuration

**MCP Server Setup:**

```csharp
using ModelContextProtocol.Server;  // Official package

var builder = WebApplication.CreateBuilder(args);

// Register MCP Server with official package
builder.Services.AddMcpServer()
    .WithHttpTransport()        // Use HTTP transport
    .WithToolsFromAssembly();   // Auto-discover tools with [McpServerToolType]

var app = builder.Build();

// Map MCP endpoint (default: /mcp)
app.MapMcp();

app.Run();
```

**What This Does:**
1. `AddMcpServer()` - Registers MCP server services
2. `WithHttpTransport()` - Configures HTTP-based JSON-RPC 2.0 transport
3. `WithToolsFromAssembly()` - Scans assembly for `[McpServerToolType]` classes
4. `MapMcp()` - Creates endpoint at `/mcp` for MCP protocol communication

## MCP Tools Available

### 1. UploadDocument
- **Description:** Upload a document to the system
- **Parameters:**
  - `filename` (string): Document filename
  - `contentType` (string): MIME type
  - `content` (string): Base64 encoded content
  - `uploadedBy` (string): Username (optional, defaults to "mcp-client")
- **Returns:** JSON with document metadata

### 2. GetDocument
- **Description:** Get details for a specific document
- **Parameters:**
  - `documentId` (string): Document GUID
- **Returns:** JSON with document details

### 3. ListDocuments
- **Description:** List documents with pagination
- **Parameters:**
  - `page` (int): Page number (default: 1)
  - `pageSize` (int): Items per page (default: 10)
- **Returns:** JSON with document list

### 4. DeleteDocument
- **Description:** Delete a document
- **Parameters:**
  - `documentId` (string): Document GUID to delete
- **Returns:** JSON with success status

### 5. GetUploadStatistics
- **Description:** Get upload statistics
- **Parameters:**
  - `period` (string): Time grouping (day/week/month/year, default: "month")
  - `groupBy` (string): Group by field (status/user/contentType, default: "status")
- **Returns:** JSON with statistics

## MCP Protocol Compliance

The official package provides:

1. **JSON-RPC 2.0 Protocol**
   - Standard request/response handling
   - Error codes and messages per MCP spec

2. **MCP Methods Support**
   - `initialize` - Server initialization
   - `tools/list` - List available tools
   - `tools/call` - Execute a tool
   - Server information and capabilities

3. **Type Safety**
   - Strong typing for tool parameters
   - Schema validation
   - Type coercion where appropriate

4. **Auto-Discovery**
   - Tools are automatically discovered via reflection
   - No manual registration required
   - Description attributes become tool documentation

## Testing the MCP Endpoint

### Using HTTP Client

```bash
# List available tools
curl -X POST http://localhost:5001/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list"
  }'

# Call a tool
curl -X POST http://localhost:5001/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/call",
    "params": {
      "name": "ListDocuments",
      "arguments": {
        "page": 1,
        "pageSize": 10
      }
    }
  }'
```

### Using MCP Client

AI agents like Claude can connect to this endpoint using the MCP protocol:

```json
{
  "mcpServers": {
    "documentUpload": {
      "command": "http",
      "args": ["http://localhost:5001/mcp"]
    }
  }
}
```

## Benefits of Official Package

1. **Standards Compliance** - Full MCP specification compliance
2. **Automatic Tool Discovery** - No boilerplate registration code
3. **Type Safety** - Compile-time checking of tool signatures
4. **Documentation** - Description attributes become API documentation
5. **Maintenance** - Updates handled by official package maintainers
6. **Interoperability** - Works with any MCP-compliant client

## Next Steps

To extend MCP to other services:

1. **Add Package Reference**
   ```xml
   <PackageReference Include="ModelContextProtocol.AspNetCore" Version="0.4.0-preview.3" />
   ```

2. **Create Tools Class**
   ```csharp
   [McpServerToolType]
   public class MyServiceTools {
       [McpServerTool]
       [Description("Tool description")]
       public Task<string> MyTool(string param) { ... }
   }
   ```

3. **Configure in Program.cs**
   ```csharp
   builder.Services.AddMcpServer()
       .WithHttpTransport()
       .WithToolsFromAssembly();
   
   app.MapMcp();
   ```

## Architecture

```
┌─────────────────────────────────────────────────┐
│  AI Agent (Claude, GPT-4, etc.)                 │
│  Uses MCP Protocol                               │
└───────────────┬─────────────────────────────────┘
                │ JSON-RPC 2.0 over HTTP
                │
┌───────────────▼─────────────────────────────────┐
│  DocumentUpload Service                          │
│  ┌────────────────────────────────────────┐    │
│  │ Official MCP Server                     │    │
│  │ (ModelContextProtocol.AspNetCore)       │    │
│  │                                          │    │
│  │  • Endpoint: POST /mcp                   │    │
│  │  • Protocol: JSON-RPC 2.0                │    │
│  │  • Auto-discovers [McpServerToolType]    │    │
│  └────────────┬───────────────────────────┘    │
│               │                                  │
│  ┌────────────▼───────────────────────────┐    │
│  │ DocumentUploadTools                     │    │
│  │ [McpServerToolType]                     │    │
│  │                                          │    │
│  │  • UploadDocument                       │    │
│  │  • GetDocument                           │    │
│  │  • ListDocuments                         │    │
│  │  • DeleteDocument                        │    │
│  │  • GetUploadStatistics                   │    │
│  └────────────┬───────────────────────────┘    │
│               │                                  │
│  ┌────────────▼───────────────────────────┐    │
│  │ DocumentUploadService                   │    │
│  │ (Business Logic)                        │    │
│  └─────────────────────────────────────────┘    │
└─────────────────────────────────────────────────┘
```

## Comparison: Custom vs Official Implementation

| Aspect | Custom Implementation | Official Package |
|--------|----------------------|------------------|
| **Setup Complexity** | Manual JSON-RPC handling | Simple fluent configuration |
| **Tool Registration** | Manual dictionary/reflection | Automatic via attributes |
| **Protocol Compliance** | Manual implementation | Built-in, spec-compliant |
| **Type Safety** | Runtime type checking | Compile-time validation |
| **Maintenance** | Must maintain protocol code | Package handles updates |
| **Documentation** | Manual OpenAPI/Swagger | Auto-generated from attributes |
| **Error Handling** | Custom error codes | Standard MCP error codes |
| **Interoperability** | Depends on implementation | Guaranteed with MCP clients |

## Conclusion

The official `ModelContextProtocol.AspNetCore` package provides a production-ready, standards-compliant MCP server implementation with minimal boilerplate. The DocumentUpload service demonstrates how to:

1. Use official package attributes for tool definition
2. Leverage automatic tool discovery
3. Maintain clean separation between MCP layer and business logic
4. Provide type-safe, documented APIs to AI agents

This approach is recommended for all services going forward as it provides better maintainability, compliance, and developer experience.
