<#
.SYNOPSIS
    End-to-end check of a multi-line purchase (E-12): one document, many lines, one lot per line.

.DESCRIPTION
    Registers a fresh tenant and drives the real HTTP surface through the gateway:
      1. creates two products (one tracks lots) and a supplier,
      2. registers ONE purchase with three lines (the same product twice in different lots),
      3. verifies the document keeps its three lines, the stock sums and the weighted cost,
      4. verifies the soon-expiring lot is stored with its quantity,
      5. registers a draft with two lines, verifies stock is untouched, then receives it.

.EXAMPLE
    ./scripts/smoke-purchase-lines.ps1
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

# ── tienda nueva ────────────────────────────────────────────────────────────
$slug = 'multi-' + [Guid]::NewGuid().ToString('N').Substring(0, 10)
$email = "$slug@smoke.local"
$password = 'Smoke12345!'

Write-Host ''
Write-Host "Tienda de prueba: $slug" -ForegroundColor Cyan

$register = Invoke-Api -Method POST -Path 'api/auth/register-tenant' -Body @{
    slug = $slug; displayName = 'Multi-line purchase'; adminEmail = $email; adminPassword = $password
}
if ($register.Status -ne 201) {
    Write-Host ("  No se pudo crear la tienda: {0} {1}" -f $register.Status, $register.Content) -ForegroundColor Red
    exit 1
}

$tenant = $register.Content | ConvertFrom-Json

$login = Invoke-Api -Method POST -Path 'api/auth/login' -Body @{ email = $email; password = $password }
$session = $login.Content | ConvertFrom-Json
$token = $session.accessToken
$tenantId = $tenant.tenantId.ToString()

$branch = Invoke-Api -Method POST -Path 'api/branches' -Token $token -TenantId $tenantId -Body @{
    name = 'Principal'; isDefault = $true
}
Assert-Status -Name 'crear sucursal devuelve 201' -Expected 201 -Response $branch

$supplier = Invoke-Api -Method POST -Path 'api/suppliers' -Token $token -TenantId $tenantId -Body @{
    name = 'Distribuidora Andina'; taxId = '1790000000001'
}
Assert-Status -Name 'crear proveedor devuelve 201' -Expected 201 -Response $supplier
$supplierId = ($supplier.Content | ConvertFrom-Json).id

$soda = Invoke-Api -Method POST -Path 'api/products' -Token $token -TenantId $tenantId -Body @{
    name = 'Gaseosa 500 ml'; price = 0.50; stockQuantity = 0; currency = 'USD'; tracksBatches = $true
}
Assert-Status -Name 'crear producto con lote devuelve 201' -Expected 201 -Response $soda
$sodaId = ($soda.Content | ConvertFrom-Json).id

$shirt = Invoke-Api -Method POST -Path 'api/products' -Token $token -TenantId $tenantId -Body @{
    name = 'Camiseta logo'; price = 12.50; stockQuantity = 0; currency = 'USD'; tracksBatches = $false
}
Assert-Status -Name 'crear producto sin lote devuelve 201' -Expected 201 -Response $shirt
$shirtId = ($shirt.Content | ConvertFrom-Json).id

$soon = (Get-Date).AddDays(5).ToString('yyyy-MM-dd')
$later = (Get-Date).AddDays(60).ToString('yyyy-MM-dd')

# ── 1. un documento con tres líneas (y dos lotes del mismo producto) ─────────
Write-Host ''
Write-Host 'Documento de compra con 3 líneas' -ForegroundColor Cyan

$purchase = Invoke-Api -Method POST -Path 'api/purchases' -Token $token -TenantId $tenantId -Body @{
    supplierId = $supplierId
    supplierInvoiceNumber = 'F-MULTI-1'
    purchasedAt = (Get-Date).ToString('o')
    notes = 'Pedido grande en un solo documento'
    receiveNow = $true
    lines = @(
        @{ productId = $sodaId; productName = 'Gaseosa 500 ml'; quantity = 100; unitCost = 0.35; costIncludesTax = $true; lotCode = 'L-2026A'; expiresAt = $soon },
        @{ productId = $sodaId; productName = 'Gaseosa 500 ml'; quantity = 50;  unitCost = 0.40; costIncludesTax = $true; lotCode = 'L-2026B'; expiresAt = $later },
        @{ productId = $shirtId; productName = 'Camiseta logo'; quantity = 10; unitCost = 8.00; costIncludesTax = $true }
    )
}
Assert-Status -Name 'registrar compra multi-línea devuelve 201' -Expected 201 -Response $purchase
$purchaseId = ($purchase.Content | ConvertFrom-Json).id

$detail = Invoke-Api -Method GET -Path "api/purchases/$purchaseId" -Token $token -TenantId $tenantId
Assert-Status -Name 'consultar la compra devuelve 200' -Expected 200 -Response $detail
$document = $detail.Content | ConvertFrom-Json
Assert-That -Name 'el documento conserva sus 3 líneas' -Condition ($document.lines.Count -eq 3) `
    -Detail "(obtuve $($document.lines.Count))"
Assert-That -Name 'el documento quedó recibido' -Condition ($document.status -eq 2) `
    -Detail "(obtuve $($document.status))"

