<#
.SYNOPSIS
    End-to-end check of image upload (E-13), branches/settings screens' API surface (E-13)
    and US6/US7 (order state machine + filter, and analytics aggregates).

.DESCRIPTION
    Registers a fresh tenant and drives the real HTTP surface through the gateway:
      1. uploads a product picture and reads it back,
      2. lists/creates a branch and reads/updates store settings,
      3. advances an order through the state machine and filters by status,
      4. verifies the analytics summary reflects the order.

.EXAMPLE
    ./scripts/smoke-us6-us7-image.ps1
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

# ── una tienda nueva ─────────────────────────────────────────────────────────
$slug = 'img-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$email = "$slug@smoke.local"
$password = 'Smoke12345!'

Write-Host ''
Write-Host "Tienda de prueba: $slug" -ForegroundColor Cyan

$register = Invoke-Api -Method POST -Path 'api/auth/register-tenant' -Body @{
    slug = $slug; displayName = 'Image/US6/US7 smoke'; adminEmail = $email; adminPassword = $password
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

# ── 0. sucursal predeterminada (la tienda nueva no tiene ninguna) ────────────
Write-Host ''
Write-Host 'Sucursal predeterminada' -ForegroundColor Cyan

$defaultBranch = Invoke-Api -Method POST -Path 'api/branches' -Token $token -TenantId $tenantId -Body @{
    name = 'Principal'; address = 'Matriz'; phone = '555-0001'; isDefault = $true
}
Assert-Status -Name 'crear sucursal predeterminada devuelve 201' -Expected 201 -Response $defaultBranch

# ── 1. imagen de producto ────────────────────────────────────────────────────
Write-Host ''
Write-Host 'Imagen de producto' -ForegroundColor Cyan

$product = Invoke-Api -Method POST -Path 'api/products' -Token $token -TenantId $tenantId -Body @{
    name = 'Camiseta logo'; price = 12.50; stockQuantity = 10; currency = 'USD'
}
Assert-Status -Name 'crear producto devuelve 201' -Expected 201 -Response $product
$productId = ($product.Content | ConvertFrom-Json).id

# 1x1 PNG
$png = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==')
$pngPath = Join-Path $env:TEMP 'saga-smoke.png'
[System.IO.File]::WriteAllBytes($pngPath, $png)

$upload = & curl.exe -s -w "`n%{http_code}" -X POST `
    -H "Authorization: Bearer $token" `
    -H "X-Tenant-Id: $tenantId" `
    -F "file=@$pngPath;type=image/png" `
    "$($Gateway.TrimEnd('/'))/api/products/$productId/image"

$uploadBody = ($upload -join "`n")
$uploadCode = $uploadBody.Substring($uploadBody.LastIndexOf("`n") + 1)
$uploadJson = $uploadBody.Substring(0, $uploadBody.LastIndexOf("`n"))

Assert-That -Name 'subir imagen devuelve 200' -Condition ($uploadCode -eq '200') -Detail "(obtuve $uploadCode)"
$imageProduct = $uploadJson | ConvertFrom-Json
Assert-That -Name 'el producto guarda la ruta de imagen' -Condition (-not [string]::IsNullOrWhiteSpace($imageProduct.imagePath)) `
    -Detail "(imagePath: $($imageProduct.imagePath))"

$imageUrl = "$($Gateway.TrimEnd('/'))/$($imageProduct.imagePath)"
$imageResponse = & curl.exe -s -o NUL -w "%{http_code}" $imageUrl
Assert-That -Name 'la imagen se sirve por el gateway' -Condition ($imageResponse -eq '200') -Detail "(GET $imageUrl -> $imageResponse)"

# ── 2. sucursales y parámetros ───────────────────────────────────────────────
Write-Host ''
Write-Host 'Sucursales y parámetros' -ForegroundColor Cyan

$branches = Invoke-Api -Method GET -Path 'api/branches' -Token $token -TenantId $tenantId
Assert-Status -Name 'listar sucursales devuelve 200' -Expected 200 -Response $branches
$branchCount = @(($branches.Content | ConvertFrom-Json)).Count

$newBranch = Invoke-Api -Method POST -Path 'api/branches' -Token $token -TenantId $tenantId -Body @{
    name = 'Norte'; address = 'Av. Norte 1'; phone = '555-0002'; isDefault = $false
}
Assert-Status -Name 'crear sucursal devuelve 201' -Expected 201 -Response $newBranch
$newBranchId = ($newBranch.Content | ConvertFrom-Json).id

$settings = Invoke-Api -Method GET -Path 'api/settings' -Token $token -TenantId $tenantId
Assert-Status -Name 'leer parámetros devuelve 200' -Expected 200 -Response $settings
$settingsObj = $settings.Content | ConvertFrom-Json
Assert-That -Name 'el aviso de vencimiento por defecto es 30' -Condition ($settingsObj.expiryWarningDays -eq 30) `
    -Detail "(obtuve $($settingsObj.expiryWarningDays))"

$settingsUpdate = Invoke-Api -Method PUT -Path 'api/settings' -Token $token -TenantId $tenantId -Body @{
    expiryWarningDays = 45; defaultBranchId = $settingsObj.defaultBranchId
}
Assert-Status -Name 'actualizar parámetros devuelve 200' -Expected 200 -Response $settingsUpdate
$updatedSettings = $settingsUpdate.Content | ConvertFrom-Json
Assert-That -Name 'el aviso de vencimiento quedó en 45' -Condition ($updatedSettings.expiryWarningDays -eq 45) `
    -Detail "(obtuve $($updatedSettings.expiryWarningDays))"

# ── 3. US6: máquina de estados y filtro ──────────────────────────────────────
Write-Host ''
Write-Host 'Pedidos: máquina de estados y filtro' -ForegroundColor Cyan

$customer = Invoke-Api -Method POST -Path 'api/customers' -Token $token -TenantId $tenantId -Body @{
    firstName = 'Ana'; lastName = 'Ruiz'; email = "ana-$slug@smoke.local"; phone = '0990000000'
}
Assert-Status -Name 'crear cliente devuelve 201' -Expected 201 -Response $customer
$customerId = ($customer.Content | ConvertFrom-Json).id

$order = Invoke-Api -Method POST -Path 'api/orders' -Token $token -TenantId $tenantId -Body @{
    customerId = $customerId
    shippingAddress = 'Calle 1, Guayaquil'
    items = @(@{ productId = $productId; quantity = 2 })
}
Assert-Status -Name 'crear pedido devuelve 201' -Expected 201 -Response $order
$orderId = ($order.Content | ConvertFrom-Json).id

$advance = Invoke-Api -Method PUT -Path "api/orders/$orderId/status" -Token $token -TenantId $tenantId -Body @{ newStatus = 2 }
Assert-Status -Name 'Pendiente -> Confirmado devuelve 200' -Expected 200 -Response $advance

$illegal = Invoke-Api -Method PUT -Path "api/orders/$orderId/status" -Token $token -TenantId $tenantId -Body @{ newStatus = 5 }
Assert-Status -Name 'salto ilegal (Confirmado -> Entregado) devuelve 409' -Expected 409 -Response $illegal

$advance2 = Invoke-Api -Method PUT -Path "api/orders/$orderId/status" -Token $token -TenantId $tenantId -Body @{ newStatus = 3 }
Assert-Status -Name 'Confirmado -> En proceso devuelve 200' -Expected 200 -Response $advance2

$advance3 = Invoke-Api -Method PUT -Path "api/orders/$orderId/status" -Token $token -TenantId $tenantId -Body @{ newStatus = 4 }
Assert-Status -Name 'En proceso -> Enviado devuelve 200' -Expected 200 -Response $advance3

$advance4 = Invoke-Api -Method PUT -Path "api/orders/$orderId/status" -Token $token -TenantId $tenantId -Body @{ newStatus = 5 }
Assert-Status -Name 'Enviado -> Entregado devuelve 200' -Expected 200 -Response $advance4

$filtered = Invoke-Api -Method GET -Path 'api/orders?pageNumber=1&pageSize=20&status=5' -Token $token -TenantId $tenantId
Assert-Status -Name 'filtrar por estado Entregado devuelve 200' -Expected 200 -Response $filtered
$filteredPage = $filtered.Content | ConvertFrom-Json
Assert-That -Name 'el filtro devuelve al menos el pedido entregado' -Condition ($filteredPage.totalCount -ge 1) `
    -Detail "(obtuve totalCount=$($filteredPage.totalCount))"

# ── 4. US7: analítica ────────────────────────────────────────────────────────
Write-Host ''
Write-Host 'Analítica (espera al consumidor de eventos)' -ForegroundColor Cyan

Start-Sleep -Seconds 4

$from = (Get-Date).AddDays(-2).ToString('yyyy-MM-dd')
$to = (Get-Date).AddDays(1).ToString('yyyy-MM-dd')
$summary = Invoke-Api -Method GET -Path "api/analytics/sales-summary?from=$from&to=$to" -Token $token -TenantId $tenantId
Assert-Status -Name 'resumen de analítica devuelve 200' -Expected 200 -Response $summary
$summaryObj = $summary.Content | ConvertFrom-Json
Assert-That -Name 'la analítica registró 1 pedido' -Condition ($summaryObj.orderCount -ge 1) `
    -Detail "(obtuve orderCount=$($summaryObj.orderCount))"
Assert-That -Name 'la analítica registró el ingreso (2 x 12.50)' -Condition ([decimal]$summaryObj.totalRevenue -eq 25) `
    -Detail "(obtuve totalRevenue=$($summaryObj.totalRevenue))"
Assert-That -Name 'la analítica lista el producto más vendido' -Condition ($summaryObj.topProducts.Count -ge 1) `
    -Detail "(obtuve $($summaryObj.topProducts.Count) producto(s))"

# ── resumen ──────────────────────────────────────────────────────────────────
Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'SMOKE US6/US7/IMAGEN: OK — imagen, sucursales, parámetros, estados y analítica funcionan.' -ForegroundColor Green
    exit 0
}

Write-Host ("SMOKE US6/US7/IMAGEN: " + $failures.Count + ' fallo(s)') -ForegroundColor Red
$failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
exit 1
