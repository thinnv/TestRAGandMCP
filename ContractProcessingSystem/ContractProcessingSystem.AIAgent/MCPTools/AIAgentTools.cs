using System.ComponentModel;
using ModelContextProtocol.Server;
using ContractProcessingSystem.AIAgent.Services;

namespace ContractProcessingSystem.AIAgent.MCPTools;

/// <summary>
/// MCP Tools for AI Agent using official ModelContextProtocol.AspNetCore package
/// </summary>
[McpServerToolType]
public class AIAgentTools
{
    private readonly AIAgentService _aiAgentService;
    private readonly ILogger<AIAgentTools> _logger;

    public AIAgentTools(
        AIAgentService aiAgentService,
        ILogger<AIAgentTools> logger)
    {
        _aiAgentService = aiAgentService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Process a document through the complete AI pipeline (parse, embed, index).")]
    public async Task<string> ProcessDocument(
        [Description("Document ID (GUID) to process")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: ProcessDocument called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var status = await _aiAgentService.ProcessDocumentAsync(docGuid);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessDocument MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Chat with the AI agent about contracts, optionally with document context.")]
    public async Task<string> Chat(
        [Description("User message to send to AI")] string message,
        [Description("Optional document ID (GUID) for context")] string? contextDocumentId = null)
    {
        try
        {
            _logger.LogInformation("MCP Tool: Chat called");

            Guid? docGuid = null;
            if (!string.IsNullOrWhiteSpace(contextDocumentId))
            {
                if (!Guid.TryParse(contextDocumentId, out var parsed))
                {
                    return System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "Invalid context document ID format"
                    });
                }
                docGuid = parsed;
            }

            var response = await _aiAgentService.ChatAsync(message, docGuid);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Chat MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Analyze a contract and extract key insights, risks, and obligations.")]
    public async Task<string> AnalyzeContract(
        [Description("Document ID (GUID) to analyze")] string documentId)
    {
        try
        {
            _logger.LogInformation("MCP Tool: AnalyzeContract called for {DocumentId}", documentId);

            if (!Guid.TryParse(documentId, out var docGuid))
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Invalid document ID format"
                });
            }

            var analysis = await _aiAgentService.AnalyzeContractAsync(docGuid);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                analysis
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AnalyzeContract MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [McpServerTool]
    [Description("Compare multiple contracts and identify differences, similarities, and risks.")]
    public async Task<string> CompareContracts(
        [Description("JSON array of document IDs (GUIDs) to compare")] string documentIdsJson)
    {
        try
        {
            _logger.LogInformation("MCP Tool: CompareContracts called");

            var documentIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(documentIdsJson);
            
            if (documentIds == null || documentIds.Count < 2)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "At least 2 document IDs are required for comparison"
                });
            }

            var guids = new List<Guid>();
            foreach (var id in documentIds)
            {
                if (!Guid.TryParse(id, out var guid))
                {
                    return System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Invalid document ID format: {id}"
                    });
                }
                guids.Add(guid);
            }

            var comparison = await _aiAgentService.CompareContractsAsync(guids);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                comparison
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CompareContracts MCP tool");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

}
