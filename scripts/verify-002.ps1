<#
.SYNOPSIS
    Verifies feature 002 (Caja: cliente y detalle) end to end and leaves a log Claude can read.

.DESCRIPTION
    Runs, in order, and stops at the first failure:
      1. git push of the feature branch (skip with -SkipPush),
      2. dotnet build of the solution (fails fast on compile errors),
      3. ./scripts/up.ps1 -Rebuild (rebuilds images, starts the stack, applies the dev schema),
      4. ./scripts/apply-db.ps1 -UseContainer -DatabaseSuffix '_test' (schema for integration tests),
      5. ./scripts/test.ps1 (unit, contract and integration suites).

    Everything is written to deploy\verify-002.log (ignored by git through *.log).

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-002.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipPush
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot
$log = Join-Path $repoRoot 'deploy\verify-002.log'

Set-Content -Path $log -Value ("verify-002 started " + (Get-Date -Format 's')) -Encoding UTF8

function Write-Log {
    param([string]$Text, [string]$Color = 'Gray')
    Write-Host $Text -ForegroundColor $Color
    Add-Content -Path $log -Value $Text -Encoding UTF8
}

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)

    Write-Log ''
    Write-Log "== $Name" 'Cyan'
    $global:LASTEXITCODE = 0

    & $Action 2>&1 | ForEach-Object {
        $line = "$_"
        Write-Host $line
        Add-Content -Path $log -Value $line -Encoding UTF8
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Log "FALLO: $Name (exit $LASTEXITCODE)" 'Red'
        Write-Log "Log completo: $log" 'Yellow'
        Pop-Location
        exit 1
    }

    Write-Log "OK: $Name" 'Green'
}

Push-Location $repoRoot

if (-not $SkipPush) {
    Invoke-Step 'git push' { git push -u origin 002-caja-cliente-detalle }
}

Invoke-Step 'dotnet build' { dotnet build SagaSales.sln -c Debug --nologo }
Invoke-Step 'up.ps1 -Rebuild (stack + esquema dev)' { & (Join-Path $PSScriptRoot 'up.ps1') -Rebuild }
Invoke-Step "apply-db.ps1 _test" { & (Join-Path $PSScriptRoot 'apply-db.ps1') -UseContainer -DatabaseSuffix '_test' }
Invoke-Step 'test.ps1' { & (Join-Path $PSScriptRoot 'test.ps1') }

Write-Log ''
Write-Log 'VERIFICACIÓN 002: TODO OK. Prueba Caja a mano: consumidor final, buscar cliente y registrar uno nuevo.' 'Green'
Pop-Location
exit 0
