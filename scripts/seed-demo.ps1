<#
.SYNOPSIS
    Seeds a demo store with data so the panel shows every screen populated.

.DESCRIPTION
    Registers (or signs into) a demo store through the gateway and, only when a section is empty,
    creates: a default branch, two products, a supplier, a received purchase (with a lot), an IVA
    rate, a customer and an order. Safe to run again: it never duplicates what exists.

.EXAMPLE
    ./scripts/seed-demo.ps1
#>
[CmdletBinding()]
param(
    [string]$Gateway = 'http://localhost:5000',
    [string]$Slug = 'demo',
    [string]$Email = 'admin@demo.local',
    [string]$Password = 'Demo12345!'
)

$ErrorActionPreference = 'Stop'
$gw = $Gateway.TrimEnd('/')

function Post-Json {
    param([string]$Path, $Body, [hashtable]$Headers)
    Invoke-RestMethod -Uri "$gw/$Path" -Method Post -Headers $Headers `
        -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 8)
}

function Get-Json {
    param([string]$Path, [hashtable]$Headers)
    Invoke-RestMethod -Uri "$gw/$Path" -Headers $Headers
}

Write-Host "Tienda demo: $Slug ($Email)" -ForegroundColor Cyan

# ── 1. tienda + sesión ──────────────────────────────────────────────────────
try {
    Post-Json -Path 'api/auth/register-tenant' -Headers @{} -Body @{
        slug = $Slug; displayName = 'Saga Demo'; adminEmail = $Email; adminPassword = $Password
    } | Out-Null
    Write-Host '  tienda creada'
}
catch {
    Write-Host '  la tienda ya existía (se reutiliza)'
}

$login = Post-Json -Path 'api/auth/login' -Headers @{} -Body @{ email = $Email; password = $Password }
$headers = @{
    Authorization = "Bearer $($login.accessToken)"
    'X-Tenant-Id' = "$($login.user.tenantId)"
}

# ── 2. sucursal por defecto ─────────────────────────────────────────────────
$branches = Get-Json -Path 'api/branches' -Headers $headers
if ($branches.Count -eq 0) {
    $branch = Post-Json -Path 'api/branches' -Headers $headers -Body @{
        name = 'Principal'; address = 'Av. Matriz 100'; phone = '555-0001'; isDefault = $true
    }
    Write-Host "  sucursal «$($branch.name)» creada"
}

# ── 3. productos ────────────────────────────────────────────────────────────
$page = Get-Json -Path 'api/products?pageSize=100' -Headers $headers
if ($page.totalCount -eq 0) {
    $gaseosa = Post-Json -Path 'api/products' -Headers $headers -Body @{
        name = 'Gaseosa 500 ml'; description = 'Bebida fría'; price = 0.75; stockQuantity = 0
        currency = 'USD'; tracksBatches = $true
    }
    $camiseta = Post-Json -Path 'api/products' -Headers $headers -Body @{
        name = 'Camiseta logo'; description = 'Algodón'; price = 12.50; stockQuantity = 10
        currency = 'USD'; tracksBatches = $false
    }
    Write-Host '  2 productos creados'
}
else {
    $gaseosa = $page.items[0]
    $camiseta = if ($page.items.Count -gt 1) { $page.items[1] } else { $page.items[0] }
}

# ── 4. proveedor + compra recibida con lote ─────────────────────────────────
$suppliers = Get-Json -Path 'api/suppliers' -Headers $headers
if ($suppliers.Count -eq 0) {
    $supplier = Post-Json -Path 'api/suppliers' -Headers $headers -Body @{
        name = 'Distribuidora Andina'; taxId = '1790000000001'; contactName = 'Ventas'
        email = 'ventas@andina.local'; phone = '555-0100'
    }
    Write-Host "  proveedor «$($supplier.name)» creado"

    $expires = (Get-Date).AddDays(90).ToString('yyyy-MM-dd')
    $purchase = Post-Json -Path 'api/purchases' -Headers $headers -Body @{
        supplierId = $supplier.id
        receiveNow = $true
        supplierInvoiceNumber = 'F-DEMO-001'
        notes = 'Compra de demostración'
        lines = @(
            @{ productId = $gaseosa.id; productName = 'Gaseosa 500 ml'; quantity = 24; unitCost = 0.45
               costIncludesTax = $true; lotCode = 'L-DEMO-01'; expiresAt = $expires },
            @{ productId = $camiseta.id; productName = 'Camiseta logo'; quantity = 10; unitCost = 8.00
               costIncludesTax = $true }
        )
    }
    Write-Host "  compra recibida por $($purchase.total)"
}

# ── 5. impuesto (IVA) ───────────────────────────────────────────────────────
$rates = Get-Json -Path 'api/taxes' -Headers $headers
if ($rates.Count -eq 0) {
    Post-Json -Path 'api/taxes' -Headers $headers -Body @{
        ratePercentage = 0.15; startDate = '2026-01-01'; endDate = $null; description = 'IVA 15%'
    } | Out-Null
    Write-Host '  IVA 15 % configurado'
}

# ── 6. cliente + pedido ─────────────────────────────────────────────────────
$customers = Get-Json -Path 'api/customers?pageSize=100&includeInactive=true' -Headers $headers
if ($customers.totalCount -eq 0) {
    $customer = Post-Json -Path 'api/customers' -Headers $headers -Body @{
        firstName = 'Ana'; lastName = 'Ruiz'; email = 'ana@demo.local'; phone = '0990000000'
        customerType = 1
    }
    Write-Host "  cliente «$($customer.firstName) $($customer.lastName)» creado"

    $order = Post-Json -Path 'api/orders' -Headers $headers -Body @{
        customerId = $customer.id
        shippingAddress = 'Av. Siempre Viva 742, Quito'
        items = @(@{ productId = $gaseosa.id; quantity = 2 })
    }
    Write-Host "  pedido creado por $($order.subtotal)"
}

Write-Host ''
Write-Host 'Listo. Abre http://localhost:5008 y entra con:' -ForegroundColor Green
Write-Host "  Email:    $Email"
Write-Host "  Password: $Password"
