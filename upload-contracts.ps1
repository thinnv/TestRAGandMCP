# Upload Sample Contracts to DocumentUpload Service via MCP
# This script reads all contract files and uploads them using the MCP endpoint

$contractsFolder = "d:\Code\2025\MCP\MCP_forUpload\sampleContracts"
$mcpEndpoint = "https://localhost:7107/mcp"

# Get all contract files
$contractFiles = Get-ChildItem -Path $contractsFolder -Filter "*.txt"

Write-Host "Found $($contractFiles.Count) contract files to upload" -ForegroundColor Green
Write-Host ""

$uploadResults = @()

foreach ($file in $contractFiles) {
    Write-Host "Processing: $($file.Name)" -ForegroundColor Cyan
    
    # Read file content and convert to Base64
    $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $base64Content = [System.Convert]::ToBase64String($fileBytes)
    
    # Prepare MCP request for UploadDocument tool
    $mcpRequest = @{
        jsonrpc = "2.0"
        id = 1
        method = "tools/call"
        params = @{
            name = "UploadDocument"
            arguments = @{
                filename = $file.Name
                contentType = "text/plain"
                content = $base64Content
                uploadedBy = "mcp-demo"
            }
        }
    } | ConvertTo-Json -Depth 10
    
    try {
        # Send request to MCP endpoint (skip SSL validation for localhost)
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        $response = Invoke-RestMethod -Uri $mcpEndpoint -Method Post -Body $mcpRequest -ContentType "application/json"
        
        if ($response.result.content -and $response.result.content.Count -gt 0) {
            $resultText = $response.result.content[0].text
            $resultData = $resultText | ConvertFrom-Json
            
            if ($resultData.success) {
                Write-Host "  SUCCESS - Document ID: $($resultData.documentId)" -ForegroundColor Green
                $uploadResults += [PSCustomObject]@{
                    FileName = $file.Name
                    DocumentId = $resultData.documentId
                    Size = $resultData.size
                    Status = "Success"
                }
            } else {
                Write-Host "  FAILED: $($resultData.error)" -ForegroundColor Red
                $uploadResults += [PSCustomObject]@{
                    FileName = $file.Name
                    DocumentId = $null
                    Size = $file.Length
                    Status = "Failed"
                }
            }
        } else {
            Write-Host "  ERROR: Unexpected response format" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "  ERROR uploading: $($_.Exception.Message)" -ForegroundColor Red
        $uploadResults += [PSCustomObject]@{
            FileName = $file.Name
            DocumentId = $null
            Size = $file.Length
            Status = "Error"
        }
    }
    
    Write-Host ""
    Start-Sleep -Milliseconds 500
}

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Upload Summary" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
$uploadResults | Format-Table -AutoSize
Write-Host ""

$successCount = ($uploadResults | Where-Object { $_.Status -eq "Success" }).Count
$failedCount = $uploadResults.Count - $successCount
$summaryColor = if ($successCount -eq $uploadResults.Count) { "Green" } else { "Yellow" }
Write-Host "Total: $($uploadResults.Count) files, Success: $successCount, Failed: $failedCount" -ForegroundColor $summaryColor
