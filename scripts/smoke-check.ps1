Param(
    [string]$ApiBase = 'http://localhost:5000'
)

Write-Host "Checking backend health at $ApiBase/health"
try {
    $resp = Invoke-RestMethod -Uri "$ApiBase/health" -UseBasicParsing -TimeoutSec 5
    Write-Host "Health check returned:" $resp
} catch {
    Write-Host "Health check failed: $_" -ForegroundColor Red
    exit 2
}

Write-Host "Backend health OK. Checking sample map feed (bounds)..."

$body = @{ 
    Bounds = @{ MinLat = 47.59; MaxLat = 47.62; MinLng = -122.35; MaxLng = -122.32 }
    Window = @{ StartUtc = [DateTimeOffset]::UtcNow.AddDays(-1); EndUtc = [DateTimeOffset]::UtcNow.AddDays(7); Timezone = 'UTC' }
} | ConvertTo-Json -Depth 6

try {
    # Try the canonical API path under /api first
    $mapUrl = "$ApiBase/api/events/map"
    try {
        $feed = Invoke-RestMethod -Uri $mapUrl -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 5
        Write-Host "Map feed response contains" ($feed.Events.Length) "events (via $mapUrl)"
    } catch {
        # Fallback to legacy path without /api
        $mapUrl2 = "$ApiBase/events/map"
        $feed = Invoke-RestMethod -Uri $mapUrl2 -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 5
        Write-Host "Map feed response contains" ($feed.Events.Length) "events (via $mapUrl2)"
    }
} catch {
    Write-Host "Map feed check failed: $_" -ForegroundColor Red
    exit 3
}

Write-Host "Smoke checks passed." -ForegroundColor Green
