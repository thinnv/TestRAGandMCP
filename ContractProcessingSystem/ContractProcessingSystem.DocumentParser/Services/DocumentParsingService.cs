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
            // IP licenses and complex agreements need more context
            var textSample = text.Substring(0, Math.Min(text.Length, 25000));
            
            // If we had to truncate, try to end at a clean boundary
            if (textSample.Length < text.Length && textSample.Length > 1000)
            {
                // Try to end at paragraph break
                var lastDoubleNewline = textSample.LastIndexOf("\n\n");
                if (lastDoubleNewline > textSample.Length - 500) // Within last 500 chars
                {
                    textSample = textSample.Substring(0, lastDoubleNewline);
                }
            }
            
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

For IP LICENSE AGREEMENTS:
- Sum: Upfront fee + first year minimum royalty + milestone payments
- OR use largest total value stated
- Parties: LICENSOR and LICENSEE roles
- Do NOT try to calculate potential royalty revenue (too variable)

For ALL CONTRACTS:
1. TITLE: Extract exact title from first meaningful line
2. DATE: Contract execution date (""as of"", ""entered into on"", or signature date)
3. EXPIRATION: Termination date, end date, or renewal date if specified
4. VALUE: 
   - Service contracts: Sum of all payments/milestones
   - Employment: Annual base salary (+ signing bonus if applicable)
   - IP License: Upfront fee + Year 1 minimum royalty + milestone payments
   - Use largest stated total value if multiple amounts exist
5. CURRENCY: Extract from $ symbol (USD) or explicit currency code
6. PARTIES: Include ALL parties with their roles in parentheses
   - Examples: ""TechCorp Inc. (CLIENT)"", ""Jessica Martinez (EMPLOYEE)"", ""NanoTech Institute (LICENSOR)""
7. KEY TERMS: Extract from section headers and critical clauses:
   - Employment: compensation, benefits, equity, vacation, confidentiality, IP, non-compete, termination
   - Service: scope, deliverables, payment, IP rights, warranties, support, liability, governing law
   - IP License: license grant, exclusivity, territory, royalties, patents, enforcement, improvements
8. TYPE: Exact contract type from title

Return ONLY a JSON object (no markdown, no explanations):
{{
    ""title"": ""EXACT contract title"",
    ""contractDate"": ""YYYY-MM-DD or null"",
    ""expirationDate"": ""YYYY-MM-DD or null"",
    ""contractValue"": numeric_value_or_null,
    ""currency"": ""USD or other"",
    ""parties"": [""Party Name (ROLE)""],
    ""keyTerms"": [""term1"", ""term2""],
    ""contractType"": ""type from title""
}}

EXAMPLES:
Employment: {{""contractValue"": 145000, ""parties"": [""Innovation Tech Solutions (EMPLOYER)"", ""Jessica Martinez (EMPLOYEE)""]}}
Service: {{""contractValue"": 120000, ""parties"": [""TechCorp Inc. (CLIENT)"", ""Digital Solutions LLC (DEVELOPER)""]}}
IP License: {{""contractValue"": 850000, ""parties"": [""NanoTech Institute (LICENSOR)"", ""Advanced Manufacturing (LICENSEE)""]}}

CALCULATION NOTES:
- For IP License example: $500k upfront + $100k year 1 minimum + $250k first milestone = $850k
- Focus on guaranteed/fixed payments, not potential future royalties

