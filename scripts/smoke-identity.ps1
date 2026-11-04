<#
.SYNOPSIS
    End-to-end smoke test of story US1 (open a store and sign in).

.DESCRIPTION
    Applies the Identity schema, then drives the real HTTP surface through the gateway:
    register a store, sign in, check the token claims, and verify that the failure cases refuse
    correctly. It is the executable version of the US1 acceptance scenarios in spec.md.

    Requires: SQL Server up, the gateway and the Identity service running.

.EXAMPLE
    ./scripts/smoke-identity.ps1

.EXAMPLE
    ./scripts/smoke-identity.ps1 -Gateway http://localhost:5001 -SkipDb
#>
[CmdletBinding()]
param(
    [string]$Gateway = 'http://localhost:5000',
    [string]$Server = 'localhost,1433',
    [string]$User = 'sa',
    [string]$Password = $env:MSSQL_SA_PASSWORD,
    [switch]$SkipDb
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]

function Invoke-Api {
    param([string]$Method, [string]$Path, $Body)

    $uri = "$($Gateway.TrimEnd('/'))/$Path"
    try {
        $response = Invoke-WebRequest -Uri $uri -Method $Method -ContentType 'application/json' `
            -Body ($Body | ConvertTo-Json -Depth 6) -UseBasicParsing
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Content = $response.Content }
    } catch {
        $errorResponse = $_.Exception.Response
        if ($null -eq $errorResponse) { throw }
        $reader = New-Object System.IO.StreamReader($errorResponse.GetResponseStream())
        return [pscustomobject]@{ Status = [int]$errorResponse.StatusCode; Content = $reader.ReadToEnd() }
    }
}

function Assert-Status {
    param([string]$Name, [int]$Expected, [int]$Actual, [string]$Body = '')

    if ($Actual -eq $Expected) {
        Write-Host ("  PASS  {0} ({1})" -f $Name, $Actual) -ForegroundColor Green
        return
    }

    Write-Host ("  FAIL  {0}: esperaba {1}, obtuve {2}" -f $Name, $Expected, $Actual) -ForegroundColor Red
    if ($Body) { Write-Host ("        " + $Body.Substring(0, [Math]::Min(200, $Body.Length))) -ForegroundColor DarkRed }
    $script:failures.Add($Name)
}

function Read-Claims {
    param([string]$Token)

    $segments = $Token.Split('.')
    if ($segments.Length -lt 2) { return $null }

    $payload = $segments[1].Replace('-', '+').Replace('_', '/')
    switch ($payload.Length % 4) {
        2 { $payload += '==' }
        3 { $payload += '=' }
    }

    $json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload))
    return $json | ConvertFrom-Json
}

# ── 1. schema ────────────────────────────────────────────────────────────────
if (-not $SkipDb) {
    Write-Host 'Aplicando el esquema de Identity…' -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'apply-db.ps1') -Server $Server -User $User -Password $Password -Service identity
    if ($LASTEXITCODE -ne 0) { throw 'apply-db.ps1 fallo; revisa la salida anterior.' }
}

# ── 2. scenarios ─────────────────────────────────────────────────────────────
$slug = 'smoke-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$email = "$slug@smoke.local"
$password = 'Smoke12345!'

Write-Host ''
Write-Host "Escenario: alta de comercio ($slug)" -ForegroundColor Cyan

$register = Invoke-Api -Method 'POST' -Path 'api/auth/register-tenant' -Body @{
    slug        = $slug
    displayName = 'Smoke store'
    adminEmail  = $email
    adminPassword = $password
}
Assert-Status -Name 'registro devuelve 201' -Expected 201 -Actual $register.Status -Body $register.Content

$tenant = $register.Content | ConvertFrom-Json

$duplicate = Invoke-Api -Method 'POST' -Path 'api/auth/register-tenant' -Body @{
    slug        = $slug
    displayName = 'Smoke store'
    adminEmail  = "$slug-2@smoke.local"
    adminPassword = $password
}
Assert-Status -Name 'slug duplicado devuelve 409' -Expected 409 -Actual $duplicate.Status -Body $duplicate.Content

Write-Host ''
Write-Host 'Escenario: inicio de sesion' -ForegroundColor Cyan

$login = Invoke-Api -Method 'POST' -Path 'api/auth/login' -Body @{
    email    = $email
    password = $password
}
Assert-Status -Name 'login devuelve 200' -Expected 200 -Actual $login.Status -Body $login.Content

if ($login.Status -eq 200) {
    $session = $login.Content | ConvertFrom-Json
    $claims = Read-Claims -Token $session.accessToken

    if ($claims -and $claims.tenant_id -eq $tenant.tenantId) {
        Write-Host '  PASS  el token lleva el tenant_id correcto' -ForegroundColor Green
    } else {
        Write-Host ("  FAIL  tenant_id del token: " + $claims.tenant_id + ' esperaba ' + $tenant.tenantId) -ForegroundColor Red
        $failures.Add('claim tenant_id')
    }

    if ($claims -and $claims.role -eq 'admin') {
        Write-Host '  PASS  el token lleva role=admin' -ForegroundColor Green
    } else {
        Write-Host '  FAIL  el token no lleva role=admin' -ForegroundColor Red
        $failures.Add('claim role')
    }
}

Write-Host ''
Write-Host 'Escenario: credenciales invalidas no revelan la cuenta' -ForegroundColor Cyan

$wrongPassword = Invoke-Api -Method 'POST' -Path 'api/auth/login' -Body @{
    email    = $email
    password = 'WrongPassword!'
}
$unknownEmail = Invoke-Api -Method 'POST' -Path 'api/auth/login' -Body @{
    email    = "nobody-$slug@smoke.local"
    password = $password
}

Assert-Status -Name 'contrasena incorrecta devuelve 401' -Expected 401 -Actual $wrongPassword.Status -Body $wrongPassword.Content
Assert-Status -Name 'email inexistente devuelve 401' -Expected 401 -Actual $unknownEmail.Status -Body $unknownEmail.Content

if ($wrongPassword.Content -eq $unknownEmail.Content) {
    Write-Host '  PASS  ambos fallos devuelven el mismo cuerpo' -ForegroundColor Green
} else {
    Write-Host '  FAIL  los dos fallos devuelven cuerpos distintos (enumeracion de cuentas)' -ForegroundColor Red
    $failures.Add('respuesta uniforme')
}

# ── 3. summary ───────────────────────────────────────────────────────────────
Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'SMOKE US1: OK — todos los escenarios de aceptación pasan.' -ForegroundColor Green
    exit 0
}

Write-Host ("SMOKE US1: " + $failures.Count + ' fallo(s)') -ForegroundColor Red
$failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
exit 1
