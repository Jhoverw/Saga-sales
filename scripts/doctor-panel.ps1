<#
.SYNOPSIS
    Tells whether the panel is actually working — not just "listening".

.DESCRIPTION
    Every failure that cost hours in this project had a signature, and all of them are checkable
    over HTTP in a second:

      1. No process serving the port          -> nothing at 5008
      2. Old image served (Docker cache)      -> the app file has no new code inside
      3. Mixed build artifacts (v8 loader     -> blazor.boot.json is missing, and the loader
         + v10 runtime)                          dies with "Failed to load config file undefined"
      4. Panel up but API down                -> the gateway does not answer

    Run it after any change. It prints PASS/FAIL per check plus the exact command that fixes the
    failure, so there is no guessing and no need to re-read a conversation.

.PARAMETER Port
    Panel port. 5008 by default.

.PARAMETER Marker
    Text that must exist inside the compiled app. Proves the served build is current.

.EXAMPLE
    ./scripts/doctor-panel.ps1
#>
[CmdletBinding()]
param(
    [int]$Port = 5008,
    [string]$Marker = 'Existencias'
)

$ErrorActionPreference = 'Continue'

$failures = New-Object System.Collections.Generic.List[string]

function Check {
    param([string]$Name, [bool]$Ok, [string]$Detail, [string]$Fix)

    if ($Ok) {
        Write-Host ("  PASS  {0,-38} {1}" -f $Name, $Detail) -ForegroundColor Green
        return
    }

    Write-Host ("  FAIL  {0,-38} {1}" -f $Name, $Detail) -ForegroundColor Red
    if ($Fix) { Write-Host ("        -> {0}" -f $Fix) -ForegroundColor Yellow }
    $script:failures.Add($Name)
}

function Get-Status([string]$Url) {
    try { return [int](Invoke-WebRequest $Url -UseBasicParsing -TimeoutSec 10).StatusCode }
    catch { if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode } return 0 }
}

$base = "http://localhost:$Port"

Write-Host ''
Write-Host "== Panel en $base" -ForegroundColor Cyan

# ── 1. hay algo escuchando ──────────────────────────────────────────────────
$status = Get-Status "$base/"
Check -Name 'el panel responde' -Ok ($status -eq 200) -Detail "HTTP $status" `
    -Fix 'Arrancalo: ./scripts/dev-panel.ps1   (o el contenedor: docker compose up -d blazor-web)'

if ($status -ne 200) {
    Write-Host ''
    Write-Host 'Sin panel no hay nada mas que comprobar.' -ForegroundColor Red
    exit 1
}

# ── 2. el index es el actual ────────────────────────────────────────────────
$page = (Invoke-WebRequest "$base/" -UseBasicParsing -TimeoutSec 10).Content
Check -Name 'index.html actual' -Ok ($page -match 'saga-splash') -Detail $(if ($page -match 'saga-splash') { 'splash propio' } else { 'index antiguo' }) `
    -Fix 'Reconstruye sin cache: dotnet clean src\SagaSales.BlazorWeb; borra bin/obj; ./scripts/dev-panel.ps1'

# ── 3. EL chequeo que detecta el desajuste de versiones ────────────────────
# Sin el manifiesto, el cargador muere con "Failed to load config file undefined".
$boot = Get-Status "$base/_framework/blazor.boot.json"
Check -Name 'manifiesto de arranque' -Ok ($boot -eq 200) -Detail "HTTP $boot" `
    -Fix 'Artefactos mezclados (cargador v8 + runtime v10). Limpia: dotnet clean; borra bin y obj del panel; vuelve a compilar'

# ── 4. la app servida lleva tu codigo ──────────────────────────────────────
$app = $null
try { $app = (Invoke-WebRequest "$base/_framework/SagaSales.BlazorWeb.wasm" -UseBasicParsing -TimeoutSec 20).Content } catch { }

if ($null -eq $app) {
    Check -Name 'app compilada servida' -Ok $false -Detail 'no se pudo descargar' `
        -Fix 'Reconstruye el panel; si persiste, revisa que _framework se este sirviendo'
}
else {
    $even = [Text.Encoding]::Unicode.GetString($app)
    $odd = [Text.Encoding]::Unicode.GetString($app[1..($app.Length - 1)])
    $hasMarker = $even.Contains($Marker) -or $odd.Contains($Marker)
    Check -Name ("la app contiene '{0}'" -f $Marker) -Ok $hasMarker -Detail $(if ($hasMarker) { "$($app.Length) b" } else { 'build antiguo' }) `
        -Fix 'La build servida no tiene los ultimos cambios: reconstruye sin cache'
}

# ── 5. la API esta disponible ──────────────────────────────────────────────
$api = Get-Status 'http://localhost:5000/health'
Check -Name 'gateway responde' -Ok ($api -eq 200) -Detail "HTTP $api" `
    -Fix 'Levanta el stack: ./scripts/up.ps1'

$identity = Get-Status 'http://localhost:5001/health'
Check -Name 'identity responde' -Ok ($identity -eq 200) -Detail "HTTP $identity" `
    -Fix 'Levanta el stack: ./scripts/up.ps1'

# ── 6. el panel apunta a la API correcta ──────────────────────────────────
try {
    $config = (Invoke-WebRequest "$base/appsettings.json" -UseBasicParsing -TimeoutSec 10).Content | ConvertFrom-Json
    $url = $config.ApiBaseUrl
    Check -Name 'el panel apunta al gateway' -Ok ($url -eq 'http://localhost:5000/') -Detail "$url" `
        -Fix 'Corrige wwwroot\appsettings.json: ApiBaseUrl debe ser http://localhost:5000/'
}
catch {
    Check -Name 'el panel apunta al gateway' -Ok $false -Detail 'appsettings.json no legible' -Fix 'Revisa src\SagaSales.BlazorWeb\wwwroot\appsettings.json'
}

Write-Host ''
if ($failures.Count -eq 0) {
    Write-Host 'PANEL: OK — sirve la build actual y la API responde.' -ForegroundColor Green
    Write-Host 'Abrelo en incognito si ya lo tenias abierto.' -ForegroundColor DarkGray
    exit 0
}

Write-Host ("PANEL: " + $failures.Count + ' problema(s). Aplica el -> de cada FAIL.') -ForegroundColor Red
exit 1
