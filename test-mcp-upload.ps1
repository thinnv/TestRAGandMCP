# Test MCP Upload Service
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Testing MCP Document Upload Service" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# SSL bypass for local testing
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

$baseUrl = "https://localhost:7048"

# Test 1: List MCP Tools
Write-Host "1. Testing MCP tools/list..." -ForegroundColor Yellow
$listRequest = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/list"
    params = @{}
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" -Method POST -Body $listRequest -ContentType "application/json" -TimeoutSec 10
    Write-Host "   ✓ MCP endpoint found!" -ForegroundColor Green
    Write-Host "   Available tools:" -ForegroundColor Cyan
    $response.result.tools | ForEach-Object {
        Write-Host "     - $($_.name)" -ForegroundColor White
        Write-Host "       $($_.description)" -ForegroundColor Gray
    }
} catch {
    Write-Host "   ✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Trying alternate endpoint..." -ForegroundColor Yellow
    
    # Try the swagger-documented endpoint
    try {
        $response2 = Invoke-RestMethod -Uri "$baseUrl/api/EnhancedDocumentUploadMCP/mcp" -Method POST -Body $listRequest -ContentType "application/json" -TimeoutSec 10
        Write-Host "   ✓ Alternate endpoint works!" -ForegroundColor Green
        $baseUrl = "$baseUrl/api/EnhancedDocumentUploadMCP"
    } catch {
        Write-Host "   ✗ Alternate also failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "`nNote: MCP endpoints may not be properly configured." -ForegroundColor Yellow
        Write-Host "Falling back to REST API testing...`n" -ForegroundColor Yellow
        
        # Test REST API instead
        Write-Host "Testing REST API Upload..." -ForegroundColor Cyan
        try {
            $health = Invoke-RestMethod -Uri "$baseUrl/api/Documents/health" -TimeoutSec 5
            Write-Host "✓ REST API is healthy: $($health.status)" -ForegroundColor Green
        } catch {
            Write-Host "✗ REST API health check failed" -ForegroundColor Red
        }
        exit
    }
}

# Test 2: Upload document via MCP
Write-Host "`n2. Testing MCP UploadDocument tool..." -ForegroundColor Yellow

$sampleContent = @"
SAMPLE CONTRACT - MCP UPLOAD TEST
Date: November 9, 2025

PARTIES:
- Company A: TechCorp Industries  
- Company B: Innovation Solutions LLC

TERMS:
This is a test contract document uploaded via Model Context Protocol (MCP).
The purpose is to validate the MCP upload functionality.

DURATION: 12 months
VALUE: Test purposes only

SIGNATURES:
_______________________
John Doe, CEO TechCorp

_______________________  
Jane Smith, CTO Innovation Solutions
"@

$bytes = [System.Text.Encoding]::UTF8.GetBytes($sampleContent)
$base64Content = [Convert]::ToBase64String($bytes)
$filename = "mcp_test_contract_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"

$uploadRequest = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/call"
    params = @{
        name = "UploadDocument"
        arguments = @{
            filename = $filename
            contentType = "text/plain"
            content = $base64Content
            uploadedBy = "mcp-test-client"
        }
    }
} | ConvertTo-Json -Depth 10

try {
    $uploadResponse = Invoke-RestMethod -Uri "$baseUrl/mcp" -Method POST -Body $uploadRequest -ContentType "application/json" -TimeoutSec 30
    
    # Parse the response
    if ($uploadResponse.result.content) {
        $resultText = $uploadResponse.result.content[0].text
        $uploadResult = $resultText | ConvertFrom-Json
        
        if ($uploadResult.success) {
            Write-Host "   ✓ Document uploaded successfully!" -ForegroundColor Green
            Write-Host "   Document ID: $($uploadResult.documentId)" -ForegroundColor White
            Write-Host "   Filename: $($uploadResult.filename)" -ForegroundColor White
            Write-Host "   Size: $($uploadResult.size) bytes" -ForegroundColor White
            Write-Host "   Uploaded By: $($uploadResult.uploadedBy)" -ForegroundColor White
            
            $documentId = $uploadResult.documentId
        } else {
            Write-Host "   ✗ Upload failed: $($uploadResult.error)" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "   ✗ Upload request failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: List documents via MCP
if ($documentId) {
    Write-Host "`n3. Testing MCP ListDocuments tool..." -ForegroundColor Yellow
    
    $listDocsRequest = @{
        jsonrpc = "2.0"
        id = 3
        method = "tools/call"
        params = @{
            name = "ListDocuments"
            arguments = @{
                page = 1
                pageSize = 5
            }
        }
    } | ConvertTo-Json -Depth 10
    
    try {
        $listResponse = Invoke-RestMethod -Uri "$baseUrl/mcp" -Method POST -Body $listDocsRequest -ContentType "application/json" -TimeoutSec 10
        
        if ($listResponse.result.content) {
            $resultText = $listResponse.result.content[0].text
            $listResult = $resultText | ConvertFrom-Json
            
            if ($listResult.success) {
                Write-Host "   ✓ Documents listed successfully!" -ForegroundColor Green
                Write-Host "   Total documents: $($listResult.documents.Count)" -ForegroundColor White
                $listResult.documents | ForEach-Object {
                    Write-Host "     - $($_.fileName) (ID: $($_.id), Status: $($_.status))" -ForegroundColor Gray
                }
            }
        }
    } catch {
        Write-Host "   ✗ List request failed: $($_.Exception.Message)" -ForegroundColor Red
    }

    # Test 4: Get specific document via MCP
    Write-Host "`n4. Testing MCP GetDocument tool..." -ForegroundColor Yellow
    
    $getDocRequest = @{
        jsonrpc = "2.0"
        id = 4
        method = "tools/call"
        params = @{
            name = "GetDocument"
            arguments = @{
                documentId = $documentId
            }
        }
    } | ConvertTo-Json -Depth 10
    
    try {
        $getResponse = Invoke-RestMethod -Uri "$baseUrl/mcp" -Method POST -Body $getDocRequest -ContentType "application/json" -TimeoutSec 10
        
        if ($getResponse.result.content) {
            $resultText = $getResponse.result.content[0].text
            $getResult = $resultText | ConvertFrom-Json
            
            if ($getResult.success) {
                Write-Host "   ✓ Document retrieved successfully!" -ForegroundColor Green
                Write-Host "   ID: $($getResult.document.id)" -ForegroundColor White
                Write-Host "   Filename: $($getResult.document.fileName)" -ForegroundColor White
                Write-Host "   Status: $($getResult.document.status)" -ForegroundColor White
                Write-Host "   Size: $($getResult.document.fileSize) bytes" -ForegroundColor White
            }
        }
    } catch {
        Write-Host "   ✗ Get request failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "MCP Testing Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan
