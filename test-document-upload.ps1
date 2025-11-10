# Test Document Upload Service
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Testing Document Upload Service" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# SSL bypass for local testing
add-type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCerts : ICertificatePolicy {
        public bool CheckValidationResult(
            ServicePoint srvPoint, X509Certificate certificate,
            WebRequest request, int certificateProblem) {
            return true;
        }
    }
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCerts
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = {$true}

# Test 1: Health Check
Write-Host "1. Testing Health Endpoint..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "https://localhost:7048/api/Documents/health" -TimeoutSec 5
    Write-Host "   ✓ Service is HEALTHY" -ForegroundColor Green
    Write-Host "   Response: $($health | ConvertTo-Json)" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: List Documents  
Write-Host "`n2. Testing List Documents..." -ForegroundColor Yellow
try {
    $docs = Invoke-RestMethod -Uri "https://localhost:7048/api/Documents" -TimeoutSec 5
    Write-Host "   ✓ Documents retrieved: $($docs.Count) documents" -ForegroundColor Green
} catch {
    Write-Host "   ✗ List failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Upload a sample document
Write-Host "`n3. Testing Document Upload..." -ForegroundColor Yellow
$sampleContent = @"
SAMPLE CONTRACT AGREEMENT
Date: November 9, 2025

This Agreement is between:
Party A: Test Corporation
Party B: Sample Services Inc.

Terms: This is a test contract for MCP upload testing.
"@

$bytes = [System.Text.Encoding]::UTF8.GetBytes($sampleContent)
$boundary = [System.Guid]::NewGuid().ToString()
$fileName = "test_contract_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"

$bodyLines = @(
    "--$boundary",
    "Content-Disposition: form-data; name=`"file`"; filename=`"$fileName`"",
    "Content-Type: text/plain",
    "",
    $sampleContent,
    "--$boundary--"
)
$body = $bodyLines -join "`r`n"

try {
    $uploadResult = Invoke-RestMethod -Uri "https://localhost:7048/api/Documents/upload" `
        -Method POST `
        -ContentType "multipart/form-data; boundary=$boundary" `
        -Body $body `
        -TimeoutSec 10
    
    Write-Host "   ✓ Document uploaded successfully!" -ForegroundColor Green
    Write-Host "   Document ID: $($uploadResult.id)" -ForegroundColor White
    Write-Host "   Filename: $($uploadResult.fileName)" -ForegroundColor White
    Write-Host "   Status: $($uploadResult.status)" -ForegroundColor White
    
    $documentId = $uploadResult.id
} catch {
    Write-Host "   ✗ Upload failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Retrieve the uploaded document
if ($documentId) {
    Write-Host "`n4. Testing Get Document..." -ForegroundColor Yellow
    try {
        $doc = Invoke-RestMethod -Uri "https://localhost:7048/api/Documents/$documentId" -TimeoutSec 5
        Write-Host "   ✓ Document retrieved!" -ForegroundColor Green
        Write-Host "   ID: $($doc.id)" -ForegroundColor White
        Write-Host "   Filename: $($doc.fileName)" -ForegroundColor White
        Write-Host "   Size: $($doc.fileSize) bytes" -ForegroundColor White
        Write-Host "   Uploaded: $($doc.uploadedAt)" -ForegroundColor White
    } catch {
        Write-Host "   ✗ Retrieve failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Testing Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan
