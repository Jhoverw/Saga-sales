<#
.SYNOPSIS
    End-to-end check of E-12 (purchases and costs).

.DESCRIPTION
    Drives the real HTTP surface through the gateway and verifies the acceptance criteria of the
    epic: two purchases at different costs end with the weighted average (0.375 for 0.35 and 0.40),
    the margin against a 0.50 selling price is 25 %, the kardex has one entry per purchase, and
    receiving the same purchase twice is refused with 409.

.EXAMPLE
    ./scripts/smoke-purchasing.ps1
#>
[CmdletBinding()]
param(
    [string]$Gateway = 'http://localhost:5000'
)

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body, [string]$Token, [string]$TenantId)

    $headers = @{}
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    if ($TenantId) { $headers['X-Tenant-Id'] = $TenantId }

    $parameters = @{
        Uri             = "$($Gateway.TrimEnd('/'))/$Path"
        Method          = $Method
        Headers         = $headers
        UseBasicParsing = $true
    }

    if ($null -ne $Body) {
        $parameters['ContentType'] = 'application/json'
        $parameters['Body'] = ($Body | ConvertTo-Json -Depth 8)
    }

    try {
        $response = Invoke-WebRequest @parameters
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Content = $response.Content }
    }
    catch {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) { throw }
        $reader = New-Object System.IO.StreamReader($errorResponse.GetResponseStream())
        return [pscustomobject]@{ Status = [int]$errorResponse.StatusCode; Content = $reader.ReadToEnd() }
    }
}

function Assert-That {
    param([string]$Name, [bool]$Condition, [string]$Detail = '')

    if ($Condition) {
        Write-Host ("  PASS  {0}" -f $Name) -ForegroundColor Green
        return
    }

    Write-Host ("  FAIL  {0} {1}" -f $Name, $Detail) -ForegroundColor Red
    $script:failures.Add($Name)
}

