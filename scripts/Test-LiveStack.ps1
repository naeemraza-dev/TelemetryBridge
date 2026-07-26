[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$required = @(
    "http://localhost:8080/health",
    "http://localhost:8081/health",
    "http://localhost:8082/health",
    "http://localhost:8083/health",
    "http://localhost:8084/health",
    "http://localhost:3200/ready"
)

foreach ($uri in $required) {
    $healthy = $false
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $uri -TimeoutSec 10 | Out-Null
            $healthy = $true
            break
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }
    if (-not $healthy) {
        throw "Health check failed after 60 seconds: $uri"
    }
}

$env:TELEMETRYBRIDGE_E2E = "1"
dotnet test tests/TelemetryBridge.EndToEndTests --no-restore -nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "The live connected-trace or NLog-correlation assertion failed."
}

Write-Host "Live trace and direct-NLog correlation assertions passed."
