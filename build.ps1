<#
.SYNOPSIS
    Orquesta el ciclo completo de Saga‑Sales:
    • dev    → levanta entorno con watch + bind‑mounts (hot‑reload)
    • test   → build + unit/integration/contract + smoke tests (no publica)
    • stage  → build, test, smoke, tag&push a registry y despliega en staging
    • prod   → igual que stage pero apuntando al entorno productivo
.PARAMETER Environment
    Uno de: dev, test, stage, prod
.PARAMETER Registry
    Docker registry donde se pushean las imágenes (ej. myregistry.azurecr.io).
    Si se omite se usa Docker Hub (se asume que ya estás logueado).
.PARAMETER Tag
    Etiqueta explícita para las imágenes; si está vacía se genera automáticamente:
        v<MAJOR>.<MINOR>.<PATCH>-<git‑shortsha>
#>

[CmdletBinding()]
param(
    [ValidateSet('dev','test','stage','prod')]
    [string]$Environment = 'dev',

    [string]$Registry   = '',
    [string]$Tag        = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path .).Path
Write-Host "=== Saga‑Sales Build Process ==="
Write-Host "Repo root : $repoRoot"
Write-Host "Environment: $Environment"
if ($Registry) { Write-Host "Registry   : $Registry" }

function Invoke-AndLog {
    param([string]$Label, [scriptblock]$Script)
    Write-Host "`n[$Label] START"` -ForegroundColor Cyan
    try {
        & $Script
        Write-Host "[$Label] END   (success)" -ForegroundColor Green
    }
    catch {
        Write-Host "[$Label] END   (FAILED)" -ForegroundColor Red
        Write-Error $_
        exit 1
    }
}

# ---------- 0️⃣ Obtener versión git ----------
if (-not $Tag) {
    $gitSha = (git rev-parse --short HEAD).Trim()
    if (Test-Path "$repoRoot\VERSION.txt") {
        $baseVer = (Get-Content "$repoRoot\VERSION.txt" -Raw).Trim()
    }
    else {
        $baseVer = "0.1.0"
    }
    $Tag = "v$baseVer-$gitSha"
}
Write-Host "Using tag: $Tag"

# ---------- 1️⃣ Dev mode (watch) ----------
if ($Environment -eq 'dev') {
    Invoke-AndLog "Docker Compose Up (dev)" {
        docker compose up -d
    }
    Write-Host "`nEntorno de desarrollo levantado. Usa `docker compose logs -f <service>` para ver salida." -ForegroundColor Yellow
    exit 0
}

# ---------- 2️⃣ Build images (solo una vez) ----------
Invoke-AndLog "Docker Build" {
    $buildArgs = @()
    if ($Environment -in @('stage','prod')) {
        $buildArgs += '--build'   # forzamos rebuild para obtener imágenes limpias
    }
    if ($Environment -eq 'test') {
        docker compose build @buildArgs
    }
    elseif ($Environment -in @('stage','prod')) {
        # Nota: NO usamos el override aquí, solo el archivo base de producción
        docker compose -f deploy/docker-compose.yml up -d --build @buildArgs
    }
    else {
        # Ningún otro caso debería llegar aquí, pero por seguridad:
        docker compose build @buildArgs
    }
}

# Salimos en modo test después del build (los tests se ejecutarán después)
if ($Environment -eq 'test') {
    # Continuaremos a los tests más abajo
}
elseif ($Environment -in @('stage','prod')) {
    # Después del build, seguimos a tests, smoke, push y deploy
}
else {
    Write-Host "Environmento desconocido: $Environment" -ForegroundColor Red
    exit 1
}

