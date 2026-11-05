<#
.SYNOPSIS
    Starts the whole Saga-Sales stack and leaves it verified.

.DESCRIPTION
    One command instead of six. It:
      1. checks that Docker is running,
      2. builds and starts the containers,
      3. waits for SQL Server to report healthy,
      4. applies the versioned SQL scripts (no host sqlcmd needed),
      5. restarts the gateway so YARP re-resolves the service addresses,
      6. checks every health endpoint and prints the URLs.

    Step 5 matters: recreating a service gives it a new IP, and the gateway keeps its pooled
    connections to the old one, which shows up as HTTP 502.

.PARAMETER Rebuild
    Rebuild every image before starting.

.PARAMETER Service
    Rebuild a single service (for example "identity") and restart the gateway afterwards.

.PARAMETER SkipDb
    Do not apply the SQL scripts (use when the schema is already in place).

.EXAMPLE
    ./scripts/up.ps1

.EXAMPLE
    ./scripts/up.ps1 -Rebuild

.EXAMPLE
    ./scripts/up.ps1 -Service identity
#>
[CmdletBinding()]
param(
    [switch]$Rebuild,
    [string]$Service,
    [switch]$SkipDb
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $repoRoot 'deploy\docker-compose.yml'
$composeArgs = @('-f', $compose)

function Write-Step([string]$Text) {
    Write-Host ''
    Write-Host "== $Text" -ForegroundColor Cyan
}

# ── 1. Docker ───────────────────────────────────────────────────────────────
Write-Step 'Docker'
$engine = & docker info --format '{{.ServerVersion}}' 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($engine)) {
    Write-Host '  El motor de Docker no responde. Abre Docker Desktop y espera a "Engine running".' -ForegroundColor Red
    exit 1
}
Write-Host "  motor $engine" -ForegroundColor DarkGray

# ── 2. contenedores ─────────────────────────────────────────────────────────
Write-Step 'Construyendo y arrancando'

if ($Service) {
    Write-Host "  reconstruyo solo $Service y reinicio el gateway" -ForegroundColor Yellow
    & docker compose @composeArgs up -d --build $Service
}
elseif ($Rebuild) {
    & docker compose @composeArgs up -d --build
}
else {
    & docker compose @composeArgs up -d
}

if ($LASTEXITCODE -ne 0) {
    Write-Host '  Fallo docker compose. Revisa la salida de arriba.' -ForegroundColor Red
    exit 1
}

# ── 3. esperar a SQL Server ─────────────────────────────────────────────────
Write-Step 'Esperando a SQL Server'
$deadline = (Get-Date).AddSeconds(120)
$healthy = $false

while ((Get-Date) -lt $deadline) {
    $state = & docker inspect -f '{{.State.Health.Status}}' saga-sales-sqlserver 2>$null
    if ($state -eq 'healthy') { $healthy = $true; break }
    Start-Sleep -Seconds 3
}

if (-not $healthy) {
    Write-Host '  SQL Server no llego a healthy en 120 s. Revisa: docker logs saga-sales-sqlserver' -ForegroundColor Red
    exit 1
}
Write-Host '  healthy' -ForegroundColor DarkGray

# ── 4. esquema ──────────────────────────────────────────────────────────────
if (-not $SkipDb) {
    Write-Step 'Aplicando el esquema'
    & (Join-Path $PSScriptRoot 'apply-db.ps1') -UseContainer
    if ($LASTEXITCODE -ne 0) {
        Write-Host '  El esquema fallo. Arreglalo antes de probar la API.' -ForegroundColor Red
        exit 1
    }
}

# ── 5. reiniciar el gateway ─────────────────────────────────────────────────
# Recreating any service changes its IP; the gateway keeps the old pooled connection → 502.
Write-Step 'Reiniciando el gateway (IPs nuevas)'
& docker compose @composeArgs restart api-gateway | Out-Null
Start-Sleep -Seconds 3
Write-Host '  hecho' -ForegroundColor DarkGray

# ── 6. comprobaciones ───────────────────────────────────────────────────────
Write-Step 'Comprobando'

$endpoints = [ordered]@{
    'gateway   ' = 'http://localhost:5000/health'
    'identity  ' = 'http://localhost:5001/health'
    'catalog   ' = 'http://localhost:5002/health'
    'sales     ' = 'http://localhost:5003/health'
    'payment   ' = 'http://localhost:5004/health'
    'customer  ' = 'http://localhost:5005/health'
    'analytics ' = 'http://localhost:5006/health'
    'notifica. ' = 'http://localhost:5007/health'
    'panel     ' = 'http://localhost:5008/'
}

# A service whose database has no schema yet answers 503 on /health. That is expected until its
# story is implemented, so it is reported as pending instead of as a failure.
$pending = @('payment', 'customer', 'analytics', 'notifica.')

$failures = 0
foreach ($name in $endpoints.Keys) {
    $code = $null

    try {
        $code = [int](Invoke-WebRequest $endpoints[$name] -UseBasicParsing -TimeoutSec 10).StatusCode
    }
    catch {
        # Invoke-WebRequest throws on 4xx/5xx: the status code is in the response, not in the message.
        $response = $_.Exception.Response
        if ($null -ne $response) { $code = [int]$response.StatusCode }
    }

    if ($null -eq $code) {
        Write-Host ("  {0} sin respuesta ({1})" -f $name, $endpoints[$name]) -ForegroundColor Red
        $failures++
    }
    elseif ($code -ge 200 -and $code -lt 300) {
        Write-Host ("  {0} HTTP {1}" -f $name, $code) -ForegroundColor Green
    }
    elseif ($code -eq 503 -and $pending -contains $name.Trim()) {
        Write-Host ("  {0} HTTP 503  (sin esquema todavia: historia pendiente)" -f $name) -ForegroundColor DarkYellow
    }
    else {
        Write-Host ("  {0} HTTP {1}" -f $name, $code) -ForegroundColor Yellow
        $failures++
    }
}

# ── 7. resumen ──────────────────────────────────────────────────────────────
Write-Step 'Listo'

if ($failures -eq 0) {
    Write-Host '  Panel:   http://localhost:5008' -ForegroundColor Green
    Write-Host '  Gateway: http://localhost:5000' -ForegroundColor Green
    Write-Host '  Seq:     http://localhost:8081' -ForegroundColor Green
    Write-Host ''
    Write-Host '  Prueba de la historia 1:  ./scripts/smoke-identity.ps1 -SkipDb'
    exit 0
}

Write-Host ("  {0} endpoint(s) con problemas. Logs: docker compose -f deploy/docker-compose.yml logs --tail=30 <servicio>" -f $failures) -ForegroundColor Yellow
exit 1
