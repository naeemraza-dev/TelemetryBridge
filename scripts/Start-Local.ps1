[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $AdminKey,

    [Parameter(Mandatory)]
    [string] $OperatorKey
)

$ErrorActionPreference = "Stop"
if ($AdminKey.Length -lt 16 -or $OperatorKey.Length -lt 16 -or $AdminKey -eq $OperatorKey) {
    throw "Use different AdminKey and OperatorKey values of at least 16 characters."
}

$env:TELEMETRYBRIDGE_ADMIN_KEY = $AdminKey
$env:TELEMETRYBRIDGE_OPERATOR_KEY = $OperatorKey
$env:COMPOSE_PARALLEL_LIMIT = "1"

docker info --format "{{.ServerVersion}}" | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Docker Desktop is not ready."
}

docker compose up -d --build
if ($LASTEXITCODE -ne 0) {
    throw "Docker Compose failed. Run 'docker compose logs --tail 200' for details."
}

docker compose ps
Write-Host "Frontend: http://localhost:5173"
Write-Host "Grafana:  http://localhost:3000 (admin/admin; local only)"
Write-Host "OpenAPI:  http://localhost:8080/openapi/public-api.yaml"
