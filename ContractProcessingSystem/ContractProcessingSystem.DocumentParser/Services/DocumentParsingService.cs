using Azure.AI.OpenAI;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.SemanticKernel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ContractProcessingSystem.DocumentParser.Services;

public class DocumentTextExtractor
{
    public async Task<string> ExtractTextAsync(Stream documentStream, string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => await ExtractPdfTextAsync(documentStream),
            "application/msword" or 
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" 
                => await ExtractWordTextAsync(documentStream),
            _ => throw new NotSupportedException($"Content type {contentType} is not supported")
        };
    }

    private async Task<string> ExtractPdfTextAsync(Stream pdfStream)
    {
        var text = new StringBuilder();
        
        using var pdfReader = new PdfReader(pdfStream);
        using var pdfDocument = new PdfDocument(pdfReader);
        
        for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
        {
            var page = pdfDocument.GetPage(i);
            var strategy = new SimpleTextExtractionStrategy();
            var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
            text.AppendLine(pageText);
        }
        
        return text.ToString();
    }

    private async Task<string> ExtractWordTextAsync(Stream wordStream)
    {
        var text = new StringBuilder();
        
        using var document = WordprocessingDocument.Open(wordStream, false);
        if (document.MainDocumentPart?.Document.Body != null)
        {
            foreach (var paragraph in document.MainDocumentPart.Document.Body.Elements<Paragraph>())
            {
                text.AppendLine(paragraph.InnerText);
            }
        }
        
        return text.ToString();
    }
}

