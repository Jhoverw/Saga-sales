<#
.SYNOPSIS
    Rebuilds only what changed since the last refresh and restarts the gateway.

.DESCRIPTION
    A full "up -d --build" rebuilds nine images for a one-line change. This script looks at the
    commits since the last time it ran, maps the changed files to services, rebuilds just those,
    restarts the gateway so YARP re-resolves their new addresses, and checks the stack is alive.

    It never touches the database: applying SQL scripts stays the job of apply-db.ps1 (run by
    up.ps1). Volumes are never removed.

    Rules:
      - src/SagaSales.<X>/          -> rebuild service <x>
      - src/SagaSales.Shared/       -> rebuild every service (shared by all)
      - src/SagaSales.BuildingBlocks/ -> rebuild every service
      - db/, scripts/, docs, specs   -> no rebuild needed

.PARAMETER All
    Rebuild every image, ignoring what changed.

.PARAMETER Service
    Rebuild only the given service(s), for example -Service identity.

.EXAMPLE
    ./scripts/refresh.ps1

.EXAMPLE
    ./scripts/refresh.ps1 -All

.EXAMPLE
    ./scripts/refresh.ps1 -Service catalog
#>
[CmdletBinding()]
param(
    [switch]$All,
    [string[]]$Service
)

$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $repoRoot 'deploy\docker-compose.yml'
$marker = Join-Path $repoRoot 'deploy\.last-refresh'
$composeArgs = @('-f', $compose)
$dockerQuiet = { param($line) $line -notmatch 'level=warning' }

$appServices = @(
    'identity', 'catalog', 'sales', 'payment', 'customer', 'analytics', 'notification',
    'api-gateway', 'blazor-web'
)

function Get-HeadCommit {
    $value = & git -C $repoRoot rev-parse HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return $value.Trim()
}

function Resolve-ServiceName([string]$project) {
    switch ($project) {
        'BlazorWeb' { return 'blazor-web' }
        'ApiGateway' { return 'api-gateway' }
        default { return $project.ToLowerInvariant() }
    }
}

# ── what changed? ───────────────────────────────────────────────────────────
$head = Get-HeadCommit
$targets = @()

if ($Service) {
    $targets = $Service
}
elseif ($All) {
    $targets = $appServices
}
elseif (-not $head) {
    Write-Host '  Sin git no puedo saber que cambio: reconstruyo todo.' -ForegroundColor Yellow
    $targets = $appServices
}
else {
    $from = if (Test-Path $marker) { (Get-Content $marker -Raw).Trim() } else { $null }

    if (-not $from) {
        Write-Host '  Primera ejecucion de refresh: reconstruyo todo.' -ForegroundColor Yellow
        $targets = $appServices
    }
    elseif ($from -eq $head) {
        Write-Host '  No hay commits nuevos desde el ultimo refresh.' -ForegroundColor DarkGray
        Write-Host '  Si cambiaste archivos sin commitear, usa -All o -Service <nombre>.' -ForegroundColor DarkGray
        exit 0
    }
    else {
        $changed = & git -C $repoRoot diff --name-only $from $head 2>$null

        if ($changed | Where-Object { $_ -match '^src/SagaSales\.(Shared|BuildingBlocks)/' }) {
            Write-Host '  Cambio Shared o BuildingBlocks: afecta a todos los servicios.' -ForegroundColor Yellow
            $targets = $appServices
        }
        else {
            foreach ($file in $changed) {
                if ($file -match '^src/SagaSales\.([A-Za-z]+)/') {
                    $targets += Resolve-ServiceName $Matches[1]
                }
            }
        }

        $targets = @($targets | Where-Object { $_ } | Select-Object -Unique)
    }
}

if (-not $targets -or $targets.Count -eq 0) {
    Write-Host '  Nada que reconstruir: los cambios no afectan a ningun servicio.' -ForegroundColor Green
    Write-Host '  (si cambiaste SQL en db/, aplicalo con: ./scripts/apply-db.ps1 -UseContainer)'
    exit 0
}

Write-Host ''
Write-Host ("Reconstruyendo: " + ($targets -join ', ')) -ForegroundColor Cyan

& docker compose @composeArgs up -d --build @targets 2>&1 | Where-Object $dockerQuiet

if ($LASTEXITCODE -ne 0) {
    Write-Host '  Fallo la construccion. Revisa la salida de arriba.' -ForegroundColor Red
    exit 1
}

# ── the gateway caches the old addresses ────────────────────────────────────
Write-Host 'Reiniciando el gateway (IPs nuevas)...' -ForegroundColor DarkGray
& docker compose @composeArgs restart api-gateway | Out-Null
Start-Sleep -Seconds 3

# ── is it alive? ────────────────────────────────────────────────────────────
Write-Host ''
$checks = [ordered]@{
    'gateway ' = 'http://localhost:5000/health'
    'identity' = 'http://localhost:5001/health'
    'catalog ' = 'http://localhost:5002/health'
    'panel   ' = 'http://localhost:5008/'
}

$bad = 0
foreach ($name in $checks.Keys) {
    $code = $null
    try { $code = [int](Invoke-WebRequest $checks[$name] -UseBasicParsing -TimeoutSec 8).StatusCode }
    catch {
        $response = $_.Exception.Response
        if ($null -ne $response) { $code = [int]$response.StatusCode }
    }

    if ($null -eq $code) { Write-Host ("  {0} sin respuesta" -f $name) -ForegroundColor Red; $bad++ }
    elseif ($code -lt 300) { Write-Host ("  {0} HTTP {1}" -f $name, $code) -ForegroundColor Green }
    else { Write-Host ("  {0} HTTP {1}" -f $name, $code) -ForegroundColor Yellow; $bad++ }
}

if ($head -and -not $Service) {
    Set-Content -Path $marker -Value $head -Encoding utf8
}

Write-Host ''
if ($bad -eq 0) {
    Write-Host 'Listo. Recarga el panel con Ctrl+F5.' -ForegroundColor Green
    exit 0
}

Write-Host 'Algo no responde. Logs: docker compose -f deploy/docker-compose.yml logs --tail=30 <servicio>' -ForegroundColor Yellow
exit 1