Return ONLY valid JSON, nothing else.";

            _logger.LogDebug("Sending enhanced prompt to AI for metadata extraction. Text length: {Length}", textSample.Length);

            // Use IChatCompletionService directly
            #pragma warning disable SKEXP0001
            var chatService = (Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService)chatCompletionService;
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            
            _logger.LogDebug("Invoking AI chat completion service...");
            
            // Configure execution settings with low temperature for deterministic responses
            var temperature = float.TryParse(_configuration["AI:Temperature"], out var temp) ? temp : 0.1f;
            var executionSettings = new Microsoft.SemanticKernel.PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["temperature"] = temperature // Configurable temperature (default 0.1 for deterministic extraction)
                }
            };
            
            _logger.LogDebug("Using AI temperature: {Temperature}", temperature);
            
            var chatResults = await chatService.GetChatMessageContentsAsync(
                chatHistory, 
                executionSettings: executionSettings,
                kernel: _semanticKernel);
            
            // Log detailed response information
            _logger.LogDebug("Chat results received. Count: {Count}", chatResults?.Count ?? 0);
            
            if (chatResults == null || !chatResults.Any())
            {
                _logger.LogWarning("AI returned null or empty chat results. Falling back to rule-based extraction");
                return ExtractMetadataWithRulesAsync(text);
            }
            
            var lastMessage = chatResults.LastOrDefault();
            if (lastMessage == null)
            {
                _logger.LogWarning("Last message in chat results is null. Falling back to rule-based extraction");
                return ExtractMetadataWithRulesAsync(text);
            }
            
            var rawResponse = lastMessage.Content ?? "{}";
            _logger.LogDebug("Last message content length: {Length}, IsEmpty: {IsEmpty}", 
                rawResponse?.Length ?? 0, 
                string.IsNullOrWhiteSpace(rawResponse));
            
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                _logger.LogWarning("AI returned empty content. Falling back to rule-based extraction");
                return ExtractMetadataWithRulesAsync(text);
            }
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
- ""EMPLOYER: Innovation Tech Solutions\nAddress: 555 Corporate..."" = Header
- ""1. POSITION AND DUTIES\nJob Title: Senior Software Engineer..."" = Clause
- ""Base Salary: $145,000 per year\nPay Frequency: Bi-weekly..."" = Term
- ""Either party may terminate at any time"" = Condition
- ""ACKNOWLEDGED AND AGREED:\nEMPLOYER: _____"" = Signature

Service Contract:
- ""CLIENT: TechCorp Inc."" = Header
- ""1. PROJECT SCOPE\nDeveloper agrees to develop..."" = Clause
- ""Phase 1: Analysis (Weeks 1-3) - Payment: $25,000"" = Term
- ""If Client fails to provide requirements within 5 days..."" = Condition