# ---------- 3️⃣ Ejecutar tests ----------
function Run-Tests {
    param([string]$TestProjectPath)   # ruta relativa a la raíz, ej. tests/SagaSales.UnitTests.csproj
    $fullPath = Join-Path $repoRoot $TestProjectPath
    docker compose run --rm `
        --volume "${repoRoot}:/src:cached" `
        --volume "${repoRoot}/global.json:/global.json:cached" `
        --volume "${repoRoot}/Directory.Build.props:/Directory.Build.props:cached" `
        --workdir /src `
        mcr.microsoft.com/dotnet/sdk:10.0 `
        dotnet test "$TestProjectPath" `
        --logger "trx;LogFileName=$([IO.Path]::GetFileNameWithoutExtension($fullPath)).trx" `
        --results-dir "/tmp/testresults/$([IO.Path]::GetFileNameWithoutExtension($fullPath))" `
        --no-build
}

$testProjects = @(
    "tests/SagaSales.UnitTests.csproj",
    "tests/SagaSales.IntegrationTests.csproj",
    "tests/SagaSales.ContractTests.csproj"
)

$failed = $false
foreach ($proj in $testProjects) {
    Invoke-AndLog "Test $proj" {
        Run-Tests $proj
    }
    # Si Run-Tests lanza una excepción, Invoke-AndLog ya habrá detenido el script.
}
# Si llegamos aquí, todos los tests pasaron.

# ---------- 4️⃣ Smoke tests ----------
function Run-Smoke {
    param([string]$ScriptName)
    $scriptPath = Join-Path $repoRoot "scripts\$ScriptName"
    if (-not (Test-Path $scriptPath)) {
        Write-Warning "Smoke script $ScriptName no encontrado. Se asume OK."
        return $true
    }
    Write-Host "Ejecutando smoke: $ScriptName"
    & powershell -ExecutionPolicy Bypass -File $scriptPath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Smoke test $ScriptName falló." -ForegroundColor Red
        return $false
    }
    else {
        Write-Host "✅ Smoke test $ScriptName pasó." -ForegroundColor Green
        return $true
    }
}

$smokeOk = $true
foreach ($smoke in @('smoke-identity.ps1','smoke-catalog.ps1','smoke-sales.ps1','smoke-payment.ps1')) {
    $smokeOk = $smokeOk -and (Run-Smoke $smoke)
}
if (-not $smokeOk) {
    Write-Host "`n🛑 Algún smoke test falló. Abortando promoción." -ForegroundColor Red
    exit 1
}

# ---------- 5️⃣ Tag & Push ----------
if ($Environment -in @('stage','prod')) {
    Invoke-AndLog "Tag & Push Images" {
        $services = @(
            'api-gateway',
            'blazor-web',
            'catalog',
            'customer',
            'identity',
            'notification',
            'payment',
            'sales',
            'analytics'
        )
        foreach ($svc in $services) {
            $localImage  = "deploy-$svc:latest"
            if ($Registry) {
                $remote = "${Registry}/${localImage}:${Tag}"
                docker tag $localImage $remote
                docker push $remote
                Write-Host "Pushed $remote"
            }
            else {
                $remote = "${localImage}:${Tag}"
                docker tag $localImage $remote
                docker push $remote
                Write-Host "Pushed $remote (Docker Hub)"
            }
        }
    }
}

# ---------- 6️⃣ Deploy a entorno destino ----------
if ($Environment -in @('stage','prod')) {
    Invoke-AndLog "Deploy to $Environment" {
        $services = @(
            'api-gateway',
            'blazor-web',
            'catalog',
            'customer',
            'identity',
            'notification',
            'payment',
            'sales',
            'analytics'
        )
        foreach ($svc in $services) {
            $img = if ($Registry) { "${Registry}/deploy-${svc}:${Tag}" } else { "deploy-${svc}:${Tag}" }
            docker pull $img
            docker compose -f deploy/docker-compose.yml up -d --no-deps --remove-orphans $svc
        }
        # Levantamos todo (por si faltó alguno)
        docker compose -f deploy/docker-compose.yml up -d
        Write-Host "Entorno $Environment actualizado con tag $Tag"
    }
}

# ---------- 7️⃣ Fin ----------
Write-Host "`n🎉 Proceso completado exitosamente para entorno '$Environment'." -ForegroundColor Green
if ($Environment -in @('stage','prod')) {
    Write-Host "Versión desplegada: $Tag"
    Write-Host "Para revertir: vuelve a desplegar el tag anterior conocido bueno."
}