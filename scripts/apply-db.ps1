<#
.SYNOPSIS
    Applies the versioned T-SQL scripts of every service to its own SQL Server database.

.DESCRIPTION
    There are no migrations: the schema lives in db/<service>/V###__*.sql and this script applies
    them in version order, recording each one in the service's dbo.schema_version table.

    Re-running is a no-op: a script whose version is already recorded is skipped. A script that
    fails aborts the run (sqlcmd -b) and is NOT recorded, so the next run retries it.

    The database is created when it does not exist, so a fresh SQL Server needs no preparation.

.EXAMPLE
    ./scripts/apply-db.ps1 -Server localhost,1433 -User sa -Password "Your_password123"

.EXAMPLE
    ./scripts/apply-db.ps1 -Server localhost -Service catalog -DryRun
#>
[CmdletBinding()]
param(
    [string]$Server = 'localhost,1433',
    [string]$User = 'sa',
    [string]$Password = $env:MSSQL_SA_PASSWORD,
    [string]$Service,
    [string]$DatabasePrefix = 'saga_',
    [switch]$TrustServerCertificate = $true,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dbRoot   = Join-Path $repoRoot 'db'

if (-not (Test-Path $dbRoot)) { throw "No db/ directory found at $dbRoot" }
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'sqlcmd is not on PATH. Install the SQL Server command-line tools (sqlcmd / go-sqlcmd).'
}
if ([string]::IsNullOrWhiteSpace($Password)) {
    throw 'No password supplied. Pass -Password or set MSSQL_SA_PASSWORD.'
}

function Invoke-Sql {
    param([string]$Database, [string]$Query)
    $arguments = @('-S', $Server, '-U', $User, '-P', $Password, '-d', $Database, '-b', '-h', '-1', '-W', '-Q', $Query)
    if ($TrustServerCertificate) { $arguments += @('-C') }
    $result = & sqlcmd @arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($result -join [Environment]::NewLine) }
    return ($result -join "`n").Trim()
}

function Invoke-SqlFile {
    param([string]$Database, [string]$Path)
    $arguments = @('-S', $Server, '-U', $User, '-P', $Password, '-d', $Database, '-b', '-i', $Path)
    if ($TrustServerCertificate) { $arguments += @('-C') }
    & sqlcmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "Script failed: $Path" }
}

$services = if ($Service) { @($Service) } else {
    Get-ChildItem $dbRoot -Directory | Select-Object -ExpandProperty Name | Sort-Object
}

$applied = 0
$skipped = 0
$failed  = @()

foreach ($svc in $services) {
    $dir = Join-Path $dbRoot $svc
    if (-not (Test-Path $dir)) { throw "Unknown service '$svc' (no folder db/$svc)" }

    $database = "$DatabasePrefix$svc"
    $scripts = Get-ChildItem $dir -Filter 'V*.sql' -File |
        Where-Object { $_.Name -match '^V(\d{3,})__' } |
        Sort-Object { [int]([regex]::Match($_.Name, '^V(\d{3,})__').Groups[1].Value) }

    Write-Host "$svc -> $database" -ForegroundColor Cyan

    if (-not $scripts) {
        Write-Host '  no scripts' -ForegroundColor DarkGray
        continue
    }

    foreach ($script in $scripts) {
        $version = [int]([regex]::Match($script.Name, '^V(\d{3,})__').Groups[1].Value)

        if ($DryRun) {
            Write-Host ("  [dry] V{0:000} {1}" -f $version, $script.Name) -ForegroundColor DarkYellow
            continue
        }

        try {
            # Create the database the first time this service is applied.
            $exists = Invoke-Sql -Database 'master' -Query "SET NOCOUNT ON; SELECT CASE WHEN DB_ID('$database') IS NULL THEN 0 ELSE 1 END"
            if ($exists -ne '1') {
                Invoke-Sql -Database 'master' -Query "CREATE DATABASE [$database]" | Out-Null
                Write-Host "  created $database" -ForegroundColor DarkCyan
            }

            $already = Invoke-Sql -Database $database -Query "SET NOCOUNT ON; IF OBJECT_ID('dbo.schema_version') IS NOT NULL SELECT 1 FROM dbo.schema_version WHERE version = $version"
            if ($already -eq '1') {
                Write-Host ("  skip V{0:000} {1}" -f $version, $script.Name) -ForegroundColor DarkGray
                $skipped++
                continue
            }

            Invoke-SqlFile -Database $database -Path $script.FullName

            Invoke-Sql -Database $database -Query "SET NOCOUNT ON; INSERT INTO dbo.schema_version (version, script) VALUES ($version, N'$($script.Name)')" | Out-Null

            Write-Host ("  ok   V{0:000} {1}" -f $version, $script.Name) -ForegroundColor Green
            $applied++
        } catch {
            $failed += "$svc/$($script.Name)"
            Write-Host ("  FAIL V{0:000} {1}" -f $version, $script.Name) -ForegroundColor Red
            Write-Host ("       " + $_.Exception.Message.Split([Environment]::NewLine)[0]) -ForegroundColor DarkRed
            break
        }
    }
}

Write-Host ''
Write-Host "applied=$applied skipped=$skipped failed=$($failed.Count)"

if ($failed.Count -gt 0) {
    $failed | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}
