# Complete Configuration Guide for Multi-Provider LLM Support

## Overview

All services in the ContractProcessingSystem now support **multi-provider LLM configuration** with automatic fallback. You can use **GitHub Models**, **Google Gemini**, or **OpenAI** across all services.

## Services Using LLM Providers

The following services use the `LLMProviders` configuration:

1. **AIAgent** - For chat, contract analysis, and comparison
2. **EmbeddingService** - For generating document embeddings
3. **QueryService** - For semantic search and summarization
4. **DocumentParser** - For AI-powered metadata extraction

## Quick Start Configuration

### Step 1: Choose Your Primary Provider

Edit `appsettings.Development.json` for each service:

```json
{
  "LLMProviders": {
    "DefaultProvider": "GitHubModels",  // or "Gemini" or "OpenAI"
    "EmbeddingProvider": "Gemini"       // Best for embeddings
  }
}
```

### Step 2: Add API Keys

#### Option A: GitHub Models (Recommended - Free)

```json
{
  "Providers": [
    {
      "Type": "GitHubModels",
      "Name": "GitHubModels",
      "ApiKey": "github_pat_11XXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
      "IsEnabled": true,
      "Priority": 1
    }
  ]
}
```

**Get your token:** https://github.com/settings/tokens

#### Option B: Google Gemini (Free tier available)

```json
{
  "Providers": [
    {
      "Type": "Gemini",
      "Name": "Gemini",
      "ApiKey": "AIzaSyXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
      "IsEnabled": true,
      "Priority": 1
    }
  ]
}
```

**Get your API key:** https://makersuite.google.com/app/apikey

#### Option C: OpenAI (Paid)

```json
{
  "Providers": [
    {
      "Type": "OpenAI",
      "Name": "OpenAI",
      "ApiKey": "sk-XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
      "IsEnabled": true,
      "Priority": 1
    }
  ]
}
```

**Get your API key:** https://platform.openai.com/api-keys

## Configuration Files by Service

### 1. AIAgent Service

**File:** `ContractProcessingSystem.AIAgent/appsettings.Development.json`

```json
{
  "Services": {
    "DocumentUpload": "https://localhost:7048",
    "DocumentParser": "https://localhost:7258",
    "EmbeddingService": "https://localhost:7070",
    "VectorService": "https://localhost:7197",
    "QueryService": "https://localhost:7004"
  },
  "LLMProviders": {
    "DefaultProvider": "GitHubModels",
    "EmbeddingProvider": "GitHubModels",
    "EnableFallback": true,
    "Providers": [
      {
        "Type": "GitHubModels",
        "Name": "GitHubModels",
        "ApiKey": "github_pat_YOUR_TOKEN",
        "DefaultChatModel": "gpt-4o-mini",
        "DefaultEmbeddingModel": "text-embedding-3-small",
        "IsEnabled": true,
        "Priority": 1
      },
      {
        "Type": "Gemini",
        "Name": "Gemini",
        "ApiKey": "your-gemini-key",
        "DefaultChatModel": "gemini-pro",
        "DefaultEmbeddingModel": "text-embedding-004",
        "IsEnabled": true,
        "Priority": 2
      }
    ]
  }
}
```

### 2. EmbeddingService

**File:** `ContractProcessingSystem.EmbeddingService/appsettings.Development.json`

```json
{
  "Services": {
    "VectorService": "https://localhost:7197"
  },
  "LLMProviders": {
    "EmbeddingProvider": "Gemini",  // Gemini recommended for embeddings
    "Providers": [
      {
        "Type": "Gemini",
        "Name": "Gemini",
        "ApiKey": "your-gemini-key",
        "DefaultEmbeddingModel": "text-embedding-004",
        "IsEnabled": true,
        "Priority": 1
      }
    ]
  }
}
```

### 3. QueryService

**File:** `ContractProcessingSystem.QueryService/appsettings.Development.json`

```json
{
  "Services": {
    "VectorService": "https://localhost:7197",
    "EmbeddingService": "https://localhost:7070"
  },
  "LLMProviders": {
    "DefaultProvider": "Gemini",
    "Providers": [
      {
        "Type": "Gemini",
        "Name": "Gemini",
        "ApiKey": "your-gemini-key",
        "DefaultChatModel": "gemini-pro",
        "IsEnabled": true,
        "Priority": 1
      }
    ]
  }
}
```

### 4. DocumentParser

**File:** `ContractProcessingSystem.DocumentParser/appsettings.Development.json`

DocumentParser uses legacy configuration (will be migrated):

```json
{
  "AI": {
    "Provider": "Gemini",
    "Gemini": {
      "ApiKey": "your-gemini-key",
      "Model": "gemini-2.5-flash"
    }
  }
}
```

## Recommended Provider Strategy

### For Development (Free)

```json
{
  "LLMProviders": {
    "DefaultProvider": "GitHubModels",  // Free, fast chat
    "EmbeddingProvider": "Gemini",      // Best free embeddings
    "EnableFallback": true
  }
}
```

### For Production (Paid)

