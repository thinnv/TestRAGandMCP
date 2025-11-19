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
            "text/plain" => await ExtractPlainTextAsync(documentStream),
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

    private async Task<string> ExtractPlainTextAsync(Stream plainTextStream)
    {
        using var reader = new StreamReader(plainTextStream);
        return await reader.ReadToEndAsync();
    }
}

public class DocumentParsingService : IDocumentParsingService
{
    private readonly DocumentTextExtractor _textExtractor;
    private readonly Kernel _semanticKernel;
    private readonly ILogger<DocumentParsingService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public DocumentParsingService(
        DocumentTextExtractor textExtractor,
        Kernel semanticKernel,
        ILogger<DocumentParsingService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _textExtractor = textExtractor;
        _semanticKernel = semanticKernel;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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
        try
        {
            if (_semanticKernel == null)
            {
                _logger.LogWarning("Semantic Kernel is not initialized, falling back to rule-based extraction");
                return ExtractMetadataWithRulesAsync(text);
            }

            // Check if chat completion service is available
            var chatCompletionService = _semanticKernel.Services.GetService(typeof(Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService));
            
            if (chatCompletionService == null)
            {
                _logger.LogWarning("No AI services available in kernel, falling back to rule-based extraction");
                return ExtractMetadataWithRulesAsync(text);
            }

            // Use more text for better context on structured contracts
            var textSample = text.Substring(0, Math.Min(text.Length, 15000));
            var prompt = $@"You are an expert contract analysis AI specializing in business and employment contracts. Extract comprehensive metadata.

CONTRACT TEXT:
{textSample}

EXTRACTION RULES FOR DIFFERENT CONTRACT TYPES:

For SERVICE/DEVELOPMENT CONTRACTS:
- Sum all milestone payments for total value
- Parties: CLIENT and DEVELOPER/PROVIDER roles

For EMPLOYMENT CONTRACTS:
- Use annual BASE SALARY as contract value
- Can also include signing bonus, stock options value
- Parties: EMPLOYER and EMPLOYEE roles

For ALL CONTRACTS:
1. TITLE: Extract exact title from first meaningful line
2. DATE: Contract execution date (""as of"", ""entered into on"", or signature date)
3. EXPIRATION: Termination date, end date, or renewal date if specified
4. VALUE: 
   - Service contracts: Sum of all payments/milestones
   - Employment: Annual base salary (+ signing bonus if applicable)
   - Use largest total value if multiple amounts exist
5. CURRENCY: Extract from $ symbol (USD) or explicit currency code
6. PARTIES: Include ALL parties with their roles in parentheses
   - Examples: ""TechCorp Inc. (CLIENT)"", ""Jessica Martinez (EMPLOYEE)""
7. KEY TERMS: Extract from section headers and critical clauses:
   - Employment: compensation, benefits, equity, vacation, confidentiality, IP, non-compete, termination
   - Service: scope, deliverables, payment, IP rights, warranties, support, liability, governing law
8. TYPE: Exact contract type from title

Return ONLY a JSON object (no markdown, no explanations):
{{
    ""title"": ""EXACT contract title"",
    ""contractDate"": ""YYYY-MM-DD or null"",
    ""expirationDate"": ""YYYY-MM-DD or null"",
    ""contractValue"": numeric_value_or_null,
    ""currency"": ""USD or other"",
    ""parties"": [""Party Name (ROLE)""]
    ""keyTerms"": [""term1"", ""term2""],
    ""contractType"": ""type from title""
}}

EXAMPLES:
Employment: {{""contractValue"": 145000, ""parties"": [""Innovation Tech Solutions (EMPLOYER)"", ""Jessica Martinez (EMPLOYEE)""]}}
Service: {{""contractValue"": 120000, ""parties"": [""TechCorp Inc. (CLIENT)"", ""Digital Solutions LLC (DEVELOPER)""]}}

Return ONLY valid JSON, nothing else.";

            _logger.LogDebug("Sending enhanced prompt to AI for metadata extraction. Text length: {Length}", textSample.Length);

            // Use IChatCompletionService directly
            #pragma warning disable SKEXP0001
            var chatService = (Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService)chatCompletionService;
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            
            var chatResults = await chatService.GetChatMessageContentsAsync(chatHistory);
            var rawResponse = chatResults?.LastOrDefault()?.Content ?? "{}";
            #pragma warning restore SKEXP0001
            
            _logger.LogDebug("Received raw AI response (length: {Length}): {Response}", 
                rawResponse.Length, 
                rawResponse.Length > 500 ? rawResponse.Substring(0, 500) + "..." : rawResponse);

            // Clean up the response - remove markdown code blocks if present
            var jsonResponse = CleanJsonResponse(rawResponse);
            
            _logger.LogDebug("Cleaned JSON response: {Response}", jsonResponse);

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonResponse);
                var root = jsonDoc.RootElement;
                
                var metadata = new ContractMetadata(
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
                        ? parties.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!).ToList() : new List<string>(),
                    root.TryGetProperty("keyTerms", out var keyTerms) && keyTerms.ValueKind == JsonValueKind.Array
                        ? keyTerms.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()!).ToList() : new List<string>(),
                    root.TryGetProperty("contractType", out var contractType) && contractType.ValueKind != JsonValueKind.Null
                        ? contractType.GetString() : null,
                    new Dictionary<string, object>
                    {
                        ["extractionMethod"] = "ai",
                        ["extractionDate"] = DateTime.UtcNow,
                        ["aiModel"] = _configuration["AI:Gemini:Model"] ?? "unknown",
                        ["textSampleLength"] = textSample.Length
                    }
                );
                
                _logger.LogInformation("Successfully extracted metadata using AI. Title: {Title}, Parties: {PartyCount}, Terms: {TermCount}, Value: {Value} {Currency}", 
                    metadata.Title ?? "None", 
                    metadata.Parties.Count, 
                    metadata.KeyTerms.Count,
                    metadata.ContractValue ?? 0,
                    metadata.Currency ?? "N/A");
                
                return metadata;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI response as JSON. Raw response: {Response}. Falling back to rule-based extraction", rawResponse);
                return ExtractMetadataWithRulesAsync(text);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ITextGenerationService"))
        {
            _logger.LogError(ex, "ITextGenerationService not registered. AI Provider might not be properly configured. Falling back to rule-based extraction");
            return ExtractMetadataWithRulesAsync(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI metadata extraction. Text length: {Length}. Error: {Error}. Falling back to rule-based extraction", 
                text.Length, ex.Message);
            return ExtractMetadataWithRulesAsync(text);
        }
    }

    private async Task<ChunkType> ClassifyChunkTypeAsync(string chunkText)
    {
        try
        {
            if (_semanticKernel == null)
            {
                _logger.LogWarning("Semantic Kernel is not initialized, using default chunk type");
                return ChunkType.Other;
            }

            // Check if chat completion service is available
            var chatCompletionService = _semanticKernel.Services.GetService(typeof(Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService));
            
            if (chatCompletionService == null)
            {
                _logger.LogWarning("No AI services available, using default chunk type");
                return ChunkType.Other;
            }

            var textSample = chunkText.Substring(0, Math.Min(chunkText.Length, 800));
            var prompt = $@"Classify this contract text chunk into ONE category.

TEXT CHUNK:
{textSample}

CLASSIFICATION RULES:

Header: 
- Contract titles and document headers
- Party information blocks (EMPLOYER:, EMPLOYEE:, CLIENT:, DEVELOPER:, etc.)
- Party details (name, address, contact info)
- Introductory recitals

Clause:
- Numbered sections with substantive terms
- Employment: ""1. POSITION"", ""2. COMPENSATION"", ""3. BENEFITS"", ""4. TERMINATION""
- Service: ""1. SCOPE OF WORK"", ""2. PAYMENT TERMS"", ""3. DELIVERABLES""
- Main contractual obligations and rights

Term:
- Specific definitions, schedules, or detailed conditions
- Employment: Benefits breakdown, vesting schedules, vacation policies
- Service: Milestone schedules, payment breakdowns, deliverable details
- Compensation details, pricing tables

Condition:
- If-then statements and conditional obligations
- Termination conditions, renewal terms
- Contingent requirements
- ""Either party may terminate if...""
- ""In the event of...""

Signature:
- Signature lines and execution blocks
- ""SIGNATURES:"", ""ACKNOWLEDGED AND AGREED:""
- Witness lines, date lines
- Notary sections

Other:
- Any content not fitting above categories

EXAMPLES:
Employment Contract:
- ""EMPLOYER: Innovation Tech Solutions\nAddress: 555 Corporate..."" ? Header
- ""1. POSITION AND DUTIES\nJob Title: Senior Software Engineer..."" ? Clause
- ""Base Salary: $145,000 per year\nPay Frequency: Bi-weekly..."" ? Term
- ""Either party may terminate at any time"" ? Condition
- ""ACKNOWLEDGED AND AGREED:\nEMPLOYER: _____"" ? Signature

Service Contract:
- ""CLIENT: TechCorp Inc."" ? Header
- ""1. PROJECT SCOPE\nDeveloper agrees to develop..."" ? Clause
- ""Phase 1: Analysis (Weeks 1-3) - Payment: $25,000"" ? Term
- ""If Client fails to provide requirements within 5 days..."" ? Condition

Respond with ONLY ONE WORD: Header, Clause, Term, Condition, Signature, or Other";

            // Use IChatCompletionService directly
            #pragma warning disable SKEXP0001
            var chatService = (Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService)chatCompletionService;
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            
            var chatResults = await chatService.GetChatMessageContentsAsync(chatHistory);
            var rawClassification = chatResults?.LastOrDefault()?.Content?.Trim() ?? "other";
            #pragma warning restore SKEXP0001
            
            // Clean up the classification - extract just the first word
            var classification = rawClassification
                .Split(new[] { ' ', '\n', '\r', ',', '.', ':', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.ToLowerInvariant() ?? "other";
            
            _logger.LogDebug("Chunk classified as: {Classification} (raw: {RawClassification}), First 100 chars: {Preview}", 
                classification, rawClassification, textSample.Substring(0, Math.Min(100, textSample.Length)));
            
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
        catch (InvalidOperationException ex) when (ex.Message.Contains("ITextGenerationService"))
        {
            _logger.LogWarning(ex, "ITextGenerationService not available, using default chunk type");
            return ChunkType.Other;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error classifying chunk type, using default. Chunk length: {Length}, Error: {Error}", 
                chunkText.Length, ex.Message);
            return ChunkType.Other;
        }
    }

    private string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "{}";
        }

        // Remove markdown code blocks (```json ... ``` or ``` ... ```)
        var cleaned = response.Trim();
        
        // Check for markdown code blocks
        if (cleaned.StartsWith("```"))
        {
            // Remove opening ```json or ```
            var lines = cleaned.Split('\n');
            if (lines.Length > 0)
            {
                var firstLine = lines[0].Trim();
                if (firstLine.StartsWith("```"))
                {
                    cleaned = string.Join('\n', lines.Skip(1));
                }
            }
            
            // Remove closing ```
            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned.Substring(0, cleaned.LastIndexOf("```")).Trim();
            }
        }

        // Find the first { and last } to extract just the JSON object
        var firstBrace = cleaned.IndexOf('{');
        var lastBrace = cleaned.LastIndexOf('}');
        
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return cleaned.Trim();
    }

    private ContractMetadata ExtractMetadataWithRulesAsync(string text)
    {
        _logger.LogInformation("Using rule-based metadata extraction");

        try
        {
            // Basic rule-based extraction
            var title = ExtractTitle(text);
            var parties = ExtractParties(text);
            var contractDate = ExtractContractDate(text);
            var expirationDate = ExtractExpirationDate(text);
            var contractValue = ExtractContractValue(text);
            var currency = ExtractCurrency(text);
            var contractType = ExtractContractType(text);
            var keyTerms = ExtractKeyTerms(text);

            return new ContractMetadata(
                title,
                contractDate,
                expirationDate,
                contractValue,
                currency,
                parties,
                keyTerms,
                contractType,
                new Dictionary<string, object>
                {
                    ["extractionMethod"] = "rule-based",
                    ["extractionDate"] = DateTime.UtcNow
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in rule-based extraction");
            return CreateDefaultMetadata();
        }
    }

    private string? ExtractTitle(string text)
    {
        // Look for common title patterns at the beginning of document
        var titlePatterns = new[]
        {
            @"^\s*([A-Z][A-Z\s]+(?:AGREEMENT|CONTRACT))\s*$",  // All caps title on first line
            @"^([A-Z][A-Za-z\s]+(?:Agreement|Contract))\s*$",  // Title case on first line
            @"(?:CONTRACT|AGREEMENT):\s*(.*?)(?=\n|\r)",
            @"TITLE:\s*(.*?)(?=\n|\r)"
        };

        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Check first few lines for title
        for (int i = 0; i < Math.Min(5, lines.Length); i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            // Check if it matches title patterns
            foreach (var pattern in titlePatterns)
            {
                var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (match.Success)
                {
                    var title = match.Groups[1].Success ? match.Groups[1].Value.Trim() : match.Value.Trim();
                    if (title.Length > 5 && title.Length < 200)
                    {
                        return title;
                    }
                }
            }
            
            // If first non-empty line contains "AGREEMENT" or "CONTRACT", use it
            if ((line.Contains("AGREEMENT", StringComparison.OrdinalIgnoreCase) || 
                 line.Contains("CONTRACT", StringComparison.OrdinalIgnoreCase)) &&
                line.Length > 10 && line.Length < 200)
            {
                return line;
            }
        }

        // Fallback to first meaningful line
        return lines.FirstOrDefault(l => l.Trim().Length > 10)?.Trim();
    }

    private List<string> ExtractParties(string text)
    {
        var parties = new HashSet<string>();
        
        // Enhanced party patterns for various contract types
        var partyPatterns = new[]
        {
            // Employment contracts: EMPLOYER: Company Name / EMPLOYEE: Person Name
            @"(?:EMPLOYER|EMPLOYEE):\s*([^\n\r]+?)(?:\s*\n|Address:|Contact:|Phone:|Email:)",
            
            // Service contracts: CLIENT: / DEVELOPER: / PROVIDER:
            @"(?:CLIENT|DEVELOPER|PROVIDER|CONTRACTOR|VENDOR|CUSTOMER|SELLER|BUYER|PARTY\s+[AB]):\s*([^\n\r]+?)(?:\s*\n|Address:|Contact:|Email:)",
            
            // Standard "between X and Y" pattern
            @"(?:between|among)\s+(.+?)\s+(?:and|&)\s+(.+?)(?:\s*\(|,|\.|\n)",
            
            // Company names with legal entities
            @"([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*\s+(?:Inc\.|LLC|Ltd\.|Corporation|Corp\.|Solutions|Systems|Technologies))",
            
            // Individual names (for employment contracts)
            @"([A-Z][a-z]+\s+[A-Z][a-z]+)(?:\s*\n|\s*Address:|\s*Phone:)",
            
            // Quoted party names
            @"""([^""]+)""(?:\s+\((?:CLIENT|DEVELOPER|PROVIDER|CONTRACTOR|EMPLOYER|EMPLOYEE)\))?",
        };

        foreach (var pattern in partyPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(match.Groups[i].Value))
                    {
                        var party = match.Groups[i].Value.Trim();
                        // Clean up party names
                        party = Regex.Replace(party, @"\s+", " ");
                        party = party.TrimEnd(',', '.', ';', ':');
                        
                        // Filter out common false positives
                        if (party.Length > 3 && party.Length < 200 &&
                            !party.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                            !Regex.IsMatch(party, @"^\d+$"))
                        {
                            parties.Add(party);
                        }
                    }
                }
            }
        }

        return parties.Take(10).ToList();
    }

    private DateTime? ExtractContractDate(string text)
    {
        var datePatterns = new[]
        {
            // "as of January 15, 2025"
            @"(?:as\s+of|dated|executed\s+as\s+of)\s+(\w+\s+\d{1,2},\s+\d{4})",
            
            // "DATE: 01/15/2025" or similar
            @"(?:DATE|DATED|EXECUTED|SIGNED|EFFECTIVE).*?(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})",
            
            // Standard date formats
            @"(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})",
            @"(\w+\s+\d{1,2},\s+\d{4})"
        };

        foreach (var pattern in datePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
            {
                // Only accept reasonable dates (not too far in past/future)
                if (date.Year >= 1990 && date.Year <= 2100)
                {
                    return date;
                }
            }
        }

        return null;
    }

    private DateTime? ExtractExpirationDate(string text)
    {
        var expirationPatterns = new[]
        {
            @"(?:EXPIR|TERMINAT|END).*?(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})",
            @"(?:UNTIL|THROUGH).*?(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})"
        };

        foreach (var pattern in expirationPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
            {
                return date;
            }
        }

        return null;
    }

    private decimal? ExtractContractValue(string text)
    {
        var allValues = new List<decimal>();
        
        // Enhanced value patterns for different contract types
        var valuePatterns = new[]
        {
            // Employment: Base Salary: $145,000
            @"(?:base\s+salary|annual\s+salary|yearly\s+salary|compensation).*?\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Service contracts: Total Contract Value
            @"(?:total|grand\s+total|contract\s+value|total\s+value|total\s+amount).*?\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Signing bonus (for employment)
            @"(?:signing\s+bonus|sign-on\s+bonus).*?\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Milestone payments
            @"(?:payment|deliverable|milestone|phase).*?\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Standard dollar amounts
            @"\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Currency format
            @"([\d,]+(?:\.\d{2})?)\s*(?:USD|DOLLARS|per\s+year)"
        };

        // Track if we found a base salary (for employment contracts)
        decimal? baseSalary = null;
        decimal? signingBonus = null;

        foreach (var pattern in valuePatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    var valueStr = match.Groups[1].Value.Replace(",", "");
                    if (decimal.TryParse(valueStr, out var value) && value > 0)
                    {
                        // Check if it's a base salary
                        if (pattern.Contains("base") || pattern.Contains("annual") || pattern.Contains("yearly"))
                        {
                            baseSalary = value;
                        }
                        // Check if it's a signing bonus
                        else if (pattern.Contains("signing") || pattern.Contains("sign-on"))
                        {
                            signingBonus = value;
                        }
                        
                        allValues.Add(value);
                    }
                }
            }
        }

        if (allValues.Count == 0)
            return null;

        // For employment contracts, prioritize base salary
        if (baseSalary.HasValue)
        {
            // Can optionally add signing bonus: return baseSalary.Value + (signingBonus ?? 0);
            return baseSalary.Value; // Return annual salary
        }

        // For service contracts, return the largest value (likely the total)
        return allValues.Max();
    }

    private string? ExtractCurrency(string text)
    {
        var currencyPatterns = new[]
        {
            @"\b(USD|EUR|GBP|CAD|AUD|JPY)\b",
            @"\$([\d,]+(?:\.\d{2})?)" // Dollar sign implies USD
        };

        foreach (var pattern in currencyPatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return pattern.Contains("$") ? "USD" : match.Groups[1].Value.ToUpper();
            }
        }

        return null;
    }

    private string? ExtractContractType(string text)
    {
        // Enhanced patterns for various contract types
        var titlePatterns = new[]
        {
            // Employment
            @"(EMPLOYMENT\s+(?:AGREEMENT|CONTRACT))",
            @"(OFFER\s+LETTER)",
            @"(JOB\s+OFFER)",
            
            // Software/Development
            @"(SOFTWARE\s+DEVELOPMENT\s+AGREEMENT)",
            @"(DEVELOPMENT\s+AGREEMENT)",
            
            // Service
            @"(SERVICE(?:S)?\s+AGREEMENT)",
            @"(PROFESSIONAL\s+SERVICES\s+AGREEMENT)",
            @"(CONSULTING\s+AGREEMENT)",
            @"(MASTER\s+SERVICE(?:S)?\s+AGREEMENT|MSA)",
            @"(STATEMENT\s+OF\s+WORK|SOW)",
            
            // Other common types
            @"(LICENSE\s+AGREEMENT)",
            @"(PURCHASE\s+AGREEMENT)",
            @"(LEASE\s+AGREEMENT)",
            @"(NON-DISCLOSURE\s+AGREEMENT|NDA)",
            @"(INDEPENDENT\s+CONTRACTOR\s+AGREEMENT)"
        };

        foreach (var pattern in titlePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.ToTitleCase();
            }
        }

        return null;
    }

    private List<string> ExtractKeyTerms(string text)
    {
        var keyTerms = new HashSet<string>();
        
        // Enhanced term patterns for all contract types
        var termPatterns = new[]
        {
            // Employment-specific
            @"\b(position|job\s+title|duties|responsibilities)\b",
            @"\b(compensation|salary|base\s+pay)\b",
            @"\b(bonus|incentive|stock\s+options?|equity)\b",
            @"\b(benefits?|health\s+insurance|401k|retirement)\b",
            @"\b(vacation|pto|paid\s+time\s+off|sick\s+leave)\b",
            @"\b(non-compete|non-solicitation|restrictive\s+covenants?)\b",
            @"\b(at-will|probation(?:ary)?)\b",
            @"\b(severance)\b",
            
            // Service/Development contracts
            @"\b(project\s+scope|scope\s+of\s+work)\b",
            @"\b(timeline|milestones?|deliverables?)\b",
            @"\b(payment(?:\s+terms)?|fees?)\b",
            @"\b(acceptance\s+criteria|testing)\b",
            
            // Common to all
            @"\b(intellectual\s+property|IP\s+rights?|ownership)\b",
            @"\b(confidential(?:ity)?|proprietary|trade\s+secrets?)\b",
            @"\b(warrant(?:y|ies))\b",
            @"\b(indemnif(?:y|ication))\b",
            @"\b(termination|cancellation)\b",
            @"\b(liability|damages)\b",
            @"\b(governing\s+law|jurisdiction)\b",
            @"\b(dispute\s+resolution|arbitration)\b",
            @"\b(force\s+majeure)\b",
            @"\b(support|maintenance)\b",
            @"\b(training|documentation)\b"
        };

        foreach (var pattern in termPatterns)
        {
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    var term = match.Groups[1].Value.ToLower().Trim();
                    // Normalize some terms
                    term = term.Replace("  ", " ");
                    keyTerms.Add(term);
                }
            }
        }

        return keyTerms.Take(20).OrderBy(t => t).ToList(); // Increased limit to 20
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

    private async Task<(Stream Stream, string ContentType)> GetDocumentContentAsync(Guid documentId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5); // Set reasonable timeout

            // Get the DocumentUpload service URL from configuration
            var documentUploadBaseUrl = _configuration["Services:DocumentUpload"] ?? "https://localhost:7001";
            var url = $"{documentUploadBaseUrl}/api/Documents/{documentId}/content";
            
            _logger.LogInformation("Retrieving document content from: {Url}", url);

            var response = await httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to retrieve document content. Status: {Status}, Url: {Url}", 
                    response.StatusCode, url);
                    
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new FileNotFoundException($"Document not found: {documentId}");
                }
                
                response.EnsureSuccessStatusCode();
            }

            var contentBytes = await response.Content.ReadAsByteArrayAsync();
            var stream = new MemoryStream(contentBytes);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/plain";

            _logger.LogDebug("Successfully retrieved document content. Size: {Size} bytes, ContentType: {ContentType}", 
                contentBytes.Length, contentType);

            return (stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document content for {DocumentId}", documentId);
            throw;
        }
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

    public async Task<bool> TestAIConnectionAsync()
    {
        try
        {
            if (_semanticKernel == null)
            {
                _logger.LogWarning("Semantic Kernel is not initialized");
                return false;
            }

            // Check if chat completion service is available
            var chatCompletionService = _semanticKernel.Services.GetService(typeof(Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService));
            
            if (chatCompletionService == null)
            {
                _logger.LogWarning("No chat completion service available in kernel");
                return false;
            }

            var testPrompt = "Reply with just the word 'OK' to confirm you are working.";
            
            _logger.LogInformation("Testing AI connection with prompt: {Prompt}", testPrompt);
            
            // Use IChatCompletionService directly
            #pragma warning disable SKEXP0001
            var chatService = (Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService)chatCompletionService;
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(testPrompt);
            
            var chatResults = await chatService.GetChatMessageContentsAsync(chatHistory);
            var result = chatResults?.LastOrDefault()?.Content?.Trim();
            #pragma warning restore SKEXP0001
            
            _logger.LogInformation("AI connection test result: {Result}", result);
            return !string.IsNullOrEmpty(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI connection test failed with error: {ErrorMessage}", ex.Message);
            
            // Log specific Google API errors for debugging
            if (ex.Message.Contains("404") && ex.Message.Contains("NOT_FOUND"))
            {
                _logger.LogError("Google Gemini API returned 404 - this usually means the model name is incorrect or not available. Current model: {Model}", 
                    _configuration["AI:Gemini:Model"]);
            }
            
            return false;
        }
    }
}

// Extension methods for string manipulation
public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
    }
}