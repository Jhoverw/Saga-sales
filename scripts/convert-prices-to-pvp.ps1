<#
.SYNOPSIS
    Converts product prices saved without VAT into PVP (VAT included), once (spec 003, FR-006).

.DESCRIPTION
    For every product with price_includes_tax = 0:
      - taxable: price = ROUND(price * (1 + VAT in force today for its store), 2), then marked as PVP;
      - not taxable: the price already is the PVP; it is only marked.
    Stores with no VAT in force are skipped and listed. Converted products are never touched again,
    so running the script twice cannot add the VAT twice.

    This is a one-off operation script: it reads the VAT from saga_sales and updates saga_catalog on
    the same SQL Server. No service reads another service's database (constitution §VII).

.PARAMETER DryRun
    Shows what would change, without changing anything. Run this first.

.PARAMETER MarkOnly
    Use when the prices typed in Productos already are the PVP (for example Coca-Cola shows 0.40):
    every product is marked as PVP and NO price changes.

.EXAMPLE
    ./scripts/convert-prices-to-pvp.ps1 -DryRun

.EXAMPLE
    ./scripts/convert-prices-to-pvp.ps1

.EXAMPLE
    ./scripts/convert-prices-to-pvp.ps1 -MarkOnly -DryRun
#>
[CmdletBinding()]
param(
    [switch]$DryRun,
    [switch]$MarkOnly,
    [string]$DatabasePrefix = 'saga_',
    [string]$DatabaseSuffix = '',
    [string]$User = 'sa',
    [string]$Password = $(if ($env:MSSQL_SA_PASSWORD) { $env:MSSQL_SA_PASSWORD } else { 'SagaSales_Dev_2026!' }),
    [string]$Container = 'saga-sales-sqlserver'
)

$ErrorActionPreference = 'Stop'

$catalog = "$DatabasePrefix" + 'catalog' + "$DatabaseSuffix"
$sales = "$DatabasePrefix" + 'sales' + "$DatabaseSuffix"
$apply = if ($DryRun) { 0 } else { 1 }
$markOnly = if ($MarkOnly) { 1 } else { 0 }

$sql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM [$catalog].sys.columns
               WHERE object_id = OBJECT_ID('[$catalog].dbo.products') AND name = 'price_includes_tax')
BEGIN
    PRINT 'ERROR: [$catalog] no tiene price_includes_tax. Aplica primero las migraciones (apply-db.ps1).';
    RETURN;
END

DECLARE @today date = CAST(SYSUTCDATETIME() AS date);
DECLARE @apply bit = $apply;
DECLARE @mark_only bit = $markOnly;

-- VAT in force today for every store that still has unconverted products.
SELECT t.tenant_id, r.rate_percentage
INTO #rates
FROM (SELECT DISTINCT tenant_id FROM [$catalog].dbo.products WHERE price_includes_tax = 0) t
OUTER APPLY
(
    SELECT TOP (1) x.rate_percentage
    FROM [$sales].dbo.tax_rates x
    WHERE x.tenant_id = t.tenant_id
      AND x.start_date <= @today
      AND (x.end_date IS NULL OR x.end_date >= @today)
    ORDER BY x.start_date DESC
) r;

PRINT '== Tiendas sin IVA vigente (sus productos gravados NO se convierten):';
SELECT CONCAT('   ', CAST(tenant_id AS varchar(36))) FROM #rates WHERE rate_percentage IS NULL;

IF @mark_only = 1
    PRINT '== MarkOnly: los precios ya son PVP; se marcan sin cambiar ningun precio:';
ELSE
    PRINT '== Productos gravados a convertir (precio sin IVA -> PVP):';
SELECT CONCAT('   ', p.name, ': ', CAST(p.base_price AS decimal(18,2)), ' -> ',
              CAST(CASE WHEN @mark_only = 1 THEN p.base_price ELSE ROUND(p.base_price * (1 + r.rate_percentage), 2) END AS decimal(18,2)),
              '  (IVA ', CAST(r.rate_percentage * 100 AS decimal(5,2)), ' %)')
FROM [$catalog].dbo.products p
JOIN #rates r ON r.tenant_id = p.tenant_id
WHERE p.price_includes_tax = 0 AND p.is_taxable = 1 AND r.rate_percentage IS NOT NULL
ORDER BY p.name;

PRINT '== Productos sin IVA (solo se marcan como PVP, el precio no cambia):';
SELECT CONCAT('   ', p.name, ': ', CAST(p.base_price AS decimal(18,2)))
FROM [$catalog].dbo.products p
WHERE p.price_includes_tax = 0 AND p.is_taxable = 0
ORDER BY p.name;

IF @apply = 0
BEGIN
    PRINT '== DRY RUN: no se cambio nada. Ejecuta sin -DryRun para aplicar.';
    RETURN;
END

BEGIN TRAN;

UPDATE p
SET base_price = CASE WHEN @mark_only = 1 THEN p.base_price ELSE ROUND(p.base_price * (1 + r.rate_percentage), 2) END,
    price_includes_tax = 1,
    updated_at = SYSUTCDATETIME()
FROM [$catalog].dbo.products p
JOIN #rates r ON r.tenant_id = p.tenant_id
WHERE p.price_includes_tax = 0 AND p.is_taxable = 1 AND r.rate_percentage IS NOT NULL;

DECLARE @converted int = @@ROWCOUNT;

UPDATE [$catalog].dbo.products
SET price_includes_tax = 1,
    updated_at = SYSUTCDATETIME()
WHERE price_includes_tax = 0 AND is_taxable = 0;

DECLARE @marked int = @@ROWCOUNT;

COMMIT;

PRINT CONCAT('== LISTO: ', @converted, ' producto(s) gravados convertidos a PVP; ', @marked, ' producto(s) sin IVA marcados.');
"@

$file = Join-Path ([System.IO.Path]::GetTempPath()) 'saga-convert-prices-to-pvp.sql'
Set-Content -Path $file -Value $sql -Encoding UTF8

Write-Host ("Conversión a PVP · catálogo {0} · ventas {1} · {2}" -f $catalog, $sales, ($(if ($DryRun) { 'DRY RUN' } else { 'APLICANDO' }) + $(if ($MarkOnly) { ' · SOLO MARCAR' } else { '' }))) -ForegroundColor Cyan

Get-Content $file -Raw | docker exec -i $Container /opt/mssql-tools18/bin/sqlcmd `
    -S localhost -U $User -P "$Password" -C -d master -b -I -h -1 -W
$code = $LASTEXITCODE
Remove-Item $file -ErrorAction SilentlyContinue

if ($code -ne 0) {
    Write-Host "FALLO: sqlcmd terminó con código $code" -ForegroundColor Red
    exit 1
}

exit 0