```json
{
  "LLMProviders": {
    "DefaultProvider": "OpenAI",        // Reliable, quality
    "EmbeddingProvider": "OpenAI",      // Consistent embeddings
    "EnableFallback": true,
    "Providers": [
      {
        "Type": "OpenAI",
        "Priority": 1,
        "DefaultChatModel": "gpt-4o",
        "DefaultEmbeddingModel": "text-embedding-3-large"
      },
      {
        "Type": "GitHubModels",
        "Priority": 2  // Fallback
      }
    ]
  }
}
```

## Provider Comparison

| Feature | GitHub Models | Gemini | OpenAI |
|---------|--------------|--------|--------|
| **Cost** | Free | Free tier | Paid |
| **Chat Quality** | Excellent (GPT-4o) | Very Good | Excellent |
| **Embedding Quality** | Excellent | Excellent | Excellent |
| **Rate Limits** | High (authenticated) | Moderate | High (paid) |
| **Recommended For** | Development, Chat | Embeddings, Analysis | Production |
| **Setup Complexity** | Easy (GitHub token) | Easy (API key) | Easy (API key) |

## Environment Variables (Alternative)

You can also use environment variables instead of appsettings:

```bash
# Windows PowerShell
$env:LLMProviders__DefaultProvider = "GitHubModels"
$env:LLMProviders__Providers__0__ApiKey = "github_pat_YOUR_TOKEN"

# Linux/Mac
export LLMProviders__DefaultProvider="GitHubModels"
export LLMProviders__Providers__0__ApiKey="github_pat_YOUR_TOKEN"
```

## User Secrets (Recommended for API Keys)

### Initialize User Secrets

```bash
cd ContractProcessingSystem.AIAgent
dotnet user-secrets init
```

### Set API Keys Securely

```bash
# GitHub Models
dotnet user-secrets set "LLMProviders:Providers:0:ApiKey" "github_pat_YOUR_TOKEN"

# Gemini
dotnet user-secrets set "LLMProviders:Providers:1:ApiKey" "your-gemini-key"

# OpenAI
dotnet user-secrets set "LLMProviders:Providers:2:ApiKey" "sk-your-openai-key"
```

Repeat for each service that needs LLM access.

## Troubleshooting

### Provider Not Working

1. **Check API key is set:**
   ```bash
   # In appsettings.Development.json
   "ApiKey": "github_pat_11XXXX..."  # Should NOT be empty
   ```

2. **Check provider is enabled:**
   ```json
   "IsEnabled": true,  // Must be true
   ```

3. **Check logs:**
   ```
   [INF] Initialized LLM provider: GitHubModels (GitHubModels)
   [WRN] Default provider 'GitHubModels' not available, using 'Gemini'
   ```

### Authentication Errors

**GitHub Models 401:**
- Token expired - generate a new one
- Token doesn't have correct format (should start with `github_pat_`)

**Gemini 400:**
- API key invalid or not activated
- Go to https://makersuite.google.com/app/apikey

**OpenAI 401:**
- API key invalid
- Check at https://platform.openai.com/api-keys

### Fallback Not Working

Ensure `EnableFallback` is true:

```json
{
  "LLMProviders": {
    "EnableFallback": true,  // Required for fallback
    "RetryAttempts": 3
  }
}
```

## Testing Your Configuration

### Test AIAgent Chat

```bash
curl -X POST https://localhost:7070/api/agent/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Hello, what can you do?",
    "contextDocumentId": null
  }'
```

### Test Embedding Generation

```bash
curl -X POST https://localhost:7070/api/embeddings/generate-single \
  -H "Content-Type: application/json" \
  -d '"This is a test contract clause"'
```

### Check Provider Status

```bash
curl https://localhost:7070/health
```

## Advanced Configuration

### Different Providers for Different Tasks

```json
{
  "LLMProviders": {
    "DefaultProvider": "GitHubModels",  // For chat
    "EmbeddingProvider": "Gemini",      // For embeddings
    "Providers": [
      {
        "Type": "GitHubModels",
        "DefaultChatModel": "gpt-4o",       // Better quality
        "DefaultEmbeddingModel": "text-embedding-3-large"
      },
      {
        "Type": "Gemini",
        "DefaultChatModel": "gemini-pro",
        "DefaultEmbeddingModel": "text-embedding-004"
      }
    ]
  }
}
```

### Custom Model Configuration

```json
{
  "Providers": [
    {
      "Type": "GitHubModels",
      "Name": "GitHubModels-GPT4",
      "DefaultChatModel": "gpt-4o",  // Use GPT-4o instead of mini
      "AdditionalSettings": {
        "Temperature": 0.3,   // More deterministic
        "MaxTokens": 4000     // Longer responses
      }
    }
  ]
}
```

## Security Best Practices

1. ? **Use User Secrets** for development
2. ? **Use Azure Key Vault** for production
3. ? **Never commit API keys** to source control
4. ? **Rotate keys regularly**
5. ? **Use separate keys** for dev/staging/prod

## Support

- **GitHub Models:** https://github.com/marketplace/models
- **Google Gemini:** https://ai.google.dev/docs
- **OpenAI:** https://platform.openai.com/docs

---

**Last Updated:** January 2025  
**Supported Providers:** GitHub Models, Google Gemini, OpenAI  
**Minimum .NET Version:** .NET 8
