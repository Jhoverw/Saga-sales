<#
.SYNOPSIS
    Checks whether this machine can build and run Saga-Sales, and says what is missing.

.DESCRIPTION
    Read-only. Prints PASS/FAIL per requirement so a broken setup is diagnosed before spending
    time on a build that cannot work.

.EXAMPLE
    ./scripts/doctor.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]

function Check {
    param([string]$Name, [scriptblock]$Test, [string]$Hint)

    try {
        $result = & $Test
        if ($result) {
            Write-Host ("  PASS  {0}" -f $Name) -ForegroundColor Green
            return
        }
    } catch {
        # fall through to the failure branch
    }

    Write-Host ("  FAIL  {0}" -f $Name) -ForegroundColor Red
    if ($Hint) { Write-Host ("        {0}" -f $Hint) -ForegroundColor DarkYellow }
    $script:failures.Add($Name)
}

Write-Host 'Saga-Sales · comprobación del entorno' -ForegroundColor Cyan
Write-Host ''

Write-Host 'Toolchain' -ForegroundColor Cyan

Check -Name 'dotnet CLI disponible' -Hint 'Instala el SDK de .NET.' -Test {
    $null -ne (Get-Command dotnet -ErrorAction SilentlyContinue)
}

Check -Name 'el SDK resuelve para este repositorio (global.json)' -Hint (
    'global.json permite 10 o superior con rollForward latestMajor. ' +
    'Ejecuta "dotnet --version" en la raíz del repo y revisa el error.') -Test {
    $version = & dotnet --version 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }
    $major = [int]($version.Split('.')[0])
    Write-Host ("        SDK {0}" -f $version) -ForegroundColor DarkGray
    return $major -ge 10
}

Check -Name 'sqlcmd disponible' -Hint 'Instala las SQL Server command-line tools.' -Test {
    $null -ne (Get-Command sqlcmd -ErrorAction SilentlyContinue)
}

Write-Host ''
Write-Host 'Infraestructura' -ForegroundColor Cyan

Check -Name 'docker CLI disponible' -Hint 'Instala Docker Desktop.' -Test {
    $null -ne (Get-Command docker -ErrorAction SilentlyContinue)
}

Check -Name 'el daemon de Docker responde' -Hint 'Abre Docker Desktop y espera a que el motor arranque.' -Test {
    $server = & docker info --format '{{.ServerVersion}}' 2>$null
    return ($LASTEXITCODE -eq 0) -and -not [string]::IsNullOrWhiteSpace($server)
}

Check -Name 'docker compose disponible' -Hint 'Docker Desktop incluye el plugin compose.' -Test {
    & docker compose version 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

Check -Name 'puertos 1433/5672/5000/5001/5008 libres' -Hint (
    'Algún servicio está ocupando un puerto del stack. Para el proceso o cambia el mapeo en deploy/docker-compose.yml.') -Test {
    $busy = @(1433, 5672, 5000, 5001, 5008) | Where-Object {
        $null -ne (Get-NetTCPConnection -LocalPort $_ -State Listen -ErrorAction SilentlyContinue)
    }
    if ($busy) { Write-Host ("        ocupados: " + ($busy -join ', ')) -ForegroundColor DarkGray }
    return ($busy.Count -eq 0)
}

Write-Host ''
Write-Host 'Repositorio' -ForegroundColor Cyan

Check -Name 'la solución lista 14 proyectos' -Test {
    $count = (Select-String -Path (Join-Path $repoRoot 'SagaSales.sln') -Pattern '^Project\(').Count
    Write-Host ("        {0} proyectos" -f $count) -ForegroundColor DarkGray
    return $count -eq 14
}

Check -Name 'los scripts SQL de los 7 servicios están presentes' -Test {
    $dirs = Get-ChildItem (Join-Path $repoRoot 'db') -Directory -ErrorAction SilentlyContinue
    Write-Host ("        {0} carpetas en db/" -f $dirs.Count) -ForegroundColor DarkGray
    return $dirs.Count -eq 7
}

Check -Name 'hay al menos un esquema aplicable (V001__schema.sql)' -Hint (
    'Solo Identity tiene esquema hasta ahora: el resto llega con US2-US9.') -Test {
    $schemas = Get-ChildItem (Join-Path $repoRoot 'db') -Recurse -Filter 'V*__*.sql' -ErrorAction SilentlyContinue
    Write-Host ("        {0} scripts versionados" -f $schemas.Count) -ForegroundColor DarkGray
    return $schemas.Count -gt 0
}

Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'Todo listo. Siguiente paso:' -ForegroundColor Green
    Write-Host '  docker compose -f deploy/docker-compose.yml up -d sqlserver'
    Write-Host '  ./scripts/apply-db.ps1 -Server localhost,1433 -User sa -Password $env:MSSQL_SA_PASSWORD'
    Write-Host '  dotnet build SagaSales.sln'
    exit 0
}

Write-Host ("Faltan {0} requisito(s). Resuelve los FAIL y vuelve a ejecutar." -f $failures.Count) -ForegroundColor Red
exit 1
