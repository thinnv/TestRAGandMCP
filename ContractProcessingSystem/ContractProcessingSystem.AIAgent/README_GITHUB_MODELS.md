# GitHub Models Integration Guide

## Overview

The AIAgent service now supports **GitHub Models** as an LLM provider alongside Gemini and OpenAI. GitHub Models provides access to various AI models including GPT-4o, GPT-4o-mini, Phi-3, and more through GitHub's inference API.

## Getting Started

### 1. Get a GitHub Personal Access Token

1. Go to [GitHub Settings > Developer settings > Personal access tokens](https://github.com/settings/tokens)
2. Click "Generate new token (classic)"
3. Select the following scopes:
   - `read:org` (if using organization models)
   - No specific scopes needed for public models
4. Generate and copy your token

### 2. Configure GitHub Models in `appsettings.json`

Update your `ContractProcessingSystem.AIAgent/appsettings.Development.json`:

```json
{
  "LLMProviders": {
    "DefaultProvider": "GitHubModels",
    "EmbeddingProvider": "GitHubModels",
    "SelectionStrategy": "Priority",
    "EnableFallback": true,
    "RetryAttempts": 3,
    "Providers": [
      {
        "Type": "GitHubModels",
        "Name": "GitHubModels",
        "ApiKey": "github_pat_YOUR_TOKEN_HERE",
        "Endpoint": "https://models.inference.ai.azure.com",
        "DefaultChatModel": "gpt-4o-mini",
        "DefaultEmbeddingModel": "text-embedding-3-small",
        "IsEnabled": true,
        "Priority": 1
      },
      {
        "Type": "Gemini",
        "Name": "Gemini",
        "ApiKey": "your-gemini-api-key",
        "DefaultChatModel": "gemini-pro",
        "DefaultEmbeddingModel": "text-embedding-004",
        "IsEnabled": true,
        "Priority": 2
      }
    ]
  }
}
```

### 3. Available Models

#### Chat Models
- **gpt-4o** - Most capable model (recommended for complex analysis)
- **gpt-4o-mini** - Faster and more cost-effective (recommended for chat)
- **Phi-3-medium-4k-instruct** - Microsoft's efficient model
- **Phi-3-mini-4k-instruct** - Lightweight option

#### Embedding Models
- **text-embedding-3-small** - 1536 dimensions (recommended)
- **text-embedding-3-large** - 3072 dimensions (higher quality)

### 4. Usage Example

Once configured, the AIAgent service will automatically use GitHub Models:

```csharp
// In your code, the factory will automatically use the configured provider
var llmProvider = _llmProviderFactory.GetDefaultProvider();
var response = await llmProvider.GenerateTextAsync("Analyze this contract...");
```

## Configuration Options

### Provider Priority

Providers are selected based on priority (higher priority = preferred):

```json
{
  "Providers": [
    {
      "Name": "GitHubModels",
      "Priority": 1,  // Used first
      "IsEnabled": true
    },
    {
      "Name": "Gemini",
      "Priority": 2,  // Fallback if GitHub Models fails
      "IsEnabled": true
    }
  ]
}
```

### Fallback Strategy

Enable fallback to automatically use the next available provider if one fails:

```json
{
  "EnableFallback": true,
  "RetryAttempts": 3
}
```

## Features

The GitHub Models provider supports:

? **Chat Completions** - For contract analysis, Q&A, and recommendations  
? **Embeddings** - For semantic search and document similarity  
? **Batch Processing** - Efficient processing of multiple requests  
? **Auto-retry** - Automatic retry on transient failures  
? **Fallback** - Seamless fallback to other providers  

## API Endpoints

### Chat with AIAgent

```http
POST https://localhost:7070/api/agent/chat
Content-Type: application/json

{
  "message": "Explain the termination clause in this contract",
  "contextDocumentId": "your-document-guid"
}
```

### Analyze Contract

```http
POST https://localhost:7070/api/agent/analyze/your-document-guid
```

### Compare Contracts

```http
POST https://localhost:7070/api/agent/compare
Content-Type: application/json

{
  "documentIds": [
    "guid-1",
    "guid-2"
  ]
}
```

## Testing the Integration

### Test Provider Availability

You can check if GitHub Models is properly configured by checking the service health:

```http
GET https://localhost:7070/health
```

### Test Chat Functionality

Use the MCP tool or direct API:

```csharp
var response = await _aiAgentService.ChatAsync(
    "What are the payment terms?",
    documentId: yourDocumentGuid
);
```

## Troubleshooting

### Authentication Errors

**Error:** `401 Unauthorized`  
**Solution:** Verify your GitHub Personal Access Token is valid and has not expired

### Model Not Found

**Error:** `404 Model not found`  
**Solution:** Check that the model name matches exactly (e.g., `gpt-4o-mini` not `gpt-4o-mini-instruct`)

### Rate Limiting

**Error:** `429 Too Many Requests`  
**Solution:** GitHub Models has rate limits. Enable fallback to another provider or implement retry with exponential backoff

## Advanced Configuration

### Custom Model Configuration

```json
{
  "Type": "GitHubModels",
  "Name": "GitHubModels-Custom",
  "ApiKey": "your-token",
  "DefaultChatModel": "gpt-4o",  // Use more powerful model
  "DefaultEmbeddingModel": "text-embedding-3-large",  // Higher quality embeddings
  "IsEnabled": true,
  "Priority": 1,
  "AdditionalSettings": {
    "Temperature": 0.3,  // More deterministic responses
    "MaxTokens": 2000    // Longer responses
  }
}
```

### Multi-Provider Setup

Use different providers for different tasks:

```json
{
  "DefaultProvider": "GitHubModels",     // For chat
  "EmbeddingProvider": "Gemini",         // For embeddings
  "Providers": [
    {
      "Name": "GitHubModels",
      "DefaultChatModel": "gpt-4o-mini",
      "SupportsChat": true,
      "SupportsEmbeddings": true,
      "Priority": 1
    },
    {
      "Name": "Gemini",
      "DefaultEmbeddingModel": "text-embedding-004",
      "SupportsChat": true,
      "SupportsEmbeddings": true,
      "Priority": 2
    }
  ]
}
```

## Cost Optimization

GitHub Models is currently **free** for GitHub users (subject to rate limits). Consider:

1. **Use `gpt-4o-mini`** for most tasks (faster, more quota)
2. **Reserve `gpt-4o`** for complex contract analysis
3. **Use `text-embedding-3-small`** for embeddings (sufficient for most use cases)
4. **Enable caching** to reduce redundant API calls

## Resources

- [GitHub Models Documentation](https://github.com/marketplace/models)
- [Available Models](https://github.com/marketplace/models)
- [API Reference](https://docs.github.com/en/rest/models)
- [Rate Limits](https://docs.github.com/en/rest/overview/resources-in-the-rest-api#rate-limiting)

## Support

For issues or questions:
1. Check the logs in `ContractProcessingSystem.AIAgent` service
2. Verify your configuration in `appsettings.Development.json`
3. Test with a simple prompt first before complex contract analysis
4. Enable fallback to Gemini for reliability
