<#
.SYNOPSIS
    Compara el stock que ve la Caja (products.stock_quantity) con el stock que
    usa la venta para reservar (product_stock por sucursal), para un producto.
.EXAMPLE
    ./scripts/diag-stock.ps1 -Name coca
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Name,
    [string]$Database = 'saga_catalog',
    [string]$Container = 'saga-sales-sqlserver',
    [string]$Password = $(if ($env:MSSQL_SA_PASSWORD) { $env:MSSQL_SA_PASSWORD } else { 'SagaSales_Dev_2026!' })
)

$safe = $Name.Replace("'", "''")
$query = @"
SET NOCOUNT ON;
SELECT LEFT(CONVERT(char(36), p.tenant_id), 8) AS tienda, p.name AS producto, p.status AS estado, p.stock_quantity AS stock_caja,
       b.name AS sucursal, b.status AS suc_estado, ISNULL(ps.quantity, 0) AS stock_sucursal,
       CASE WHEN b.id = dbo.fn_default_branch(p.tenant_id) THEN 'si' ELSE '' END AS suc_por_defecto
FROM dbo.products p
LEFT JOIN dbo.branches b ON b.tenant_id = p.tenant_id
LEFT JOIN dbo.product_stock ps ON ps.product_id = p.id AND ps.branch_id = b.id
WHERE p.name LIKE N'%$safe%'
ORDER BY tienda, p.name, b.name;
"@

docker exec $Container /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$Password" -C -d $Database -W -s ' | ' -Q $query
