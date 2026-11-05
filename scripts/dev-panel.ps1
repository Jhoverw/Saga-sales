<#
.SYNOPSIS
    Runs the panel on the host, so a change is visible in seconds instead of in a rebuilt image.

.DESCRIPTION
    The panel is a static WebAssembly app: it does not need a container to be looked at. Building an
    image for every change means waiting for restore + publish + image export, and Docker's layer
    cache can even serve an old build while looking successful. That is what kept happening.

    This script stops the container (so the port is free), runs the panel with the .NET dev server,
    and leaves the API path untouched: the panel still talks to the gateway on :5000, which keeps
    running in Docker with SQL Server and RabbitMQ.

    While it runs, stop it with Ctrl+C and start it again; there is no image to rebuild and no
    browser cache to fight, because the dev server always serves the current files.

.PARAMETER Port
    Port for the panel. 5008 by default, the one the gateway already allows for CORS.

.PARAMETER SkipBuild
    Start without a previous compile check (faster when the code is known to compile).

.EXAMPLE
    ./scripts/dev-panel.ps1

.EXAMPLE
    ./scripts/dev-panel.ps1 -Port 5009
#>
[CmdletBinding()]
param(
    [int]$Port = 5008,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\SagaSales.BlazorWeb\SagaSales.BlazorWeb.csproj'

if (-not (Test-Path $project)) {
    Write-Host "No encuentro el proyecto del panel en $project" -ForegroundColor Red
    exit 1
}

# ── 1. el contenedor del panel ocupa el puerto: se para (no se borra) ────────
Write-Host "== Parando el contenedor del panel (si esta corriendo)" -ForegroundColor Cyan
& docker stop saga-sales-blazor-web 2>&1 | Where-Object { $_ -notmatch 'level=warning' }
Write-Host '   El resto del stack sigue en Docker: SQL Server, RabbitMQ, gateway, servicios.' -ForegroundColor DarkGray

# ── 2. comprobacion de compilacion: el error sale aqui, no en un navegador ──
if (-not $SkipBuild) {
    Write-Host ''
    Write-Host '== Compilando el panel' -ForegroundColor Cyan
    & dotnet build $project -v quiet --nologo 2>&1 | Select-String -Pattern 'error|Compilaci|Build succeeded|Compilación correcta'

    if ($LASTEXITCODE -ne 0) {
        Write-Host ''
        Write-Host 'El panel no compila. Arregla esos errores antes de abrirlo.' -ForegroundColor Red
        exit 1
    }
}

# ── 3. en marcha ────────────────────────────────────────────────────────────
Write-Host ''
Write-Host ("== Sirviendo el panel en http://localhost:" + $Port) -ForegroundColor Green
Write-Host '   Ctrl+C para parar. Al volver a ejecutar este script, el cambio ya esta dentro.' -ForegroundColor DarkGray
Write-Host ''

$env:ASPNETCORE_URLS = "http://localhost:$Port"

Push-Location (Join-Path $repoRoot 'src\SagaSales.BlazorWeb')
try {
    & dotnet run --no-build --project $project
}
finally {
    Pop-Location
}