Respond with ONLY ONE WORD: Header, Clause, Term, Condition, Signature, or Other";

            // Use IChatCompletionService directly
            #pragma warning disable SKEXP0001
            var chatService = (Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService)chatCompletionService;
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);
            
            // Configure execution settings with low temperature for deterministic classification
            var executionSettings = new Microsoft.SemanticKernel.PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["temperature"] = 0.1 // Low temperature for consistent chunk classification
                }
            };
            
            var chatResults = await chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings: executionSettings,
                kernel: _semanticKernel);
            
            // Better null handling
            if (chatResults == null || !chatResults.Any())
            {
                _logger.LogWarning("AI returned null or empty results for chunk classification, using default");
                return ChunkType.Other;
            }
            
            var lastMessage = chatResults.LastOrDefault();
            var rawClassification = lastMessage?.Content?.Trim() ?? "other";
            
            if (string.IsNullOrWhiteSpace(rawClassification))
            {
                _logger.LogWarning("AI returned empty classification, using default");
                return ChunkType.Other;
            }
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

        return cleaned;
    }

    private List<string> ExtractParties(string text)
    {
        var parties = new HashSet<string>();
        
        // Enhanced party patterns for various contract types
        var partyPatterns = new[]
        {
            // IP License: LICENSOR: / LICENSEE:
            @"(?:LICENSOR|LICENSEE):\s*([^\n\r]+?)(?:\s*\n|Address:|Represented by:|Email:|\("")",
            
            // Employment contracts: EMPLOYER: Company Name / EMPLOYEE: Person Name
            @"(?:EMPLOYER|EMPLOYEE):\s*([^\n\r]+?)(?:\s*\n|Address:|Contact:|Phone:|Email:)",
            
            // Service contracts: CLIENT: / DEVELOPER: / PROVIDER:
            @"(?:CLIENT|DEVELOPER|PROVIDER|CONTRACTOR|VENDOR|CUSTOMER|SELLER|BUYER|PARTY\s+[AB]):\s*([^\n\r]+?)(?:\s*\n|Address:|Contact:|Email:)",
            
            // Standard "between X and Y" pattern
            @"(?:between|among)\s+(.+?)\s+(?:and|&)\s+(.+?)(?:\s*\(|,|\.|\n)",
            
            // Company names with legal entities (including Institute, Research, Corp.)
            @"([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*\s+(?:Inc\.|LLC|Ltd\.|Corporation|Corp\.|Solutions|Systems|Technologies|Institute|Research|University))",
            
            // Individual names (for employment contracts)
            @"([A-Z][a-z]+\s+[A-Z][a-z]+)(?:\s*\n|\s*Address:|\s*Phone:)",
            
            // Quoted party names
            @"""([^""]+)""(?:\s+\((?:CLIENT|DEVELOPER|PROVIDER|CONTRACTOR|EMPLOYER|EMPLOYEE|LICENSOR|LICENSEE)\))?",
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
                        party = party.TrimEnd(',', '.', ';', ':', ')');
                        
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

    private decimal? ExtractContractValue(string text)
    {
        var allValues = new List<decimal>();
        
        // Enhanced value patterns for different contract types
        var valuePatterns = new[]
        {
            // IP License: Upfront License Fee, Annual Minimum Royalty, Milestone Payments
            @"(?:upfront|license\s+fee|initial\s+fee).*?\$\s*([\d,]+(?:\.\d{2})?)",
            @"(?:annual\s+minimum|minimum\s+royalty|year\s+1).*?\$\s*([\d,]+(?:\.\d{2})?)",
            @"(?:milestone|achievement).*?\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Employment: Base Salary: $145,000
            @"(?:base\s+salary|annual\s+salary|yearly\s+salary|compensation).*?\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Service contracts: Total Contract Value
            @"(?:total|grand\s+total|contract\s+value|total\s+value|total\s+amount).*?\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Signing bonus (for employment)
            @"(?:signing\s+bonus|sign-on\s+bonus).*?\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Milestone payments (general)
            @"(?:payment|deliverable|phase).*?\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Standard dollar amounts
            @"\$\s*([\d,]+(?:\.\d{2})?)",
            
            // Currency format
            @"([\d,]+(?:\.\d{2})?)\s*(?:USD|DOLLARS|per\s+year)"
        };

        // Track specific contract type values
        decimal? baseSalary = null;
        decimal? upfrontFee = null;
        decimal? minimumRoyalty = null;
        var milestonePayments = new List<decimal>();

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
                        // Categorize the value
                        if (pattern.Contains("upfront") || pattern.Contains("license"))
                        {
                            upfrontFee = Math.Max(upfrontFee ?? 0, value);
                        }
                        else if (pattern.Contains("annual") || pattern.Contains("minimum"))
                        {
                            minimumRoyalty = Math.Max(minimumRoyalty ?? 0, value);
                        }
                        else if (pattern.Contains("milestone") || pattern.Contains("achievement"))
                        {
                            milestonePayments.Add(value);
                        }
                        else if (pattern.Contains("base") || pattern.Contains("salary"))
                        {
                            baseSalary = value;
                        }
                        
                        allValues.Add(value);
                    }
                }
            }
        }

        if (allValues.Count == 0)
            return null;

        // Smart contract type detection and value calculation
        
        // IP License: Sum guaranteed payments
        if (upfrontFee.HasValue || minimumRoyalty.HasValue)
        {
            var ipTotal = (upfrontFee ?? 0) + (minimumRoyalty ?? 0);
            if (milestonePayments.Any())
            {
                ipTotal += milestonePayments.First(); // Add first milestone
            }
            return ipTotal > 0 ? ipTotal : allValues.Max();
        }

        // Employment: Base salary
        if (baseSalary.HasValue)
        {
            return baseSalary.Value;
        }

        // Service contracts: Return largest value (likely total)
        return allValues.Max();
    }

    private string? ExtractContractType(string text)
    {
        // Enhanced patterns for various contract types
        var titlePatterns = new[]
        {
            // IP License
            @"(INTELLECTUAL\s+PROPERTY\s+LICENSE\s+AGREEMENT|IP\s+LICENSE\s+AGREEMENT)",
            @"(LICENSE\s+AGREEMENT)",
            @"(PATENT\s+LICENSE\s+AGREEMENT)",
            @"(TECHNOLOGY\s+LICENSE\s+AGREEMENT)",
            
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
            // IP License-specific
            @"\b(license\s+grant|licensing|licensed\s+technology)\b",
            @"\b(exclusivity|exclusive|non-exclusive)\b",
            @"\b(royalt(?:y|ies)|running\s+royalt(?:y|ies))\b",
            @"\b(patent(?:s)?|licensed\s+patents?)\b",
            @"\b(know-how|trade\s+secrets?)\b",
            @"\b(sublicense|sublicensing)\b",
            @"\b(territory|field\s+of\s+use)\b",
            @"\b(improvements?|joint\s+improvements?)\b",
            @"\b(patent\s+enforcement|patent\s+prosecution)\b",
            @"\b(diligence|commercialization)\b",
            
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
            @"\b(confidential(?:ity)?|proprietary)\b",
            @"\b(warrant(?:y|ies))\b",
            @"\b(indemnif(?:y|ication))\b",
            @"\b(termination|cancellation)\b",
            @"\b(liability|damages)\b",
            @"\b(governing\s+law|jurisdiction)\b",
            @"\b(dispute\s+resolution|arbitration)\b",
            @"\b(force\s+majeure)\b",
            @"\b(support|maintenance)\b",
            @"\b(training|documentation)\b",
            @"\b(audit|records|reporting)\b"
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

        return keyTerms.Take(20).OrderBy(t => t).ToList();
    }

    private ContractMetadata ExtractMetadataWithRulesAsync(string text)
    {
        _logger.LogInformation("Using rule-based metadata extraction");

        try
        {
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
        var titlePatterns = new[]
        {
            @"^\s*([A-Z][A-Z\s]+(?:AGREEMENT|CONTRACT))\s*$",
            @"^([A-Z][A-Za-z\s]+(?:Agreement|Contract))\s*$",
            @"(?:CONTRACT|AGREEMENT):\s*(.*?)(?=\n|\r)",
            @"TITLE:\s*(.*?)(?=\n|\r)"
        };

        var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < Math.Min(5, lines.Length); i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            
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
            
            if ((line.Contains("AGREEMENT", StringComparison.OrdinalIgnoreCase) || 
                 line.Contains("CONTRACT", StringComparison.OrdinalIgnoreCase)) &&
                line.Length > 10 && line.Length < 200)
            {
                return line;
            }
        }

        return lines.FirstOrDefault(l => l.Trim().Length > 10)?.Trim();
    }

    private DateTime? ExtractContractDate(string text)
    {
        var datePatterns = new[]
        {
            @"(?:as\s+of|dated|executed\s+as\s+of)\s+(\w+\s+\d{1,2},\s+\d{4})",
            @"(?:DATE|DATED|EXECUTED|SIGNED|EFFECTIVE).*?(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})",
            @"(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})",
            @"(\w+\s+\d{1,2},\s+\d{4})"
        };

        foreach (var pattern in datePatterns)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
            {
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

    private string? ExtractCurrency(string text)
    {
        var currencyPatterns = new[]
        {
            @"\b(USD|EUR|GBP|CAD|AUD|JPY)\b",
            @"\$([\d,]+(?:\.\d{2})?)"
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

    private async Task<List<string>> CreateSemanticChunksAsync(string text)
    {
        _logger.LogInformation("Creating semantic chunks with improved section-based splitting");
        
        var chunks = new List<string>();
        const int maxChunkSize = 700; // Reduced from 1500 for better granularity
        const int minChunkSize = 100;
        
        // Strategy 1: Try to split on numbered sections (e.g., "1. SECTION", "2. SECTION")
        // This works best for structured contracts like the Software Development Agreement
        var sectionPattern = @"(?=(?:^|\r?\n)\s*\d+\.\s+[A-Z][A-Z\s]+(?:\r?\n|$))";
        var sections = Regex.Split(text, sectionPattern, RegexOptions.Multiline)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();
        
        _logger.LogDebug("Section-based split resulted in {SectionCount} potential sections", sections.Count);
        
        // If section-based splitting produced good results (more than 3 sections), use it
        if (sections.Count > 3)
        {
            _logger.LogInformation("Using section-based chunking with {SectionCount} sections", sections.Count);
            
            var currentChunk = new StringBuilder();
            
            foreach (var section in sections)
            {
                // If this section alone is too large, split it further
                if (section.Length > maxChunkSize)
                {
                    if (currentChunk.Length >= minChunkSize)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }
                    
                    // Split large section by sentences
                    var sentences = Regex.Split(section, @"(?<=[.!?])\s+")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                    
                    if (sentences.Count > 1)
                    {
                        var sentenceChunk = new StringBuilder();
                        foreach (var sentence in sentences)
                        {
                            if (sentenceChunk.Length + sentence.Length + 1 > maxChunkSize && sentenceChunk.Length > 0)
                            {
                                chunks.Add(sentenceChunk.ToString().Trim());
                                sentenceChunk.Clear();
                            }
                            if (sentenceChunk.Length > 0) sentenceChunk.Append(' ');
                            sentenceChunk.Append(sentence);
                        }
                        if (sentenceChunk.Length > 0)
                        {
                            chunks.Add(sentenceChunk.ToString().Trim());
                        }
                    }
                    else
                    {
                        chunks.Add(section.Trim());
                    }
                }
                // If adding this section would exceed max size, save current chunk first
                else if (currentChunk.Length > 0 && currentChunk.Length + section.Length + 2 > maxChunkSize)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                    currentChunk.Append(section);
                }
                // Otherwise, add to current chunk
                else
                {
                    if (currentChunk.Length > 0) currentChunk.Append("\r\n\r\n");
                    currentChunk.Append(section);
                }
            }
            
            if (currentChunk.Length >= minChunkSize)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }
            else if (currentChunk.Length > 0 && chunks.Count > 0)
            {
                chunks[chunks.Count - 1] += "\r\n\r\n" + currentChunk.ToString().Trim();
            }
            else if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }
            
            _logger.LogInformation("Section-based chunking created {ChunkCount} chunks. Average size: {AvgSize} chars", 
                chunks.Count, chunks.Any() ? chunks.Average(c => c.Length) : 0);
            
            return chunks;
        }
        
        // Strategy 2: Fall back to paragraph-based splitting if section splitting didn't work
        _logger.LogInformation("Section-based splitting didn't work well, falling back to paragraph-based chunking");
        
        var paragraphSeparators = new[] { "\r\n\r\n", "\n\n", "\r\n\n", "\n\r\n" };
        List<string> paragraphs = new List<string>();
        
        foreach (var separator in paragraphSeparators)
        {
            paragraphs = text.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
            
            if (paragraphs.Count > 2)
            {
                _logger.LogDebug("Found {ParagraphCount} paragraphs using separator", paragraphs.Count);
                break;
            }
        }
        
        if (paragraphs.Count > 2)
        {
            var currentChunk = new StringBuilder();
            
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Length > maxChunkSize)
                {
                    if (currentChunk.Length >= minChunkSize)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }
                    chunks.Add(paragraph.Trim());
                }
                else if (currentChunk.Length + paragraph.Length + 2 > maxChunkSize && currentChunk.Length >= minChunkSize)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                    currentChunk.Append(paragraph);
                }
                else
                {
                    if (currentChunk.Length > 0) currentChunk.Append("\r\n\r\n");
                    currentChunk.Append(paragraph);
                }
            }
            
            if (currentChunk.Length >= minChunkSize)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }
            else if (currentChunk.Length > 0 && chunks.Count > 0)
            {
                chunks[chunks.Count - 1] += "\r\n\r\n" + currentChunk.ToString().Trim();
            }
            else if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }
            
            _logger.LogInformation("Paragraph-based chunking created {ChunkCount} chunks. Average size: {AvgSize} chars", 
                chunks.Count, chunks.Any() ? chunks.Average(c => c.Length) : 0);
            
            return chunks;
        }
        
        // Strategy 3: Last resort - split by fixed size
        _logger.LogWarning("No good paragraph structure found, using fixed-size chunking");
        for (int i = 0; i < text.Length; i += maxChunkSize)
        {
            var chunkSize = Math.Min(maxChunkSize, text.Length - i);
            chunks.Add(text.Substring(i, chunkSize).Trim());
        }
        
        _logger.LogInformation("Fixed-size chunking created {ChunkCount} chunks", chunks.Count);
        return chunks;
    }

    private async Task<List<ContractChunk>> ChunkTextWithAIAsync(string text, Guid documentId)
    {
        var chunks = new List<ContractChunk>();
        var semanticChunks = await CreateSemanticChunksAsync(text);
        
        int chunkIndex = 0;
        int currentPosition = 0;
        
        foreach (var semanticChunk in semanticChunks)
        {
            var chunkType = await ClassifyChunkTypeAsync(semanticChunk);
            
            // Extract section information if available
            var sectionInfo = ExtractSectionInfo(semanticChunk);
            
            // Build metadata dictionary with useful information
            var metadata = new Dictionary<string, object>
            {
                ["chunkType"] = chunkType.ToString(),
                ["charCount"] = semanticChunk.Length,
                ["createdAt"] = DateTime.UtcNow.ToString("O"),
                ["processingMethod"] = "ai_classification"
            };
            
            // Add section information if detected
            if (sectionInfo.HasValue)
            {
                metadata["sectionNumber"] = sectionInfo.Value.Number;
                metadata["sectionTitle"] = sectionInfo.Value.Title;
                metadata["hasSectionNumber"] = true;
            }
            else
            {
                metadata["hasSectionNumber"] = false;
            }
            
            // Add word count for analytics
            var wordCount = semanticChunk.Split(new[] { ' ', '\n', '\r', '\t' }, 
                StringSplitOptions.RemoveEmptyEntries).Length;
            metadata["wordCount"] = wordCount;
            
            // Add first few words as preview
            var preview = semanticChunk.Length > 50 
                ? semanticChunk.Substring(0, 50).Replace("\n", " ").Replace("\r", " ") + "..."
                : semanticChunk.Replace("\n", " ").Replace("\r", " ");
            metadata["preview"] = preview;
            
            var chunk = new ContractChunk(
                Guid.NewGuid(),
                documentId,
                semanticChunk,
                chunkIndex++,
                currentPosition,
                currentPosition + semanticChunk.Length,
                chunkType,
                metadata
            );
            
            chunks.Add(chunk);
            currentPosition += semanticChunk.Length;
        }
        
        _logger.LogInformation("Created {ChunkCount} chunks with metadata for document {DocumentId}", 
            chunks.Count, documentId);
        
        return chunks;
    }
    
    private (int Number, string Title)? ExtractSectionInfo(string chunkText)
    {
        // Try to extract section number and title from the beginning of the chunk
        // Pattern: "1. PROJECT SCOPE", "2. TIMELINE AND MILESTONES", etc.
        var sectionPattern = @"^\s*(\d+)\.\s+([A-Z][A-Z\s]+?)(?:\r?\n|$)";
        var match = Regex.Match(chunkText, sectionPattern, RegexOptions.Multiline);
        
        if (match.Success && int.TryParse(match.Groups[1].Value, out var sectionNumber))
        {
            var sectionTitle = match.Groups[2].Value.Trim();
            return (sectionNumber, sectionTitle);
        }
        
        return null;
    }

    private async Task<(Stream Stream, string ContentType)> GetDocumentContentAsync(Guid documentId)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

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

            var chatCompletionService = _semanticKernel.Services.GetService(typeof(Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService));
            
            if (chatCompletionService == null)
            {
                _logger.LogWarning("No chat completion service available in kernel");
                return false;
            }

            var testPrompt = "Reply with just the word 'OK' to confirm you are working.";
            
            _logger.LogInformation("Testing AI connection with prompt: {Prompt}", testPrompt);
            
            #pragma warning disable SKEXP0001
            var chatService = (Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService)chatCompletionService;
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(testPrompt);
            
            // Configure execution settings
            var executionSettings = new Microsoft.SemanticKernel.PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["temperature"] = 0.1
                }
            };
            
            var chatResults = await chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings: executionSettings,
                kernel: _semanticKernel);
            var result = chatResults?.LastOrDefault()?.Content?.Trim();
            #pragma warning restore SKEXP0001
            
            _logger.LogInformation("AI connection test result: {Result}", result);
            return !string.IsNullOrEmpty(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI connection test failed with error: {ErrorMessage}", ex.Message);
            
            if (ex.Message.Contains("404") && ex.Message.Contains("NOT_FOUND"))
            {
                _logger.LogError("Google Gemini API returned 404 - this usually means the model name is incorrect or not available. Current model: {Model}", 
                    _configuration["AI:Gemini:Model"]);
            }
            
            return false;
        }
    }
}

public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
    }
}