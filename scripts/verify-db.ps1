<#
.SYNOPSIS
    Applies pending SQL scripts and verifies the database state, in one command.

.DESCRIPTION
    Runs every pending db/<service>/V###__*.sql, then checks the things that actually matter:
    which version each database reached, whether the inventory migration was lossless, whether the
    expected procedures exist, and whether the settings row is there.

    Prints a short PASS/FAIL report, so a single paste is enough to know the true state.

.PARAMETER Apply
    Apply pending scripts first. Without it, only verify.

.PARAMETER Password
    The sa password. Defaults to MSSQL_SA_PASSWORD, then to the compose default.

.EXAMPLE
    ./scripts/verify-db.ps1 -Apply

.EXAMPLE
    ./scripts/verify-db.ps1
#>
[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$Password = $(if ($env:MSSQL_SA_PASSWORD) { $env:MSSQL_SA_PASSWORD } else { 'SagaSales_Dev_2026!' }),
    [string]$Container = 'saga-sales-sqlserver'
)

$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dbRoot = Join-Path $repoRoot 'db'

function Invoke-Sql {
    param([string]$Database, [string]$Query)

    $result = & docker exec $Container /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P $Password -C -d $Database -b -I -h -1 -W -Q "SET NOCOUNT ON; $Query" 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw ($result -join ' ')
    }

    return (($result -join "`n").Trim())
}

$failures = New-Object System.Collections.Generic.List[string]

function Check {
    param([string]$Name, [bool]$Ok, [string]$Detail)

    if ($Ok) {
        Write-Host ("  PASS  {0,-42} {1}" -f $Name, $Detail) -ForegroundColor Green
        return
    }

    Write-Host ("  FAIL  {0,-42} {1}" -f $Name, $Detail) -ForegroundColor Red
    $script:failures.Add($Name)
}

# ── 1. aplicar si se pidio ──────────────────────────────────────────────────
if ($Apply) {
    Write-Host '== Aplicando scripts pendientes' -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'apply-db.ps1') -UseContainer -Password $Password
    if ($LASTEXITCODE -ne 0) {
        Write-Host '  El aplicador fallo. Ese error es el que hay que arreglar.' -ForegroundColor Red
        exit 1
    }
}

# ── 2. version alcanzada por cada base ──────────────────────────────────────
Write-Host ''
Write-Host '== Versiones de esquema' -ForegroundColor Cyan

$expected = @{}
foreach ($svc in Get-ChildItem $dbRoot -Directory) {
    $scripts = Get-ChildItem $svc.FullName -Filter 'V*.sql' -File |
        Where-Object { $_.Name -match '^V(\d{3,})__' }
    if ($scripts.Count -gt 0) {
        $expected[$svc.Name] = ($scripts | ForEach-Object {
            [int]([regex]::Match($_.Name, '^V(\d{3,})__').Groups[1].Value)
        } | Measure-Object -Maximum).Maximum
    }
}

foreach ($svc in $expected.Keys | Sort-Object) {
    $database = "saga_$svc"
    try {
        $row = Invoke-Sql -Database $database -Query "SELECT ISNULL(MAX(version), 0) FROM dbo.schema_version"
        $applied = if ($row) { [int]$row } else { 0 }
        $want = $expected[$svc]
        Check -Name "$database version" -Ok ($applied -eq $want) -Detail ("V{0:000} de V{1:000}" -f $applied, $want)
    }
    catch {
        Check -Name "$database version" -Ok $false -Detail ("sin schema_version: " + $_.Exception.Message)
    }
}

# ── 3. migracion de inventario (E-13): no se puede perder stock ─────────────
Write-Host ''
Write-Host '== Inventario' -ForegroundColor Cyan

try {
    $branches = [int](Invoke-Sql -Database 'saga_catalog' -Query "SELECT COUNT(*) FROM dbo.branches")
    Check -Name 'cada tienda tiene sucursal' -Ok ($branches -ge 1) -Detail "$branches sucursal(es)"

    $byBranch = [decimal](Invoke-Sql -Database 'saga_catalog' -Query "SELECT ISNULL(SUM(quantity), 0) FROM dbo.product_stock")
    $byProduct = [decimal](Invoke-Sql -Database 'saga_catalog' -Query "SELECT ISNULL(SUM(stock_quantity), 0) FROM dbo.products")
    Check -Name 'migracion sin perdida' -Ok ($byBranch -eq $byProduct) -Detail ("{0} en sucursales = {1} en productos" -f $byBranch, $byProduct)

    $settings = Invoke-Sql -Database 'saga_catalog' -Query "SELECT ISNULL(MAX(expiry_warning_days), 0) FROM dbo.tenant_settings"
    Check -Name 'parametros de vencimiento' -Ok ([int]$settings -gt 0) -Detail ("$settings dias")
}
catch {
    Check -Name 'inventario por sucursal' -Ok $false -Detail $_.Exception.Message
}

# ── 4. procedimientos clave ─────────────────────────────────────────────────
Write-Host ''
Write-Host '== Procedimientos' -ForegroundColor Cyan

$required = [ordered]@{
    'saga_identity' = @('sp_tenant_create', 'sp_user_find_by_email')
    'saga_catalog'  = @('sp_product_create', 'sp_stock_reserve', 'sp_price_resolve',
                        'sp_purchase_create', 'sp_purchase_receive',
                        'sp_branch_create', 'sp_settings_get', 'sp_stock_by_branch')
    'saga_sales'    = @('sp_order_create', 'sp_order_change_status')
}

foreach ($database in $required.Keys) {
    $names = ($required[$database] | ForEach-Object { "'$_'" }) -join ','

    try {
        $found = [int](Invoke-Sql -Database $database -Query "SELECT COUNT(*) FROM sys.procedures WHERE name IN ($names)")
        $want = $required[$database].Count
        Check -Name "$database procedimientos" -Ok ($found -eq $want) -Detail "$found de $want"
    }
    catch {
        Check -Name "$database procedimientos" -Ok $false -Detail $_.Exception.Message
    }
}

# ── 5. datos de negocio ─────────────────────────────────────────────────────
Write-Host ''
Write-Host '== Datos' -ForegroundColor Cyan

foreach ($pair in @(
    @{ Db = 'saga_identity'; Name = 'tiendas / usuarios'; Query = "SELECT CAST((SELECT COUNT(*) FROM dbo.tenants) AS varchar) + ' / ' + CAST((SELECT COUNT(*) FROM dbo.users) AS varchar)" },
    @{ Db = 'saga_catalog'; Name = 'productos / proveedores'; Query = "SELECT CAST((SELECT COUNT(*) FROM dbo.products) AS varchar) + ' / ' + CAST((SELECT COUNT(*) FROM dbo.suppliers) AS varchar)" },
    @{ Db = 'saga_sales'; Name = 'pedidos'; Query = "SELECT CAST(COUNT(*) AS varchar) FROM dbo.orders" })) {
    try {
        $value = Invoke-Sql -Database $pair.Db -Query $pair.Query
        Write-Host ("  ----  {0,-42} {1}" -f $pair.Name, $value) -ForegroundColor DarkGray
    }
    catch {
        Write-Host ("  ----  {0,-42} no disponible" -f $pair.Name) -ForegroundColor DarkGray
    }
}

# ── resumen ─────────────────────────────────────────────────────────────────
Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'VERIFICACION: OK — esquema y datos en orden.' -ForegroundColor Green
    exit 0
}

Write-Host ("VERIFICACION: " + $failures.Count + ' problema(s): ' + ($failures -join ', ')) -ForegroundColor Red
exit 1
