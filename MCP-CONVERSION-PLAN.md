# MCP Server Conversion Plan

## Overview
Converting Contract Processing System services to Model Context Protocol (MCP) servers for better standardization and interoperability.

## MCP Server Architecture

### Core MCP Components
Each MCP server will implement:
1. **Protocol Compliance** - Standard MCP JSON-RPC communication
2. **Tool Definitions** - Service-specific capabilities exposed as tools
3. **Resource Management** - Standardized access to data and files
4. **Error Handling** - Consistent error responses
5. **Logging & Monitoring** - Integrated observability

### Service Conversion Plan

#### 1. DocumentUpload MCP Server
**Purpose**: File upload and storage management
**Tools**:
- `upload_contract_file` - Upload contract documents
- `get_file_metadata` - Retrieve file information
- `list_uploaded_files` - List all uploaded documents
- `delete_file` - Remove uploaded documents

**Resources**:
- `contract://file/{id}` - Individual contract files
- `contract://metadata/{id}` - File metadata

#### 2. DocumentParser MCP Server
**Purpose**: Document parsing and content extraction
**Tools**:
- `parse_contract_document` - Extract text from documents
- `chunk_document_content` - Split content into chunks
- `extract_contract_metadata` - Extract structured metadata
- `get_parsing_status` - Check parsing progress

**Resources**:
- `contract://parsed/{id}` - Parsed document content
- `contract://chunks/{id}` - Document chunks

#### 3. EmbeddingService MCP Server
**Purpose**: Multi-provider vector embedding generation
**Tools**:
- `generate_text_embedding` - Create embeddings for text
- `batch_generate_embeddings` - Process multiple texts
- `switch_embedding_provider` - Change AI provider
- `get_embedding_models` - List available models

**Resources**:
- `contract://embeddings/{id}` - Generated embeddings
- `contract://providers` - Available AI providers

#### 4. VectorService MCP Server
**Purpose**: Vector storage and similarity search
**Tools**:
- `store_document_embeddings` - Store vector embeddings
- `search_similar_content` - Find similar documents
- `delete_document_vectors` - Remove document embeddings
- `get_collection_stats` - Vector database statistics

**Resources**:
- `contract://vectors/{id}` - Stored vectors
- `contract://similarity/{query}` - Search results

#### 5. QueryService MCP Server
**Purpose**: Semantic search and information retrieval
**Tools**:
- `semantic_search_contracts` - Search using natural language
- `hybrid_search_documents` - Combined text and vector search
- `filter_contract_search` - Apply filters to search
- `get_search_suggestions` - Query suggestions

**Resources**:
- `contract://search/{query}` - Search results
- `contract://suggestions` - Search suggestions

#### 6. AIAgent MCP Server
**Purpose**: AI-powered contract analysis and chat
**Tools**:
- `chat_about_contract` - Interactive contract discussion
- `analyze_contract_content` - Contract analysis
- `compare_multiple_contracts` - Contract comparison
- `extract_contract_insights` - Key insights extraction
- `generate_contract_summary` - Document summarization

**Resources**:
- `contract://analysis/{id}` - Analysis results
- `contract://chat/{session}` - Chat sessions
- `contract://insights/{id}` - Extracted insights

## Implementation Strategy

### Phase 1: Base MCP Infrastructure
1. Create MCP server base classes
2. Implement protocol handling
3. Add standard error handling
4. Set up logging and monitoring

### Phase 2: Service Conversion
1. Convert one service at a time
2. Maintain backward compatibility during transition
3. Add comprehensive testing
4. Update orchestration layer

### Phase 3: Integration & Testing
1. Create unified MCP client
2. Update Aspire orchestration
3. End-to-end testing
4. Performance validation

### Phase 4: Documentation & Migration
1. Update API documentation
2. Create migration guides
3. Update demo scripts
4. Client SDK development

## Benefits After Conversion

### For Developers
- **Standardized APIs** - Consistent tool interface
- **Better Testing** - Standard MCP testing tools
- **Easier Integration** - MCP client libraries
- **Clear Contracts** - Well-defined tool schemas

### For Users
- **Multiple Clients** - Use any MCP-compatible client
- **Tool Discovery** - Automatic capability discovery
- **Better Error Handling** - Standard error responses
- **Resource Access** - Uniform resource addressing

### For Operations
- **Monitoring** - Standard MCP metrics
- **Scaling** - Independent server scaling
- **Deployment** - Container-friendly architecture
- **Security** - Standard authentication patterns

## Next Steps
1. Create MCP server base template
2. Start with DocumentUpload conversion
3. Implement tool definitions and handlers
4. Add comprehensive testing
5. Update orchestration layer