using System.ComponentModel;
using ModelContextProtocol.Server;
using ContractProcessingSystem.DocumentParser.Services;

namespace ContractProcessingSystem.DocumentParser.MCPTools;

/// <summary>
/// MCP Tools for Document Parser using official ModelContextProtocol.AspNetCore package
/// </summary>
[McpServerToolType]
public class DocumentParserTools
{
    private readonly IDocumentParsingService _parsingService;
    private readonly ILogger<DocumentParserTools> _logger;

    public DocumentParserTools(
        IDocumentParsingService parsingService,
        ILogger<DocumentParserTools> logger)
    {
        _parsingService = parsingService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Parse a document and extract structured metadata.")]
    public async Task<string> ParseDocument(
        [Description("Document ID (GUID) to parse")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: ParseDocument called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var metadata = await _parsingService.ParseDocumentAsync(docGuid);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                metadata
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ParseDocument MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Split a document into chunks for processing.")]
    public async Task<string> ChunkDocument(
        [Description("Document ID (GUID) to chunk")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: ChunkDocument called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var chunks = await _parsingService.ChunkDocumentAsync(docGuid);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                chunks,
                chunkCount = chunks.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChunkDocument MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Get the parsing status of a document.")]
    public async Task<string> GetParsingStatus(
        [Description("Document ID (GUID) to check status")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: GetParsingStatus called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var status = await _parsingService.GetParsingStatusAsync(docGuid);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetParsingStatus MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Test AI connectivity for document parsing.")]
    public async Task<string> TestAIConnection()
    {
        try
        {
            _logger.LogInformation("MCP Tool: TestAIConnection called");

            var parsingService = _parsingService as DocumentParsingService;
            if (parsingService == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "DocumentParsingService not available"
                });
            }

            var isConnected = await parsingService.TestAIConnectionAsync();

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                aiConnected = isConnected,
                message = isConnected ? "AI connection successful" : "AI connection failed - using rule-based processing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestAIConnection MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
