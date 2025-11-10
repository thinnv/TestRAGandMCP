# Test MCP Endpoint
Write-Host "Testing MCP Upload Service Connection..." -ForegroundColor Cyan

# Disable certificate validation
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

$request = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/list"
} | ConvertTo-Json

Write-Host "`nTrying HTTPS (port 61034)..."
try {
    $response = Invoke-RestMethod -Uri "https://localhost:61034/mcp" -Method POST -Body $request -ContentType "application/json" -TimeoutSec 5
    Write-Host "SUCCESS!" -ForegroundColor Green
    Write-Host "`nAvailable MCP Tools:" -ForegroundColor Yellow
    $response.result.tools | ForEach-Object {
        Write-Host "  - $($_.name): $($_.description.Substring(0, [Math]::Min(60, $_.description.Length)))" -ForegroundColor White
    }
} catch {
    Write-Host "HTTPS Failed: $($_.Exception.Message)" -ForegroundColor Red
    
    Write-Host "`nTrying HTTP (port 61033)..."
    try {
        $response2 = Invoke-RestMethod -Uri "http://localhost:61033/mcp" -Method POST -Body $request -ContentType "application/json" -TimeoutSec 5
        Write-Host "HTTP SUCCESS!" -ForegroundColor Green
        $response2.result.tools | Select-Object -First 5 name
    } catch {
        Write-Host "HTTP also failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}