public class DocumentParsingService : IDocumentParsingService
{
    private readonly DocumentTextExtractor _textExtractor;
    private readonly Kernel _semanticKernel;
    private readonly ILogger<DocumentParsingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public DocumentParsingService(
        DocumentTextExtractor textExtractor,
        Kernel semanticKernel,
        ILogger<DocumentParsingService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _textExtractor = textExtractor;
        _semanticKernel = semanticKernel;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ContractMetadata> ParseDocumentAsync(Guid documentId)
    {
        try
        {
            _logger.LogInformation("Starting document parsing for {DocumentId}", documentId);

            // Get document content (this would integrate with DocumentUpload service)
            var documentContent = await GetDocumentContentAsync(documentId);
            
            // Extract text from document
            var text = await _textExtractor.ExtractTextAsync(documentContent.Stream, documentContent.ContentType);
            
            // Use AI to extract metadata
            var metadata = await ExtractMetadataWithAIAsync(text);
            
            _logger.LogInformation("Document parsing completed for {DocumentId}", documentId);
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<List<ContractChunk>> ChunkDocumentAsync(Guid documentId)
    {
        try
        {
            _logger.LogInformation("Starting document chunking for {DocumentId}", documentId);

            var documentContent = await GetDocumentContentAsync(documentId);
            var text = await _textExtractor.ExtractTextAsync(documentContent.Stream, documentContent.ContentType);
            
            var chunks = await ChunkTextWithAIAsync(text, documentId);
            
            _logger.LogInformation("Document chunking completed for {DocumentId}. Created {ChunkCount} chunks", 
                documentId, chunks.Count);
            
            return chunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to chunk document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<ProcessingStatus> GetParsingStatusAsync(Guid documentId)
    {
        // This would typically check a database or cache for status
        // For now, returning a mock status
        return new ProcessingStatus(
            documentId,
            "Parsing",
            0.75f,
            "Extracting contract metadata",
            DateTime.UtcNow
        );
    }

    private async Task<ContractMetadata> ExtractMetadataWithAIAsync(string text)
    {
        var textSample = text.Substring(0, Math.Min(text.Length, 8000));
        var prompt = $@"You are an expert contract analysis AI. Extract structured metadata from the following contract text.

Contract Text:
{textSample}...

Extract and return ONLY a JSON object with the following structure:
{{
    ""title"": ""contract title or null"",
    ""contractDate"": ""YYYY-MM-DD or null"",
    ""expirationDate"": ""YYYY-MM-DD or null"", 
    ""contractValue"": number or null,
    ""currency"": ""currency code or null"",
    ""parties"": [""party1"", ""party2""],
    ""keyTerms"": [""term1"", ""term2"", ""term3""],
    ""contractType"": ""type or null""
}}

Focus on accuracy. If information is unclear or missing, use null.";

        var response = await _semanticKernel.InvokePromptAsync(prompt);
        var jsonResponse = response.GetValue<string>() ?? "{}";
        
        try
        {
            var jsonDoc = JsonDocument.Parse(jsonResponse);
            var root = jsonDoc.RootElement;
            
            return new ContractMetadata(
                root.TryGetProperty("title", out var title) && title.ValueKind != JsonValueKind.Null 
                    ? title.GetString() : null,
                root.TryGetProperty("contractDate", out var contractDate) && contractDate.ValueKind != JsonValueKind.Null
                    ? DateTime.Parse(contractDate.GetString()!) : null,
                root.TryGetProperty("expirationDate", out var expDate) && expDate.ValueKind != JsonValueKind.Null
                    ? DateTime.Parse(expDate.GetString()!) : null,
                root.TryGetProperty("contractValue", out var value) && value.ValueKind == JsonValueKind.Number
                    ? value.GetDecimal() : null,
                root.TryGetProperty("currency", out var currency) && currency.ValueKind != JsonValueKind.Null
                    ? currency.GetString() : null,
                root.TryGetProperty("parties", out var parties) && parties.ValueKind == JsonValueKind.Array
                    ? parties.EnumerateArray().Select(p => p.GetString()!).ToList() : new List<string>(),
                root.TryGetProperty("keyTerms", out var keyTerms) && keyTerms.ValueKind == JsonValueKind.Array
                    ? keyTerms.EnumerateArray().Select(t => t.GetString()!).ToList() : new List<string>(),
                root.TryGetProperty("contractType", out var contractType) && contractType.ValueKind != JsonValueKind.Null
                    ? contractType.GetString() : null,
                new Dictionary<string, object>()
            );
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON: {Response}", jsonResponse);
            return CreateDefaultMetadata();
        }
    }

    private async Task<List<ContractChunk>> ChunkTextWithAIAsync(string text, Guid documentId)
    {
        var chunks = new List<ContractChunk>();
        
        // First, do semantic chunking using AI
        var semanticChunks = await CreateSemanticChunksAsync(text);
        
        int chunkIndex = 0;
        int currentPosition = 0;
        
        foreach (var semanticChunk in semanticChunks)
        {
            var chunkType = await ClassifyChunkTypeAsync(semanticChunk);
            
            var chunk = new ContractChunk(
                Guid.NewGuid(),
                documentId,
                semanticChunk,
                chunkIndex++,
                currentPosition,
                currentPosition + semanticChunk.Length,
                chunkType,
                new Dictionary<string, object>()
            );
            
            chunks.Add(chunk);
            currentPosition += semanticChunk.Length;
        }
        
        return chunks;
    }

    private async Task<List<string>> CreateSemanticChunksAsync(string text)
    {
        // Split text into paragraphs first
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var currentChunk = new StringBuilder();
        const int maxChunkSize = 1500; // Target chunk size

        foreach (var paragraph in paragraphs)
        {
            if (currentChunk.Length + paragraph.Length > maxChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }
            
            currentChunk.AppendLine(paragraph);
        }
        
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }
        
        return chunks;
    }

    private async Task<ChunkType> ClassifyChunkTypeAsync(string chunkText)
    {
        var prompt = $"""
        Classify the following contract text chunk into one of these categories:
        - Header: Title, document header, or introductory information
        - Clause: Main contract clauses or sections
        - Term: Specific terms, definitions, or conditions
        - Condition: Conditional statements, if-then clauses
        - Signature: Signature blocks, witness lines, or execution information
        - Other: Any other content

        Text: {chunkText.Substring(0, Math.Min(chunkText.Length, 500))}...

        Respond with only one word from the categories above.
        """;

        var response = await _semanticKernel.InvokePromptAsync(prompt);
        var classification = response.GetValue<string>()?.Trim().ToLowerInvariant() ?? "other";
        
        return classification switch
        {
            "header" => ChunkType.Header,
            "clause" => ChunkType.Clause,
            "term" => ChunkType.Term,
            "condition" => ChunkType.Condition,
            "signature" => ChunkType.Signature,
            _ => ChunkType.Other
        };
    }

    private async Task<(Stream Stream, string ContentType)> GetDocumentContentAsync(Guid documentId)
    {
        // This would typically call the DocumentUpload service to get the document
        // For now, we'll create a mock implementation
        var httpClient = _httpClientFactory.CreateClient();
        
        // In a real implementation, this would call:
        // var response = await httpClient.GetAsync($"http://document-upload/api/documents/{documentId}/content");
        
        // Mock implementation - returning empty stream
        var mockContent = "Mock contract content for parsing demonstration...";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(mockContent));
        
        return (stream, "text/plain");
    }

    private static ContractMetadata CreateDefaultMetadata()
    {
        return new ContractMetadata(
            null, null, null, null, null,
            new List<string>(),
            new List<string>(),
            null,
            new Dictionary<string, object>()
        );
    }
}