function Assert-Status {
    param([string]$Name, [int]$Expected, $Response)

    Assert-That -Name $Name -Condition ($Response.Status -eq $Expected) `
        -Detail ("(esperaba $Expected, obtuve $($Response.Status)) " + $Response.Content)
}

function Assert-Money {
    param([string]$Name, [decimal]$Expected, $Actual)

    $ok = ($null -ne $Actual) -and ([Math]::Abs([decimal]$Actual - $Expected) -lt 0.0005)
    Assert-That -Name $Name -Condition $ok -Detail "(esperaba $Expected, obtuve $Actual)"
}

# ── una tienda nueva para no ensuciar datos existentes ──────────────────────
$slug = 'pur-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$email = "$slug@smoke.local"
$password = 'Smoke12345!'

Write-Host ''
Write-Host "Tienda de prueba: $slug" -ForegroundColor Cyan

$register = Invoke-Api -Method POST -Path 'api/auth/register-tenant' -Body @{
    slug = $slug; displayName = 'Purchase smoke'; adminEmail = $email; adminPassword = $password
}
if ($register.Status -ne 201) {
    Write-Host ("  No se pudo crear la tienda: {0} {1}" -f $register.Status, $register.Content) -ForegroundColor Red
    exit 1
}

$tenant = $register.Content | ConvertFrom-Json

$login = Invoke-Api -Method POST -Path 'api/auth/login' -Body @{ email = $email; password = $password }
if ($login.Status -ne 200) {
    Write-Host ("  No se pudo iniciar sesion: {0} {1}" -f $login.Status, $login.Content) -ForegroundColor Red
    exit 1
}

$session = $login.Content | ConvertFrom-Json
$token = $session.accessToken
$tenantId = $tenant.tenantId.ToString()

# ── proveedor y producto ────────────────────────────────────────────────────
Write-Host ''
Write-Host 'Preparando proveedor y producto' -ForegroundColor Cyan

$supplier = Invoke-Api -Method POST -Path 'api/suppliers' -Token $token -TenantId $tenantId -Body @{
    name = 'Distribuidora Andina'; taxId = '1790000000001'; contactName = 'Ventas'; email = 'ventas@andina.local'
}
Assert-Status -Name 'crear proveedor devuelve 201' -Expected 201 -Response $supplier

$supplierId = ($supplier.Content | ConvertFrom-Json).id

$product = Invoke-Api -Method POST -Path 'api/products' -Token $token -TenantId $tenantId -Body @{
    name = 'Gaseosa 500 ml'; price = 0.50; stockQuantity = 0; currency = 'USD'
}
Assert-Status -Name 'crear producto devuelve 201' -Expected 201 -Response $product

$productId = ($product.Content | ConvertFrom-Json).id

# ── dos compras al mismo producto, con costos distintos ─────────────────────
Write-Host ''
Write-Host 'Comprando a dos precios' -ForegroundColor Cyan

function New-Purchase {
    param([decimal]$UnitCost)

    $created = Invoke-Api -Method POST -Path 'api/purchases' -Token $token -TenantId $tenantId -Body @{
        supplierId = $supplierId
        supplierInvoiceNumber = 'F-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
        notes = "Compra a $UnitCost"
        lines = @(
            @{ productId = $productId; productName = 'Gaseosa 500 ml'; quantity = 100; unitCost = $UnitCost; costIncludesTax = $true }
        )
    }

    if ($created.Status -ne 201) { return $created }

    $id = ($created.Content | ConvertFrom-Json).id
    $received = Invoke-Api -Method POST -Path "api/purchases/$id/receive" -Token $token -TenantId $tenantId
    return [pscustomobject]@{ Status = $received.Status; Content = $received.Content; PurchaseId = $id }
}

$first = New-Purchase -UnitCost 0.35
Assert-Status -Name 'primera compra recibida' -Expected 200 -Response $first

$second = New-Purchase -UnitCost 0.40
Assert-Status -Name 'segunda compra recibida' -Expected 200 -Response $second

# ── el costo promedio ponderado ─────────────────────────────────────────────
Write-Host ''
Write-Host 'Comprobando costo y margen' -ForegroundColor Cyan

$detail = Invoke-Api -Method GET -Path "api/products/$productId" -Token $token -TenantId $tenantId
$after = $detail.Content | ConvertFrom-Json

Assert-That -Name 'el stock suma 200 unidades' -Condition ($after.stockQuantity -eq 200) `
    -Detail "(obtuve $($after.stockQuantity))"

# (100 x 0.35 + 100 x 0.40) / 200 = 0.375
Assert-Money -Name 'el costo es el promedio ponderado 0,375' -Expected 0.375 -Actual $after.cost
Assert-Money -Name 'el margen unitario es 0,125' -Expected 0.125 -Actual $after.margin
Assert-Money -Name 'el margen es 25 %' -Expected 25 -Actual $after.marginPercent

# ── el kardex explica el stock ──────────────────────────────────────────────
Write-Host ''
Write-Host 'Comprobando el kardex' -ForegroundColor Cyan

$movements = Invoke-Api -Method GET -Path "api/products/$productId/movements" -Token $token -TenantId $tenantId
Assert-Status -Name 'consultar movimientos devuelve 200' -Expected 200 -Response $movements

$rows = $movements.Content | ConvertFrom-Json
Assert-That -Name 'hay dos entradas de stock' -Condition ($rows.Count -eq 2) -Detail "(obtuve $($rows.Count))"
Assert-That -Name 'cada entrada suma 100 y deja 100 y 200' `
    -Condition (($rows[0].quantity -eq 100) -and ($rows[1].quantity -eq 100)) `
    -Detail "(obtuve $($rows[0].quantity) y $($rows[1].quantity))"
Assert-That -Name 'el kardex es de tipo compra (kind 1)' `
    -Condition (($rows[0].kind -eq 1) -and ($rows[1].kind -eq 1)) -Detail "(obtuve $($rows[0].kind))"

# ── recibir dos veces la misma compra ───────────────────────────────────────
Write-Host ''
Write-Host 'Comprobando que no se puede recibir dos veces' -ForegroundColor Cyan

$again = Invoke-Api -Method POST -Path "api/purchases/$($second.PurchaseId)/receive" -Token $token -TenantId $tenantId
Assert-Status -Name 'recibir dos veces devuelve 409' -Expected 409 -Response $again

$unchanged = (Invoke-Api -Method GET -Path "api/products/$productId" -Token $token -TenantId $tenantId).Content | ConvertFrom-Json
Assert-That -Name 'el stock no cambio tras el rechazo' -Condition ($unchanged.stockQuantity -eq 200) `
    -Detail "(obtuve $($unchanged.stockQuantity))"

# ── resumen ─────────────────────────────────────────────────────────────────
Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'SMOKE E-12: OK — compras, costo promedio y kardex funcionan.' -ForegroundColor Green
    exit 0
}

Write-Host ("SMOKE E-12: " + $failures.Count + ' fallo(s)') -ForegroundColor Red
$failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
exit 1
