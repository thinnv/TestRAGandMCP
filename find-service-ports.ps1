# Find service ports from netstat
Write-Host "Finding active service ports..." -ForegroundColor Cyan

$ports = netstat -ano | Select-String "LISTENING" | Select-String "TCP"

Write-Host "`nAll listening TCP ports:" -ForegroundColor Yellow
$ports | ForEach-Object {
    if ($_ -match ":(\d+)\s") {
        $port = $matches[1]
        if ($port -ge 5000 -and $port -le 8000) {
            Write-Host "  Port: $port" -ForegroundColor White
        }
    }
}

Write-Host "`nAspire Dashboard: https://localhost:17139" -ForegroundColor Green
Write-Host "Check the dashboard to see the actual ports for each service." -ForegroundColor Yellow