$lots = @($document.lines | Where-Object { $_.productId -eq $sodaId } | ForEach-Object { $_.lotCode })
Assert-That -Name 'los dos lotes viajan en el documento' -Condition ($lots.Count -eq 2 -and $lots -contains 'L-2026A' -and $lots -contains 'L-2026B') `
    -Detail "(obtuve $($lots -join ', '))"

# ── 2. stock y costo promedio ponderado ─────────────────────────────────────
Write-Host ''
Write-Host 'Stock y costo' -ForegroundColor Cyan

$sodaAfter = (Invoke-Api -Method GET -Path "api/products/$sodaId" -Token $token -TenantId $tenantId).Content | ConvertFrom-Json
Assert-That -Name 'la gaseosa suma 150 unidades' -Condition ($sodaAfter.stockQuantity -eq 150) `
    -Detail "(obtuve $($sodaAfter.stockQuantity))"
# (100 x 0.35 + 50 x 0.40) / 150 = 0.3667
Assert-Money -Name 'el costo promedio es 0,3667' -Expected 0.3667 -Actual $sodaAfter.cost

$shirtAfter = (Invoke-Api -Method GET -Path "api/products/$shirtId" -Token $token -TenantId $tenantId).Content | ConvertFrom-Json
Assert-That -Name 'la camiseta suma 10 unidades' -Condition ($shirtAfter.stockQuantity -eq 10) `
    -Detail "(obtuve $($shirtAfter.stockQuantity))"
Assert-Money -Name 'el costo de la camiseta es 8,00' -Expected 8.00 -Actual $shirtAfter.cost

# ── 3. los lotes se guardaron con su cantidad ───────────────────────────────
Write-Host ''
Write-Host 'Lotes en el inventario' -ForegroundColor Cyan

$expiring = Invoke-Api -Method GET -Path 'api/inventory/expiring?maxRows=50' -Token $token -TenantId $tenantId
Assert-Status -Name 'consultar próximos a vencer devuelve 200' -Expected 200 -Response $expiring
$soonLot = @(($expiring.Content | ConvertFrom-Json) | Where-Object { $_.lotCode -eq 'L-2026A' })
Assert-That -Name 'el lote que caduca pronto aparece' -Condition ($soonLot.Count -eq 1) `
    -Detail "(obtuve $($soonLot.Count))"
Assert-That -Name 'el lote A conserva sus 100 unidades' -Condition ($soonLot.Count -eq 1 -and $soonLot[0].quantity -eq 100) `
    -Detail "(obtuve $(if ($soonLot.Count -eq 1) { $soonLot[0].quantity } else { 'n/a' }))"

# ── 4. borrador de varias líneas y recepción posterior ──────────────────────
Write-Host ''
Write-Host 'Borrador multi-línea que se recibe después' -ForegroundColor Cyan

$draft = Invoke-Api -Method POST -Path 'api/purchases' -Token $token -TenantId $tenantId -Body @{
    supplierId = $supplierId
    supplierInvoiceNumber = 'F-MULTI-2'
    receiveNow = $false
    lines = @(
        @{ productId = $sodaId; productName = 'Gaseosa 500 ml'; quantity = 30; unitCost = 0.38; costIncludesTax = $true; lotCode = 'L-2026C'; expiresAt = $later },
        @{ productId = $shirtId; productName = 'Camiseta logo'; quantity = 5; unitCost = 8.10; costIncludesTax = $true }
    )
}
Assert-Status -Name 'guardar borrador multi-línea devuelve 201' -Expected 201 -Response $draft
$draftId = ($draft.Content | ConvertFrom-Json).id
Assert-That -Name 'el borrador está en estado 1' -Condition ((($draft.Content | ConvertFrom-Json).status) -eq 1) `
    -Detail "(obtuve $(($draft.Content | ConvertFrom-Json).status))"

$beforeReceive = (Invoke-Api -Method GET -Path "api/products/$sodaId" -Token $token -TenantId $tenantId).Content | ConvertFrom-Json
Assert-That -Name 'el borrador no movió el stock' -Condition ($beforeReceive.stockQuantity -eq 150) `
    -Detail "(obtuve $($beforeReceive.stockQuantity))"

$received = Invoke-Api -Method POST -Path "api/purchases/$draftId/receive" -Token $token -TenantId $tenantId
Assert-Status -Name 'recibir el borrador devuelve 200' -Expected 200 -Response $received

$afterReceive = (Invoke-Api -Method GET -Path "api/products/$sodaId" -Token $token -TenantId $tenantId).Content | ConvertFrom-Json
Assert-That -Name 'al recibir entran las 30 unidades de las dos líneas' -Condition ($afterReceive.stockQuantity -eq 180) `
    -Detail "(obtuve $($afterReceive.stockQuantity))"

$again = Invoke-Api -Method POST -Path "api/purchases/$draftId/receive" -Token $token -TenantId $tenantId
Assert-Status -Name 'recibir dos veces devuelve 409' -Expected 409 -Response $again

# ── resumen ─────────────────────────────────────────────────────────────────
Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'SMOKE E-12 MULTI-LÍNEA: OK — documento agrupado, lotes, costos y borrador funcionan.' -ForegroundColor Green
    exit 0
}

Write-Host ("SMOKE E-12 MULTI-LÍNEA: " + $failures.Count + ' fallo(s)') -ForegroundColor Red
$failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
exit 1
