# MCP Demo - Contract Processing System

This demo showcases the Model Context Protocol (MCP) implementation for the Contract Processing System. MCP provides a standardized way for AI tools to interact with services through a JSON-RPC protocol.

## Overview

The Contract Processing System has been enhanced with MCP (Model Context Protocol) support, allowing AI agents to interact with contract processing services through standardized tool interfaces.

### What is MCP?

Model Context Protocol (MCP) is a standard that enables AI models to connect with external tools and data sources in a secure, controlled manner. It uses JSON-RPC for communication and provides:

- **Tools**: Functions that AI can call to perform actions
- **Resources**: Data sources that AI can read from
- **Standardized Interface**: Consistent API across different services

## MCP Architecture

```
AI Agent
    ↓ (JSON-RPC)
MCP Client
    ↓ (HTTP/JSON-RPC)
MCP Server (DocumentUpload)
    ↓
Service Layer
    ↓
Database/Storage
```

## Available MCP Tools

### DocumentUpload Service Tools

1. **upload_contract_file**
   - Upload contract documents with metadata
   - Parameters: fileName, contentType, fileContent (base64), uploadedBy, tags

2. **get_file_metadata**
   - Retrieve metadata for uploaded files
   - Parameters: fileId

3. **list_uploaded_files**
   - List all uploaded files with pagination
   - Parameters: limit, offset, uploadedBy (optional)

4. **delete_contract_file**
   - Delete uploaded contract files
   - Parameters: fileId, deletedBy

5. **update_file_metadata**
   - Update file metadata and tags
   - Parameters: fileId, updatedBy, tags, description

6. **get_upload_statistics**
   - Get upload statistics and analytics
   - Parameters: period (day/week/month), groupBy (status/user)

### Available Resources

- `contract://files` - List of all uploaded contract files
- `contract://statistics` - Upload statistics and metrics
- `contract://metadata/{fileId}` - Specific file metadata

## Running the Demo

### Prerequisites

1. .NET 9 SDK installed
2. SQL Server running (via Docker or local instance)
3. Contract Processing System services running

### Starting the Services

1. **Start Infrastructure:**
   ```powershell
   cd ContractProcessingSystem
   dotnet run --project ContractProcessingSystem.AppHost
   ```

2. **Verify Services:**
   - DocumentUpload: https://localhost:7001
   - DocumentParser: https://localhost:7002
   - EmbeddingService: https://localhost:7003
   - VectorService: https://localhost:7004
   - QueryService: https://localhost:7005
   - AIAgent: https://localhost:7006

### Running the MCP Demo

```powershell
cd ContractProcessingSystem
dotnet run --project ContractProcessingSystem.MCPDemo
```

### Expected Demo Flow

1. **Initialize MCP Connection**
   - Connect to DocumentUpload MCP server
   - List available tools and resources

2. **Upload Sample Contract**
   - Upload a sample contract file
   - Receive file ID and metadata

3. **List Files**
   - Retrieve list of uploaded files
   - Show pagination and filtering

4. **Get Statistics**
   - Retrieve upload statistics
   - Show analytics data

5. **Read Resources**
   - Access contract resources
   - Demonstrate resource reading

## MCP Implementation Details

### BaseMCPServer Class

The `BaseMCPServer` abstract class provides:
- JSON-RPC protocol handling
- Tool execution framework
- Resource management
- Error handling with MCP error codes
- Logging and monitoring

### MCPClient Class

The `MCPClient` class provides:
- HTTP-based MCP communication
- Tool calling capabilities
- Resource reading
- Error handling and retries
- Type-safe responses

### MCPModels

Standard MCP protocol models:
- `MCPRequest`/`MCPResponse` - Protocol messages
- `MCPTool` - Tool definitions
- `MCPResource` - Resource definitions
- `MCPServerInfo` - Server capabilities

## Extending with More Services

To add MCP support to other services:

1. **Create MCP Controller:**
   ```csharp
   public class ServiceMCPController : BaseMCPServer
   {
       // Implement abstract methods
   }
   ```

2. **Define Tools:**
   ```csharp
   protected override async Task<List<MCPTool>> GetAvailableTools()
   {
       return new List<MCPTool>
       {
           new MCPTool
           {
               Name = "tool_name",
               Description = "Tool description",
               InputSchema = // JSON schema
           }
       };
   }
   ```

3. **Implement Tool Execution:**
   ```csharp
   protected override async Task<MCPToolResult> ExecuteTool(string toolName, JsonElement arguments)
   {
       // Tool implementation
   }
   ```

## Testing MCP Endpoints

### Using HTTP Clients

```bash
# Initialize MCP server
curl -X POST https://localhost:7001/api/documentuploadmcp/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {}
  }'

# List tools
curl -X POST https://localhost:7001/api/documentuploadmcp/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list",
    "params": {}
  }'
```

### Using MCP Client

```csharp
var client = new MCPClient(httpClient, serverUrl, logger);
await client.InitializeAsync();

var result = await client.CallToolAsync<object>("upload_contract_file", new {
    fileName = "contract.pdf",
    contentType = "application/pdf",
    fileContent = base64Content,
    uploadedBy = "user@example.com"
});
```

## Troubleshooting

### Common Issues

1. **Connection Refused**
   - Ensure services are running on correct ports
   - Check firewall settings
   - Verify HTTPS certificates

2. **Tool Not Found**
   - Check tool name spelling
   - Verify server implements the tool
   - Check tool registration

3. **Invalid Parameters**
   - Validate JSON schema compliance
   - Check required parameters
   - Verify data types

### Debugging

1. **Enable Detailed Logging**
   ```json
   {
     "Logging": {
       "LogLevel": {
         "ContractProcessingSystem.Shared.MCP": "Debug"
       }
     }
   }
   ```

2. **Check MCP Protocol Messages**
   - Review request/response logs
   - Validate JSON-RPC format
   - Check error codes

## Future Enhancements

1. **Authentication/Authorization**
   - Add JWT token support
   - Implement role-based access
   - Add API key authentication

2. **More Service Integrations**
   - DocumentParser MCP tools
   - EmbeddingService MCP tools
   - VectorService MCP tools
   - QueryService MCP tools
   - AIAgent MCP orchestration

3. **Advanced Features**
   - Streaming responses
   - Webhooks and notifications
   - Batch operations
   - Progress tracking

## Resources

- [MCP Specification](https://modelcontextprotocol.io/)
- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
- [Contract Processing System Documentation](../README.md)