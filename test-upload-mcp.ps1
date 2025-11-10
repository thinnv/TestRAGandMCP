# Test MCP Document Upload Service
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Testing MCP Document Upload Service" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Disable SSL certificate validation for local testing
add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Try to find the DocumentUpload service on common Aspire ports
$possiblePorts = @(7048, 5185, 7000, 7001, 7002, 7003, 7004, 5000, 5001, 5002, 5003, 5004)
$baseUrl = $null

Write-Host "   Searching for DocumentUpload service..." -ForegroundColor Gray
foreach ($port in $possiblePorts) {
    # Try HTTPS first
    foreach ($protocol in @("https", "http")) {
        try {
            $testUrl = "${protocol}://localhost:$port/mcp"
            $testRequest = @{
                jsonrpc = "2.0"
                id = 0
                method = "tools/list"
            } | ConvertTo-Json
            
            $response = Invoke-RestMethod -Uri $testUrl -Method POST -Body $testRequest -ContentType "application/json" -TimeoutSec 2 -ErrorAction Stop
            if ($response.result.tools) {
                $baseUrl = "${protocol}://localhost:$port"
                Write-Host "   Found service on $baseUrl" -ForegroundColor Green
                break
            }
        } catch {
            # Continue to next protocol/port
        }
    }
    if ($baseUrl) { break }
}

if (-not $baseUrl) {
    Write-Host "   ERROR: Could not find DocumentUpload service on any port!" -ForegroundColor Red
    Write-Host "   Tried ports: $($possiblePorts -join ', ')" -ForegroundColor Yellow
    Write-Host "   Check the Aspire Dashboard at https://localhost:17139 to see the actual ports" -ForegroundColor Yellow
    exit 1
}

# Test 1: List available MCP tools
Write-Host "`n1. Testing MCP Tools List..." -ForegroundColor Yellow
$listToolsRequest = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/list"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/mcp" -Method POST -Body $listToolsRequest -ContentType "application/json" -TimeoutSec 10
    Write-Host "   SUCCESS! Available tools:" -ForegroundColor Green
    $response.result.tools | ForEach-Object {
        Write-Host "   - $($_.name)" -ForegroundColor White
        Write-Host "     Description: $($_.description)" -ForegroundColor Gray
    }
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Make sure the AppHost is running and DocumentUpload service is available." -ForegroundColor Yellow
    exit 1
}

# Test 2: Create a sample document to upload
Write-Host "`n2. Creating sample document..." -ForegroundColor Yellow
$sampleContent = @"
SAMPLE CONTRACT AGREEMENT

This Agreement is entered into as of November 9, 2025, between:
- Party A: Acme Corporation
- Party B: Global Services Inc.

TERMS:
1. Services to be provided as outlined in Exhibit A
2. Payment terms: Net 30 days
3. Contract duration: 12 months

SIGNATURES:
_______________________
John Doe, CEO, Acme Corporation

_______________________
Jane Smith, VP, Global Services Inc.
"@

$bytes = [System.Text.Encoding]::UTF8.GetBytes($sampleContent)
$base64Content = [Convert]::ToBase64String($bytes)

Write-Host "   Sample document created (Size: $($bytes.Length) bytes)" -ForegroundColor Green

# Test 3: Upload document via MCP tool
Write-Host "`n3. Testing UploadDocument MCP tool..." -ForegroundColor Yellow
$uploadRequest = @{
    jsonrpc = "2.0"
    id = 2
    method = "tools/call"
    params = @{
        name = "UploadDocument"
        arguments = @{
            filename = "sample_contract_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"
            contentType = "text/plain"
            content = $base64Content
            uploadedBy = "mcp-test-user"
        }
    }
} | ConvertTo-Json -Depth 10

try {
    $uploadResponse = Invoke-RestMethod -Uri "$baseUrl/mcp" -Method POST -Body $uploadRequest -ContentType "application/json" -TimeoutSec 30
    $uploadResult = $uploadResponse.result.content[0].text | ConvertFrom-Json
    
    if ($uploadResult.success) {
        Write-Host "   SUCCESS! Document uploaded:" -ForegroundColor Green
        Write-Host "   Document ID: $($uploadResult.documentId)" -ForegroundColor White
        Write-Host "   Filename: $($uploadResult.filename)" -ForegroundColor White
        Write-Host "   Size: $($uploadResult.size) bytes" -ForegroundColor White
        Write-Host "   Uploaded At: $($uploadResult.uploadedAt)" -ForegroundColor White
        Write-Host "   Uploaded By: $($uploadResult.uploadedBy)" -ForegroundColor White
        $documentId = $uploadResult.documentId
    } else {
        Write-Host "   FAILED: $($uploadResult.error)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   Response: $($_.Exception.Response)" -ForegroundColor Gray
    exit 1
}

# Test 4: Get document details
Write-Host "`n4. Testing GetDocument MCP tool..." -ForegroundColor Yellow
$getDocRequest = @{
    jsonrpc = "2.0"
    id = 3
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
    $getResult = $getResponse.result.content[0].text | ConvertFrom-Json
    
    if ($getResult.success) {
        Write-Host "   SUCCESS! Document retrieved:" -ForegroundColor Green
        Write-Host "   ID: $($getResult.document.id)" -ForegroundColor White
        Write-Host "   Filename: $($getResult.document.fileName)" -ForegroundColor White
        Write-Host "   Status: $($getResult.document.status)" -ForegroundColor White
    } else {
        Write-Host "   FAILED: $($getResult.error)" -ForegroundColor Red
    }
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 5: List all documents
Write-Host "`n5. Testing ListDocuments MCP tool..." -ForegroundColor Yellow
$listDocsRequest = @{
    jsonrpc = "2.0"
    id = 4
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
    $listResult = $listResponse.result.content[0].text | ConvertFrom-Json
    
    if ($listResult.success) {
        Write-Host "   SUCCESS! Documents listed:" -ForegroundColor Green
        Write-Host "   Total documents: $($listResult.documents.Count)" -ForegroundColor White
        $listResult.documents | ForEach-Object {
            Write-Host "   - $($_.fileName) (ID: $($_.id), Status: $($_.status))" -ForegroundColor Gray
        }
    } else {
        Write-Host "   FAILED: $($listResult.error)" -ForegroundColor Red
    }
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 6: Get upload statistics
Write-Host "`n6. Testing GetUploadStatistics MCP tool..." -ForegroundColor Yellow
$statsRequest = @{
    jsonrpc = "2.0"
    id = 5
    method = "tools/call"
    params = @{
        name = "GetUploadStatistics"
        arguments = @{
            period = "month"
            groupBy = "status"
        }
    }
} | ConvertTo-Json -Depth 10

try {
    $statsResponse = Invoke-RestMethod -Uri "$baseUrl/mcp" -Method POST -Body $statsRequest -ContentType "application/json" -TimeoutSec 10
    $statsResult = $statsResponse.result.content[0].text | ConvertFrom-Json
    
    if ($statsResult.success) {
        Write-Host "   SUCCESS! Statistics retrieved:" -ForegroundColor Green
        Write-Host "   Period: $($statsResult.period)" -ForegroundColor White
        Write-Host "   Group By: $($statsResult.groupBy)" -ForegroundColor White
        Write-Host "   Statistics: $($statsResult.statistics)" -ForegroundColor Gray
    } else {
        Write-Host "   FAILED: $($statsResult.error)" -ForegroundColor Red
    }
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "MCP Upload Service Test Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
